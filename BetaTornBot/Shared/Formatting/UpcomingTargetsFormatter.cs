using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BetaTornBot.Shared.Formatting
{
    /// <summary>
    /// Turns the upcoming-targets lines from <c>TornFactionService</c> into messages that fit in Discord.
    /// The lines come in soonest-okay-first; the output shows the soonest target last, so it sits at the
    /// bottom of the chat, closest to the input box.
    /// </summary>
    internal static class UpcomingTargetsFormatter
    {
        /// <summary>
        /// Keeps the first <paramref name="limit"/> lines, reverses them, and groups them into messages.
        /// Long lists come back as several messages: send them one after another.
        /// </summary>
        /// <param name="lines">One line per enemy member, soonest okay first.</param> 
        /// <param name="limit">How many targets to show.</param>
        /// <param name="maxMessageLength">Maximum characters per message (Discord's limit is 2000).</param>
        public static IReadOnlyList<string> ToMessages(
            IReadOnlyList<string> lines,
            int limit,
            int maxMessageLength = 1900)
        {
            ArgumentNullException.ThrowIfNull(lines);
            if (maxMessageLength < 200) throw new ArgumentOutOfRangeException(nameof(maxMessageLength));

            var shown = lines.Take(Math.Max(0, limit)).Reverse().ToList();
            if (shown.Count == 0) return new[] { "No upcoming targets found." };

            var messages = new List<string>();
            var sb = new StringBuilder();

            foreach (var line in shown)
            {
                // Start a new message instead of going over Discord's limit.
                if (sb.Length > 0 && sb.Length + line.Length + 1 > maxMessageLength)
                {
                    messages.Add(sb.ToString());
                    sb.Clear();
                }
                sb.AppendLine(line);
            }

            if (sb.Length > 0) messages.Add(sb.ToString());
            return messages;
        }
    }
}