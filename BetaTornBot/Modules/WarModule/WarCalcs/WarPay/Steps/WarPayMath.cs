using System;

namespace WarPay;

/// <summary>The small bits of arithmetic behind scores, shares and payouts, in one place.</summary>
internal static class WarPayMath
{
    /// <summary>
    /// Rounds down to a whole number. The tiny epsilon stops floating-point noise (9.9999999999 from
    /// adding thirds) from rounding a true 10 down to 9.
    /// </summary>
    public static long RoundDown(double value) => (long)Math.Floor(value + 1e-9);

    /// <summary>A member's share of the faction's total score, as a percentage (0-100).</summary>
    public static double SharePercent(double score, double totalScore) =>
        totalScore > 0 ? score / totalScore * 100.0 : 0;

    /// <summary>
    /// A member's cut of the payout pool: pool * share, with the share as a fraction (33% = 0.33),
    /// rounded down. Decimal keeps money maths exact, so 33% of 1,000,000,000 is 330,000,000 and
    /// not 329,999,999.
    /// </summary>
    public static long Payout(long payoutPool, double score, double totalScore)
    {
        decimal fraction = totalScore > 0 ? (decimal)score / (decimal)totalScore : 0m;
        return (long)Math.Floor(payoutPool * fraction);
    }
}
