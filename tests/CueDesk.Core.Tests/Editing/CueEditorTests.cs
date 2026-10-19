using CueDesk.Core.Editing;
using CueDesk.Core.Model;

namespace CueDesk.Core.Tests.Editing;

public class CueEditorTests
{
    // ids 1..3 in time order
    private static readonly IReadOnlyList<Cue> Three = CueList.From(
        (1_000, 3_000, "first"),
        (4_000, 6_000, "second"),
        (8_000, 9_000, "third"));

    [Fact]
    public void UpdateText_normalizes_line_breaks()
    {
        var result = CueEditor.UpdateText(Three, 2, "a\r\nb");
        Assert.Equal("a\nb", result[1].Text);
    }

    [Fact]
    public void UpdateTimes_resorts_so_sequence_follows_start_time()
    {
        var result = CueEditor.UpdateTimes(Three, 1, 10_000, 11_000);
        Assert.Equal([2, 3, 1], result.Select(c => c.Id));
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(500, 500)]
    [InlineData(600, 500)]
    public void UpdateTimes_rejects_invalid_ranges(long start, long end) =>
        Assert.Throws<EditRejectedException>(() => CueEditor.UpdateTimes(Three, 1, start, end));

    [Fact]
    public void Split_creates_two_contiguous_parts_and_keeps_order()
    {
        var cues = CueList.From((1_000, 5_000, "Hello there world"), (6_000, 7_000, "next"));
        var result = CueEditor.Split(cues, 1, 3_000, textIndex: 5);

        Assert.Equal(3, result.Length);
        Assert.Equal((1_000L, 3_000L, "Hello", 1), (result[0].StartMs, result[0].EndMs, result[0].Text, result[0].Id));
        Assert.Equal((3_000L, 5_000L, "there world"), (result[1].StartMs, result[1].EndMs, result[1].Text));
        Assert.Equal(3, result[1].Id);
        Assert.Equal("next", result[2].Text);
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(3_000)]
    [InlineData(500)]
    public void Split_rejects_points_outside_the_cue(long at) =>
        Assert.Throws<EditRejectedException>(() => CueEditor.Split(Three, 1, at));

    [Fact]
    public void Split_without_text_index_splits_two_line_text_between_lines()
    {
        var cues = CueList.From((0, 4_000, "line one\nline two"));
        var result = CueEditor.Split(cues, 1, 2_000);
        Assert.Equal(["line one", "line two"], result.Select(c => c.Text));
    }

    [Theory]
    // "Hello there world": target 9; cuts after a space are 6 and 12, both 3 away → the earlier wins.
    [InlineData("Hello there world", 0.5, 6)]
    // No separators: cut exactly at round(7 × 0.5) = 4.
    [InlineData("你好世界大家好", 0.5, 4)]
    // Target 6; the full-width comma at index 6 gives a cut at 7, distance 1.
    [InlineData("今天天氣很好，我們出門吧", 0.5, 7)]
    public void SuggestTextSplit_prefers_spaces_and_punctuation(string text, double ratio, int expected) =>
        Assert.Equal(expected, CueEditor.SuggestTextSplit(text, ratio));

    [Fact]
    public void MergeWithNext_combines_time_span_and_text()
    {
        var result = CueEditor.MergeWithNext(Three, 1);
        Assert.Equal(2, result.Length);
        Assert.Equal((1_000L, 6_000L, "first\nsecond", 1), (result[0].StartMs, result[0].EndMs, result[0].Text, result[0].Id));
        Assert.Equal("third", result[1].Text);
    }

    [Fact]
    public void MergeWithNext_rejects_last_cue() =>
        Assert.Throws<EditRejectedException>(() => CueEditor.MergeWithNext(Three, 3));

    [Fact]
    public void Delete_removes_selected_and_remaining_keep_order()
    {
        var result = CueEditor.Delete(Three, [1, 3]);
        Assert.Equal(["second"], result.Select(c => c.Text));
    }

    [Fact]
    public void InsertAfter_places_cue_after_selection_with_gap()
    {
        var (result, newId) = CueEditor.InsertAfter(Three, 2, minGapMs: 40);
        var inserted = result.Single(c => c.Id == newId);
        Assert.Equal((6_040L, 7_960L), (inserted.StartMs, inserted.EndMs)); // capped by next start − gap
        Assert.Equal(2, result.ToList().IndexOf(inserted));
    }

    [Fact]
    public void InsertAfter_uses_default_duration_when_room()
    {
        var (result, newId) = CueEditor.InsertAfter(Three, 3, 40);
        var inserted = result.Single(c => c.Id == newId);
        Assert.Equal((9_040L, 11_040L), (inserted.StartMs, inserted.EndMs));
    }

    [Fact]
    public void InsertAfter_with_no_room_still_inserts_default_duration()
    {
        var cues = CueList.From((0, 1_000, "a"), (1_020, 2_000, "b"));
        var (result, newId) = CueEditor.InsertAfter(cues, 1, 40);
        var inserted = result.Single(c => c.Id == newId);
        Assert.Equal((1_040L, 3_040L), (inserted.StartMs, inserted.EndMs));
    }

    [Fact]
    public void InsertAfter_into_empty_list_starts_at_zero()
    {
        var (result, _) = CueEditor.InsertAfter([], null, 40);
        Assert.Equal((0L, 2_000L), (result[0].StartMs, result[0].EndMs));
    }
}
