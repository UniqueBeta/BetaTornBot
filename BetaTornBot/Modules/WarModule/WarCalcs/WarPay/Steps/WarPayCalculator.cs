using BetaTornBot.Modules.WarModule.WarCalcs.WarPay;
using BetaTornBot.Shared;
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

    // The cache value is entered in billions (2.5 means 2,500,000,000). Change this to change the unit.
    private const double CacheValueUnit = 1_000_000_000;

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
    /// <param name="totalCacheValue">Total value of the caches, in billions.</param>
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
        int assistWindowSeconds,
        double hospitalBonusScore,
        string databasePath = DefaultDatabasePath,
        CancellationToken cancellationToken = default)
    {
        ValidateArguments(apiKey, databasePath);

        long payoutPool = CalculatePayoutPool(totalCacheValue, factionTake);

        var options = new WarPayOptions
        {
            OutsideChainThreshold = outsideChainThreshold,
            OutsideHitScore = outsideHitScore,
            BonusHitScore = bonusHitScore,
            AssistWindowSeconds = assistWindowSeconds,
            HospitalBonusScore = hospitalBonusScore,
        };
        var settings = ToSettings(options, payoutPool);

        var api = new TornApiClient(SharedHttp, apiKey);
        var store = new WarPayStore(databasePath);

        // 1. Which war is this? The latest finished ranked war of the faction behind the API key.
        long factionId = await api.GetFactionIdAsync(cancellationToken);
        WarWindow war = await api.GetWarWindowAsync(null, cancellationToken);
        long warId = war.WarId ?? throw new TornApiException("Torn returned a ranked war without an id.");

        // 2. Already calculated with exactly these settings? Then there is nothing to do.
        var saved = store.TryLoadResult(warId, factionId, settings);
        if (saved is not null) {
            Console.WriteLine("WTF????????");
            return saved;
        }

        // 3. Get the attack log: from the database if it was saved earlier, otherwise from Torn.
        var attacks = await LoadAttacksAsync(api, store, factionId, warId, war, cancellationToken);

        // 4. Calculate (see WarPayEngine.cs for the steps).
        var result = WarPayEngine.Run(attacks, factionId, options, payoutPool) with
        {
            FactionId = factionId,
            WarId = warId,
            From = war.From,
            To = war.To,
            AttackCount = attacks.Count,
        };

        // 5. Save the result, so asking again with the same settings is instant.
        store.SaveResult(warId, settings, result);
        return result;
    }

    private static void ValidateArguments(string apiKey, string databasePath)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An API key is required.", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("A database path is required.", nameof(databasePath));
    }

    // What is left to pay out to the members once the faction has taken its cut.
    private static long CalculatePayoutPool(double totalCacheValue, double factionTake)
    {
        double membersShare = 1 - (factionTake / 100);   // a take of 20 leaves 0.80 for the members
        long payoutPool = (long)(totalCacheValue * CacheValueUnit * membersShare);

        if (payoutPool < 0)
            throw new ArgumentOutOfRangeException(nameof(payoutPool), "The payout pool can't be negative.");

        return payoutPool;
    }

    // The settings a saved result was calculated with. A saved result is only reused if these match.
    private static WarSettings ToSettings(WarPayOptions options, long payoutPool) =>
        new(options.OutsideChainThreshold,
            options.OutsideHitScore,
            options.BonusHitScore,
            options.AssistWindowSeconds,
            payoutPool);

    // The attack log of a war: the saved copy if there is one, otherwise from Torn (and then saved).
    private static async Task<List<Attack>> LoadAttacksAsync(
        TornApiClient api,
        WarPayStore store,
        long factionId,
        long warId,
        WarWindow war,
        CancellationToken cancellationToken)
    {
        var saved = store.TryLoadAttacks(warId, factionId);
        if (saved is not null) return saved;

        var attacks = await api.GetAttacksAsync(war.From, war.To, cancellationToken);
        store.SaveAttacks(warId, factionId, war.From, war.To, attacks);
        return attacks;
    }
}
