using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WarPay;

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
    /// <param name="showStats">Adds hit counts: War, Bonus, Outside and Assist (assists = hits by others you shared in).</param>
    public static IReadOnlyList<string> ToDiscordMessages(
        WarPayResult result,
        string title = "War Pay Results",
        bool showShare = false,
        int maxMessageLength = 1900,
        bool showStats = true)
    {
        ArgumentNullException.ThrowIfNull(result);

        var inv = CultureInfo.InvariantCulture;

        // Each column: header, alignment, and how to get its text for a player and for the total row.
        // To add, remove or reorder a column, change only this list.
        var columns = new List<Column>
        {
            new("#", Align.Right, (p, i) => (i + 1).ToString(inv), "")
        };
        columns.Add(new("Name", Align.Left, (p, i) => DiscordTable.CleanName(p.Name, p.Id), "Total"));

        if (showStats)
        {
            columns.Add(Count("War", p => p.WarHits, result));
            columns.Add(Count("Bonus", p => p.BonusHits, result));
            columns.Add(Count("Out", p => p.OutsideHits, result));
            columns.Add(Count("Asst", p => p.AssistHits, result));
            columns.Add(Count("Hosp", p => p.HospitalHits, result));
            columns.Add(Count("Save", p => p.ChainSavingHits, result));
        }

        columns.Add(new("Score", Align.Right,
            (p, i) => p.Score.ToString("N0", inv),
            result.TotalScore.ToString("N0", inv)));

        if (showShare)
            columns.Add(new("Share", Align.Right,
                (p, i) => p.SharePercent.ToString("F1", inv) + "%",
                "100.0%"));

        columns.Add(new("Pay", Align.Right,
            (p, i) => p.Payout.ToString("N0", inv),
            result.Players.Sum(p => p.Payout).ToString("N0", inv)));

        var tableColumns = columns.Select(c => new TableColumn(c.Header, c.Align)).ToList();

        var rows = result.Players
            .Select((player, index) => columns.Select(c => c.Cell(player, index)).ToArray())
            .ToList();

        var total = columns.Select(c => c.Total).ToArray();

        return DiscordTable.Build(title, tableColumns, rows, total, "No scoring players found.", maxMessageLength);
    }

    // A count column (e.g. outside hits) whose total row is the sum over all players.
    private static Column Count(string header, Func<PlayerWarPay, int> value, WarPayResult result) =>
        new(header, Align.Right,
            (p, i) => value(p).ToString(CultureInfo.InvariantCulture),
            result.Players.Sum(value).ToString(CultureInfo.InvariantCulture));

    private sealed record Column(string Header, Align Align, Func<PlayerWarPay, int, string> Cell, string Total);
}
