using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BetaTornBot.Shared;

/// <summary>
/// Loading and filtering the attack log of the war that is running right now.
/// Shared by war defense, respect earned and war performance.
/// </summary>
internal static class CurrentWar
{
    private static readonly HttpClient Http = new();

    /// <summary>Faction id, the running war's window and its attack log, read fresh from Torn.</summary>
    public static async Task<(long FactionId, WarWindow Window, List<Attack> Attacks)> LoadAsync(
        string apiKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An API key is required.", nameof(apiKey));

        var api = new TornApiClient(Http, apiKey);

        long factionId = await api.GetFactionIdAsync(ct);
        WarWindow window = await api.GetCurrentWarWindowAsync(ct);
        var attacks = await api.GetAttacksAsync(window.From, window.To, ct);

        return (factionId, window, attacks);
    }

    /// <summary>The attacks that count as "a member of ours was hit by the enemy".</summary>
    public static IEnumerable<Attack> HitsTaken(IEnumerable<Attack> attacks, long factionId)
    {
        foreach (var a in attacks)
        {
            if (a.DefenderId is null) continue;

            // The log holds attacks in both directions. Our own members' attacks (including any on
            // each other) are not hits taken. A stealthed attacker has no faction and still counts.
            if (a.AttackerFactionId == factionId) continue;

            if (!a.IsRankedWar) continue;               // only hits that counted for the war
            if (!AttackResults.IsSuccessful(a)) continue; // the enemy lost, stalemated or only assisted

            yield return a;
        }
    }
}
