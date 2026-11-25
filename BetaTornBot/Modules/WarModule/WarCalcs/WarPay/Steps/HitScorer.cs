using BetaTornBot.Shared;

namespace WarPay;

/// <summary>What kind of hit it was. Only hits that score get a kind.</summary>
internal enum HitKind
{
    /// <summary>A ranked war hit, worth the respect Torn paid for it.</summary>
    War,

    /// <summary>A chain bonus hit, worth a flat amount.</summary>
    Bonus,

    /// <summary>A hit outside the war made while the chain was big enough, worth a flat amount.</summary>
    Outside,

    /// <summary>A flat extra for hospitalising a fellow faction member.</summary>
    Hospital,
}

/// <summary>A hit of ours that is worth points, before any splitting with assisters.</summary>
internal sealed record ScoredHit(Attack Attack, HitKind Kind, double Value);

/// <summary>Step 2: decides what each of our attacks is worth.</summary>
internal static class HitScorer
{
    /// <summary>The value of one attack, or null if it doesn't score at all.</summary>
    public static ScoredHit? Score(Attack attack, WarPayOptions options)
    {
        // Lost, stalemate, escape, assist... nothing to score.
        if (!AttackResults.IsSuccessful(attack)) return null;

        return attack.IsRankedWar
            ? ScoreWarHit(attack, options)
            : ScoreOutsideHit(attack, options);
    }

    // A hit that counted for the war: Torn's respect, unless it was a chain bonus.
    private static ScoredHit ScoreWarHit(Attack attack, WarPayOptions options)
    {
        if (IsChainBonus(attack, options))
            return new ScoredHit(attack, HitKind.Bonus, options.BonusHitScore);

        return new ScoredHit(attack, HitKind.War, attack.RespectGain);
    }

    // A hit outside the war: only scores while the chain is big enough.
    private static ScoredHit? ScoreOutsideHit(Attack attack, WarPayOptions options)
    {
        bool chainIsBigEnough = attack.Chain > options.OutsideChainThreshold;
        if (!chainIsBigEnough) return null;

        if (IsChainBonus(attack, options))
            //chain bonuses on outside targets are completely ignored in code, and will be highly penalized in the faction
            return new ScoredHit(attack, HitKind.Bonus, 0);

        return new ScoredHit(attack, HitKind.Outside, options.OutsideHitScore);
    }

    private static bool IsChainBonus(Attack attack, WarPayOptions options) =>
        options.ChainBonusMilestones.Contains(attack.Chain);
}
