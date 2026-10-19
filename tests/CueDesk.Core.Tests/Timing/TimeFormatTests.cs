using CueDesk.Core.Timing;

namespace CueDesk.Core.Tests.Timing;

public class TimeFormatTests
{
    [Theory]
    [InlineData(0, "00:00:00,000")]
    [InlineData(1_234, "00:00:01,234")]
    [InlineData(3_723_004, "01:02:03,004")]
    [InlineData(TimeFormat.MaxMs, "99:59:59,999")]
    public void Format_produces_srt_timestamp(long ms, string expected) =>
        Assert.Equal(expected, TimeFormat.Format(ms));

    [Fact]
    public void Format_uses_given_separator() =>
        Assert.Equal("00:00:01.500", TimeFormat.Format(1500, '.'));

    [Theory]
    [InlineData(-1)]
    [InlineData(TimeFormat.MaxMs + 1)]
    public void Format_rejects_out_of_range(long ms) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TimeFormat.Format(ms));

    [Theory]
    [InlineData("00:00:01,234", 1_234)]
    [InlineData("00:00:01.234", 1_234)]
    [InlineData("1:02:03,004", 3_723_004)]
    [InlineData("00:00:01,5", 1_500)]
    [InlineData("00:00:01,05", 1_050)]
    [InlineData(" 00:00:02,000 ", 2_000)]
    public void TryParse_accepts_tolerant_formats(string text, long expected)
    {
        Assert.True(TimeFormat.TryParse(text, out var ms));
        Assert.Equal(expected, ms);
    }

    [Theory]
    [InlineData("00:60:00,000")]
    [InlineData("00:00:60,000")]
    [InlineData("abc")]
    [InlineData("00:00:01")]
    [InlineData("100:00:00,000")]
    public void TryParse_rejects_invalid(string text) =>
        Assert.False(TimeFormat.TryParse(text, out _));
}
