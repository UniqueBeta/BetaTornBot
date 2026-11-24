using System;
using System.Collections.Generic;
using System.Linq;
using BetaTornBot.Shared;

namespace BetaTornBot.Modules.WarModule.WarCalcs.WarPay;

/// <summary>Accumulates per-player totals and produces the final result.</summary>
internal sealed class ScoreLedger
{
    private readonly Dictionary<long, PlayerWarPay> _players = new();

    private PlayerWarPay Get(Attack a)
    {
        long id = a.AttackerId!.Value;
        if (!_players.TryGetValue(id, out var player))
            _players[id] = player = new PlayerWarPay { Id = id };
        if (!string.IsNullOrEmpty(a.AttackerName)) player.Name = a.AttackerName;
        return player;
    }

    /// <summary>Records a scoring hit, splitting its value equally between the hitter and any helpers.</summary>
    public void RecordHit(Attack hit, ScoredHit scored, IReadOnlyList<Attack> helpers)
    {
        int people = 1 + helpers.Count;
        double each = scored.Value / people;

        var hitter = Get(hit);
        switch (scored.Kind)
        {
            case HitKind.War: hitter.WarHits++; break;
            case HitKind.Bonus: hitter.BonusHits++; break;
            case HitKind.Outside: hitter.OutsideHits++; break;
        }
        hitter.UnsplitExact += scored.Value;
        hitter.ScoreExact += each;
        if (people > 1) hitter.SharedHits++;

        foreach (var helperAttack in helpers)
        {
            var helper = Get(helperAttack);
            helper.ScoreExact += each;
            helper.AssistHits++;
        }
    }

    public WarPayResult Build(long totalCacheValue)
    {
        double total = _players.Values.Sum(p => p.ScoreExact);

        foreach (var p in _players.Values)
        {
            // Shares come from the exact values; only the displayed scores are rounded down.
            p.SharePercent = total > 0 ? p.ScoreExact / total * 100.0 : 0;
            p.Score = RoundDown(p.ScoreExact);
            p.UnsplitScore = RoundDown(p.UnsplitExact);

            // Payout = cache value * share as a fraction (33% = 0.33). Decimal keeps money maths
            // exact, so 33% of 1,000,000,000 is 330,000,000 and not 329,999,999.
            decimal fraction = total > 0 ? (decimal)p.ScoreExact / (decimal)total : 0m;
            p.Payout = (long)Math.Floor(totalCacheValue * fraction);
        }

        var ordered = _players.Values.OrderByDescending(p => p.ScoreExact).ToList();
        return new WarPayResult(ordered, RoundDown(total));
    }

    // The tiny epsilon stops floating-point noise (e.g. 9.9999999999 from adding thirds) rounding 10 down to 9.
    private static long RoundDown(double value) => (long)Math.Floor(value + 1e-9);
}
