using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BetaTornBot.Modules.WarModule.WarCalcs.UpcomingTargets;

// ---- DTOs matching the Torn API v2 response: GET /v2/faction/{id}/members ----
// i really need to move this into shared
// also if youre reading this screw you!

public class FactionMembersResponse
{
    [JsonPropertyName("members")]
    public List<FactionMember> Members { get; set; } = new();

    [JsonPropertyName("error")]
    public TornError? Error { get; set; }
}

public class TornError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("error")]
    public string Message { get; set; } = "";
}

// GET /v2/faction/basic
public class FactionBasicResponse
{
    [JsonPropertyName("basic")]
    public FactionBasic? Basic { get; set; }

    [JsonPropertyName("error")]
    public TornError? Error { get; set; }
}

public class FactionBasic
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

// GET /v2/faction/wars
public class FactionWarsResponse
{
    [JsonPropertyName("wars")]
    public FactionWars? Wars { get; set; }

    [JsonPropertyName("error")]
    public TornError? Error { get; set; }
}

public class FactionWars
{
    [JsonPropertyName("ranked")]
    public RankedWar? Ranked { get; set; }
}

public class RankedWar
{
    [JsonPropertyName("war_id")]
    public long WarId { get; set; }

    [JsonPropertyName("start")]
    public long Start { get; set; }

    [JsonPropertyName("end")]
    public long? End { get; set; }

    [JsonPropertyName("factions")]
    public List<WarFaction> Factions { get; set; } = new();
}

public class WarFaction
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class FactionMember
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("status")]
    public MemberStatus Status { get; set; } = new();

    /// <summary>Link to the member's Torn profile.</summary>
    [JsonIgnore]
    public string ProfileUrl => $"https://www.torn.com/profiles.php?XID={Id}";

    /// <summary>Time left until the member is "Okay" again. Zero if already okay.</summary>
    [JsonIgnore]
    public TimeSpan TimeUntilOk
    {
        get
        {
            if (string.Equals(Status.State, "Okay", StringComparison.OrdinalIgnoreCase))
                return TimeSpan.Zero;

            if (Status.Until is null or <= 0)
                return TimeSpan.Zero;

            var remaining = DateTimeOffset.FromUnixTimeSeconds(Status.Until.Value) - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }
}

public class MemberStatus
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "";   // Okay, Hospital, Jail, Traveling, Abroad, ...

    [JsonPropertyName("until")]
    public long? Until { get; set; }          // unix timestamp (seconds), null when not applicable
}

// ---- Service ----

public class TornFactionService
{
    private readonly HttpClient _http;

    public TornFactionService(HttpClient http, string apiKey)
    {
        _http = http;
        _http.BaseAddress = new Uri("https://api.torn.com/");
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);
    }

    public async Task<List<FactionMember>> GetMembersAsync(long factionId, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"v2/faction/{factionId}/members", ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<FactionMembersResponse>(stream, cancellationToken: ct)
                   ?? throw new InvalidOperationException("Empty response from Torn API.");

        // Torn returns HTTP 200 with an "error" object for things like a bad key or rate limit
        if (data.Error is not null)
            throw new InvalidOperationException($"Torn API error {data.Error.Code}: {data.Error.Message}");

        return data.Members;
    }

    private async Task<T> GetAsync<T>(string url, Func<T, TornError?> getError, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var data = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: ct)
                   ?? throw new InvalidOperationException("Empty response from Torn API.");

        var error = getError(data);
        if (error is not null)
            throw new InvalidOperationException($"Torn API error {error.Code}: {error.Message}");

        return data;
    }

    /// <summary>Our faction id (the faction the API key belongs to).</summary>
    public async Task<long> GetOwnFactionIdAsync(CancellationToken ct = default)
    {
        var data = await GetAsync<FactionBasicResponse>("v2/faction/basic", r => r.Error, ct);
        return data.Basic?.Id ?? throw new InvalidOperationException("Could not read own faction id.");
    }

    /// <summary>
    /// The faction we're currently in a ranked war with, or null if there is no active ranked war.
    /// </summary>
    public async Task<long?> GetEnemyFactionIdAsync(CancellationToken ct = default)
    {
        var ownId = await GetOwnFactionIdAsync(ct);
        var data = await GetAsync<FactionWarsResponse>("v2/faction/wars", r => r.Error, ct);

        var war = data.Wars?.Ranked;
        if (war is null)
            return null;

        // An unfinished war has no end timestamp (null or 0)
        if (war.End is > 0)
            return null;

        return war.Factions.FirstOrDefault(f => f.Id != ownId)?.Id;
    }

    /// <summary>
    /// Finds the faction we're warring and returns "name: timeUntilOk" strings,
    /// currently-okay members first, then ascending by remaining time.
    /// </summary>
    public async Task<List<string>> GetEnemyMembersByTimeUntilOkAsHtmlAsync(CancellationToken ct = default)
    {
        var enemyId = await GetEnemyFactionIdAsync(ct)
                      ?? throw new InvalidOperationException("No active ranked war found.");
        return await GetEnemyMembersByTimeUntilOkAsHtmlAsync(enemyId, ct);
    }

    /// <summary>Same as above, for an explicit faction id.</summary>
    public async Task<List<string>> GetEnemyMembersByTimeUntilOkAsHtmlAsync(long enemyFactionId, CancellationToken ct = default)
    {
        var members = await GetMembersAsync(enemyFactionId, ct);

        return members
            .OrderBy(m => m.TimeUntilOk)          // zero (okay) sorts first automatically
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(m => $"<a href=\"{m.ProfileUrl}\">{m.Name}</a>[{m.Level}]{new string('\u2007', Math.Max(2, 26 - m.Name.Length))}{FormatTime(m.TimeUntilOk)}")
            .ToList();
    }

    public async Task<List<string>> GetEnemyMembersByTimeOkayAsDiscordMemberAsync(CancellationToken ct = default)
    {
        var enemyId = await GetEnemyFactionIdAsync(ct)
              ?? throw new InvalidOperationException("No active ranked war found.");
        return await GetEnemyMembersByTimeOkayAsDiscordMemberAsync(enemyId, ct);
    }

    public async Task<List<string>> GetEnemyMembersByTimeOkayAsDiscordMemberAsync(long enemyFactionId, CancellationToken ct = default)
    {
        var members = await GetMembersAsync(enemyFactionId, ct);

        return members
            .OrderBy(m => m.TimeUntilOk)          // zero (okay) sorts first automatically
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .Select(m => m.TimeUntilOk <= TimeSpan.Zero
            ? $"[{m.Name}]({m.ProfileUrl})[{m.Level}]: Okay"
            : $"[{m.Name}]({m.ProfileUrl})[{m.Level}]: <t:{DateTimeOffset.UtcNow.Add(m.TimeUntilOk).ToUnixTimeSeconds()}:R>")
            .ToList();
    }

    private static string FormatTime(TimeSpan t) =>
        t <= TimeSpan.Zero
            ? "Okay"
            : $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
}

