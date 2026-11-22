using System.Collections.Generic;
using System.Linq;
using BetaTornBot.Shared;

namespace WarPay;

/// <summary>The calculation itself. Pure: no network, just attacks in and a result out.</summary>
internal static class WarPayEngine
{
    public static WarPayResult Run(IEnumerable<Attack> attacks, long factionId, WarPayOptions options, long totalCacheValue)
    {
        var ledger = new ScoreLedger();

        var ourAttacks = attacks
            .Where(a => a.AttackerId is not null && a.AttackerFactionId == factionId);

        // Assists only matter against the same target, so handle one target at a time.
        // Attacks with no target id are never matched with each other.
        foreach (var target in ourAttacks.GroupBy(a => a.DefenderId ?? -a.Id))
        {
            var ordered = target.OrderBy(a => a.Started).ThenBy(a => a.Id).ToList();
            ProcessTarget(ordered, options, ledger);
        }

        return ledger.Build(totalCacheValue);
    }

    private static void ProcessTarget(IReadOnlyList<Attack> attacksOnTarget, WarPayOptions options, ScoreLedger ledger)
    {
        var assists = attacksOnTarget.Where(AssistMatcher.IsAssist).ToList();
        var claimed = new HashSet<Attack>();

        foreach (var hit in attacksOnTarget.Where(a => !AssistMatcher.IsAssist(a)))
        {
            var scored = HitScorer.Score(hit, options);
            if (scored.Kind == HitKind.None) continue;

            var helpers = AssistMatcher.ClaimHelpers(hit, assists, claimed, options.AssistWindowSeconds);
            ledger.RecordHit(hit, scored, helpers);
        }
    }
}
