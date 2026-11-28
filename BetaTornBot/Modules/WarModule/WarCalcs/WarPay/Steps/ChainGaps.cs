using System;
using System.Collections.Generic;
using System.Linq;
using BetaTornBot.Shared;

namespace WarPay;

/// <summary>For each chain hit of ours: how many seconds since our previous chain hit ended.</summary>
internal static class ChainGaps
{
    public static Dictionary<long, long> SecondsSincePreviousHit(IEnumerable<Attack> ourAttacks)
    {
        var gaps = new Dictionary<long, long>();
        Attack? previous = null;

        var chainHits = ourAttacks
            .Where(a => a.Chain > 0 && AttackResults.IsSuccessful(a))
            .OrderBy(a => a.Started)
            .ThenBy(a => a.Id);

        foreach (var attack in chainHits)
        {
            if (previous is not null)
                gaps[attack.Id] = Math.Max(0, attack.Started - Math.Max(previous.Ended, previous.Started));
            previous = attack;
        }
        return gaps;
    }
}