using System.Text;

namespace CueDesk.Core.Encoding;

public enum SubtitleEncoding
{
    Utf8Bom,
    Utf8,
    Utf16LittleEndian,
    Utf16BigEndian,
    Big5,
}

/// <param name="HasReplacementCharacters">True when some bytes were not valid in the detected encoding.</param>
public sealed record DecodedText(string Text, SubtitleEncoding Encoding, bool HasReplacementCharacters)
{
    public string EncodingName => NameOf(Encoding);

    public static string NameOf(SubtitleEncoding encoding) => encoding switch
    {
        SubtitleEncoding.Utf8Bom => "UTF-8 (BOM)",
        SubtitleEncoding.Utf8 => "UTF-8",
        SubtitleEncoding.Utf16LittleEndian => "UTF-16 LE",
        SubtitleEncoding.Utf16BigEndian => "UTF-16 BE",
        SubtitleEncoding.Big5 => "Big5",
        _ => encoding.ToString(),
    };
}

/// <summary>
/// Detection order: UTF-8 BOM → UTF-16 BOM (LE/BE) → strict UTF-8 → Big5 (code page 950).
/// Pure ASCII is reported as UTF-8.
/// </summary>
public static class EncodingDetector
{
    private const int Big5CodePage = 950;

    static EncodingDetector() => System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public static DecodedText Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            return Lenient(new UTF8Encoding(false), bytes[3..], SubtitleEncoding.Utf8Bom);
        }
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE]))
        {
            return Lenient(new UnicodeEncoding(false, false), bytes[2..], SubtitleEncoding.Utf16LittleEndian);
        }
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]))
        {
            return Lenient(new UnicodeEncoding(true, false), bytes[2..], SubtitleEncoding.Utf16BigEndian);
        }
        if (TryStrict(new UTF8Encoding(false, throwOnInvalidBytes: true), bytes, out var utf8))
        {
            return new DecodedText(utf8, SubtitleEncoding.Utf8, false);
        }
        var big5Strict = System.Text.Encoding.GetEncoding(Big5CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        if (TryStrict(big5Strict, bytes, out var big5))
        {
            return new DecodedText(big5, SubtitleEncoding.Big5, false);
        }
        return Lenient(System.Text.Encoding.GetEncoding(Big5CodePage, EncoderFallback.ReplacementFallback, new DecoderReplacementFallback("\uFFFD")), bytes, SubtitleEncoding.Big5);
    }

    /// <summary>Returns the Big5 encoding (code page 950), e.g. for producing test fixtures.</summary>
    public static System.Text.Encoding Big5 => System.Text.Encoding.GetEncoding(Big5CodePage);

    private static bool TryStrict(System.Text.Encoding encoding, ReadOnlySpan<byte> bytes, out string text)
    {
        try
        {
            text = encoding.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = string.Empty;
            return false;
        }
    }

    private static DecodedText Lenient(System.Text.Encoding encoding, ReadOnlySpan<byte> bytes, SubtitleEncoding kind)
    {
        var text = encoding.GetString(bytes);
        return new DecodedText(text, kind, text.Contains('�'));
    }
}
