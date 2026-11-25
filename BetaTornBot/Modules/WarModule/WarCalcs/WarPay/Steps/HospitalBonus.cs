using System;
using System.Collections.Generic;
using System.Linq;
using BetaTornBot.Shared;

namespace WarPay;

/// <summary>
/// Step 4b: a flat extra score when a member hospitalises a fellow faction member.
/// Uses all of our attacks, not just the scoring ones, since these hits usually don't score on their own.
/// </summary>
internal static class HospitalBonus
{
    public static IEnumerable<ScoreEntry> Entries(IReadOnlyList<Attack> ourAttacks, long factionId, double bonusScore)
    {
        return ourAttacks
            .Where(attack => IsHospitalised(attack)
                             && attack.AttackerFactionId == factionId
                             && attack.DefenderFactionId == factionId
                             && attack.DefenderId != attack.AttackerId)
            .Select(attack => new ScoreEntry(
                PlayerId: attack.AttackerId!.Value,
                Name: attack.AttackerName,
                Role: EntryRole.Hitter,
                Kind: HitKind.Hospital,
                HitValue: bonusScore,
                Amount: bonusScore,
                IsShared: false));
    }

    private static bool IsHospitalised(Attack attack) =>
        string.Equals(attack.Result, "Hospitalized", StringComparison.OrdinalIgnoreCase);
}