namespace BetaTornBot.Shared;

/// <summary>A finished ranked war.</summary>
/// <param name="WarId">The ranked war id.</param>
/// <param name="OurName">Your faction's name.</param>
/// <param name="TheirName">The enemy faction's name.</param>
/// <param name="Start">When the war started (unix seconds).</param>
/// <param name="End">When the war ended (unix seconds).</param>
public sealed record FormerWar(long WarId, string OurName, string TheirName, long Start, long End)
{
    /// <summary>"TheirName vs OurName: ID"</summary>
    public string Display => $"{TheirName} vs {OurName}: {WarId}";
}
