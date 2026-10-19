using CueDesk.Core.Encoding;
using CueDesk.Core.Export;
using CueDesk.Core.Model;
using CueDesk.Core.Parsing;

namespace CueDesk.Core.Tests.Export;

public class SubtitleWritersTests
{
    private static readonly IReadOnlyList<Cue> Cues = CueList.From(
        (1_000, 2_500, "<i>Hello</i> & welcome\n{\\an8}<font color=\"red\">第二行</font>"),
        (3_000, 4_000, "Single <B>line</B>"));

    [Fact]
    public void Srt_uses_crlf_comma_and_sequential_numbers()
    {
        var srt = SubtitleWriters.WriteSrt(Cues);
        Assert.Equal(
            "1\r\n00:00:01,000 --> 00:00:02,500\r\n<i>Hello</i> & welcome\r\n{\\an8}<font color=\"red\">第二行</font>\r\n\r\n" +
            "2\r\n00:00:03,000 --> 00:00:04,000\r\nSingle <B>line</B>\r\n\r\n", srt);
        Assert.DoesNotContain("\n", srt.Replace("\r\n", ""));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Utf8_bytes_have_bom_only_when_requested(bool withBom)
    {
        var bytes = SubtitleWriters.ToUtf8Bytes("字", withBom);
        Assert.Equal(withBom, bytes.AsSpan().StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]));
        Assert.Equal(withBom ? 6 : 3, bytes.Length);
    }

    [Fact]
    public void Srt_round_trip_preserves_times_text_tags_and_line_breaks()
    {
        const string original =
            "1\n00:00:01,000 --> 00:00:02,500\n<i>Hello</i>\n{\\an8}第二行\n\n" +
            "5\r\n00:01:02,345 --> 00:01:03,000\r\n<font color=\"#00ff00\">綠</font> <b>bold</b>\r\n";
        var first = SrtParser.Parse(original).Cues;
        var exported = SubtitleWriters.WriteSrt(first);
        var second = SrtParser.Parse(exported).Cues;

        Assert.Equal(first.Select(c => (c.StartMs, c.EndMs, c.Text)), second.Select(c => (c.StartMs, c.EndMs, c.Text)));
        Assert.Equal("<i>Hello</i>\n{\\an8}第二行", second[0].Text);
    }

    [Fact]
    public void Srt_round_trip_through_bytes_for_big5_source()
    {
        var source = EncodingDetector.Big5.GetBytes("1\r\n00:00:01,000 --> 00:00:02,000\r\n繁體中文字幕\r\n");
        var cues = SrtParser.Parse(EncodingDetector.Decode(source).Text).Cues;
        var bytes = SubtitleWriters.ToUtf8Bytes(SubtitleWriters.WriteSrt(cues), withBom: true);
        var reread = EncodingDetector.Decode(bytes);
        Assert.Equal(SubtitleEncoding.Utf8Bom, reread.Encoding);
        Assert.Equal("繁體中文字幕", SrtParser.Parse(reread.Text).Cues[0].Text);
    }

    [Fact]
    public void Vtt_has_header_period_separator_and_handles_tags()
    {
        var vtt = SubtitleWriters.WriteVtt(Cues);
        Assert.Equal(
            "WEBVTT\r\n\r\n" +
            "1\r\n00:00:01.000 --> 00:00:02.500\r\n<i>Hello</i> &amp; welcome\r\n第二行\r\n\r\n" +
            "2\r\n00:00:03.000 --> 00:00:04.000\r\nSingle <b>line</b>\r\n\r\n", vtt);
    }

    [Fact]
    public void Vtt_escapes_stray_angle_brackets_and_arrow()
    {
        var vtt = SubtitleWriters.WriteVtt(CueList.From((0, 1000, "a --> b")));
        Assert.Contains("a --&gt; b\r\n", vtt);
    }

    [Fact]
    public void Vtt_drops_lines_that_become_empty()
    {
        var vtt = SubtitleWriters.WriteVtt(CueList.From((0, 1000, "{\\an8}\nText")));
        Assert.EndsWith("00:00:00.000 --> 00:00:01.000\r\nText\r\n\r\n", vtt);
    }

    [Fact]
    public void Transcript_has_text_only_without_times_or_tags()
    {
        var txt = SubtitleWriters.WriteTranscript(Cues);
        Assert.Equal("Hello & welcome\r\n第二行\r\nSingle line\r\n", txt);
    }

    [Fact]
    public void Write_dispatches_by_format()
    {
        Assert.StartsWith("WEBVTT", SubtitleWriters.Write(ExportFormat.WebVtt, Cues));
        Assert.StartsWith("1\r\n", SubtitleWriters.Write(ExportFormat.Srt, Cues));
        Assert.StartsWith("Hello", SubtitleWriters.Write(ExportFormat.Transcript, Cues));
    }

    [Fact]
    public void Srt_empty_cue_has_no_extra_blank_line_and_round_trips()
    {
        var cues = CueList.From((0, 1_000, ""), (2_000, 3_000, "B"));
        var srt = SubtitleWriters.WriteSrt(cues);
        Assert.Equal("1\r\n00:00:00,000 --> 00:00:01,000\r\n\r\n2\r\n00:00:02,000 --> 00:00:03,000\r\nB\r\n\r\n", srt);
        Assert.Equal(["", "B"], SrtParser.Parse(srt).Cues.Select(c => c.Text));
    }
}