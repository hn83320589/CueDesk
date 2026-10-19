using System.Text;
using System.Text.RegularExpressions;

namespace CueDesk.Core.Text;

/// <summary>Recognises inline tags (&lt;i&gt;, &lt;font …&gt;) and ASS style codes ({\an8}).</summary>
public static partial class Markup
{
    public static string Strip(string text) => MarkupRegex().Replace(text, string.Empty);

    /// <summary>Splits text into plain segments and markup tokens, in order.</summary>
    public static IEnumerable<(string Value, bool IsMarkup)> Tokenize(string text)
    {
        var last = 0;
        foreach (Match match in MarkupRegex().Matches(text))
        {
            if (match.Index > last)
            {
                yield return (text[last..match.Index], false);
            }
            yield return (match.Value, true);
            last = match.Index + match.Length;
        }
        if (last < text.Length)
        {
            yield return (text[last..], false);
        }
    }

    /// <summary>Visible width: East Asian Wide/Fullwidth characters count 2, everything else 1.</summary>
    public static int DisplayWidth(string plainText)
    {
        var width = 0;
        foreach (var rune in plainText.EnumerateRunes())
        {
            width += IsWide(rune) ? 2 : 1;
        }
        return width;
    }

    internal static bool IsWide(Rune rune)
    {
        var v = rune.Value;
        return v is >= 0x1100 and <= 0x115F
            or >= 0x2E80 and <= 0x303E
            or >= 0x3041 and <= 0x33FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xA000 and <= 0xA4CF
            or >= 0xAC00 and <= 0xD7A3
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFE30 and <= 0xFE4F
            or >= 0xFF00 and <= 0xFF60
            or >= 0xFFE0 and <= 0xFFE6
            or >= 0x20000 and <= 0x3FFFD;
    }

    [GeneratedRegex(@"<[^<>\n]*>|\{\\[^{}\n]*\}")]
    private static partial Regex MarkupRegex();
}
