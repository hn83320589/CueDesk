using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CueDesk.Core.Editing;
using CueDesk.Core.History;
using CueDesk.Core.Model;
using CueDesk.Core.Settings;
using CueDesk.Presentation.Services;

namespace CueDesk.Presentation;

/// <summary>
/// Document state, undo history and status. Feature areas live in the other partial files:
/// file handling, editing, time operations and quality.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly IFileDialogService _fileDialogs;
    private readonly IFileSystem _fileSystem;
    private readonly ISettingsStore _settingsStore;
    private readonly UndoStack<ImmutableArray<Cue>> _history = new([]);
    private HashSet<int> _selectedIds = [];

    public MainViewModel(IDialogService dialogs, IFileDialogService fileDialogs, IFileSystem fileSystem, ISettingsStore settingsStore)
    {
        _dialogs = dialogs;
        _fileDialogs = fileDialogs;
        _fileSystem = fileSystem;
        _settingsStore = settingsStore;

        var loaded = settingsStore.Load();
        _settings = loaded.Settings;
        SettingsWarning = loaded.Warning;
        SettingsWarningNeedsDialog = loaded.Warning is not null && !loaded.FileMissing;
        LoadThresholdFields(_settings.Thresholds);
        _sourceFrameRate = FrameRates.FirstOrDefault(f => f.Name == _settings.SourceFrameRate) ?? FrameRates[0];
        _targetFrameRate = FrameRates.FirstOrDefault(f => f.Name == _settings.TargetFrameRate) ?? FrameRates[2];
        Refresh();
        if (loaded.FileMissing)
        {
            // First run: create the file with defaults so later runs find it.
            SaveSettings(_settings);
        }
        if (loaded.Warning is not null)
        {
            StatusMessage = loaded.Warning;
        }
    }

    /// <summary>Raised when the view should scroll to (and select) a cue.</summary>
    public event Action<int>? ScrollToCueRequested;

    public ImmutableArray<Cue> Cues => _history.Current;

    [ObservableProperty]
    private IReadOnlyList<CueRowViewModel> _rows = [];

    [ObservableProperty]
    private AppSettings _settings;

    /// <summary>Warning from loading settings (missing/corrupt file); shown in the status bar at startup.</summary>
    public string? SettingsWarning { get; }

    /// <summary>True when the settings file existed but was unusable; the view shows a dialog.</summary>
    public bool SettingsWarningNeedsDialog { get; }

    public string BomMenuText => Settings.WriteBom ? "✓ 儲存/匯出時加入 BOM" : "儲存/匯出時加入 BOM";

    public string BackupMenuText => Settings.KeepBackup ? "✓ 覆寫時保留 .bak 備份" : "覆寫時保留 .bak 備份";

    public IReadOnlyList<string> RecentFiles => Settings.RecentFiles;

    partial void OnSettingsChanged(AppSettings value)
    {
        OnPropertyChanged(nameof(BomMenuText));
        OnPropertyChanged(nameof(BackupMenuText));
        OnPropertyChanged(nameof(RecentFiles));
    }

    [ObservableProperty]
    private string _statusMessage = "請開啟 .srt 檔，或將檔案拖放到視窗中";

    public bool IsModified => _history.IsModified;

    public bool CanUndo => _history.CanUndo;

    public bool CanRedo => _history.CanRedo;

    public string UndoText => _history.UndoDescription is { } d ? $"復原：{d}" : "復原";

    public string RedoText => _history.RedoDescription is { } d ? $"重做：{d}" : "重做";

    public string CountText => $"{Cues.Length} 筆字幕";

    public IReadOnlyCollection<int> SelectedIds => _selectedIds;

    /// <summary>Called by the view when the list selection changes.</summary>
    public void SetSelection(IEnumerable<int> ids)
    {
        _selectedIds = [.. ids];
        OnSelectionChanged();
    }

    /// <summary>Selected cues in list order.</summary>
    public IReadOnlyList<Cue> SelectedCues => [.. Cues.Where(c => _selectedIds.Contains(c.Id))];

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var description = _history.UndoDescription;
        _history.Undo();
        Refresh();
        StatusMessage = $"已復原：{description}";
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        var description = _history.RedoDescription;
        _history.Redo();
        Refresh();
        StatusMessage = $"已重做：{description}";
    }

    /// <summary>Records a new document state as one undo step.</summary>
    private void Commit(ImmutableArray<Cue> cues, string description, IEnumerable<int>? select = null)
    {
        _history.Push(cues, description);
        if (select is not null)
        {
            _selectedIds = [.. select];
        }
        Refresh();
        StatusMessage = description;
    }

    /// <summary>Runs an edit; a rejected edit is reported to the user and leaves the document unchanged.</summary>
    private async Task<bool> TryEditAsync(Action edit)
    {
        try
        {
            edit();
            return true;
        }
        catch (EditRejectedException ex)
        {
            await _dialogs.AlertAsync("無法執行", ex.Message);
            return false;
        }
    }

    private void Refresh()
    {
        var cues = Cues;
        _selectedIds.IntersectWith(cues.Select(c => c.Id));
        RecomputeIssues();
        var withIssues = Issues.Select(i => i.CueId).ToHashSet();
        Rows = [.. cues.Select((c, i) => new CueRowViewModel(c, i + 1, withIssues.Contains(c.Id)))];

        OnPropertyChanged(nameof(Cues));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoText));
        OnPropertyChanged(nameof(RedoText));
        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(WindowTitle));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnSelectionChanged();
        UpdateShiftPreview();
    }

    private void RequestScrollTo(int cueId)
    {
        _selectedIds = [cueId];
        OnSelectionChanged();
        ScrollToCueRequested?.Invoke(cueId);
    }

    private void SaveSettings(AppSettings settings)
    {
        Settings = settings;
        try
        {
            _settingsStore.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusMessage = $"設定檔儲存失敗：{ex.Message}";
        }
    }
}
