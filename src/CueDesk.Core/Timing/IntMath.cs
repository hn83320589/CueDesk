namespace CueDesk.Core.Timing;

/// <summary>
/// The single rounding rule used by every time calculation:
/// exact integer division, then round half away from zero to the nearest millisecond.
/// </summary>
public static class IntMath
{
    public static long DivRound(Int128 numerator, Int128 denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }
        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }
        var quotient = Int128.DivRem(Int128.Abs(numerator), denominator);
        var magnitude = quotient.Quotient + (quotient.Remainder * 2 >= denominator ? 1 : 0);
        return (long)(numerator < 0 ? -magnitude : magnitude);
    }
}
