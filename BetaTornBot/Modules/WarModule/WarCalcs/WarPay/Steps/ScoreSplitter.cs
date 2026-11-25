using Discord;
using System.Collections.Generic;

namespace WarPay;

/// <summary>Step 4: divides each hit between the member who landed it and the members who assisted.</summary>
internal static class ScoreSplitter
{
    public static List<ScoreEntry> Split(IEnumerable<LinkedHit> linkedHits, double assistScoreFraction)
    {
        var entries = new List<ScoreEntry>();
        foreach (var linked in linkedHits)
            entries.AddRange(SplitOne(linked, assistScoreFraction));
        return entries;
    }

    private static IEnumerable<ScoreEntry> SplitOne(LinkedHit linked, double assistScoreFraction)
    {
        var hit = linked.Hit;

        // The hitter keeps the whole hit. Each assister gets half of the hit's value on top of it.
        yield return new ScoreEntry(
            PlayerId: hit.Attack.AttackerId!.Value,
            Name: hit.Attack.AttackerName,
            Role: EntryRole.Hitter,
            Kind: hit.Kind,
            HitValue: hit.Value,
            Amount: hit.Value,
            IsShared: linked.Helpers.Count > 0);

        double true_value = 0;
        if (assistScoreFraction > 1)
        {
            true_value = assistScoreFraction; //give static score instead of fraction
            //give static score instead of fraction
        }
        else
        {
            true_value = hit.Value * assistScoreFraction; //give fraction of hit value
        }

            foreach (var helper in linked.Helpers)
            {


                yield return new ScoreEntry(
                    PlayerId: helper.AttackerId!.Value,
                    Name: helper.AttackerName,
                    Role: EntryRole.Assister,
                    Kind: hit.Kind,
                    HitValue: hit.Value,
                    Amount: true_value,
                    IsShared: true);
            }
    }
}
