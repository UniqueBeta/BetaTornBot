using BetaTornBot.Modules.WarModule.WarCalcs.WarDefense;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BetaTornBot.Shared;

/// <summary>Turns a <see cref="WarDefenseResult"/> into Discord-ready text.</summary>
public static class WarDefenseFormatter
{
    /// <summary>A table of how many times each member was hit, most hit first, with a total row.</summary>
    public static IReadOnlyList<string> ToDiscordMessages(
        WarDefenseResult result,
        string title = "War Defense: Hits Taken",
        int maxMessageLength = 1900)
    {
        ArgumentNullException.ThrowIfNull(result);

        var inv = CultureInfo.InvariantCulture;

        var columns = new[]
        {
            new TableColumn("#", Align.Right),
            new TableColumn("Name", Align.Left),
            new TableColumn("Hits", Align.Right),
        };

        var rows = result.Players
            .Select((p, i) => new[]
            {
                (i + 1).ToString(inv),
                DiscordTable.CleanName(p.Name, p.PlayerId),
                p.TimesHit.ToString("N0", inv),
            })
            .ToList();

        var total = new[] { "", "Total", result.TotalHits.ToString("N0", inv) };

        return DiscordTable.Build(title, columns, rows, total, "Nobody has been hit yet.", maxMessageLength);
    }
}
