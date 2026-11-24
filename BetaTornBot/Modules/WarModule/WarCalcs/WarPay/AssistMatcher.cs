using System;
using System.Collections.Generic;
using System.Linq;
using BetaTornBot.Shared;

namespace BetaTornBot.Modules.WarModule.WarCalcs.WarPay;

/// <summary>Finds which assists belong to which hit.</summary>
internal static class AssistMatcher
{
    public static bool IsAssist(Attack a) =>
        string.Equals(a.Result, "Assist", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns one assist per distinct helper for <paramref name="hit"/>: assists by other members that
    /// finished just before (or overlapped) the hit. Matched assists are added to
    /// <paramref name="claimed"/> so a later hit cannot use them again.
    /// </summary>
    public static IReadOnlyList<Attack> ClaimHelpers(
        Attack hit,
        IReadOnlyList<Attack> assists,
        HashSet<Attack> claimed,
        int windowSeconds)
    {
        long hitEnd = Math.Max(hit.Ended, hit.Started);

        var matches = assists
            .Where(a => !claimed.Contains(a)
                        && a.AttackerId != hit.AttackerId
                        && a.Started <= hitEnd
                        && hit.Started - Math.Max(a.Ended, a.Started) <= windowSeconds)
            .ToList();

        foreach (var m in matches) claimed.Add(m);

        // One share per helper, even if they assisted more than once.
        return matches
            .GroupBy(m => m.AttackerId!.Value)
            .Select(g => g.First())
            .ToList();
    }
}
