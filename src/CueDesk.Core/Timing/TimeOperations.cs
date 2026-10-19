using System.Collections.Immutable;
using CueDesk.Core.Editing;
using CueDesk.Core.Model;

namespace CueDesk.Core.Timing;

/// <summary>
/// Outcome of a batch time operation; also serves as the preview summary.
/// </summary>
/// <param name="TargetCount">Cues the operation was applied to.</param>
/// <param name="ChangedCount">Kept cues whose start or end actually changed.</param>
/// <param name="ClampedCount">Cues that crossed zero and had their start clamped to 0.</param>
/// <param name="RemovedCount">Cues that ended at or before 0 and were removed.</param>
public sealed record TimeOperationResult(
    ImmutableArray<Cue> Cues, int TargetCount, int ChangedCount, int ClampedCount, int RemovedCount);

public sealed record FrameRate(string Name, long Numerator, long Denominator)
{
    public static readonly FrameRate Fps23_976 = new("23.976", 24000, 1001);
    public static readonly FrameRate Fps24 = new("24", 24, 1);
    public static readonly FrameRate Fps25 = new("25", 25, 1);
    public static readonly FrameRate Fps29_97 = new("29.97", 30000, 1001);
    public static readonly FrameRate Fps30 = new("30", 30, 1);

    public static IReadOnlyList<FrameRate> All { get; } = [Fps23_976, Fps24, Fps25, Fps29_97, Fps30];

    public override string ToString() => Name;
}

/// <summary>
/// Batch time operations. Shared rules after mapping each start/end:
/// end ≤ 0 → cue removed; start &lt; 0 &lt; end → start clamped to 0;
/// a duration that rounding collapsed to 0 is restored to 1 ms;
/// any time beyond 99:59:59,999 rejects the whole operation.
/// </summary>
public static class TimeOperations
{
    /// <summary>Adds <paramref name="offsetMs"/> to start and end of the target cues (all when <paramref name="ids"/> is null).</summary>
    public static TimeOperationResult Shift(IReadOnlyList<Cue> cues, long offsetMs, IReadOnlyCollection<int>? ids = null) =>
        Map(cues, ids, t => t + offsetMs);

    /// <summary>
    /// Linear re-timing from two anchors: t' = b1 + (t − a1) × (b2 − b1) / (a2 − a1),
    /// where a1/a2 are the anchors' current starts and b1/b2 their desired starts.
    /// </summary>
    public static TimeOperationResult TwoPointSync(IReadOnlyList<Cue> cues, int anchorAId, long newStartA, int anchorBId, long newStartB)
    {
        if (anchorAId == anchorBId)
        {
            throw new EditRejectedException("兩個錨點必須是不同的兩筆字幕");
        }
        var a = Find(cues, anchorAId);
        var b = Find(cues, anchorBId);
        if (newStartA < 0 || newStartB < 0)
        {
            throw new EditRejectedException("錨點的目標時間不可為負");
        }
        long a1 = a.StartMs, a2 = b.StartMs, b1 = newStartA, b2 = newStartB;
        if (a1 == a2)
        {
            throw new EditRejectedException("兩個錨點的原始起始時間相同，無法計算比例");
        }
        if (b1 == b2 || (a2 - a1 > 0) != (b2 - b1 > 0))
        {
            throw new EditRejectedException("錨點目標時間的先後順序必須與原本順序一致");
        }
        return Map(cues, null, t => b1 + IntMath.DivRound((Int128)(t - a1) * (b2 - b1), a2 - a1));
    }

    /// <summary>
    /// Speed change between frame rates (frame count preserved): t' = t × source / target.
    /// E.g. 23.976 → 25 makes everything ~4.1% earlier.
    /// </summary>
    public static TimeOperationResult ConvertFrameRate(IReadOnlyList<Cue> cues, FrameRate source, FrameRate target) =>
        Map(cues, null, t => IntMath.DivRound((Int128)t * source.Numerator * target.Denominator,
            (Int128)source.Denominator * target.Numerator));

    private static TimeOperationResult Map(IReadOnlyList<Cue> cues, IReadOnlyCollection<int>? ids, Func<long, long> map)
    {
        var result = new List<Cue>(cues.Count);
        int targets = 0, changed = 0, clamped = 0, removed = 0;
        foreach (var cue in cues)
        {
            if (ids is not null && !ids.Contains(cue.Id))
            {
                result.Add(cue);
                continue;
            }
            targets++;
            var start = map(cue.StartMs);
            var end = map(cue.EndMs);
            if (end <= 0)
            {
                removed++;
                continue;
            }
            if (start < 0)
            {
                start = 0;
                clamped++;
            }
            if (end <= start)
            {
                end = start + 1;
            }
            if (end > TimeFormat.MaxMs)
            {
                throw new EditRejectedException($"第 {result.Count + 1} 筆附近的時間會超過 99:59:59,999，操作已取消");
            }
            if (start != cue.StartMs || end != cue.EndMs)
            {
                changed++;
            }
            result.Add(cue with { StartMs = start, EndMs = end });
        }
        return new TimeOperationResult(CueList.Sort(result), targets, changed, clamped, removed);
    }

    private static Cue Find(IReadOnlyList<Cue> cues, int id)
    {
        var index = CueList.IndexOf(cues, id);
        return index >= 0 ? cues[index] : throw new EditRejectedException($"找不到字幕（id={id}）");
    }
}
