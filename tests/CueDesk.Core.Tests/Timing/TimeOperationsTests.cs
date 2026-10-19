using CueDesk.Core.Editing;
using CueDesk.Core.Model;
using CueDesk.Core.Timing;

namespace CueDesk.Core.Tests.Timing;

public class TimeOperationsTests
{
    private static readonly IReadOnlyList<Cue> Cues = CueList.From(
        (1_000, 2_000, "a"),
        (3_000, 5_000, "b"),
        (10_000, 12_000, "c"));

    // ---- Shift ----

    [Fact]
    public void Shift_positive_moves_all_cues()
    {
        var r = TimeOperations.Shift(Cues, 500);
        Assert.Equal([(1_500L, 2_500L), (3_500L, 5_500L), (10_500L, 12_500L)], r.Cues.Select(c => (c.StartMs, c.EndMs)));
        Assert.Equal((3, 3, 0, 0), (r.TargetCount, r.ChangedCount, r.ClampedCount, r.RemovedCount));
    }

    [Fact]
    public void Shift_only_selected_cues()
    {
        var r = TimeOperations.Shift(Cues, -1_000, [2]);
        Assert.Equal([(1_000L, 2_000L), (2_000L, 4_000L), (10_000L, 12_000L)], r.Cues.Select(c => (c.StartMs, c.EndMs)));
        Assert.Equal((1, 1), (r.TargetCount, r.ChangedCount));
    }

    [Fact]
    public void Shift_crossing_zero_clamps_start_and_keeps_shifted_end()
    {
        var r = TimeOperations.Shift(Cues, -1_500);
        Assert.Equal((0L, 500L), (r.Cues[0].StartMs, r.Cues[0].EndMs));
        Assert.Equal((1, 0), (r.ClampedCount, r.RemovedCount));
    }

    [Fact]
    public void Shift_entirely_before_zero_removes_cue()
    {
        var r = TimeOperations.Shift(Cues, -3_500);
        Assert.Equal(["b", "c"], r.Cues.Select(c => c.Text));
        Assert.Equal((0L, 1_500L), (r.Cues[0].StartMs, r.Cues[0].EndMs));
        Assert.Equal((3, 2, 1, 1), (r.TargetCount, r.ChangedCount, r.ClampedCount, r.RemovedCount));
    }

    [Fact]
    public void Shift_ending_exactly_at_zero_counts_as_before_zero()
    {
        var r = TimeOperations.Shift(Cues, -2_000);
        Assert.Equal(1, r.RemovedCount);
        Assert.DoesNotContain(r.Cues, c => c.Text == "a");
    }

    [Fact]
    public void Shift_never_produces_negative_times()
    {
        var r = TimeOperations.Shift(Cues, -11_000);
        Assert.All(r.Cues, c => Assert.True(c.StartMs >= 0 && c.EndMs > c.StartMs));
    }

    [Fact]
    public void Shift_by_zero_reports_no_changes()
    {
        var r = TimeOperations.Shift(Cues, 0);
        Assert.Equal((3, 0), (r.TargetCount, r.ChangedCount));
    }

    [Fact]
    public void Shift_beyond_max_time_is_rejected() =>
        Assert.Throws<EditRejectedException>(() => TimeOperations.Shift(Cues, TimeFormat.MaxMs));

    [Fact]
    public void Shift_of_selection_resorts_list()
    {
        var r = TimeOperations.Shift(Cues, 20_000, [1]);
        Assert.Equal(["b", "c", "a"], r.Cues.Select(c => c.Text));
    }

    // ---- Two-point sync ----

    [Fact]
    public void TwoPoint_places_anchors_exactly_and_others_match_hand_calculation()
    {
        // a1 = 10000 → b1 = 11000, a2 = 110000 → b2 = 114000; slope = 103000 / 100000 = 1.03
        var cues = CueList.From(
            (10_000, 12_000, "A"),
            (60_000, 61_000, "mid"),
            (110_000, 111_500, "B"),
            (0, 500, "before"));
        var a = cues.Single(c => c.Text == "A").Id;
        var b = cues.Single(c => c.Text == "B").Id;

        var r = TimeOperations.TwoPointSync(cues, a, 11_000, b, 114_000);
        var byText = r.Cues.ToDictionary(c => c.Text);

        Assert.Equal((11_000L, 13_060L), (byText["A"].StartMs, byText["A"].EndMs));      // 11000 + 2000×1.03
        Assert.Equal((114_000L, 115_545L), (byText["B"].StartMs, byText["B"].EndMs));    // 114000 + 1500×1.03
        Assert.Equal((62_500L, 63_530L), (byText["mid"].StartMs, byText["mid"].EndMs));  // 11000 + 50000×1.03
        Assert.Equal((700L, 1_215L), (byText["before"].StartMs, byText["before"].EndMs)); // 11000 − 10000×1.03 = 700
        Assert.Equal(["before", "A", "mid", "B"], r.Cues.Select(c => c.Text));
    }

