using BetaTornBot.Modules.WarModule.WarCalcs.WarPay;
using System.Collections.Generic;
using System.Linq;

namespace WarPay;

/// <summary>Step 5: adds the score sheet up per member and turns it into scores, shares and payouts.</summary>
internal static class PayoutCalculator
{
    // One member's entries and their exact (unrounded) score.
    private sealed record PlayerTotal(long PlayerId, IReadOnlyList<ScoreEntry> Entries, double Score);

    public static WarPayResult Build(IReadOnlyList<ScoreEntry> entries, long payoutPool)
    {
        var totals = entries
            .GroupBy(entry => entry.PlayerId)
            .Select(group => new PlayerTotal(group.Key, group.ToList(), group.Sum(entry => entry.Amount)))
            .OrderByDescending(total => total.Score)
            .ThenBy(total => total.PlayerId)
            .ToList();

        double factionScore = totals.Sum(total => total.Score);

        var players = totals
            .Select(total => BuildPlayer(total, factionScore, payoutPool))
            .ToList();

        return new WarPayResult(players, WarPayMath.RoundDown(factionScore));
    }

    private static PlayerWarPay BuildPlayer(PlayerTotal total, double factionScore, long payoutPool)
    {
        var hitterEntries = total.Entries.Where(entry => entry.Role == EntryRole.Hitter).ToList();
        var assisterEntries = total.Entries.Where(entry => entry.Role == EntryRole.Assister).ToList();

        // Shares and payouts use the exact score; only the numbers shown are rounded down.
        return new PlayerWarPay
        {
            Id = total.PlayerId,
            Name = LatestName(total.Entries),

            WarHits = hitterEntries.Count(entry => entry.Kind == HitKind.War),
            BonusHits = hitterEntries.Count(entry => entry.Kind == HitKind.Bonus),
            OutsideHits = hitterEntries.Count(entry => entry.Kind == HitKind.Outside),
            SharedHits = hitterEntries.Count(entry => entry.IsShared),
            AssistHits = assisterEntries.Count,

            UnsplitScore = WarPayMath.RoundDown(hitterEntries.Sum(entry => entry.HitValue)),
            Score = WarPayMath.RoundDown(total.Score),
            SharePercent = WarPayMath.SharePercent(total.Score, factionScore),
            Payout = WarPayMath.Payout(payoutPool, total.Score, factionScore),
        };
    }

    private static string LatestName(IReadOnlyList<ScoreEntry> entries) =>
        entries.Select(entry => entry.Name).LastOrDefault(name => !string.IsNullOrEmpty(name)) ?? "";
}
