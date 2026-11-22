using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WarPay;

namespace BetaTornBot.Shared;

/// <summary>Turns a <see cref="WarDefenseRespectResult"/> into Discord-ready text.</summary>
public static class RespectEarnedFormatter
{
    /// <summary>
    /// A table of how many times each member was hit and how much respect the enemy earned from it,
    /// most hit first, with a total row.
    /// </summary>
    public static IReadOnlyList<string> ToDiscordMessages(
        WarDefenseRespectResult result,
        string title = "Enemy Respect Earned",
        int maxMessageLength = 1900)
    {
        ArgumentNullException.ThrowIfNull(result);

        var inv = CultureInfo.InvariantCulture;

        var columns = new[]
        {
            new TableColumn("#", Align.Right),
            new TableColumn("Name", Align.Left),
            new TableColumn("Hits", Align.Right),
            new TableColumn("Respect", Align.Right),
        };

        var rows = result.Players
            .Select((p, i) => new[]
            {
                (i + 1).ToString(inv),
                DiscordTable.CleanName(p.Name, p.PlayerId),
                p.TimesHit.ToString("N0", inv),
                p.RespectGivenToEnemy.ToString("N2", inv),
            })
            .ToList();

        var total = new[]
        {
            "",
            "Total",
            result.TotalHits.ToString("N0", inv),
            result.TotalRespectGivenToEnemy.ToString("N2", inv),
        };

        return DiscordTable.Build(title, columns, rows, total, "Nobody has been hit yet.", maxMessageLength);
    }
}
