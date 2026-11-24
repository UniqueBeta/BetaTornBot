using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BetaTornBot.Shared;

namespace WarPay;

/// <summary>Entry point for listing your faction's finished ranked wars.</summary>
public static class WarListCalculator
{
    private static readonly HttpClient Http = new();

    /// <summary>
    /// Lists your faction's finished ranked wars, newest first, as "TheirName vs OurName: ID"
    /// (see <see cref="FormerWar.Display"/>). A war that is still running is not included.
    /// </summary>
    /// <param name="apiKey">Torn API key with faction access.</param>
    /// <param name="limit">How many wars to return (null = all of them).</param>
    /// <exception cref="TornApiException">Torn returned an error or an unexpected response.</exception>
    public static async Task<IReadOnlyList<FormerWar>> GetFormerWarsAsync(
        string apiKey,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An API key is required.", nameof(apiKey));
        if (limit is < 1)
            throw new ArgumentOutOfRangeException(nameof(limit), "The limit must be at least 1.");

        var api = new TornApiClient(Http, apiKey);

        long factionId = await api.GetFactionIdAsync(cancellationToken);
        var rankedWars = await api.GetRankedWarsAsync(cancellationToken);

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var wars = rankedWars
            // No end time yet, or an end in the future: the war is still running, so it isn't "former".
            .Where(w => w.End > 0 && w.End <= now)
            .OrderByDescending(w => w.Start)
            .Select(w =>
            {
                var ours = w.Factions.FirstOrDefault(f => f.Id == factionId);
                var theirs = w.Factions.FirstOrDefault(f => f.Id != factionId);

                return new FormerWar(
                    WarId: w.Id,
                    OurName: NameOf(ours, factionId),
                    TheirName: theirs is null ? "Unknown" : NameOf(theirs, theirs.Id),
                    Start: w.Start,
                    End: w.End);
            });

        return (limit is int n ? wars.Take(n) : wars).ToList();
    }

    private static string NameOf(RankedWarFaction? faction, long id) =>
        faction is null || string.IsNullOrEmpty(faction.Name) ? $"#{id}" : faction.Name;
}