    [Fact]
    public void TwoPoint_rounds_half_away_from_zero()
    {
        // slope = 1/2 : t = 3001 → 0 + 3001 / 2 = 1500.5 → 1501
        var cues = CueList.From((0, 1_000, "A"), (2_000, 2_500, "B"), (3_001, 3_500, "C"));
        var r = TimeOperations.TwoPointSync(cues, 1, 0, 2, 1_000);
        Assert.Equal(1_501, r.Cues.Single(c => c.Text == "C").StartMs);
    }

    [Fact]
    public void TwoPoint_works_with_anchors_given_in_reverse_order()
    {
        var cues = CueList.From((10_000, 12_000, "A"), (110_000, 111_500, "B"));
        var r = TimeOperations.TwoPointSync(cues, 2, 114_000, 1, 11_000);
        Assert.Equal([11_000L, 114_000L], r.Cues.Select(c => c.StartMs));
    }

    [Fact]
    public void TwoPoint_rejects_same_cue() =>
        Assert.Throws<EditRejectedException>(() => TimeOperations.TwoPointSync(Cues, 1, 0, 1, 100));

    [Fact]
    public void TwoPoint_rejects_reversed_target_order() =>
        Assert.Throws<EditRejectedException>(() => TimeOperations.TwoPointSync(Cues, 1, 5_000, 3, 4_000));

    [Fact]
    public void TwoPoint_rejects_equal_targets() =>
        Assert.Throws<EditRejectedException>(() => TimeOperations.TwoPointSync(Cues, 1, 5_000, 3, 5_000));

    [Fact]
    public void TwoPoint_rejects_anchors_with_identical_original_start()
    {
        var cues = CueList.From((1_000, 2_000, "x"), (1_000, 3_000, "y"));
        Assert.Throws<EditRejectedException>(() => TimeOperations.TwoPointSync(cues, 1, 0, 2, 5_000));
    }

    // ---- Frame rate ----

    [Theory]
    // t' = t × src / dst, rounded half away from zero (hand calculated)
    [InlineData("23.976", "25", 60_000, 57_542)]   // 60000 × 24000 / 25025 = 57542.46
    [InlineData("25", "23.976", 60_000, 62_563)]   // 60000 × 25025 / 24000 = 62562.5
    [InlineData("24", "25", 60_000, 57_600)]
    [InlineData("25", "24", 60_000, 62_500)]
    [InlineData("25", "29.97", 60_000, 50_050)]    // 60000 × 25025 / 30000
    [InlineData("29.97", "25", 60_000, 71_928)]    // 1.8e9 / 25025 = 71928.07
    [InlineData("23.976", "25", 3_600_000, 3_452_547)] // 1 h: 86.4e9 / 25025 = 3452547.45
    [InlineData("30", "29.97", 1_000, 1_001)]
    public void FrameRate_conversion_matches_hand_calculation(string from, string to, long t, long expected)
    {
        var cues = CueList.From((t, t + 1, "x"));
        var r = TimeOperations.ConvertFrameRate(cues, Rate(from), Rate(to));
        Assert.Equal(expected, r.Cues[0].StartMs);
    }

    [Fact]
    public void FrameRate_round_trip_returns_close_to_original()
    {
        var once = TimeOperations.ConvertFrameRate(Cues, FrameRate.Fps23_976, FrameRate.Fps25).Cues;
        var back = TimeOperations.ConvertFrameRate(once, FrameRate.Fps25, FrameRate.Fps23_976).Cues;
        Assert.All(back.Zip(Cues), p => Assert.InRange(p.First.StartMs - p.Second.StartMs, -1, 1));
    }

    [Fact]
    public void FrameRate_speedup_shortens_and_slowdown_lengthens()
    {
        var faster = TimeOperations.ConvertFrameRate(Cues, FrameRate.Fps23_976, FrameRate.Fps25).Cues;
        var slower = TimeOperations.ConvertFrameRate(Cues, FrameRate.Fps25, FrameRate.Fps23_976).Cues;
        Assert.True(faster[2].StartMs < Cues[2].StartMs);
        Assert.True(slower[2].StartMs > Cues[2].StartMs);
    }

    [Fact]
    public void Rounding_never_collapses_duration_to_zero()
    {
        var cues = CueList.From((1_002, 1_003, "tiny")); // ×0.8 → 801.6 and 802.4 both round to 802
        var r = TimeOperations.ConvertFrameRate(cues, FrameRate.Fps24, FrameRate.Fps30); // ×0.8
        Assert.True(r.Cues[0].DurationMs >= 1);
    }

    private static FrameRate Rate(string name) => FrameRate.All.Single(f => f.Name == name);
}
