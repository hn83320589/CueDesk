using System.Diagnostics;
using System.Text;
using CueDesk.Core.Editing;
using CueDesk.Core.Encoding;
using CueDesk.Core.Export;
using CueDesk.Core.Parsing;
using CueDesk.Core.Quality;
using CueDesk.Core.Timing;

namespace CueDesk.Core.Tests;

/// <summary>The app targets files up to 20,000 cues / 5 MB; core operations must stay fast at that size.</summary>
public class LargeFileTests
{
    public const int CueCount = 20_000;

    /// <summary>20,000 bilingual cues, 3 s each with a 500 ms gap (≈ 12 cps, so no quality issues).</summary>
    public static string BuildLargeSrt()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < CueCount; i++)
        {
            var start = i * 3_500L;
            sb.Append(i + 1).Append("\r\n")
              .Append(TimeFormat.Format(start)).Append(" --> ").Append(TimeFormat.Format(start + 3_000)).Append("\r\n")
              .Append("第 ").Append(i + 1).Append(" 句字幕內容\r\n")
              .Append("Subtitle line number ").Append(i + 1).Append("\r\n\r\n");
        }
        return sb.ToString();
    }

    [Fact]
    public void Twenty_thousand_cues_fit_in_five_megabytes_and_process_quickly()
    {
        var content = BuildLargeSrt();
        var bytes = SubtitleWriters.ToUtf8Bytes(content, withBom: true);
        Assert.True(bytes.Length < 5 * 1024 * 1024, $"fixture is {bytes.Length} bytes");

        var watch = Stopwatch.StartNew();
        var parsed = SrtParser.Parse(EncodingDetector.Decode(bytes).Text);
        var issues = QualityChecker.Check(parsed.Cues, QualityThresholds.Default);
        var shifted = TimeOperations.ConvertFrameRate(parsed.Cues, FrameRate.Fps23_976, FrameRate.Fps25);
        var fixedResult = GapFixer.Fix(shifted.Cues, 40);
        var edited = CueEditor.MergeWithNext(fixedResult.Cues, fixedResult.Cues[100].Id);
        var exported = SubtitleWriters.WriteSrt(edited);
        watch.Stop();

        Assert.Equal(CueCount, parsed.Cues.Length);
        Assert.Empty(parsed.Errors);
        Assert.Empty(issues);
        Assert.Equal(CueCount - 1, edited.Length);
        Assert.StartsWith("1\r\n", exported);
        // Generous bound so slow CI machines do not flake; locally this takes well under a second.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), $"took {watch.Elapsed}");
    }
}
