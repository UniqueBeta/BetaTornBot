using System.Collections.Generic;

namespace BetaTornBot.Modules.WarModule.WarCalcs.WarPerformance;

/// <summary>How each member, and the faction as a whole, is doing in the current war, measured in respect.</summary>
/// <param name="Players">Every member who landed or took at least one war hit, best performance first.</param>
/// <param name="Performance">Faction total: OurRespect minus EnemyRespect. Positive means you are ahead.</param>
/// <param name="OurRespect">Faction total: respect your faction earned from its successful war hits, rounded to 2 decimals.</param>
/// <param name="EnemyRespect">Faction total: respect the enemy earned from its successful war hits on you, rounded to 2 decimals.</param>
public sealed record WarPerformanceResult(
    IReadOnlyList<PlayerWarPerformance> Players,
    double Performance,
    double OurRespect,
    double EnemyRespect)
{
    /// <summary>Successful war hits your members landed.</summary>
    public int OurHits { get; init; }

    /// <summary>Successful war hits the enemy landed on your members.</summary>
    public int EnemyHits { get; init; }

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

/// <summary>One member's respect balance in the current war.</summary>
/// <param name="PlayerId">Torn player id.</param>
/// <param name="Name">Player name.</param>
/// <param name="RespectGained">Respect earned from the member's own successful war hits.</param>
/// <param name="EnemyRespectGained">Respect the enemy earned from successfully hitting this member.</param>
/// <param name="Performance">RespectGained minus EnemyRespectGained. Positive means the member earned more than they gave away.</param>
/// <param name="HitsMade">Successful war hits the member landed.</param>
/// <param name="HitsTaken">Times the enemy successfully hit the member.</param>
public sealed record PlayerWarPerformance(
    long PlayerId,
    string Name,
    double RespectGained,
    double EnemyRespectGained,
    double Performance,
    int HitsMade,
    int HitsTaken);
