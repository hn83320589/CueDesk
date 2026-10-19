using CueDesk.Core.Timing;

namespace CueDesk.Core.Tests.Timing;

public class IntMathTests
{
    [Theory]
    [InlineData(10, 4, 3)]    // 2.5 → 3
    [InlineData(-10, 4, -3)]  // -2.5 → -3 (away from zero)
    [InlineData(9, 4, 2)]     // 2.25 → 2
    [InlineData(11, 4, 3)]    // 2.75 → 3
    [InlineData(10, -4, -3)]
    [InlineData(8, 4, 2)]
    public void DivRound_rounds_half_away_from_zero(long n, long d, long expected) =>
        Assert.Equal(expected, IntMath.DivRound(n, d));

    [Fact]
    public void DivRound_throws_on_zero_denominator() =>
        Assert.Throws<DivideByZeroException>(() => IntMath.DivRound(1, 0));
}
