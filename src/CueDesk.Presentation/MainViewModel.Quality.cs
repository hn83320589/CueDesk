using System.Collections.Immutable;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CueDesk.Core.Quality;

namespace CueDesk.Presentation;

public sealed partial class MainViewModel
{
    /// <summary>Current issues; overlap/gap issues the fixer cannot resolve carry a <see cref="QualityIssue.FixFailure"/>.</summary>
    [ObservableProperty]
    private ImmutableArray<QualityIssue> _issues = [];

    public string IssueSummary => Issues.IsEmpty ? "沒有發現問題" : $"{Issues.Length} 個問題";

    private void RecomputeIssues()
    {
        var thresholds = Settings.Thresholds;
        var issues = QualityChecker.Check(Cues, thresholds);
        // A dry run of the fixer tells the user up front which overlaps/gaps cannot be fixed and why.
        Issues = GapFixer.Annotate(issues, GapFixer.Fix(Cues, thresholds.MinGapMs).Unfixed);
        OnPropertyChanged(nameof(IssueSummary));
    }

    [RelayCommand]
    private void SelectIssue(QualityIssue? issue)
    {
        if (issue is not null)
        {
            RequestScrollTo(issue.CueId);
        }
    }

    [RelayCommand]
    private async Task FixGapsAsync()
    {
        var result = GapFixer.Fix(Cues, Settings.Thresholds.MinGapMs);
        if (result.FixedCount == 0)
        {
            await _dialogs.AlertAsync("一鍵修正",
                result.Unfixed.IsEmpty ? "沒有需要修正的重疊或間隔。" : $"有 {result.Unfixed.Length} 處無法自動修正，原因已標示在品質面板。");
            return;
        }
        Commit(result.Cues, $"一鍵修正 {result.FixedCount} 處重疊/間隔");
        if (!result.Unfixed.IsEmpty)
        {
            StatusMessage += $"；{result.Unfixed.Length} 處無法修正（已標示原因）";
        }
    }

    // ---- Threshold settings (edited as text, validated on apply) ----

    [ObservableProperty]
    private string _minGapMs = string.Empty;

    [ObservableProperty]
    private string _minDurationMs = string.Empty;

    [ObservableProperty]
    private string _maxDurationMs = string.Empty;

    [ObservableProperty]
    private string _maxCharsPerSecond = string.Empty;

    [ObservableProperty]
    private string _maxLineLength = string.Empty;

    [ObservableProperty]
    private string _maxLines = string.Empty;

    private void LoadThresholdFields(QualityThresholds t)
    {
        MinGapMs = t.MinGapMs.ToString(CultureInfo.InvariantCulture);
        MinDurationMs = t.MinDurationMs.ToString(CultureInfo.InvariantCulture);
        MaxDurationMs = t.MaxDurationMs.ToString(CultureInfo.InvariantCulture);
        MaxCharsPerSecond = t.MaxCharsPerSecond.ToString(CultureInfo.InvariantCulture);
        MaxLineLength = t.MaxLineLength.ToString(CultureInfo.InvariantCulture);
        MaxLines = t.MaxLines.ToString(CultureInfo.InvariantCulture);
    }

    [RelayCommand]
    private async Task ApplyThresholdsAsync()
    {
        static bool Long(string s, out long v) => long.TryParse(s.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out v);
        static bool Int(string s, out int v) => int.TryParse(s.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out v);

        if (!Long(MinGapMs, out var gap) || !Long(MinDurationMs, out var minDur) || !Long(MaxDurationMs, out var maxDur) ||
            !double.TryParse(MaxCharsPerSecond.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cps) ||
            !Int(MaxLineLength, out var lineLength) || !Int(MaxLines, out var lines))
        {
            await _dialogs.AlertAsync("設定", "門檻必須是非負數字（閱讀速度可含小數）。");
            return;
        }
        if (maxDur <= minDur || cps <= 0 || lineLength <= 0 || lines <= 0)
        {
            await _dialogs.AlertAsync("設定", "最長時長必須大於最短時長，閱讀速度、單行長度與行數必須大於 0。");
            return;
        }
        var thresholds = new QualityThresholds
        {
            MinGapMs = gap,
            MinDurationMs = minDur,
            MaxDurationMs = maxDur,
            MaxCharsPerSecond = cps,
            MaxLineLength = lineLength,
            MaxLines = lines,
        };
        SaveSettings(Settings with { Thresholds = thresholds });
        Refresh();
        StatusMessage = "門檻已更新並儲存";
    }

    [RelayCommand]
    private async Task ResetThresholdsAsync()
    {
        LoadThresholdFields(QualityThresholds.Default);
        await ApplyThresholdsAsync();
    }
}
