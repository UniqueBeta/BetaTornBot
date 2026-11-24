using System.Collections.Generic;

namespace BetaTornBot.Modules.WarModule.WarCalcs.WarPay;

/// <summary>Result of a war pay calculation.</summary>
/// <param name="Players">Every contributing player, highest score first.</param>
/// <param name="TotalScore">Sum of all players' adjusted scores, rounded down to a whole number.</param>
public sealed record WarPayResult(IReadOnlyList<PlayerWarPay> Players, long TotalScore)
{
    public long FactionId { get; init; }

    /// <summary>The ranked war id, or null when a custom From/To window was used.</summary>
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
    public string Name { get; internal set; } = "";

    public int WarHits { get; internal set; }
    public int BonusHits { get; internal set; }
    public int OutsideHits { get; internal set; }

    /// <summary>Own hits that were split with one or more assisters.</summary>
    public int SharedHits { get; internal set; }

    /// <summary>Hits by other members that this player assisted and got a share of.</summary>
    public int AssistHits { get; internal set; }

    /// <summary>
    /// This player's cut of the total cache value: totalCacheValue * share (33% = 0.33),
    /// rounded down to a whole number.
    /// </summary>
    public long Payout { get; internal set; }

    /// <summary>Score of own hits before any splitting, rounded down to a whole number.</summary>
    public long UnsplitScore { get; internal set; }

    /// <summary>Score after splitting with assisters, rounded down to a whole number.</summary>
    public long Score { get; internal set; }

    // Unrounded running totals used for the calculation. Rounding only happens once, at the end.
    internal double UnsplitExact { get; set; }
    internal double ScoreExact { get; set; }

    /// <summary>
    /// Percentage (0-100) of the faction's total adjusted score. Calculated from the unrounded
    /// scores, so it is not affected by the rounding above.
    /// </summary>
    public double SharePercent { get; internal set; }
}
