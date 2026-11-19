using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WarPay;

/// <summary>Front door of the WarPay folder. This is the only method you need to call.</summary>
public static class WarPayCalculator
{
    /// <summary>Where wars are saved unless you pass another path.</summary>
    public const string DefaultDatabasePath = "warpay.db";

    private static readonly HttpClient SharedHttp = new();

    /// <summary>
    /// Works out how much of the total war score each member of your faction's latest finished
    /// ranked war contributed, and saves the war to a SQLite database.
    /// <list type="bullet">
    /// <item>War hits count as Torn's respect; chain-bonus hits count as a flat <paramref name="bonusHitScore"/>.</item>
    /// <item>Successful hits outside the war count as <paramref name="outsideHitScore"/> while the chain is above <paramref name="outsideChainThreshold"/>.</item>
    /// <item>When a member assisted a hit, its score is split equally between the attacker and the assisters.</item>
    /// </list>
    /// If the war is already saved with the same settings, the saved result is returned without
    /// calculating again. If it is saved with different settings, the saved attack log is reused
    /// (no Torn attack-log calls) and the result is recalculated and replaced.
    /// </summary>
    /// <param name="outsideChainThreshold">Outside-war hits only score while the chain is strictly above this number (e.g. 100).</param>
    /// <param name="outsideHitScore">Score for a successful hit outside the war (e.g. 3).</param>
    /// <param name="bonusHitScore">Flat score for every chain-bonus hit (e.g. 10).</param>
    /// <param name="apiKey">Torn API key with faction access.</param>
    /// <param name="totalCacheValue">Total value to pay out. Each player's <see cref="PlayerWarPay.Payout"/> is this times their share (33% = 0.33), rounded down.</param>
    /// <param name="factionTake">Percentage of war pay that goes to the faction (e.g. 20 = 20%).</param>
    /// <param name="databasePath">SQLite file to save to and read from. Use one file per faction.</param>
    /// <exception cref="TornApiException">Torn returned an error or an unexpected response.</exception>
    public static async Task<WarPayResult> CalculateWarPayAsync(
        int outsideChainThreshold,
        double outsideHitScore,
        double bonusHitScore,
        string apiKey,
        double totalCacheValue,
        double factionTake,
        string databasePath = DefaultDatabasePath,
        CancellationToken cancellationToken = default)

    {

        double facPayPercentage = 1 - (factionTake / 100);
        long payoutPool = (long)(totalCacheValue * 1_000_000_000 * facPayPercentage);

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An API key is required.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database path is required.", nameof(databasePath));
        if (payoutPool < 0)
            throw new ArgumentOutOfRangeException(nameof(payoutPool), "The payout pool can't be negative.");

        var options = new WarPayOptions
        {
            OutsideChainThreshold = outsideChainThreshold,
            OutsideHitScore = outsideHitScore,
            BonusHitScore = bonusHitScore,
        };
        var settings = new WarSettings(
            options.OutsideChainThreshold,
            options.OutsideHitScore,
            options.BonusHitScore,
            options.AssistWindowSeconds,
            payoutPool);

        var api = new TornApiClient(SharedHttp, apiKey);

        // These two calls are cheap; they tell us which war this is so the database can be checked.
        long factionId = await api.GetFactionIdAsync(cancellationToken);
        WarWindow window = await api.GetWarWindowAsync(null, cancellationToken);

        WarPayResult Build(List<Attack> attacks) =>
            WarPayEngine.Run(attacks, factionId, options, payoutPool) with
            {
                FactionId = factionId,
                WarId = window.WarId,
                From = window.From,
                To = window.To,
                AttackCount = attacks.Count,
            };

        // Without a war id there is nothing to save the war under, so just calculate.
        if (window.WarId is not long warId)
            return Build(await api.GetAttacksAsync(window.From, window.To, cancellationToken));

        var store = new WarPayStore(databasePath);

        // 1. Already calculated with these exact settings: done.
        var saved = store.TryLoadResult(warId, factionId, settings);
        if (saved is not null) return saved;

        // 2. Attack log saved earlier: reuse it. Otherwise fetch it from Torn.
        var attacksForWar = store.TryLoadAttacks(warId, factionId);
        bool fetchedFromApi = attacksForWar is null;
        attacksForWar ??= await api.GetAttacksAsync(window.From, window.To, cancellationToken);

        var result = Build(attacksForWar);

        if (fetchedFromApi)
            store.SaveAttacks(warId, factionId, window.From, window.To, attacksForWar);
        store.SaveResult(warId, settings, result);

        return result;
    }
}
