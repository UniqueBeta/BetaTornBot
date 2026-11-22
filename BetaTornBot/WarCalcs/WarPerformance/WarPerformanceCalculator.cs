using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BetaTornBot.Shared;

namespace WarPay;

/// <summary>Entry point for the respect balance of the current war.</summary>
public static class WarPerformanceCalculator
{
    private sealed class Tally
    {
        public string Name = "";
        public double Gained;
        public double EnemyGained;
        public int HitsMade;
        public int HitsTaken;
    }

    /// <summary>
    /// For the ranked war your faction is fighting right now, works out for every member:
    /// the respect they gained from their own hits minus the respect the enemy gained from hitting them.
    /// Only successful hits that counted for the war are included, and each hit is worth Torn's own
    /// <c>respect_gain</c> (chain bonuses included). The faction totals are returned as well.
    /// Never cached: the war is still going, so every call reads the attack log fresh from Torn.
    /// </summary>
    /// <param name="apiKey">Torn API key with faction access.</param>
    /// <exception cref="TornApiException">Torn returned an error, or no ranked war is running.</exception>
    public static async Task<WarPerformanceResult> CalculatePerformanceAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var (factionId, window, attacks) = await CurrentWar.LoadAsync(apiKey, cancellationToken);

        var members = new Dictionary<long, Tally>();
        Tally Get(long id)
        {
            if (!members.TryGetValue(id, out var tally))
                members[id] = tally = new Tally();
            return tally;
        }

        double ourTotal = 0, enemyTotal = 0;
        int ourHits = 0, enemyHits = 0;

        // Respect each member earned from their own hits.
        foreach (var a in attacks)
        {
            if (a.AttackerFactionId != factionId) continue;    // only our own members' attacks
            if (!a.IsRankedWar) continue;                       // only hits that counted for the war
            if (!AttackResults.IsSuccessful(a)) continue;       // lost, stalemate, escape, assist...

            ourTotal += a.RespectGain;
            ourHits++;

            if (a.AttackerId is not long attackerId) continue;
            var tally = Get(attackerId);
            tally.Gained += a.RespectGain;
            tally.HitsMade++;
            if (!string.IsNullOrEmpty(a.AttackerName)) tally.Name = a.AttackerName;
        }

        // Respect the enemy earned from hitting each member.
        foreach (var a in CurrentWar.HitsTaken(attacks, factionId))
        {
            enemyTotal += a.RespectGain;
            enemyHits++;

            var tally = Get(a.DefenderId!.Value);
            tally.EnemyGained += a.RespectGain;
            tally.HitsTaken++;
            if (!string.IsNullOrEmpty(a.DefenderName)) tally.Name = a.DefenderName;
        }

        var players = members
            .Select(kv => new PlayerWarPerformance(
                PlayerId: kv.Key,
                Name: string.IsNullOrEmpty(kv.Value.Name) ? $"#{kv.Key}" : kv.Value.Name,
                RespectGained: Math.Round(kv.Value.Gained, 2),
                EnemyRespectGained: Math.Round(kv.Value.EnemyGained, 2),
                Performance: Math.Round(kv.Value.Gained - kv.Value.EnemyGained, 2),
                HitsMade: kv.Value.HitsMade,
                HitsTaken: kv.Value.HitsTaken))
            .OrderByDescending(p => p.Performance)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WarPerformanceResult(
            Players: players,
            Performance: Math.Round(ourTotal - enemyTotal, 2),
            OurRespect: Math.Round(ourTotal, 2),
            EnemyRespect: Math.Round(enemyTotal, 2))
        {
            OurHits = ourHits,
            EnemyHits = enemyHits,
            FactionId = factionId,
            WarId = window.WarId,
            From = window.From,
            To = window.To,
            AttackCount = attacks.Count,
        };
    }
}
