using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WarPay;

internal readonly record struct WarWindow(long? WarId, long From, long To);

/// <summary>All communication with the Torn API lives here.</summary>
internal sealed class TornApiClient
{
    private const string BaseUrl = "https://api.torn.com/";

    private readonly HttpClient _http;
    private readonly string _key;

    public TornApiClient(HttpClient http, string apiKey)
    {
        _http = http;
        _key = apiKey;
    }

    /// <summary>GET v2/faction/basic: the faction the API key belongs to.</summary>
    public async Task<long> GetFactionIdAsync(CancellationToken ct)
    {
        var json = await GetAsync("v2/faction/basic", ct);
        try
        {
            return json.GetProperty("basic").GetProperty("id").GetInt64();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            throw new TornApiException("Unexpected response from faction/basic (no basic.id).", ex);
        }
    }

    /// <summary>
    /// GET v2/faction/rankedwars: start/end of the given war, or of the latest finished one when
    /// <paramref name="warId"/> is null.
    /// </summary>
    public async Task<WarWindow> GetWarWindowAsync(long? warId, CancellationToken ct)
    {
        var json = await GetAsync("v2/faction/rankedwars", ct);

        List<JsonElement> wars;
        try
        {
            wars = json.GetProperty("rankedwars").EnumerateArray().ToList();
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
        {
            throw new TornApiException("Unexpected response from faction/rankedwars (no rankedwars array).", ex);
        }

        JsonElement war;
        if (warId is long id)
        {
            war = wars.FirstOrDefault(w => ReadLong(w, "id") == id);
            if (war.ValueKind != JsonValueKind.Object)
                throw new TornApiException($"Ranked war {id} was not found for this faction.");
        }
        else
        {
            var finished = wars
                .Where(w => ReadLong(w, "end") is > 0)
                .OrderByDescending(w => ReadLong(w, "start") ?? 0)
                .ToList();
            if (finished.Count == 0)
                throw new TornApiException("No finished ranked wars found for this faction.");
            war = finished[0];
        }

        long start = ReadLong(war, "start") ?? throw new TornApiException("Ranked war has no start time.");
        long end = ReadLong(war, "end") is > 0 and long e ? e : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return new WarWindow(ReadLong(war, "id"), start, end);
    }

    /// <summary>GET v2/faction/attacks for the window, following the paging links until done.</summary>
    public async Task<List<Attack>> GetAttacksAsync(long from, long to, CancellationToken ct)
    {
        var attacks = new Dictionary<long, Attack>();
        string? next = $"v2/faction/attacks?limit=100&sort=asc&from={from}&to={to}";

        while (next is not null)
        {
            var page = await GetAsync(next, ct);
            int before = attacks.Count;

            if (!page.TryGetProperty("attacks", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new TornApiException("Unexpected response from faction/attacks (no attacks array).");

            foreach (var item in items.EnumerateArray())
            {
                var attack = AttackParser.Parse(item);
                attacks[attack.Id] = attack;
            }

            // Stop when there is no next link, or when a page added nothing new (avoids endless loops).
            next = null;
            if (attacks.Count > before &&
                page.TryGetProperty("_metadata", out var meta) &&
                meta.TryGetProperty("links", out var links) &&
                links.TryGetProperty("next", out var link) &&
                link.ValueKind == JsonValueKind.String)
            {
                next = ToRelative(link.GetString());
            }
        }

        return attacks.Values.ToList();
    }

    private async Task<JsonElement> GetAsync(string pathAndQuery, CancellationToken ct)
    {
        string sep = pathAndQuery.Contains('?') ? "&" : "?";
        string url = $"{BaseUrl}{pathAndQuery}{sep}key={Uri.EscapeDataString(_key)}";

        using var response = await _http.GetAsync(url, ct);
        string body = await response.Content.ReadAsStringAsync(ct);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            throw new TornApiException($"Torn API returned non-JSON (HTTP {(int)response.StatusCode}).", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            // Torn often reports errors with HTTP 200, so check the body first.
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("error", out var error))
                throw new TornApiException($"Torn API error: {error}");
            if (!response.IsSuccessStatusCode)
                throw new TornApiException($"Torn API returned HTTP {(int)response.StatusCode}.");
            return root.Clone();
        }
    }

    // The "next" link can be absolute and may carry a key; turn it into a path+query without the key.
    private static string? ToRelative(string? link)
    {
        if (string.IsNullOrEmpty(link)) return null;

        string path = Uri.TryCreate(link, UriKind.Absolute, out var uri)
            ? uri.PathAndQuery
            : link;
        path = path.TrimStart('/');
        path = Regex.Replace(path, @"([?&])key=[^&]*&?", "$1").TrimEnd('?', '&');
        return path;
    }

    private static long? ReadLong(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(name, out var v) &&
        v.ValueKind == JsonValueKind.Number
            ? v.GetInt64()
            : null;
}
