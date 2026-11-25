using System.Collections.Generic;

namespace WarPay;

/// <summary>
/// Every number that decides how war pay is scored. Change a default here to change the rules for everyone;
/// the first three can also be set per run from the Discord command.
/// </summary>
internal sealed record WarPayOptions
{
    // The chain numbers that give a bonus hit, matching Torn's chain bonuses.
    private static readonly HashSet<int> DefaultChainBonusMilestones = new()
    {
        10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000
    };

    /// <summary>A hit made at one of these chain numbers is a bonus hit.</summary>
    public IReadOnlySet<int> ChainBonusMilestones { get; init; } = DefaultChainBonusMilestones;

    /// <summary>Every bonus hit counts as exactly this much, whatever respect Torn paid for it.</summary>
    public double BonusHitScore { get; init; } = 10;

    /// <summary>Score for a successful hit on a player outside the war.</summary>
    public double OutsideHitScore { get; init; } = 3;

    /// <summary>Outside hits only score while the chain is strictly above this number.</summary>
    public int OutsideChainThreshold { get; init; } = 100;

    /// <summary>Extra score for a scoring hit that hospitalised a member of our faction. 0 turns it off.</summary>
    public double HospitalBonusScore { get; init; } = 0;

    /// <summary>
    /// An assist counts for a hit if it was made on the same target and finished no more than this
    /// many seconds before the hit started (or overlapped it).
    /// </summary>
    //change this so 0 means only hits that overlap
    public int AssistWindowSeconds { get; init; } = 10;
}
