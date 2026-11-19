using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WarPay;

/// <summary>Turns a <see cref="WarPayResult"/> into Discord-ready text.</summary>
public static class WarPayFormatter
{
    private const int MaxNameWidth = 16; // Torn names are at most 16 characters.

    private readonly record struct Row(string Rank, string Name, string Score, string Share, string Pay);

    /// <summary>
    /// Builds an aligned table inside a code block. Code blocks use a fixed-width font, so the columns
    /// line up, and Discord doesn't treat "_" or "*" in player names as formatting.
    /// Discord caps a message at 2000 characters, so long results come back as several messages:
    /// send them one after another.
    /// </summary>
    /// <param name="result">The result from <see cref="WarPayCalculator.CalculateWarPayAsync"/>.</param>
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
        if (maxMessageLength < 200) throw new ArgumentOutOfRangeException(nameof(maxMessageLength));

        var players = result.Players;
        if (players.Count == 0)
            return new[] { $"**{title}**\nNo scoring players found." };

        var inv = CultureInfo.InvariantCulture;

        var rows = players
            .Select((p, i) => new Row(
                (i + 1).ToString(inv),
                CleanName(p.Name, p.Id),
                p.Score.ToString("N0", inv),
                p.SharePercent.ToString("F1", inv) + "%",
                p.Payout.ToString("N0", inv)))
            .ToList();

        var total = new Row(
            "",
            "Total",
            result.TotalScore.ToString("N0", inv),
            "100.0%",
            players.Sum(p => p.Payout).ToString("N0", inv));

        var all = rows.Append(total).ToList();
        int rankW = Math.Max(1, all.Max(r => r.Rank.Length));
        int nameW = Math.Max("Name".Length, all.Max(r => r.Name.Length));
        int scoreW = Math.Max("Score".Length, all.Max(r => r.Score.Length));
        int shareW = Math.Max("Share".Length, all.Max(r => r.Share.Length));
        int payW = Math.Max("Pay".Length, all.Max(r => r.Pay.Length));

        string Line(string rank, string name, string score, string share, string pay)
        {
            var line = new StringBuilder();
            line.Append(rank.PadLeft(rankW)).Append("  ")
                .Append(name.PadRight(nameW)).Append("  ")
                .Append(score.PadLeft(scoreW));
            if (showShare) line.Append("  ").Append(share.PadLeft(shareW));
            line.Append("  ").Append(pay.PadLeft(payW));
            return line.ToString();
        }

        string header = Line("#", "Name", "Score", "Share", "Pay");
        string separator = Line(
            new string('-', rankW), new string('-', nameW), new string('-', scoreW),
            new string('-', shareW), new string('-', payW));

        // The total goes last, under its own separator, as one unit so it is never split from its line.
        var lines = rows.Select(r => Line(r.Rank, r.Name, r.Score, r.Share, r.Pay)).ToList();
        lines.Add(separator + "\n" + Line(total.Rank, total.Name, total.Score, total.Share, total.Pay));

        var messages = new List<string>();
        var sb = new StringBuilder();

        void Start(bool first)
        {
            sb.Clear();
            if (first) sb.Append("**").Append(title).Append("**\n");
            sb.Append("```\n").Append(header).Append('\n').Append(separator).Append('\n');
        }

        void Finish()
        {
            sb.Append("```");
            messages.Add(sb.ToString());
        }

        Start(first: true);
        foreach (var line in lines)
        {
            // Leave room for the closing ``` and the newline after this line.
            if (sb.Length + line.Length + 1 + 3 > maxMessageLength)
            {
                Finish();
                Start(first: false);
            }
            sb.Append(line).Append('\n');
        }
        Finish();

        return messages;
    }

    // Backticks would end the code block early; the length cap keeps columns tidy.
    private static string CleanName(string? name, long id)
    {
        if (string.IsNullOrWhiteSpace(name)) return $"#{id}";
        name = name.Replace('`', '\'').Replace('\n', ' ').Trim();
        return name.Length <= MaxNameWidth ? name : name[..MaxNameWidth];
    }
}
