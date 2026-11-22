using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BetaTornBot.Shared;

namespace WarPay;

/// <summary>
/// Entry points for how often your members were hit by the enemy during the current war.
/// This class is split over two folders: the plain count lives in WarDefense, the version that
/// also adds up the enemy's respect lives in RespectEarned.
/// </summary>
public static partial class WarDefenseCalculator
{
    /// <summary>
    /// Counts, for the ranked war your faction is fighting right now, how many times each member was
    /// successfully hit by the enemy so far.
    /// This is never cached: the war is still going, so every call reads the attack log fresh from Torn.
    /// </summary>
    /// <param name="apiKey">Torn API key with faction access.</param>
    /// <exception cref="TornApiException">Torn returned an error, or no ranked war is running.</exception>
    public static async Task<WarDefenseResult> CountTimesHitAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var (factionId, window, attacks) = await CurrentWar.LoadAsync(apiKey, cancellationToken);

        var perPlayer = new Dictionary<long, (string Name, int Count)>();
        int total = 0;

        foreach (var a in CurrentWar.HitsTaken(attacks, factionId))
        {
            long defenderId = a.DefenderId!.Value;
            perPlayer.TryGetValue(defenderId, out var entry);
            perPlayer[defenderId] = (PickName(a, entry.Name), entry.Count + 1);
            total++;
        }

        var players = perPlayer
            .Select(kv => new PlayerHitsTaken(kv.Key, DisplayName(kv.Key, kv.Value.Name), kv.Value.Count))
            .OrderByDescending(p => p.TimesHit)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new WarDefenseResult(players, total)
        {
            FactionId = factionId,
            WarId = window.WarId,
            From = window.From,
            To = window.To,
            AttackCount = attacks.Count,
        };
    }

    // Used by both halves of this class.
    private static string PickName(Attack a, string? current) =>
        string.IsNullOrEmpty(a.DefenderName) ? current ?? "" : a.DefenderName;

    private static string DisplayName(long id, string? name) =>
        string.IsNullOrEmpty(name) ? $"#{id}" : name;
}
