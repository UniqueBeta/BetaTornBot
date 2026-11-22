namespace WarPay;

/// <summary>Internal settings passed from the entry point to the calculation.</summary>
internal sealed record WarPayOptions
{
    /// <summary>
    /// An assist counts for a hit if it was made on the same target and finished no more than this
    /// many seconds before the hit started (or overlapped it).
    /// </summary>
    public int AssistWindowSeconds { get; init; } = 30;

    /// <summary>Every chain-bonus (milestone) hit counts as exactly this much, whatever Torn paid.</summary>
    public double BonusHitScore { get; init; } = 10;

    /// <summary>Score for a successful hit on a player outside the war.</summary>
    public double OutsideHitScore { get; init; } = 3;

    /// <summary>Outside hits only score while the chain is strictly above this number.</summary>
    public int OutsideChainThreshold { get; init; } = 100;
}
