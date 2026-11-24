using BetaTornBot.Modules.WarModule.WarCalcs.WarPay;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BetaTornBot.Shared;

/// <summary>Turns a <see cref="WarPayResult"/> into Discord-ready text.</summary>
public static class WarPayFormatter
{
    /// <summary>
    /// Builds an aligned pay table inside a code block, with a total row at the bottom.
    /// Long results come back as several messages: send them one after another.
    /// </summary>
    /// <param name="result">The result from the war pay calculation.</param>
    /// <param name="title">Bold heading above the first table.</param>
    /// <param name="showShare">Adds a Share column with each player's percentage.</param>
    /// <param name="maxMessageLength">Maximum characters per message (Discord's limit is 2000).</param>
    public static IReadOnlyList<string> ToDiscordMessages(
        WarPayResult result,
        string title = "War Pay Results",
        bool showShare = false,
        int maxMessageLength = 1900)
    {
        ArgumentNullException.ThrowIfNull(result);

        var inv = CultureInfo.InvariantCulture;

        var columns = new List<TableColumn>
        {
            new("#", Align.Right),
            new("Name", Align.Left),
            new("Score", Align.Right),
        };
        if (showShare) columns.Add(new TableColumn("Share", Align.Right));
        columns.Add(new TableColumn("Pay", Align.Right));

        string[] Cells(string rank, string name, string score, string share, string pay) =>
            showShare
                ? new[] { rank, name, score, share, pay }
                : new[] { rank, name, score, pay };

        var rows = result.Players
            .Select((p, i) => Cells(
                (i + 1).ToString(inv),
                DiscordTable.CleanName(p.Name, p.Id),
                p.Score.ToString("N0", inv),
                p.SharePercent.ToString("F1", inv) + "%",
                p.Payout.ToString("N0", inv)))
            .ToList();

        var total = Cells(
            "",
            "Total",
            result.TotalScore.ToString("N0", inv),
            "100.0%",
            result.Players.Sum(p => p.Payout).ToString("N0", inv));

        return DiscordTable.Build(title, columns, rows, total, "No scoring players found.", maxMessageLength);
    }
}
