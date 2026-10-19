using System.Collections.Immutable;

namespace CueDesk.Core.Model;

/// <summary>Helpers that keep a cue list in its canonical form: ordered by start time.</summary>
public static class CueList
{
    /// <summary>Stable sort by start time; cues with equal start keep their relative order.</summary>
    public static ImmutableArray<Cue> Sort(IEnumerable<Cue> cues) =>
        cues.OrderBy(c => c.StartMs).ToImmutableArray();

    public static int NextId(IReadOnlyList<Cue> cues) =>
        cues.Count == 0 ? 1 : cues.Max(c => c.Id) + 1;

    /// <summary>Returns the index of the cue with <paramref name="id"/>, or -1.</summary>
    public static int IndexOf(IReadOnlyList<Cue> cues, int id)
    {
        for (var i = 0; i < cues.Count; i++)
        {
            if (cues[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Builds cues with ids 1..n from (start, end, text) tuples; handy for callers and tests.</summary>
    public static ImmutableArray<Cue> From(params (long Start, long End, string Text)[] items) =>
        Sort(items.Select((t, i) => new Cue(i + 1, t.Start, t.End, t.Text)));
}
