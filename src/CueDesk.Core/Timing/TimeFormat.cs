using System.Globalization;
using System.Text.RegularExpressions;

namespace CueDesk.Core.Timing;

/// <summary>Formats and parses "HH:MM:SS,mmm" timestamps (hours at most two digits).</summary>
public static partial class TimeFormat
{
    /// <summary>99:59:59,999 — the largest time representable with two hour digits.</summary>
    public const long MaxMs = (((99L * 60) + 59) * 60 + 59) * 1000 + 999;

    public static string Format(long ms, char separator = ',')
    {
        if (ms < 0 || ms > MaxMs)
        {
            throw new ArgumentOutOfRangeException(nameof(ms), ms, $"時間必須介於 0 與 {MaxMs} ms 之間");
        }
        var h = ms / 3_600_000;
        var m = ms / 60_000 % 60;
        var s = ms / 1000 % 60;
        var f = ms % 1000;
        return string.Create(CultureInfo.InvariantCulture, $"{h:00}:{m:00}:{s:00}{separator}{f:000}");
    }

    /// <summary>
    /// Parses H:MM:SS,mmm. Accepts ',' or '.' before the fraction, 1–2 digit fields and a 1–3 digit
    /// fraction which is read as a decimal fraction of a second (",5" = 500 ms).
    /// </summary>
    public static bool TryParse(string text, out long ms, out string? error)
    {
        ms = 0;
        var match = TimestampRegex().Match(text.Trim());
        if (!match.Success)
        {
            error = $"無法辨識的時間「{text.Trim()}」";
            return false;
        }
        return TryFromParts(match.Groups["h"].Value, match.Groups["m"].Value, match.Groups["s"].Value,
            match.Groups["f"].Value, out ms, out error);
    }

    public static bool TryParse(string text, out long ms) => TryParse(text, out ms, out _);

    internal static bool TryFromParts(string h, string m, string s, string f, out long ms, out string? error)
    {
        ms = 0;
        var hours = int.Parse(h, CultureInfo.InvariantCulture);
        var minutes = int.Parse(m, CultureInfo.InvariantCulture);
        var seconds = int.Parse(s, CultureInfo.InvariantCulture);
        if (minutes >= 60 || seconds >= 60)
        {
            error = $"分或秒超出範圍（{h}:{m}:{s}）";
            return false;
        }
        var fraction = int.Parse(f.PadRight(3, '0'), CultureInfo.InvariantCulture);
        ms = (((hours * 60L) + minutes) * 60 + seconds) * 1000 + fraction;
        error = null;
        return true;
    }

    internal const string TimestampPattern = @"(?<h>\d{1,2}):(?<m>\d{1,2}):(?<s>\d{1,2})[,.](?<f>\d{1,3})";

    [GeneratedRegex("^" + TimestampPattern + "$")]
    private static partial Regex TimestampRegex();
}
