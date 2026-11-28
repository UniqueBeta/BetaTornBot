using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using WarPay;

namespace BetaTornBot.Shared.Formatting;

/// <summary>Turns a saved <see cref="WarPayResult"/> into one Torn "send money" link per member.</summary>
public static class FinalPayoutFormatter
{
    // Opens the faction controls page with the money form already filled in for that member.
    // To change the link, change this one line. {0} is the member id and {1} the amount.
    private const string LinkTemplate =
        "https://www.torn.com/factions.php?step=your#/tab=controls&addMoneyTo={0}&money={1}";

    /// <summary>The link for one member, e.g. ...&addMoneyTo=123&money=4567890 (no thousands separators).</summary>
    public static string PayoutLink(long memberId, long amount) =>
        string.Format(CultureInfo.InvariantCulture, LinkTemplate, memberId, amount);

    /// <summary>
    /// One line per member who is owed something, highest payout first: "Name – $1,234,567" as a link.
    /// Long lists come back as several messages: send them one after another.
    /// </summary>
    public static IReadOnlyList<string> ToDiscordMessages(
        WarPayResult result,
        string title = "Final Payout",
        int maxMessageLength = 1900)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (maxMessageLength < 200) throw new ArgumentOutOfRangeException(nameof(maxMessageLength));

        var inv = CultureInfo.InvariantCulture;

        var owed = result.Players
            .Where(p => p.Payout > 0)
            .OrderByDescending(p => p.Payout)
            .ToList();

        string heading = result.WarId is { } warId ? $"**{title}** (war {warId})" : $"**{title}**";
        if (owed.Count == 0) return new[] { heading + "\nNobody is owed anything." };

        // <...> around the url stops Discord from adding a big preview under every link.
        var lines = owed
            .Select(p => $"[{Escape(NameOrId(p))} – ${p.Payout.ToString("N0", inv)}](<{PayoutLink(p.Id, p.Payout)}>)")
            .ToList();

        string footer = $"Total: ${owed.Sum(p => p.Payout).ToString("N0", inv)} to {owed.Count} members";

        var messages = new List<string>();
        var sb = new StringBuilder(heading).Append('\n');

        foreach (var line in lines)
        {
            if (sb.Length + line.Length + 1 > maxMessageLength)
            {
                messages.Add(sb.ToString());
                sb.Clear();
            }
            sb.Append(line).Append('\n');
        }

        if (sb.Length + footer.Length > maxMessageLength)
        {
            messages.Add(sb.ToString());
            sb.Clear();
        }
        sb.Append(footer);
        messages.Add(sb.ToString());
        return messages;
    }

    private static string NameOrId(PlayerWarPay player) =>
        string.IsNullOrWhiteSpace(player.Name) ? $"#{player.Id}" : player.Name.Trim();

    // Names like Babcia_Jaga would turn into italics, and brackets would break the link.
    private static string Escape(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c is '\\' or '_' or '*' or '~' or '`' or '[' or ']' or '|' or '>')
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
    }
}
