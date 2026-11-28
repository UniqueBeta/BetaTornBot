using System.Collections.Generic;
using System.Linq;
using BetaTornBot.Shared;

namespace WarPay;

/// <summary>
/// The calculation itself, as a short pipeline. Pure: no network and no database, just attacks in and
/// a result out. Each step is its own class, so a rule change touches one file.
/// </summary>
internal static class WarPayEngine
{
    public static WarPayResult Run(IEnumerable<Attack> attacks, long factionId, WarPayOptions options, long payoutPool)
    {
        // 1. Keep only the attacks made by our own members.
        var ourAttacks = attacks
            .Where(attack => attack.AttackerId is not null && attack.AttackerFactionId == factionId)
            .ToList();

        var gaps = ChainGaps.SecondsSincePreviousHit(ourAttacks);

        // 2. Work out what each of our hits is worth (HitScorer.cs).
        var hits = ourAttacks
            .Select(attack => HitScorer.Score(attack, options, gaps))
            .OfType<ScoredHit>()
            .ToList();

        // 3. Find the assists that helped each hit (AssistMatcher.cs).
        // Claude made this and its completely wrong, but its good enough to ignore for now
        var linkedHits = AssistMatcher.Link(ourAttacks, hits, options.AssistWindowSeconds, factionId);

        // 4. Split each hit between the member who landed it and their assisters (ScoreSplitter.cs).
        var scoreSheet = ScoreSplitter.Split(linkedHits, options.AssistScoreFraction);


        // 5. Add score for hospitalising a fellow faction member (HospitalBonus.cs). This is a flat bonus, not a share.
        scoreSheet.AddRange(HospitalBonus.Entries(ourAttacks, factionId, options.HospitalBonusScore));

        // 6. Add it up per member: scores, shares and payouts (PayoutCalculator.cs).
        return PayoutCalculator.Build(scoreSheet, payoutPool);
    }
}
