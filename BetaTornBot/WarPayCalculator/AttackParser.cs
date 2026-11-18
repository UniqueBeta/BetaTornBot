using System.Text.Json;

namespace WarPay;

/// <summary>
/// Turns one JSON attack from the Torn API into an <see cref="Attack"/>.
/// Accepts both the v2 shape (nested attacker/defender) and the older v1 shape (flat fields).
/// If a field comes back empty or wrong, this is the file to adjust.
/// </summary>
internal static class AttackParser
{
    public static Attack Parse(JsonElement el)
    {
        var attacker = Obj(el, "attacker");
        var defender = Obj(el, "defender");

        string attackerName = Str(attacker, "name");
        if (attackerName.Length == 0) attackerName = Str(el, "attacker_name");

        long started = Int(el, "started") ?? Int(el, "timestamp_started") ?? 0;
        long ended = Int(el, "ended") ?? Int(el, "timestamp_ended") ?? started;

        return new Attack(
            Id: Int(el, "id") ?? 0,
            Started: started,
            Ended: ended,
            AttackerId: Int(attacker, "id") ?? Int(el, "attacker_id"),
            AttackerName: attackerName,
            AttackerFactionId: Int(Obj(attacker, "faction"), "id") ?? Int(el, "attacker_faction"),
            DefenderId: Int(defender, "id") ?? Int(el, "defender_id"),
            Result: Str(el, "result"),
            RespectGain: Dbl(el, "respect_gain") ?? Dbl(el, "respect") ?? 0,
            Chain: (int)(Int(el, "chain") ?? 0),
            IsRankedWar: IsRankedWar(el));
    }

    private static bool IsRankedWar(JsonElement el)
    {
        if (el.TryGetProperty("is_ranked_war", out var v2))
        {
            if (v2.ValueKind == JsonValueKind.True) return true;
            if (v2.ValueKind == JsonValueKind.False) return false;
        }
        if (el.TryGetProperty("ranked_war", out var v1) && v1.ValueKind == JsonValueKind.Number)
            return v1.GetInt32() != 0;
        return false;
    }

    private static JsonElement Obj(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) ? v : default;

    private static long? Int(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetInt64()
            : null;

    private static double? Dbl(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
            ? v.GetDouble()
            : null;

    private static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";
}
