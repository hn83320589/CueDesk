using CueDesk.Core.Encoding;
using CueDesk.Core.Export;
using CueDesk.Core.Parsing;
using CueDesk.Core.Quality;

namespace CueDesk.Core.Tests;

/// <summary>Asserts the results documented in samples/EXPECTED.md.</summary>
public class SampleFilesTests
{
    private static (DecodedText Decoded, ParseResult Parsed, IssueType[] IssueTypes, int[] IssueSequences) Load(string name)
    {
        var decoded = EncodingDetector.Decode(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", name)));
        var parsed = SrtParser.Parse(decoded.Text);
        var issues = QualityChecker.Check(parsed.Cues, QualityThresholds.Default);
        return (decoded, parsed, [.. issues.Select(i => i.Type)], [.. issues.Select(i => i.Sequence)]);
    }

    /// <summary>The seven checks, in the order the two "one of each" samples trigger them.</summary>
    private static readonly IssueType[] EachTypeOnceChinese =
    [
        IssueType.ReadingSpeed, IssueType.GapTooSmall, IssueType.Overlap, IssueType.TooShort,
        IssueType.TooLong, IssueType.LineTooLong, IssueType.TooManyLines,
    ];

    [Fact]
    public void Sample1_utf8_bom_chinese_triggers_each_check_once()
    {
        var (decoded, parsed, types, seqs) = Load("01-utf8-bom-zh-tw.srt");
        Assert.Equal(SubtitleEncoding.Utf8Bom, decoded.Encoding);
        Assert.Equal(10, parsed.Cues.Length);
        Assert.Empty(parsed.Errors);
        Assert.Equal("大家好，歡迎來到今天的教學影片。", parsed.Cues[0].Text);
        Assert.Equal(EachTypeOnceChinese, types);
        Assert.Equal([2, 3, 4, 5, 6, 8, 9], seqs);
    }

    [Fact]
    public void Sample2_big5_chinese_is_clean()
    {
        var (decoded, parsed, types, _) = Load("02-big5-zh-tw.srt");
        Assert.Equal(SubtitleEncoding.Big5, decoded.Encoding);
        Assert.False(decoded.HasReplacementCharacters);
        Assert.Equal(5, parsed.Cues.Length);
        Assert.Equal("CueDesk 會自動偵測，\n並正確顯示繁體中文。", parsed.Cues[3].Text);
        Assert.Empty(types);
    }

    [Fact]
    public void Sample3_malformed_blocks_are_listed_and_the_rest_loads()
    {
        var (decoded, parsed, types, _) = Load("03-malformed-blocks.srt");
        Assert.Equal(SubtitleEncoding.Utf8, decoded.Encoding);
        Assert.Equal(
            ["Comma separator, CRLF.", "Period separator, LF.", "No sequence number.",
             "Mixed line endings\ninside one block.", "No blank line before this block."],
            parsed.Cues.Select(c => c.Text));
        Assert.Equal([14, 19, 23], parsed.Errors.Select(e => e.LineNumber));
        Assert.Contains("找不到時間軸行", parsed.Errors[0].Reason);
        Assert.Contains("分或秒超出範圍", parsed.Errors[1].Reason);
        Assert.Equal("結束時間早於起始時間", parsed.Errors[2].Reason);
        Assert.Empty(types);
    }

    [Fact]
    public void Sample4_english_triggers_each_check_once()
    {
        var (decoded, parsed, types, seqs) = Load("04-english.srt");
        Assert.Equal(SubtitleEncoding.Utf8, decoded.Encoding);
        Assert.Equal(10, parsed.Cues.Length);
        Assert.Equal(
            [IssueType.GapTooSmall, IssueType.Overlap, IssueType.TooShort, IssueType.TooLong,
             IssueType.ReadingSpeed, IssueType.LineTooLong, IssueType.TooManyLines],
            types);
        Assert.Equal([3, 4, 5, 6, 7, 8, 9], seqs);
    }

    [Fact]
    public void Sample5_utf16_bilingual_is_clean()
    {
        var (decoded, parsed, types, _) = Load("05-utf16le-bilingual.srt");
        Assert.Equal(SubtitleEncoding.Utf16LittleEndian, decoded.Encoding);
        Assert.Equal(3, parsed.Cues.Length);
        Assert.Equal("<i>中英對照</i>\n<i>Bilingual captions</i>", parsed.Cues[2].Text);
        Assert.Empty(types);
    }

    [Fact]
    public void Sample6_utf8_without_bom_chinese_is_clean_and_not_garbled()
    {
        var (decoded, parsed, types, _) = Load("06-utf8-nobom-zh-tw.srt");
        Assert.Equal(SubtitleEncoding.Utf8, decoded.Encoding);
        Assert.False(decoded.HasReplacementCharacters);
        Assert.Equal(4, parsed.Cues.Length);
        Assert.Equal("中文不會亂碼，\n標點符號也正常顯示。", parsed.Cues[2].Text);
        Assert.Equal("<i>行內標籤</i>同樣保留。", parsed.Cues[3].Text);
        Assert.Empty(types);
    }

    [Theory]
    [InlineData("01-utf8-bom-zh-tw.srt")]
    [InlineData("02-big5-zh-tw.srt")]
    [InlineData("03-malformed-blocks.srt")]
    [InlineData("04-english.srt")]
    [InlineData("05-utf16le-bilingual.srt")]
    [InlineData("06-utf8-nobom-zh-tw.srt")]
    public void Every_sample_round_trips_through_srt_export(string name)
    {
        var cues = Load(name).Parsed.Cues;
        var reparsed = SrtParser.Parse(SubtitleWriters.WriteSrt(cues)).Cues;
        Assert.Equal(cues.Select(c => (c.StartMs, c.EndMs, c.Text)), reparsed.Select(c => (c.StartMs, c.EndMs, c.Text)));
    }

    [Fact]
    public void Sample1_one_click_fix_resolves_both_fixable_issues()
    {
        var cues = Load("01-utf8-bom-zh-tw.srt").Parsed.Cues;
        var fixedResult = GapFixer.Fix(cues, QualityThresholds.Default.MinGapMs);
        Assert.Equal(2, fixedResult.FixedCount);
        Assert.Empty(fixedResult.Unfixed);
        Assert.DoesNotContain(QualityChecker.Check(fixedResult.Cues, QualityThresholds.Default), i => i.IsFixable);
    }
}
