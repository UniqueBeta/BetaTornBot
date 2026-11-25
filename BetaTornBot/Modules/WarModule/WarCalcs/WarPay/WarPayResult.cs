using System.Collections.Generic;

namespace WarPay;

/// <summary>Result of a war pay calculation.</summary>
/// <param name="Players">Every contributing player, highest score first.</param>
/// <param name="TotalScore">Sum of all players' adjusted scores, rounded down to a whole number.</param>
public sealed record WarPayResult(IReadOnlyList<PlayerWarPay> Players, long TotalScore)
{
    public long FactionId { get; init; }

    /// <summary>The ranked war id.</summary>
    public long? WarId { get; init; }

    /// <summary>Start of the window that was analysed (unix seconds).</summary>
    public long From { get; init; }

    /// <summary>End of the window that was analysed (unix seconds).</summary>
    public long To { get; init; }

    /// <summary>How many attacks the API returned for the window (all members, all types).</summary>
    public int AttackCount { get; init; }
}

/// <summary>One player's contribution to the war.</summary>
public sealed class PlayerWarPay
{
    public long Id { get; init; }
    public string Name { get; init; } = "";

    public int WarHits { get; init; }
    public int BonusHits { get; init; }
    public int OutsideHits { get; init; }

    /// <summary>Own hits that were split with one or more assisters.</summary>
    public int SharedHits { get; init; }

    /// <summary>Hits by other members that this player assisted and got a share of.</summary>
    public int AssistHits { get; init; }

    /// <summary>
    /// This player's cut of the payout pool: pool * share (33% = 0.33), rounded down to a whole number.
    /// </summary>
    public long Payout { get; init; }

    /// <summary>Score of own hits before any splitting, rounded down to a whole number.</summary>
    public long UnsplitScore { get; init; }

    /// <summary>Score after splitting with assisters, rounded down to a whole number.</summary>
    public long Score { get; init; }

    /// <summary>
    /// Percentage (0-100) of the faction's total adjusted score. Calculated from the unrounded
    /// scores, so it is not affected by the rounding above.
    /// </summary>
    public double SharePercent { get; init; }


    /// <summary>Fellow faction members this player hospitalised.</summary>
    public int HospitalHits { get; init; }
}
