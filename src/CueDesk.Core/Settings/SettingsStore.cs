using System.Text.Json;
using CueDesk.Core.Quality;

namespace CueDesk.Core.Settings;

/// <param name="Warning">Non-null when defaults had to be used; meant to be shown to the user.</param>
/// <param name="FileMissing">True on first run (no file yet); the UI shows this less prominently than corruption.</param>
public sealed record SettingsLoadResult(AppSettings Settings, string? Warning, bool FileMissing = false);

public interface ISettingsStore
{
    SettingsLoadResult Load();

    void Save(AppSettings settings);
}

/// <summary>
/// JSON settings file. Loading never throws for missing, corrupt or out-of-range content:
/// defaults are used and a warning is returned. A corrupt file is kept as *.corrupt.json for inspection.
/// </summary>
public sealed class SettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CueDesk", "settings.json");

    public string Path { get; } = path;

    public SettingsLoadResult Load()
    {
        string json;
        try
        {
            json = File.ReadAllText(Path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SettingsLoadResult(AppSettings.Default, "找不到設定檔，已使用預設值。", FileMissing: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // e.g. no read permission or the file is locked: still start with defaults.
            return new SettingsLoadResult(AppSettings.Default, $"無法讀取設定檔，已使用預設值（{ex.Message}）");
        }

        AppSettings? loaded;
        try
        {
            loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            var backup = BackupCorruptFile();
            return new SettingsLoadResult(AppSettings.Default,
                $"設定檔已損毀，已改用預設值。{(backup is null ? "" : $"原檔備份於 {backup}")}（{ex.Message}）");
        }

        if (loaded is null)
        {
            return new SettingsLoadResult(AppSettings.Default, "設定檔內容為空，已使用預設值。");
        }
        return Sanitize(loaded);
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temp = Path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temp, Path, overwrite: true);
    }

    private static SettingsLoadResult Sanitize(AppSettings s)
    {
        var d = QualityThresholds.Default;
        var t = s.Thresholds ?? d;
        var fixedFields = new List<string>();

        T Check<T>(T value, bool valid, T fallback, string name)
        {
            if (valid)
            {
                return value;
            }
            fixedFields.Add(name);
            return fallback;
        }

        var minDuration = Check(t.MinDurationMs, t.MinDurationMs >= 0, d.MinDurationMs, "最短時長");
        var thresholds = new QualityThresholds
        {
            MinGapMs = Check(t.MinGapMs, t.MinGapMs >= 0, d.MinGapMs, "最小間隔"),
            MinDurationMs = minDuration,
            MaxDurationMs = Check(t.MaxDurationMs, t.MaxDurationMs > minDuration, Math.Max(d.MaxDurationMs, minDuration + 1), "最長時長"),
            MaxCharsPerSecond = Check(t.MaxCharsPerSecond, t.MaxCharsPerSecond > 0 && double.IsFinite(t.MaxCharsPerSecond), d.MaxCharsPerSecond, "閱讀速度上限"),
            MaxLineLength = Check(t.MaxLineLength, t.MaxLineLength > 0, d.MaxLineLength, "單行長度"),
            MaxLines = Check(t.MaxLines, t.MaxLines > 0, d.MaxLines, "行數上限"),
        };
        var recent = (s.RecentFiles ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Take(AppSettings.MaxRecentFiles).ToList();

        var settings = s with
        {
            Thresholds = thresholds,
            RecentFiles = recent,
            SourceFrameRate = s.SourceFrameRate ?? AppSettings.Default.SourceFrameRate,
            TargetFrameRate = s.TargetFrameRate ?? AppSettings.Default.TargetFrameRate,
        };
        var warning = fixedFields.Count == 0 ? null : $"設定值不合法，已還原為預設值：{string.Join("、", fixedFields)}";
        return new SettingsLoadResult(settings, warning);
    }

    private string? BackupCorruptFile()
    {
        try
        {
            var backup = System.IO.Path.ChangeExtension(Path, ".corrupt.json");
            File.Copy(Path, backup, overwrite: true);
            return backup;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The warning already tells the user defaults are used; a missing backup is not fatal.
            return null;
        }
    }
}
