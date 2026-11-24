using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BetaTornBot.Modules.WarModule.WarCalcs.RespectEarned;
using BetaTornBot.Shared;

namespace WarPay;

// The other half of WarDefenseCalculator (see WarDefense/WarDefenseCalculator.cs).
public static partial class WarDefenseCalculator
{
    /// <summary>
    /// Same as <see cref="CountTimesHitAsync"/>, and also adds up how much respect the enemy faction
    /// earned from each of those hits (Torn's own <c>respect_gain</c> for the attack, chain bonuses included).
    /// Never cached, for the same reason.
    /// </summary>
    /// <param name="apiKey">Torn API key with faction access.</param>
    /// <exception cref="TornApiException">Torn returned an error, or no ranked war is running.</exception>
    public static async Task<WarDefenseRespectResult> CountTimesHitWithRespectAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var (factionId, window, attacks) = await CurrentWar.LoadAsync(apiKey, cancellationToken);

        var perPlayer = new Dictionary<long, (string Name, int Count, double Respect)>();
        int total = 0;
        double totalRespect = 0;

        foreach (var a in CurrentWar.HitsTaken(attacks, factionId))
        {
            long defenderId = a.DefenderId!.Value;
            perPlayer.TryGetValue(defenderId, out var entry);
            perPlayer[defenderId] = (PickName(a, entry.Name), entry.Count + 1, entry.Respect + a.RespectGain);
            total++;
            totalRespect += a.RespectGain;
        }

        var players = perPlayer
            .Select(kv => new PlayerHitsTakenWithRespect(
                kv.Key,
                DisplayName(kv.Key, kv.Value.Name),
                kv.Value.Count,
                Math.Round(kv.Value.Respect, 2)))
            .OrderByDescending(p => p.TimesHit)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WarDefenseRespectResult(players, total, Math.Round(totalRespect, 2))
        {
            FactionId = factionId,
            WarId = window.WarId,
            From = window.From,
            To = window.To,
            AttackCount = attacks.Count,
        };
    }
}
