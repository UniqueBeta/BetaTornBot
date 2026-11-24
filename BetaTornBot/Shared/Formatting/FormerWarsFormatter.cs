using System;
using System.Collections.Generic;
using System.Text;
using WarPay;

namespace BetaTornBot.Shared.Formatting;

/// <summary>Turns a list of <see cref="FormerWar"/> into Discord-ready text.</summary>
public static class FormerWarsFormatter
{
    /// <summary>
    /// One line per war, "TheirName vs OurName: ID", inside a code block so the ids are easy to copy.
    /// Long lists come back as several messages: send them one after another.
    /// </summary>
    public static IReadOnlyList<string> ToDiscordMessages(
        IReadOnlyList<FormerWar> wars,
        string title = "Former Wars",
        int maxMessageLength = 1900)
    {
        ArgumentNullException.ThrowIfNull(wars);
        if (maxMessageLength < 200) throw new ArgumentOutOfRangeException(nameof(maxMessageLength));
        if (wars.Count == 0) return new[] { $"**{title}**\nNo former wars found." };

        var messages = new List<string>();
        var sb = new StringBuilder();

        void Start(bool first)
        {
            sb.Clear();
            if (first) sb.Append("**").Append(title).Append("**\n");
            sb.Append("```\n");
        }

        void Finish()
        {
            sb.Append("```");
            messages.Add(sb.ToString());
        }

        Start(first: true);
        foreach (var war in wars)
        {
            // Backticks would end the code block early.
            string line = war.Display.Replace('`', '\'').Replace('\n', ' ');

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
}
