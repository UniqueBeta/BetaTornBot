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

    /// <summary>
    /// how long there can be left on the timer before an outside hit gets scored as chain saving
    /// </summary>
    /// <summary>A chain dies when this many seconds pass without a hit.</summary>
    public int ChainTimeoutSeconds { get; } = 300;

    /// <summary>
    /// An outside hit is chain-saving if the chain had this many seconds or less left when it landed
    /// (120 = hit at least 3 minutes after the last one). 0 turns chain-saving off.
    /// </summary>
    public int OutsideChainSavingSecondsLeft { get; set; } = 90;

    /// <summary>A hit made at one of these chain numbers is a bonus hit.</summary>
    public IReadOnlySet<int> ChainBonusMilestones { get; init; } = DefaultChainBonusMilestones;

    /// <summary>Every bonus hit counts as exactly this much, whatever respect Torn paid for it.</summary>
    public double BonusHitScore { get; init; } = 10;

    /// <summary>Score for a successful hit on a player outside the war.</summary>
    public double OutsideHitScore { get; init; } = 1;

    /// <summary>Outside hits only score while the chain is strictly above this number.</summary>
    public int OutsideChainThreshold { get; init; } = 50;

    /// <summary>Extra score for a scoring hit that hospitalised a member of our faction. 0 turns it off.</summary>
    public double HospitalBonusScore { get; init; } = 0;

    /// <summary>
    /// An assist counts for a hit if it was made on the same target and finished no more than this
    /// many seconds before the hit started (or overlapped it).
    /// </summary>
    //change this so 0 means only hits that overlap
    public int AssistWindowSeconds { get; init; } = 10;

    /// <summary>
    /// The fraction of a hit's an assister gets. 0.5 means an assister gets half of the score hit for
    /// this score is NOT deducted from the original hit
    /// if the value is above 1 the assister will instead be granted a static amount of score equal to the value of this property
    // / </summary>
    public double AssistScoreFraction { get; init; } = 0.5;

    /// <summary>Score for an outside hit that comes more than OutsideMinGapMinutes after the previous chain hit.</summary>
    public double OutsideChainSavingHitScore { get; init; } = 6;

    //the following two values probably shouldnt be kept here as theyre static constant, but oh well
}
