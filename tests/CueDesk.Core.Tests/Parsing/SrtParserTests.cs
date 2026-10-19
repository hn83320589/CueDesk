using CueDesk.Core.Parsing;

namespace CueDesk.Core.Tests.Parsing;

public class SrtParserTests
{
    [Fact]
    public void Parses_standard_blocks()
    {
        var result = SrtParser.Parse("1\r\n00:00:01,000 --> 00:00:02,500\r\nHello\r\nWorld\r\n\r\n2\r\n00:00:03,000 --> 00:00:04,000\r\n<i>Bye</i>\r\n");
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Cues.Length);
        Assert.Equal((1000L, 2500L, "Hello\nWorld"), (result.Cues[0].StartMs, result.Cues[0].EndMs, result.Cues[0].Text));
        Assert.Equal("<i>Bye</i>", result.Cues[1].Text);
    }

    [Fact]
    public void Accepts_period_as_millisecond_separator()
    {
        var result = SrtParser.Parse("1\n00:00:01.250 --> 00:00:02.750\nA\n");
        Assert.Equal((1250L, 2750L), (result.Cues[0].StartMs, result.Cues[0].EndMs));
    }

    [Fact]
    public void Accepts_missing_sequence_numbers()
    {
        var result = SrtParser.Parse("00:00:01,000 --> 00:00:02,000\nA\n\n00:00:03,000 --> 00:00:04,000\nB\n");
        Assert.Empty(result.Errors);
        Assert.Equal(["A", "B"], result.Cues.Select(c => c.Text));
    }

    [Fact]
    public void Ignores_extra_blank_lines()
    {
        var result = SrtParser.Parse("\n\n1\n00:00:01,000 --> 00:00:02,000\nA\n\n\n\n2\n00:00:03,000 --> 00:00:04,000\nB\n\n\n");
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Cues.Length);
    }

    [Fact]
    public void Handles_mixed_crlf_lf_and_cr()
    {
        var result = SrtParser.Parse("1\r\n00:00:01,000 --> 00:00:02,000\nLine1\r\nLine2\n\r\n2\r00:00:03,000 --> 00:00:04,000\rB\r");
        Assert.Empty(result.Errors);
        Assert.Equal("Line1\nLine2", result.Cues[0].Text);
        Assert.Equal("B", result.Cues[1].Text);
    }

    [Fact]
    public void Splits_blocks_without_blank_separator_when_index_precedes_timing()
    {
        var result = SrtParser.Parse("1\n00:00:01,000 --> 00:00:02,000\nA\n2\n00:00:03,000 --> 00:00:04,000\nB\n");
        Assert.Equal(["A", "B"], result.Cues.Select(c => c.Text));
    }

    [Fact]
    public void Malformed_block_is_reported_with_line_number_and_others_still_load()
    {
        const string content =
            "1\n00:00:01,000 --> 00:00:02,000\nGood one\n\n" +   // lines 1-3
            "2\n00:00:0X,000 --> 00:00:04,000\nBad time\n\n" +   // lines 5-7
            "3\n00:00:05,000 --> 00:00:06,000\nGood two\n";      // lines 9-11
        var result = SrtParser.Parse(content);
        Assert.Equal(["Good one", "Good two"], result.Cues.Select(c => c.Text));
        var error = Assert.Single(result.Errors);
        Assert.Equal(6, error.LineNumber);
        Assert.Contains("時間軸格式錯誤", error.Reason);
    }

    [Fact]
    public void Block_without_timing_line_is_reported_at_its_first_line()
    {
        var result = SrtParser.Parse("1\n00:00:01,000 --> 00:00:02,000\nA\n\nthis is junk\nmore junk\n\n3\n00:00:05,000 --> 00:00:06,000\nC\n");
        Assert.Equal(2, result.Cues.Length);
        var error = Assert.Single(result.Errors);
        Assert.Equal(5, error.LineNumber);
        Assert.Contains("找不到時間軸行", error.Reason);
    }

    [Fact]
    public void Out_of_range_minutes_are_reported()
    {
        var result = SrtParser.Parse("1\n00:61:00,000 --> 00:62:00,000\nA\n");
        Assert.Empty(result.Cues);
        Assert.Contains("超出範圍", Assert.Single(result.Errors).Reason);
    }

    [Fact]
    public void End_before_start_is_reported()
    {
        var result = SrtParser.Parse("1\n00:00:05,000 --> 00:00:04,000\nA\n");
        Assert.Empty(result.Cues);
        Assert.Equal("結束時間早於起始時間", Assert.Single(result.Errors).Reason);
    }

    [Fact]
    public void Index_only_block_is_reported()
    {
        var result = SrtParser.Parse("7\n\n1\n00:00:01,000 --> 00:00:02,000\nA\n");
        Assert.Single(result.Cues);
        Assert.Equal(1, Assert.Single(result.Errors).LineNumber);
    }

    [Fact]
    public void Timing_without_text_loads_empty_cue()
    {
        var result = SrtParser.Parse("1\n00:00:01,000 --> 00:00:02,000\n\n2\n00:00:03,000 --> 00:00:04,000\nB\n");
        Assert.Empty(result.Errors);
        Assert.Equal("", result.Cues[0].Text);
    }

    [Fact]
    public void Preserves_inline_tags_and_style_codes()
    {
        var result = SrtParser.Parse("1\n00:00:01,000 --> 00:00:02,000\n{\\an8}<font color=\"#ff0000\">紅色</font>\n<b>粗</b>\n");
        Assert.Equal("{\\an8}<font color=\"#ff0000\">紅色</font>\n<b>粗</b>", result.Cues[0].Text);
    }

    [Fact]
    public void Ignores_position_coordinates_after_timing()
    {
        var result = SrtParser.Parse("1\n00:00:01,000 --> 00:00:02,000 X1:100 X2:200 Y1:10 Y2:20\nA\n");
        Assert.Empty(result.Errors);
        Assert.Equal(2000, result.Cues[0].EndMs);
    }

    [Fact]
    public void Sorts_cues_by_start_time()
    {
        var result = SrtParser.Parse("1\n00:00:05,000 --> 00:00:06,000\nLate\n\n2\n00:00:01,000 --> 00:00:02,000\nEarly\n");
        Assert.Equal(["Early", "Late"], result.Cues.Select(c => c.Text));
    }

    [Fact]
    public void Strips_leading_bom_character()
    {
        var result = SrtParser.Parse("﻿1\n00:00:01,000 --> 00:00:02,000\nA\n");
        Assert.Empty(result.Errors);
        Assert.Single(result.Cues);
    }

    [Fact]
    public void Empty_content_yields_nothing()
    {
        var result = SrtParser.Parse("");
        Assert.Empty(result.Cues);
        Assert.Empty(result.Errors);
    }
}
