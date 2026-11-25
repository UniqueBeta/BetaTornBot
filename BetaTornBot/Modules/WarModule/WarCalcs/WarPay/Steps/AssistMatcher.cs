using BetaTornBot.Modules.WarModule.WarCalcs.WarPay;
using BetaTornBot.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace WarPay;

/// <summary>A scoring hit together with the members who assisted it.</summary>
internal sealed record LinkedHit(ScoredHit Hit, IReadOnlyList<Attack> Helpers);

/// <summary>Step 3: finds which assists belong to which hit.</summary>
internal static class AssistMatcher
{
    /// <param name="ourAttacks">All of our attacks, including the assists.</param>
    /// <param name="hits">The hits that score.</param>
    /// <param name="windowSeconds">How long before a hit an assist may have finished.</param>
    public static List<LinkedHit> Link(
    IReadOnlyList<Attack> ourAttacks,
    IReadOnlyList<ScoredHit> hits,
    int windowSeconds,
    long factionId)
    {
        var assistsByTarget = ourAttacks
            .Where(AttackResults.IsAssist)
            .ToLookup(TargetOf);

        var alreadyUsed = new HashSet<Attack>();

        var linked = new List<LinkedHit>();
        foreach (var hit in hits.OrderBy(h => h.Attack.Started).ThenBy(h => h.Attack.Id))
        {
            // A hit on one of our own members can never be assisted.
            if (hit.Attack.DefenderFactionId == factionId)
            {
                linked.Add(new LinkedHit(hit, Array.Empty<Attack>()));
                continue;
            }

            var assistsOnSameTarget = assistsByTarget[TargetOf(hit.Attack)];
            var helpers = TakeHelpers(hit, assistsOnSameTarget, alreadyUsed, windowSeconds);
            linked.Add(new LinkedHit(hit, helpers));
        }
        return linked;
    }
    // The assists that helped this hit, one per member, marked as used so no other hit can claim them.
    private static IReadOnlyList<Attack> TakeHelpers(
        ScoredHit hit,
        IEnumerable<Attack> assistsOnSameTarget,
        HashSet<Attack> alreadyUsed,
        int windowSeconds)
    {
        var matching = assistsOnSameTarget
            .Where(assist => !alreadyUsed.Contains(assist) && IsHelpfulAssist(assist, hit.Attack, windowSeconds))
            .ToList();

        foreach (var assist in matching) alreadyUsed.Add(assist);

        // One share per member, even if they assisted more than once.
        return matching
            .GroupBy(assist => assist.AttackerId!.Value)
            .Select(group => group.First())
            .ToList();
    }

    private static bool IsHelpfulAssist(Attack assist, Attack hit, int windowSeconds)
    {
        long hitEnded = Math.Max(hit.Ended, hit.Started);
        long assistEnded = Math.Max(assist.Ended, assist.Started);

        bool byAnotherMember = assist.AttackerId != hit.AttackerId;
        bool startedBeforeHitEnded = assist.Started <= hitEnded;
        bool closeEnoughToHit = hit.Started - assistEnded <= windowSeconds;

        return byAnotherMember && startedBeforeHitEnded && closeEnoughToHit;
    }

    // Attacks on the same player share a key. An attack with no known target never matches anything else.
    private static long TargetOf(Attack attack) => attack.DefenderId ?? -attack.Id;
}
