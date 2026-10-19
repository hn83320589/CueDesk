using CueDesk.Core.Quality;

namespace CueDesk.Core.Settings;

public sealed record AppSettings
{
    public const int MaxRecentFiles = 10;

    public QualityThresholds Thresholds { get; init; } = QualityThresholds.Default;

    public IReadOnlyList<string> RecentFiles { get; init; } = [];

    /// <summary>Whether saved/exported UTF-8 files start with a BOM.</summary>
    public bool WriteBom { get; init; } = true;

    public string SourceFrameRate { get; init; } = "23.976";

    public string TargetFrameRate { get; init; } = "25";

    public static AppSettings Default { get; } = new();

    /// <summary>Moves <paramref name="path"/> to the top of the recent list (case-insensitive), keeping at most 10.</summary>
    public AppSettings WithRecentFile(string path) => this with
    {
        RecentFiles = [.. RecentFiles.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase))
            .Prepend(path).Take(MaxRecentFiles)],
    };

    public AppSettings WithoutRecentFile(string path) => this with
    {
        RecentFiles = [.. RecentFiles.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase))],
    };
}
