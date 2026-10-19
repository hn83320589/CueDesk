using System.Collections.Immutable;
using System.Globalization;
using CueDesk.Core.Model;
using CueDesk.Core.Text;

namespace CueDesk.Core.Quality;

public enum IssueType
{
    Overlap,
    GapTooSmall,
    TooShort,
    TooLong,
    ReadingSpeed,
    LineTooLong,
    TooManyLines,
}

public sealed record QualityThresholds
{
    public long MinGapMs { get; init; } = 40;
    public long MinDurationMs { get; init; } = 700;
    public long MaxDurationMs { get; init; } = 7000;
    public double MaxCharsPerSecond { get; init; } = 17;
    public int MaxLineLength { get; init; } = 42;
    public int MaxLines { get; init; } = 2;

    public static QualityThresholds Default { get; } = new();
}

/// <param name="Sequence">1-based position of the cue at check time.</param>
/// <param name="FixFailure">Set when an automatic fix was attempted and could not be applied.</param>
public sealed record QualityIssue(IssueType Type, int CueId, int Sequence, string Message, string? FixFailure = null)
{
    public string TypeName => Type switch
    {
        IssueType.Overlap => "時間重疊",
        IssueType.GapTooSmall => "間隔過小",
        IssueType.TooShort => "時長過短",
        IssueType.TooLong => "時長過長",
        IssueType.ReadingSpeed => "閱讀速度過快",
        IssueType.LineTooLong => "單行過長",
        IssueType.TooManyLines => "行數過多",
        _ => Type.ToString(),
    };

    public bool IsFixable => Type is IssueType.Overlap or IssueType.GapTooSmall;

    public bool HasFixFailure => FixFailure is not null;
}

/// <summary>
/// Runs the seven checks. Overlap and gap compare each cue with the previous one in start order and are
/// mutually exclusive (gap &lt; 0 is an overlap, 0 ≤ gap &lt; min is a small gap).
/// Character counts strip tags/style codes and weight East Asian wide characters as 2.
/// </summary>
public static class QualityChecker
{
    public static ImmutableArray<QualityIssue> Check(IReadOnlyList<Cue> cues, QualityThresholds t)
    {
        var issues = ImmutableArray.CreateBuilder<QualityIssue>();
        for (var i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            var seq = i + 1;
            void Add(IssueType type, string message) => issues.Add(new QualityIssue(type, cue.Id, seq, message));

            if (i > 0)
            {
                var gap = cue.StartMs - cues[i - 1].EndMs;
                if (gap < 0)
                {
                    Add(IssueType.Overlap, $"與第 {seq - 1} 筆重疊 {-gap} ms");
                }
                else if (gap < t.MinGapMs)
                {
                    Add(IssueType.GapTooSmall, $"與第 {seq - 1} 筆間隔 {gap} ms（最小 {t.MinGapMs} ms）");
                }
            }

            if (cue.DurationMs < t.MinDurationMs)
            {
                Add(IssueType.TooShort, $"時長 {cue.DurationMs} ms（最短 {t.MinDurationMs} ms）");
            }
            else if (cue.DurationMs > t.MaxDurationMs)
            {
                Add(IssueType.TooLong, $"時長 {cue.DurationMs} ms（最長 {t.MaxDurationMs} ms）");
            }

            var cps = CharsPerSecond(cue);
            if (cps > t.MaxCharsPerSecond)
            {
                Add(IssueType.ReadingSpeed, string.Create(CultureInfo.InvariantCulture,
                    $"閱讀速度 {cps:0.0} 字元/秒（上限 {t.MaxCharsPerSecond:0.#}）"));
            }

            var lines = cue.Lines;
            for (var l = 0; l < lines.Length; l++)
            {
                var width = Markup.DisplayWidth(Markup.Strip(lines[l]));
                if (width > t.MaxLineLength)
                {
                    Add(IssueType.LineTooLong, $"第 {l + 1} 行長度 {width}（上限 {t.MaxLineLength}）");
                }
            }

            if (lines.Length > t.MaxLines)
            {
                Add(IssueType.TooManyLines, $"共 {lines.Length} 行（上限 {t.MaxLines} 行）");
            }
        }
        return issues.ToImmutable();
    }

    /// <summary>Weighted visible characters (line breaks excluded) divided by duration in seconds.</summary>
    public static double CharsPerSecond(Cue cue)
    {
        if (cue.DurationMs <= 0)
        {
            return 0;
        }
        var chars = cue.Lines.Sum(l => Markup.DisplayWidth(Markup.Strip(l)));
        return chars * 1000.0 / cue.DurationMs;
    }
}
