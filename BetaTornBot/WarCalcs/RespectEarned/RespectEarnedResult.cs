using System.Collections.Generic;

namespace WarPay;

/// <summary>Like <see cref="WarDefenseResult"/>, plus the respect the enemy faction earned from those hits.</summary>
/// <param name="Players">Members who were hit at least once, most hit first.</param>
/// <param name="TotalHits">Total successful enemy hits on your members during the war.</param>
/// <param name="TotalRespectGivenToEnemy">Respect the enemy faction earned from all those hits, rounded to 2 decimals.</param>
public sealed record WarDefenseRespectResult(
    IReadOnlyList<PlayerHitsTakenWithRespect> Players,
    int TotalHits,
    double TotalRespectGivenToEnemy)
{
    public long FactionId { get; init; }

    /// <summary>The ranked war that was analysed (the one running right now).</summary>
    public long? WarId { get; init; }

    /// <summary>Start of the war (unix seconds).</summary>
    public long From { get; init; }

    /// <summary>The moment of the call (unix seconds), since the war is still running.</summary>
    public long To { get; init; }

    /// <summary>How many attacks the log held for the war window (all kinds, both directions).</summary>
    public int AttackCount { get; init; }
}

/// <summary>One of your members, how many times the enemy hit them, and the respect the enemy earned from it.</summary>
public sealed record PlayerHitsTakenWithRespect(long PlayerId, string Name, int TimesHit, double RespectGivenToEnemy);
