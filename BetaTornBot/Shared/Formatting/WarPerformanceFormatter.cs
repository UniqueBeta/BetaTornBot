using BetaTornBot.Modules.WarModule.WarCalcs.WarPerformance;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BetaTornBot.Shared;

/// <summary>Turns a <see cref="WarPerformanceResult"/> into Discord-ready text.</summary>
public static class WarPerformanceFormatter
{
    /// <summary>
    /// A table with one row per member: respect gained, respect the enemy gained from hitting them, and
    /// the difference (with a + or - sign), best performance first, plus a total row.
    /// Long results come back as several messages: send them one after another.
    /// </summary>
    public static IReadOnlyList<string> ToDiscordMessages(
        WarPerformanceResult result,
        string title = "War Performance",
        int maxMessageLength = 1900)
    {
        ArgumentNullException.ThrowIfNull(result);

        var inv = CultureInfo.InvariantCulture;

        var columns = new[]
        {
            new TableColumn("#", Align.Right),
            new TableColumn("Name", Align.Left),
            new TableColumn("Gained", Align.Right),
            new TableColumn("Enemy", Align.Right),
            new TableColumn("Perf", Align.Right),
        };

        var rows = result.Players
            .Select((p, i) => new[]
            {
                (i + 1).ToString(inv),
                DiscordTable.CleanName(p.Name, p.PlayerId),
                p.RespectGained.ToString("N2", inv),
                p.EnemyRespectGained.ToString("N2", inv),
                Signed(p.Performance),
            })
            .ToList();

        var total = new[]
        {
            "",
            "Total",
            result.OurRespect.ToString("N2", inv),
            result.EnemyRespect.ToString("N2", inv),
            Signed(result.Performance),
        };

        return DiscordTable.Build(title, columns, rows, total, "No war hits yet.", maxMessageLength);
    }

    // Positive numbers get a leading "+" so the sign is always visible; negatives already have "-".
    private static string Signed(double value) =>
        (value > 0 ? "+" : "") + value.ToString("N2", CultureInfo.InvariantCulture);
}
