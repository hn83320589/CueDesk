using System.Text;
using System.Text.RegularExpressions;
using CueDesk.Core.Model;
using CueDesk.Core.Text;
using CueDesk.Core.Timing;

namespace CueDesk.Core.Export;

public enum ExportFormat
{
    Srt,
    WebVtt,
    Transcript,
}

/// <summary>All exports use CRLF line endings and UTF-8 (BOM optional).</summary>
public static partial class SubtitleWriters
{
    private const string NewLine = "\r\n";

    public static string Write(ExportFormat format, IReadOnlyList<Cue> cues) => format switch
    {
        ExportFormat.Srt => WriteSrt(cues),
        ExportFormat.WebVtt => WriteVtt(cues),
        ExportFormat.Transcript => WriteTranscript(cues),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    public static byte[] ToUtf8Bytes(string content, bool withBom)
    {
        var body = new UTF8Encoding(false).GetBytes(content);
        return withBom ? [0xEF, 0xBB, 0xBF, .. body] : body;
    }

    /// <summary>Text and inline tags are written verbatim; sequence numbers restart at 1.</summary>
    public static string WriteSrt(IReadOnlyList<Cue> cues)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            sb.Append(i + 1).Append(NewLine);
            sb.Append(TimeFormat.Format(cue.StartMs)).Append(" --> ").Append(TimeFormat.Format(cue.EndMs)).Append(NewLine);
            // An empty cue writes no text line; otherwise it would add a second blank line.
            foreach (var line in cue.Text.Length == 0 ? [] : cue.Lines)
            {
                sb.Append(line).Append(NewLine);
            }
            sb.Append(NewLine);
        }
        return sb.ToString();
    }

    /// <summary>
    /// WEBVTT header, '.' separator. &lt;i&gt;/&lt;b&gt;/&lt;u&gt; are kept; other tags and {\…} codes are
    /// removed; '&amp;', '&lt;' and '&gt;' in text are escaped; blank lines inside a cue are dropped.
    /// </summary>
    public static string WriteVtt(IReadOnlyList<Cue> cues)
    {
        var sb = new StringBuilder("WEBVTT").Append(NewLine).Append(NewLine);
        for (var i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];
            sb.Append(i + 1).Append(NewLine);
            sb.Append(TimeFormat.Format(cue.StartMs, '.')).Append(" --> ").Append(TimeFormat.Format(cue.EndMs, '.')).Append(NewLine);
            foreach (var line in cue.Lines.Select(ToVttLine).Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                sb.Append(line).Append(NewLine);
            }
            sb.Append(NewLine);
        }
        return sb.ToString();
    }

    /// <summary>Text only: markup removed, one subtitle line per output line, empty lines dropped.</summary>
    public static string WriteTranscript(IReadOnlyList<Cue> cues)
    {
        var sb = new StringBuilder();
        foreach (var line in cues.SelectMany(c => c.Lines).Select(l => Markup.Strip(l).Trim()))
        {
            if (line.Length > 0)
            {
                sb.Append(line).Append(NewLine);
            }
        }
        return sb.ToString();
    }

    private static string ToVttLine(string line)
    {
        var sb = new StringBuilder();
        foreach (var (value, isMarkup) in Markup.Tokenize(line))
        {
            if (!isMarkup)
            {
                sb.Append(value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"));
                continue;
            }
            var basic = BasicTagRegex().Match(value);
            if (basic.Success)
            {
                sb.Append('<').Append(basic.Groups["close"].Value).Append(basic.Groups["name"].Value.ToLowerInvariant()).Append('>');
            }
        }
        return sb.ToString();
    }

    [GeneratedRegex(@"^<(?<close>/?)\s*(?<name>[ibu])\s*>$", RegexOptions.IgnoreCase)]
    private static partial Regex BasicTagRegex();
}
