using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CueDesk.Core.Encoding;
using CueDesk.Core.Export;
using CueDesk.Core.Parsing;
using CueDesk.Presentation.Services;

namespace CueDesk.Presentation;

public sealed partial class MainViewModel
{
    /// <summary>Files larger than this are refused (the app targets ≤ 5 MB / 20,000 cues).</summary>
    public const long MaxFileBytes = 5 * 1024 * 1024;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _filePath;

    [ObservableProperty]
    private string _encodingName = "—";

    /// <summary>Malformed blocks reported by the last open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ParseErrorsText), nameof(HasParseErrors))]
    private ImmutableArray<ParseError> _parseErrors = [];

    public bool HasParseErrors => !ParseErrors.IsEmpty;

    public string ParseErrorsText => $"開檔時略過 {ParseErrors.Length} 個格式錯誤的區塊：\n{string.Join("\n", ParseErrors)}";

    private SubtitleEncoding? _sourceEncoding;

    public string WindowTitle =>
        $"{(IsModified ? "* " : "")}{(FilePath is null ? "未命名" : Path.GetFileName(FilePath))} - CueDesk 字幕校稿台";

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }
        var path = await _fileDialogs.PickOpenFileAsync();
        if (path is not null)
        {
            await LoadFileAsync(path);
        }
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string path)
    {
        if (!await ConfirmDiscardChangesAsync())
        {
            return;
        }
        if (!_fileSystem.Exists(path))
        {
            await _dialogs.AlertAsync("找不到檔案", $"「{path}」已不存在，將從最近開啟清單移除。");
            SaveSettings(Settings.WithoutRecentFile(path));
            return;
        }
        await LoadFileAsync(path);
    }

    /// <summary>Handles files dropped on the window: opens the first .srt, explains anything else.</summary>
    public async Task OpenDroppedAsync(IReadOnlyList<string> paths)
    {
        var srt = paths.FirstOrDefault(IsSrtPath);
        if (srt is null)
        {
            var names = string.Join("、", paths.Select(Path.GetFileName));
            await _dialogs.AlertAsync("不支援的檔案", $"只能開啟 .srt 字幕檔。拖入的檔案：{(names.Length == 0 ? "（無法取得檔名）" : names)}");
            return;
        }
        if (await ConfirmDiscardChangesAsync())
        {
            await LoadFileAsync(srt);
        }
    }

    public static bool IsSrtPath(string path) => string.Equals(Path.GetExtension(path), ".srt", StringComparison.OrdinalIgnoreCase);

    private async Task LoadFileAsync(string path)
    {
        if (!IsSrtPath(path))
        {
            await _dialogs.AlertAsync("不支援的檔案", $"「{Path.GetFileName(path)}」不是 .srt 字幕檔。");
            return;
        }
        byte[] bytes;
        try
        {
            if (_fileSystem.GetLength(path) > MaxFileBytes)
            {
                await _dialogs.AlertAsync("檔案過大", "字幕檔超過 5 MB，無法開啟。");
                return;
            }
            bytes = _fileSystem.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.AlertAsync("無法開啟", ex.Message);
            return;
        }

        var decoded = EncodingDetector.Decode(bytes);
        var parsed = SrtParser.Parse(decoded.Text);
        _history.Reset(parsed.Cues);
        _sourceEncoding = decoded.Encoding;
        _selectedIds = [];
        FilePath = path;
        EncodingName = decoded.HasReplacementCharacters ? $"{decoded.EncodingName}（部分字元無法辨識）" : decoded.EncodingName;
        ParseErrors = parsed.Errors;
        Refresh();
        SaveSettings(Settings.WithRecentFile(path));

        StatusMessage = parsed.Errors.IsEmpty
            ? $"已開啟 {Path.GetFileName(path)}：{parsed.Cues.Length} 筆字幕"
            : $"已開啟 {Path.GetFileName(path)}：{parsed.Cues.Length} 筆字幕，{parsed.Errors.Length} 個格式錯誤的區塊已略過";
        if (!parsed.Errors.IsEmpty)
        {
            var list = string.Join("\n", parsed.Errors.Take(20));
            var more = parsed.Errors.Length > 20 ? $"\n…另有 {parsed.Errors.Length - 20} 筆" : "";
            await _dialogs.AlertAsync("部分區塊格式錯誤", $"以下區塊未載入，其餘字幕已正常載入：\n{list}{more}");
        }
    }

    [RelayCommand]
    private Task SaveAsync() => SaveCoreAsync(FilePath);

    [RelayCommand]
    private Task SaveAsAsync() => SaveCoreAsync(null);

    /// <summary>Saves as SRT (UTF-8, BOM per settings). Returns false when cancelled or failed.</summary>
    private async Task<bool> SaveCoreAsync(string? path)
    {
        path ??= await _fileDialogs.PickSaveFileAsync(SuggestedName(".srt"), ExportFormat.Srt);
        if (path is null)
        {
            return false;
        }
        if (_sourceEncoding is SubtitleEncoding.Big5 or SubtitleEncoding.Utf16LittleEndian or SubtitleEncoding.Utf16BigEndian
            && !await _dialogs.ConfirmAsync("儲存編碼", $"原檔為 {EncodingName}，將改以 UTF-8 儲存。要繼續嗎？"))
        {
            return false;
        }
        if (!await TryWriteAsync(path, ExportFormat.Srt))
        {
            return false;
        }
        _history.MarkSaved();
        _sourceEncoding = Settings.WriteBom ? SubtitleEncoding.Utf8Bom : SubtitleEncoding.Utf8;
        EncodingName = Settings.WriteBom ? "UTF-8 (BOM)" : "UTF-8";
        FilePath = path;
        SaveSettings(Settings.WithRecentFile(path));
        Refresh();
        StatusMessage = $"已儲存 {Path.GetFileName(path)}";
        return true;
    }

    [RelayCommand]
    private async Task ExportAsync(ExportFormat format)
    {
        var extension = format switch
        {
            ExportFormat.Srt => ".srt",
            ExportFormat.WebVtt => ".vtt",
            _ => ".txt",
        };
        var path = await _fileDialogs.PickSaveFileAsync(SuggestedName(extension), format);
        if (path is not null && await TryWriteAsync(path, format))
        {
            StatusMessage = $"已匯出 {Path.GetFileName(path)}";
        }
    }

    [RelayCommand]
    private void ToggleBom() => SaveSettings(Settings with { WriteBom = !Settings.WriteBom });

    [RelayCommand]
    private void ToggleBackup() => SaveSettings(Settings with { KeepBackup = !Settings.KeepBackup });

    /// <summary>
    /// Asks before discarding unsaved changes. Returns true when the caller may proceed
    /// (no changes, user chose to discard, or saving succeeded).
    /// </summary>
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!IsModified)
        {
            return true;
        }
        return await _dialogs.AskSaveChangesAsync("目前的字幕有未儲存的修改，要先儲存嗎？") switch
        {
            SaveChoice.Discard => true,
            SaveChoice.Save => await SaveCoreAsync(FilePath),
            _ => false,
        };
    }

    private async Task<bool> TryWriteAsync(string path, ExportFormat format)
    {
        try
        {
            var bytes = SubtitleWriters.ToUtf8Bytes(SubtitleWriters.Write(format, Cues), Settings.WriteBom);
            _fileSystem.WriteAllBytesSafely(path, bytes, Settings.KeepBackup);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await _dialogs.AlertAsync("寫入失敗", ex.Message);
            return false;
        }
    }

    private string SuggestedName(string extension) =>
        (FilePath is null ? "subtitle" : Path.GetFileNameWithoutExtension(FilePath)) + extension;
}
