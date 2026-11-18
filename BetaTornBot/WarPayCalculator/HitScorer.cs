using System;
using System.Collections.Generic;

namespace WarPay;

internal enum HitKind { None, War, Bonus, Outside }

internal readonly record struct ScoredHit(double Value, HitKind Kind);

/// <summary>Decides what a single attack is worth, before any assist splitting.</summary>
internal static class HitScorer
{
    private static readonly HashSet<int> ChainMilestones = new()
    {
        10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 25000, 50000, 100000
    };

    // Results that do not count as a successful hit.
    private static readonly HashSet<string> FailedResults = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lost", "Stalemate", "Assist", "Escape", "Timeout", "None", ""
    };

    public static bool IsSuccessful(Attack a) => !FailedResults.Contains(a.Result ?? "");

    public static ScoredHit Score(Attack a, WarPayOptions o)
    {
        if (!IsSuccessful(a)) return new ScoredHit(0, HitKind.None);

        bool isBonus = ChainMilestones.Contains(a.Chain);

        // War hit: Torn's respect, except chain bonuses are a flat value.
        if (a.IsRankedWar)
            return isBonus
                ? new ScoredHit(o.BonusHitScore, HitKind.Bonus)
                : new ScoredHit(a.RespectGain, HitKind.War);

        // Outside the war: only counts while the chain is above the threshold.
        if (a.Chain > o.OutsideChainThreshold)
            return isBonus
                ? new ScoredHit(o.BonusHitScore, HitKind.Bonus)
                : new ScoredHit(o.OutsideHitScore, HitKind.Outside);

        return new ScoredHit(0, HitKind.None);
    }
}
