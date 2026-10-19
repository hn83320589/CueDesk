using System.Collections.Immutable;
using System.Text.RegularExpressions;
using CueDesk.Core.Model;
using CueDesk.Core.Timing;

namespace CueDesk.Core.Parsing;

/// <summary>A block that could not be loaded. <see cref="LineNumber"/> is 1-based in the source file.</summary>
public sealed record ParseError(int LineNumber, string Reason)
{
    public override string ToString() => $"第 {LineNumber} 行：{Reason}";
}

public sealed record ParseResult(ImmutableArray<Cue> Cues, ImmutableArray<ParseError> Errors);

/// <summary>
/// Tolerant SRT parser. Accepts ',' or '.' millisecond separators, missing sequence numbers,
/// extra blank lines, mixed CRLF/LF/CR, and blocks not separated by a blank line
/// (a digits-only line followed by a timing line starts a new block).
/// A malformed block is skipped and reported; parsing always continues.
/// </summary>
public static partial class SrtParser
{
    public static ParseResult Parse(string content)
    {
        var lines = LineBreakRegex().Split(content.TrimStart('﻿'));
        var cues = new List<Cue>();
        var errors = new List<ParseError>();
        var i = 0;

        while (i < lines.Length)
        {
            if (IsBlank(lines[i]))
            {
                i++;
                continue;
            }

            var blockStart = i;
            if (IsIndexLine(lines[i]) && i + 1 < lines.Length && IsTimingLike(lines[i + 1]))
            {
                i++;
            }

            if (!IsTimingLike(lines[i]))
            {
                errors.Add(new ParseError(blockStart + 1, "找不到時間軸行（應為 00:00:00,000 --> 00:00:00,000）"));
                i = SkipToNextBlock(lines, i + 1);
                continue;
            }

            var timingLine = i;
            i++;
            var textStart = i;
            while (i < lines.Length && !IsBlank(lines[i]) && !StartsNewBlock(lines, i))
            {
                i++;
            }

            if (!TryParseTiming(lines[timingLine], out var start, out var end, out var reason))
            {
                errors.Add(new ParseError(timingLine + 1, reason));
                continue;
            }
            if (end < start)
            {
                errors.Add(new ParseError(timingLine + 1, "結束時間早於起始時間"));
                continue;
            }

            var text = string.Join("\n", lines[textStart..i]);
            cues.Add(new Cue(cues.Count + 1, start, end, text));
        }

        return new ParseResult(CueList.Sort(cues), [.. errors]);
    }

    private static int SkipToNextBlock(string[] lines, int i)
    {
        while (i < lines.Length && !IsBlank(lines[i]) && !StartsNewBlock(lines, i))
        {
            i++;
        }
        return i;
    }

    /// <summary>A timing line, or an index line directly followed by a timing line.</summary>
    private static bool StartsNewBlock(string[] lines, int i) =>
        IsTimingLike(lines[i]) || (IsIndexLine(lines[i]) && i + 1 < lines.Length && IsTimingLike(lines[i + 1]));

    private static bool TryParseTiming(string line, out long start, out long end, out string reason)
    {
        start = end = 0;
        var match = TimingRegex().Match(line);
        if (!match.Success)
        {
            reason = $"時間軸格式錯誤：「{line.Trim()}」";
            return false;
        }
        if (!TimeFormat.TryFromParts(match.Groups["h1"].Value, match.Groups["m1"].Value, match.Groups["s1"].Value,
                match.Groups["f1"].Value, out start, out var error) ||
            !TimeFormat.TryFromParts(match.Groups["h2"].Value, match.Groups["m2"].Value, match.Groups["s2"].Value,
                match.Groups["f2"].Value, out end, out error))
        {
            reason = $"時間軸格式錯誤：{error}";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

    private static bool IsIndexLine(string line) => IndexRegex().IsMatch(line);

    private static bool IsTimingLike(string line) => line.Contains("-->", StringComparison.Ordinal);

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"^\s*\d+\s*$")]
    private static partial Regex IndexRegex();

    // Trailing content after the end time (e.g. "X1:100 X2:200") is ignored.
    [GeneratedRegex(@"^\s*(?<h1>\d{1,2}):(?<m1>\d{1,2}):(?<s1>\d{1,2})[,.](?<f1>\d{1,3})\s*-->\s*(?<h2>\d{1,2}):(?<m2>\d{1,2}):(?<s2>\d{1,2})[,.](?<f2>\d{1,3})(\s.*)?$")]
    private static partial Regex TimingRegex();
}
