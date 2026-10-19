using CueDesk.Core.Model;
using CueDesk.Core.Quality;

namespace CueDesk.Core.Tests.Quality;

public class QualityCheckerTests
{
    private static readonly QualityThresholds Defaults = QualityThresholds.Default;

    /// <summary>A clean cue followed by the cue under test, 1 s apart.</summary>
    private static IReadOnlyList<Cue> WithClean(long start, long end, string text) =>
        CueList.From((0, 2_000, "Hello"), (start, end, text));

    private static IssueType[] Types(IReadOnlyList<Cue> cues, QualityThresholds? t = null) =>
        [.. QualityChecker.Check(cues, t ?? Defaults).Select(i => i.Type)];

    [Fact]
    public void Clean_subtitles_have_no_issues() =>
        Assert.Empty(Types(WithClean(3_000, 5_000, "World")));

    [Fact]
    public void Overlap_is_detected_alone()
    {
        var issues = QualityChecker.Check(WithClean(1_500, 3_500, "World"), Defaults);
        var issue = Assert.Single(issues);
        Assert.Equal(IssueType.Overlap, issue.Type);
        Assert.Equal(2, issue.Sequence);
        Assert.Contains("500 ms", issue.Message);
    }

    [Theory]
    [InlineData(2_000)]  // gap 0
    [InlineData(2_039)]  // gap 39
    public void Small_gap_is_detected_alone(long start) =>
        Assert.Equal([IssueType.GapTooSmall], Types(WithClean(start, start + 2_000, "World")));

    [Fact]
    public void Gap_equal_to_minimum_is_fine() =>
        Assert.Empty(Types(WithClean(2_040, 4_040, "World")));

    [Fact]
    public void Too_short_is_detected_alone() =>
        Assert.Equal([IssueType.TooShort], Types(WithClean(3_000, 3_699, "Hi")));

    [Fact]
    public void Too_long_is_detected_alone() =>
        Assert.Equal([IssueType.TooLong], Types(WithClean(3_000, 10_001, "World")));

    [Fact]
    public void Reading_speed_english_over_limit()
    {
        // 36 characters (spaces count) in 2 s = 18 cps > 17
        var text = "The quick brown fox jumps over lazy.";
        Assert.Equal(36, text.Length);
        Assert.Equal([IssueType.ReadingSpeed], Types(WithClean(3_000, 5_000, text)));
    }

    [Fact]
    public void Reading_speed_english_at_limit_is_fine()
    {
        var text = "The quick brown fox jumps over la."; // 34 chars / 2 s = 17 cps
        Assert.Empty(Types(WithClean(3_000, 5_000, text)));
    }

    [Fact]
    public void Reading_speed_ignores_tags_and_line_breaks()
    {
        // Without tags: 34 chars → exactly 17 cps, so no issue.
        var text = "<i>The quick brown fox</i>\n{\\an8}jumps over la.";
        Assert.Empty(Types(WithClean(3_000, 5_000, text)));
    }

    [Fact]
    public void Reading_speed_chinese_weights_wide_characters()
    {
        const string text = "今天我們來學習如何使用字幕軟體"; // 15 chars → weight 30
        // 30 / 2.0 s = 15 cps (≈ 7.5 漢字/秒) → fine
        Assert.Empty(Types(WithClean(3_000, 5_000, text)));
        // 30 / 1.5 s = 20 cps (10 漢字/秒) → too fast
        Assert.Equal([IssueType.ReadingSpeed], Types(WithClean(3_000, 4_500, text)));
    }

    [Fact]
    public void Line_too_long_english()
    {
        var line = new string('a', 43);
        var issues = QualityChecker.Check(WithClean(3_000, 7_000, line), Defaults);
        Assert.Equal(IssueType.LineTooLong, Assert.Single(issues).Type);
    }

    [Fact]
    public void Line_too_long_chinese_uses_display_width()
    {
        // 21 漢字 = width 42 → ok; 22 漢字 = 44 → too long (durations long enough for reading speed)
        Assert.Empty(Types(WithClean(3_000, 6_000, new string('字', 21))));
        Assert.Equal([IssueType.LineTooLong], Types(WithClean(3_000, 6_000, new string('字', 22))));
    }

    [Fact]
    public void More_than_two_lines_is_detected_alone() =>
        Assert.Equal([IssueType.TooManyLines], Types(WithClean(3_000, 6_000, "a\nb\nc")));

    [Fact]
    public void Multiple_issues_on_one_cue_are_all_reported()
    {
        // overlaps, too short, too fast
        var types = Types(WithClean(1_900, 2_200, "A sentence that is too fast"));
        Assert.Equal([IssueType.Overlap, IssueType.TooShort, IssueType.ReadingSpeed], types);
    }

    [Fact]
    public void Changing_thresholds_changes_results()
    {
        var cues = WithClean(2_100, 2_900, "Hello world"); // gap 100, duration 800, 13.75 cps, width 11
        Assert.Empty(Types(cues));
        var strict = new QualityThresholds
        {
            MinGapMs = 200,
            MinDurationMs = 1_000,
            MaxCharsPerSecond = 10,
            MaxLineLength = 10,
        };
        Assert.Equal(
            [IssueType.GapTooSmall, IssueType.TooShort, IssueType.ReadingSpeed, IssueType.LineTooLong],
            Types(cues, strict));
        Assert.Equal(
            [IssueType.TooLong, IssueType.TooLong],
            Types(cues, new QualityThresholds { MaxDurationMs = 500 }));
        Assert.Equal([IssueType.TooManyLines], Types(WithClean(3_000, 6_000, "a\nb"), new QualityThresholds { MaxLines = 1 }));
    }

    [Fact]
    public void Issues_reference_cue_ids_for_navigation()
    {
        var cues = WithClean(3_000, 3_100, "x");
        var issue = Assert.Single(QualityChecker.Check(cues, Defaults));
        Assert.Equal(cues[1].Id, issue.CueId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<i></i>")]
    [InlineData("{\\an8}")]
    public void Empty_text_is_detected_alone(string text) =>
        Assert.Equal([IssueType.EmptyText], Types(WithClean(3_000, 5_000, text)));
}