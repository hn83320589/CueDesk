using System.Collections.Immutable;
using CueDesk.Core.Model;
using CueDesk.Core.Timing;

namespace CueDesk.Core.Editing;

/// <summary>Raised when an edit is rejected; the message is shown to the user as-is.</summary>
public sealed class EditRejectedException(string message) : Exception(message);

/// <summary>
/// Single-cue edits. Every method returns a new list sorted by start time
/// (sequence numbers are positions, so they are renumbered implicitly).
/// </summary>
public static class CueEditor
{
    public const long DefaultInsertDurationMs = 2000;

    private const string SplitPunctuation = " \t，。！？、；：,.!?;:";

    public static ImmutableArray<Cue> UpdateText(IReadOnlyList<Cue> cues, int id, string text)
    {
        var cue = Find(cues, id);
        return Replace(cues, cue, cue with { Text = NormalizeText(text) });
    }

    public static ImmutableArray<Cue> UpdateTimes(IReadOnlyList<Cue> cues, int id, long startMs, long endMs)
    {
        ValidateRange(startMs, endMs);
        var cue = Find(cues, id);
        return Replace(cues, cue, cue with { StartMs = startMs, EndMs = endMs });
    }

    /// <summary>
    /// Splits a cue at <paramref name="atMs"/> into [start, at] and [at, end] (no gap between them).
    /// Text is cut at <paramref name="textIndex"/>; when null, <see cref="SuggestTextSplit"/> picks the cut.
    /// The first part keeps the original id.
    /// </summary>
    public static ImmutableArray<Cue> Split(IReadOnlyList<Cue> cues, int id, long atMs, int? textIndex = null)
    {
        var cue = Find(cues, id);
        if (atMs <= cue.StartMs || atMs >= cue.EndMs)
        {
            throw new EditRejectedException(
                $"分割點必須在 {TimeFormat.Format(cue.StartMs)} 與 {TimeFormat.Format(cue.EndMs)} 之間（不含兩端）");
        }
        var ratio = (double)(atMs - cue.StartMs) / cue.DurationMs;
        var index = Math.Clamp(textIndex ?? SuggestTextSplit(cue.Text, ratio), 0, cue.Text.Length);
        var first = cue with { EndMs = atMs, Text = cue.Text[..index].TrimEnd() };
        var second = new Cue(CueList.NextId(cues), atMs, cue.EndMs, cue.Text[index..].TrimStart());
        return CueList.Sort(cues.Where(c => c.Id != id).Append(first).Append(second));
    }

    /// <summary>
    /// Multi-line text splits between lines (first half of the lines goes first).
    /// Single-line text splits at the space or punctuation nearest to <paramref name="ratio"/> of its length,
    /// or exactly there when there is none (CJK text often has no spaces).
    /// </summary>
    public static int SuggestTextSplit(string text, double ratio)
    {
        var lines = text.Split('\n');
        if (lines.Length >= 2)
        {
            var take = (lines.Length + 1) / 2;
            return lines.Take(take).Sum(l => l.Length + 1) - 1;
        }
        if (text.Length < 2)
        {
            return text.Length;
        }
        var target = Math.Clamp((int)Math.Round(text.Length * ratio, MidpointRounding.AwayFromZero), 1, text.Length - 1);
        for (var distance = 0; distance < text.Length; distance++)
        {
            foreach (var i in new[] { target - distance, target + distance })
            {
                // Cut just after a separator character.
                if (i >= 1 && i <= text.Length - 1 && SplitPunctuation.Contains(text[i - 1]))
                {
                    return i;
                }
            }
        }
        return target;
    }

    /// <summary>Merges the cue with the next one in order: min start, max end, texts joined by a line break.</summary>
    public static ImmutableArray<Cue> MergeWithNext(IReadOnlyList<Cue> cues, int id)
    {
        var index = IndexOrThrow(cues, id);
        if (index == cues.Count - 1)
        {
            throw new EditRejectedException("最後一筆字幕沒有下一筆可以合併");
        }
        var a = cues[index];
        var b = cues[index + 1];
        var text = string.Join("\n", new[] { a.Text, b.Text }.Where(t => t.Length > 0));
        var merged = a with { StartMs = Math.Min(a.StartMs, b.StartMs), EndMs = Math.Max(a.EndMs, b.EndMs), Text = text };
        return CueList.Sort(cues.Where(c => c.Id != a.Id && c.Id != b.Id).Append(merged));
    }

    public static ImmutableArray<Cue> Delete(IReadOnlyList<Cue> cues, IReadOnlyCollection<int> ids)
    {
        if (ids.Count == 0)
        {
            throw new EditRejectedException("沒有選取要刪除的字幕");
        }
        return cues.Where(c => !ids.Contains(c.Id)).ToImmutableArray();
    }

    /// <summary>
    /// Inserts an empty cue after <paramref name="afterId"/> (or at 0 when null/the list is empty):
    /// start = previous end + minGap, end = min(start + 2000, next start − minGap);
    /// when there is no room, the default 2000 ms is used and the quality check will flag the overlap.
    /// </summary>
    public static (ImmutableArray<Cue> Cues, int NewId) InsertAfter(IReadOnlyList<Cue> cues, int? afterId, long minGapMs)
    {
        var newId = CueList.NextId(cues);
        long start = 0;
        long? nextStart = cues.Count > 0 ? cues[0].StartMs : null;
        if (afterId is { } id)
        {
            var index = IndexOrThrow(cues, id);
            start = cues[index].EndMs + minGapMs;
            nextStart = index + 1 < cues.Count ? cues[index + 1].StartMs : null;
        }
        var end = start + DefaultInsertDurationMs;
        if (nextStart is { } ns && ns - minGapMs < end && ns - minGapMs > start)
        {
            end = ns - minGapMs;
        }
        if (end > TimeFormat.MaxMs)
        {
            throw new EditRejectedException("已到達時間上限 99:59:59,999，無法再插入");
        }
        var inserted = new Cue(newId, start, end, string.Empty);
        return (CueList.Sort(cues.Append(inserted)), newId);
    }

    public static void ValidateRange(long startMs, long endMs)
    {
        if (startMs < 0)
        {
            throw new EditRejectedException("起始時間不可為負");
        }
        if (endMs <= startMs)
        {
            throw new EditRejectedException("結束時間必須晚於起始時間");
        }
        if (endMs > TimeFormat.MaxMs)
        {
            throw new EditRejectedException("時間不可超過 99:59:59,999");
        }
    }

    public static string NormalizeText(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n');

    private static ImmutableArray<Cue> Replace(IReadOnlyList<Cue> cues, Cue oldCue, Cue newCue) =>
        CueList.Sort(cues.Select(c => c.Id == oldCue.Id ? newCue : c));

    private static Cue Find(IReadOnlyList<Cue> cues, int id) => cues[IndexOrThrow(cues, id)];

    private static int IndexOrThrow(IReadOnlyList<Cue> cues, int id)
    {
        var index = CueList.IndexOf(cues, id);
        return index >= 0 ? index : throw new EditRejectedException($"找不到字幕（id={id}）");
    }
}
