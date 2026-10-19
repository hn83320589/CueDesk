using CueDesk.Core.History;
using CueDesk.Core.Model;
using CueDesk.Core.Quality;

namespace CueDesk.Core.Tests.Quality;

public class GapFixerTests
{
    [Fact]
    public void Fixes_overlap_by_shortening_previous_end()
    {
        var cues = CueList.From((0, 2_500, "a"), (2_000, 4_000, "b"));
        var r = GapFixer.Fix(cues, 40);
        Assert.Equal((0L, 1_960L), (r.Cues[0].StartMs, r.Cues[0].EndMs));
        Assert.Equal((2_000L, 4_000L), (r.Cues[1].StartMs, r.Cues[1].EndMs));
        Assert.Equal(1, r.FixedCount);
        Assert.Empty(r.Unfixed);
    }

    [Fact]
    public void Fixes_small_gap()
    {
        var cues = CueList.From((0, 1_990, "a"), (2_000, 4_000, "b"));
        var r = GapFixer.Fix(cues, 40);
        Assert.Equal(1_960, r.Cues[0].EndMs);
    }

    [Fact]
    public void Never_changes_start_times()
    {
        var cues = CueList.From((0, 2_500, "a"), (2_000, 3_100, "b"), (3_000, 5_000, "c"));
        var r = GapFixer.Fix(cues, 40);
        Assert.Equal(cues.Select(c => c.StartMs), r.Cues.Select(c => c.StartMs));
        Assert.Empty(QualityChecker.Check(r.Cues, new QualityThresholds { MinDurationMs = 0 })
            .Where(i => i.IsFixable));
    }

    [Fact]
    public void Same_start_time_is_left_unfixed_with_reason()
    {
        var cues = CueList.From((1_000, 3_000, "a"), (1_000, 2_000, "b"));
        var r = GapFixer.Fix(cues, 40);
        Assert.Equal(cues.ToArray(), r.Cues.ToArray());
        var unfixed = Assert.Single(r.Unfixed);
        Assert.Equal(cues[1].Id, unfixed.CueId);
        Assert.Contains("起始時間相同", unfixed.Reason);
    }

    [Fact]
    public void Too_little_room_is_left_unfixed_and_duration_stays_positive()
    {
        var cues = CueList.From((1_000, 3_000, "a"), (1_030, 2_000, "b")); // 30 ms room < 40 gap
        var r = GapFixer.Fix(cues, 40);
        Assert.Equal(3_000, r.Cues[0].EndMs);
        Assert.Contains("時長將 ≤ 0", Assert.Single(r.Unfixed).Reason);
        Assert.All(r.Cues, c => Assert.True(c.DurationMs > 0));
    }

    [Fact]
    public void Shortest_allowed_result_is_one_millisecond()
    {
        var cues = CueList.From((1_000, 3_000, "a"), (1_041, 2_000, "b"));
        var r = GapFixer.Fix(cues, 40);
        Assert.Equal(1, r.Cues[0].DurationMs);
    }

    [Fact]
    public void Annotate_marks_unfixed_issues_with_reason()
    {
        var cues = CueList.From((1_000, 3_000, "a"), (1_000, 2_000, "b"), (5_000, 6_000, "c"), (5_500, 7_000, "d"));
        var r = GapFixer.Fix(cues, 40);
        var issues = GapFixer.Annotate(QualityChecker.Check(r.Cues, QualityThresholds.Default), r.Unfixed);
        var overlap = Assert.Single(issues, i => i.Type == IssueType.Overlap);
        Assert.Equal(2, overlap.Sequence);
        Assert.NotNull(overlap.FixFailure);
    }

    [Fact]
    public void Whole_fix_is_undone_in_one_step()
    {
        var cues = CueList.From((0, 2_500, "a"), (2_000, 4_100, "b"), (4_000, 6_000, "c"));
        var stack = new UndoStack<IReadOnlyList<Cue>>(cues);
        stack.Push(GapFixer.Fix(cues, 40).Cues, "一鍵修正");
        Assert.NotEqual(cues.ToArray(), stack.Current.ToArray());
        Assert.Equal(cues.ToArray(), stack.Undo().ToArray());
        Assert.False(stack.CanUndo);
    }
}
