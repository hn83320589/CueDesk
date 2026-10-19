using CueDesk.Core.Quality;
using CueDesk.Core.Settings;

namespace CueDesk.Core.Tests.Settings;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "CueDeskTests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new SettingsStore(SettingsPath);
        var settings = AppSettings.Default.WithRecentFile(@"C:\a.srt") with
        {
            Thresholds = new QualityThresholds { MinGapMs = 80, MaxCharsPerSecond = 15.5 },
            WriteBom = false,
        };
        store.Save(settings);

        var loaded = store.Load();
        Assert.Null(loaded.Warning);
        Assert.Equal(80, loaded.Settings.Thresholds.MinGapMs);
        Assert.Equal(15.5, loaded.Settings.Thresholds.MaxCharsPerSecond);
        Assert.False(loaded.Settings.WriteBom);
        Assert.Equal([@"C:\a.srt"], loaded.Settings.RecentFiles);
    }

    [Fact]
    public void Missing_file_returns_defaults_with_warning()
    {
        var result = new SettingsStore(SettingsPath).Load();
        Assert.Equal(QualityThresholds.Default, result.Settings.Thresholds);
        Assert.NotNull(result.Warning);
        Assert.True(result.FileMissing);
    }

    [Fact]
    public void Corrupt_file_returns_defaults_warns_and_keeps_backup()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "{ this is not json");
        var result = new SettingsStore(SettingsPath).Load();
        Assert.Equal(AppSettings.Default.Thresholds, result.Settings.Thresholds);
        Assert.Contains("損毀", result.Warning);
        Assert.False(result.FileMissing);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.corrupt.json")));
    }

    [Fact]
    public void Wrong_types_are_treated_as_corrupt()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "thresholds": "fast", "recentFiles": 3 }""");
        var result = new SettingsStore(SettingsPath).Load();
        Assert.Contains("損毀", result.Warning);
    }

    [Fact]
    public void Out_of_range_values_fall_back_per_field()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, """{ "thresholds": { "minGapMs": -5, "maxLineLength": 30, "maxCharsPerSecond": 0 } }""");
        var result = new SettingsStore(SettingsPath).Load();
        Assert.Equal(40, result.Settings.Thresholds.MinGapMs);
        Assert.Equal(30, result.Settings.Thresholds.MaxLineLength);
        Assert.Equal(17, result.Settings.Thresholds.MaxCharsPerSecond);
        Assert.Contains("最小間隔", result.Warning);
    }

    [Fact]
    public void Null_json_returns_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsPath, "null");
        var result = new SettingsStore(SettingsPath).Load();
        Assert.NotNull(result.Warning);
        Assert.Equal(AppSettings.Default.WriteBom, result.Settings.WriteBom);
    }

    [Fact]
    public void Recent_files_are_deduplicated_most_recent_first_and_capped()
    {
        var s = AppSettings.Default;
        for (var i = 0; i < 12; i++)
        {
            s = s.WithRecentFile($@"C:\f{i}.srt");
        }
        s = s.WithRecentFile(@"c:\F5.SRT");
        Assert.Equal(AppSettings.MaxRecentFiles, s.RecentFiles.Count);
        Assert.Equal(@"c:\F5.SRT", s.RecentFiles[0]);
        Assert.Single(s.RecentFiles, p => p.Equals(@"C:\f5.srt", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(@"c:\F5.SRT", s.WithoutRecentFile(@"C:\f5.srt").RecentFiles);
    }
}
