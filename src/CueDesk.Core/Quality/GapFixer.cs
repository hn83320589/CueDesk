using System.Collections.Immutable;
using CueDesk.Core.Model;

namespace CueDesk.Core.Quality;

/// <param name="CueId">The later cue of the pair (the one the overlap/gap issue is reported on).</param>
public sealed record UnfixedPair(int CueId, int PreviousCueId, string Reason);

public sealed record FixResult(ImmutableArray<Cue> Cues, int FixedCount, ImmutableArray<UnfixedPair> Unfixed);

/// <summary>
/// One-click fix for overlaps and too-small gaps: the previous cue's end is moved to
/// next start − min gap. Start times never change (so order is preserved), and a fix
/// that would leave the previous cue with a duration ≤ 0 is skipped and reported.
/// </summary>
public static class GapFixer
{
    public static FixResult Fix(IReadOnlyList<Cue> cues, long minGapMs)
    {
        var result = cues.ToArray();
        var fixedCount = 0;
        var unfixed = ImmutableArray.CreateBuilder<UnfixedPair>();
        for (var i = 1; i < result.Length; i++)
        {
            var prev = result[i - 1];
            var next = result[i];
            if (next.StartMs - prev.EndMs >= minGapMs)
            {
                continue;
            }
            var newEnd = next.StartMs - minGapMs;
            if (newEnd > prev.StartMs)
            {
                result[i - 1] = prev with { EndMs = newEnd };
                fixedCount++;
                continue;
            }
            var reason = next.StartMs == prev.StartMs
                ? "兩筆起始時間相同，無法只靠縮短前一筆修正"
                : $"前一筆起始到本筆起始只有 {next.StartMs - prev.StartMs} ms，扣除最小間隔 {minGapMs} ms 後時長將 ≤ 0";
            unfixed.Add(new UnfixedPair(next.Id, prev.Id, reason));
        }
        return new FixResult([.. result], fixedCount, unfixed.ToImmutable());
    }

    /// <summary>Attaches fix-failure reasons to the matching overlap/gap issues.</summary>
    public static ImmutableArray<QualityIssue> Annotate(ImmutableArray<QualityIssue> issues, IEnumerable<UnfixedPair> unfixed)
    {
        var reasons = unfixed.ToDictionary(u => u.CueId, u => u.Reason);
        return [.. issues.Select(issue => issue.IsFixable && reasons.TryGetValue(issue.CueId, out var reason)
            ? issue with { FixFailure = reason }
            : issue)];
    }
}
