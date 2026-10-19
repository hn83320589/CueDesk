using System.Text;
using CueDesk.Core.Encoding;

namespace CueDesk.Core.Tests.Encoding;

public class EncodingDetectorTests
{
    private const string Sample = "1\r\n00:00:01,000 --> 00:00:02,000\r\n歡迎來到教學頻道\r\n";

    [Fact]
    public void Detects_utf8_with_bom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(new UTF8Encoding(false).GetBytes(Sample)).ToArray();
        var result = EncodingDetector.Decode(bytes);
        Assert.Equal(SubtitleEncoding.Utf8Bom, result.Encoding);
        Assert.Equal(Sample, result.Text);
        Assert.Equal("UTF-8 (BOM)", result.EncodingName);
    }

    [Fact]
    public void Detects_utf8_without_bom()
    {
        var result = EncodingDetector.Decode(new UTF8Encoding(false).GetBytes(Sample));
        Assert.Equal(SubtitleEncoding.Utf8, result.Encoding);
        Assert.Equal(Sample, result.Text);
    }

    [Fact]
    public void Detects_utf16_le_with_bom()
    {
        var result = EncodingDetector.Decode(new UnicodeEncoding(false, true).GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes(Sample)).ToArray());
        Assert.Equal(SubtitleEncoding.Utf16LittleEndian, result.Encoding);
        Assert.Equal(Sample, result.Text);
    }

    [Fact]
    public void Detects_utf16_be_with_bom()
    {
        var result = EncodingDetector.Decode(new byte[] { 0xFE, 0xFF }.Concat(System.Text.Encoding.BigEndianUnicode.GetBytes(Sample)).ToArray());
        Assert.Equal(SubtitleEncoding.Utf16BigEndian, result.Encoding);
        Assert.Equal(Sample, result.Text);
    }

    [Fact]
    public void Detects_unmarked_big5_and_decodes_chinese()
    {
        var result = EncodingDetector.Decode(EncodingDetector.Big5.GetBytes(Sample));
        Assert.Equal(SubtitleEncoding.Big5, result.Encoding);
        Assert.Equal(Sample, result.Text);
        Assert.False(result.HasReplacementCharacters);
    }

    [Fact]
    public void Pure_ascii_is_reported_as_utf8()
    {
        var result = EncodingDetector.Decode(System.Text.Encoding.ASCII.GetBytes("1\n00:00:01,000 --> 00:00:02,000\nHello\n"));
        Assert.Equal(SubtitleEncoding.Utf8, result.Encoding);
    }

    [Fact]
    public void Bytes_invalid_in_both_utf8_and_big5_fall_back_to_big5_with_replacement()
    {
        // 0xA1 0x30: invalid UTF-8, and 0x30 is not a valid Big5 trail byte.
        var result = EncodingDetector.Decode([0x41, 0xA1, 0x30, 0x42]);
        Assert.Equal(SubtitleEncoding.Big5, result.Encoding);
        Assert.True(result.HasReplacementCharacters);
    }
}
