using System;
using System.Collections.Generic;

namespace BetaTornBot.Shared;

/// <summary>Shared rules about what an attack's result means. Used by every feature.</summary>
internal static class AttackResults
{
    // Results that do not count as a successful hit.
    private static readonly HashSet<string> FailedResults = new(StringComparer.OrdinalIgnoreCase)
    {
        "Lost", "Stalemate", "Assist", "Escape", "Timeout", "None", ""
    };

    public static bool IsSuccessful(Attack a) => !FailedResults.Contains(a.Result ?? "");

    /// <summary>An assist: helped someone else's attack and is not a hit itself.</summary>
    public static bool IsAssist(Attack a) =>
        string.Equals(a.Result, "Assist", StringComparison.OrdinalIgnoreCase);
}
