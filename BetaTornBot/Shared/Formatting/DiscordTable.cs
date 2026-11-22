using System;
using System.Collections.Generic;
using System.Text;

namespace BetaTornBot.Shared;

internal enum Align { Left, Right }

internal sealed record TableColumn(string Header, Align Align);

/// <summary>
/// Builds aligned text tables inside Discord code blocks. A code block uses a fixed-width font, so the
/// columns line up, and Discord doesn't treat "_" or "*" in player names as formatting.
/// Every formatter in this folder is built on this.
/// </summary>
internal static class DiscordTable
{
    private const int MaxNameWidth = 16; // Torn names are at most 16 characters.

    /// <summary>
    /// Returns one or more messages (Discord caps a message at 2000 characters, so long tables are split
    /// and the header is repeated on each part).
    /// </summary>
    /// <param name="title">Bold heading above the first table.</param>
    /// <param name="columns">Column headers and alignment.</param>
    /// <param name="rows">One array of cell texts per row, in column order.</param>
    /// <param name="totalRow">Optional last row, shown under its own separator.</param>
    /// <param name="emptyText">What to say when there are no rows.</param>
    /// <param name="maxMessageLength">Maximum characters per message.</param>
    public static IReadOnlyList<string> Build(
        string title,
        IReadOnlyList<TableColumn> columns,
        IReadOnlyList<string[]> rows,
        string[]? totalRow,
        string emptyText,
        int maxMessageLength = 1900)
    {
        if (maxMessageLength < 200) throw new ArgumentOutOfRangeException(nameof(maxMessageLength));
        if (rows.Count == 0) return new[] { $"**{title}**\n{emptyText}" };

        var widths = new int[columns.Count];
        for (int c = 0; c < columns.Count; c++)
        {
            widths[c] = columns[c].Header.Length;
            foreach (var row in rows) widths[c] = Math.Max(widths[c], row[c].Length);
            if (totalRow is not null) widths[c] = Math.Max(widths[c], totalRow[c].Length);
        }

        string Line(IReadOnlyList<string> cells)
        {
            var line = new StringBuilder();
            for (int c = 0; c < columns.Count; c++)
            {
                if (c > 0) line.Append("  ");
                line.Append(columns[c].Align == Align.Right
                    ? cells[c].PadLeft(widths[c])
                    : cells[c].PadRight(widths[c]));
            }
            return line.ToString().TrimEnd();
        }

        var headers = new string[columns.Count];
        for (int c = 0; c < headers.Length; c++) headers[c] = columns[c].Header;

        string header = Line(headers);
        string separator = Line(Array.ConvertAll(widths, w => new string('-', w)));

        // The total goes last, under its own separator, as one unit so it is never split from its line.
        var lines = new List<string>(rows.Count + 1);
        foreach (var row in rows) lines.Add(Line(row));
        if (totalRow is not null) lines.Add(separator + "\n" + Line(totalRow));

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

    /// <summary>A player name that is safe inside a code block and fits the name column.</summary>
    public static string CleanName(string? name, long id)
    {
        if (string.IsNullOrWhiteSpace(name)) return $"#{id}";
        // Backticks would end the code block early.
        name = name.Replace('`', '\'').Replace('\n', ' ').Trim();
        return name.Length <= MaxNameWidth ? name : name[..MaxNameWidth];
    }
}
