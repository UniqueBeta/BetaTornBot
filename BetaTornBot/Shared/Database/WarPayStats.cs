using System;
using System.Collections.Generic;
using BetaTornBot.Modules.WarModule.WarCalcs.WarPay;
using Microsoft.Data.Sqlite;

namespace BetaTornBot.Shared;

/// <summary>What to rank players by in <see cref="WarPayStats.GetTopPlayers"/>.</summary>
public enum TopPlayerOrder
{
    Score,
    WarHits,
    TotalHits,
    AssistHits,
    Payout,
    AverageShare,
}

/// <summary>One player's totals across several saved wars.</summary>
public sealed record PlayerStats(
    long PlayerId,
    string Name,
    int WarsParticipated,
    int WarHits,
    int BonusHits,
    int OutsideHits,
    int AssistHits,
    long Score,
    long Payout,
    double AverageSharePercent);

/// <summary>A war that has been saved in the database.</summary>
public sealed record SavedWar(long WarId, long FactionId, long From, long To, int AttackCount);

/// <summary>Stats across the wars saved by <see cref="WarPayCalculator.CalculateWarPayAsync"/>.</summary>
public sealed class WarPayStats
{
    private readonly string _databasePath;

    public WarPayStats(string databasePath = WarPayCalculator.DefaultDatabasePath)
    {
        _databasePath = databasePath;
    }

    /// <summary>The most recently started saved wars, newest first.</summary>
    public IReadOnlyList<SavedWar> GetRecentWars(int count = 10, long? factionId = null)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));

        using var conn = WarPayDatabase.Open(_databasePath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, faction_id, start_time, end_time, attack_count
FROM wars
WHERE ($faction IS NULL OR faction_id = $faction)
ORDER BY start_time DESC
LIMIT $count";
        cmd.Parameters.AddWithValue("$faction", factionId.HasValue ? factionId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$count", count);

        var wars = new List<SavedWar>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            wars.Add(new SavedWar(
                reader.GetInt64(0),
                reader.GetInt64(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                (int)reader.GetInt64(4)));
        }
        return wars;
    }

    /// <summary>
    /// Ranks players over the last <paramref name="lastWars"/> saved wars, e.g. the top hitters
    /// of the last 10 wars.
    /// </summary>
    /// <param name="lastWars">How many of the most recent saved wars to include.</param>
    /// <param name="top">How many players to return.</param>
    /// <param name="orderBy">What to rank by.</param>
    /// <param name="factionId">Only include wars of this faction (null = all saved wars).</param>
    public IReadOnlyList<PlayerStats> GetTopPlayers(
        int lastWars = 10,
        int top = 10,
        TopPlayerOrder orderBy = TopPlayerOrder.Score,
        long? factionId = null)
    {
        if (lastWars < 1) throw new ArgumentOutOfRangeException(nameof(lastWars));
        if (top < 1) throw new ArgumentOutOfRangeException(nameof(top));

        // Fixed list of column names, never user text, so it is safe to put into the query.
        string orderColumn = orderBy switch
        {
            TopPlayerOrder.Score => "total_score",
            TopPlayerOrder.WarHits => "total_war_hits",
            TopPlayerOrder.TotalHits => "total_hits",
            TopPlayerOrder.AssistHits => "total_assists",
            TopPlayerOrder.Payout => "total_payout",
            TopPlayerOrder.AverageShare => "avg_share",
            _ => throw new ArgumentOutOfRangeException(nameof(orderBy)),
        };

        string sql = $@"
SELECT p.player_id,
       (SELECT p2.name
        FROM war_players p2
        JOIN wars w2 ON w2.id = p2.war_id
        WHERE p2.player_id = p.player_id
        ORDER BY w2.start_time DESC
        LIMIT 1)                                   AS name,
       COUNT(*)                                    AS wars_played,
       SUM(p.war_hits)                             AS total_war_hits,
       SUM(p.bonus_hits)                           AS total_bonus_hits,
       SUM(p.outside_hits)                         AS total_outside_hits,
       SUM(p.assist_hits)                          AS total_assists,
       SUM(p.score)                                AS total_score,
       SUM(p.payout)                               AS total_payout,
       AVG(p.share_percent)                        AS avg_share,
       SUM(p.war_hits + p.bonus_hits + p.outside_hits) AS total_hits
FROM war_players p
WHERE p.war_id IN (
    SELECT id FROM wars
    WHERE ($faction IS NULL OR faction_id = $faction)
    ORDER BY start_time DESC
    LIMIT $wars)
GROUP BY p.player_id
ORDER BY {orderColumn} DESC, p.player_id
LIMIT $top";

        using var conn = WarPayDatabase.Open(_databasePath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$faction", factionId.HasValue ? factionId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$wars", lastWars);
        cmd.Parameters.AddWithValue("$top", top);

        var players = new List<PlayerStats>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            players.Add(new PlayerStats(
                PlayerId: reader.GetInt64(0),
                Name: reader.IsDBNull(1) ? "" : reader.GetString(1),
                WarsParticipated: (int)reader.GetInt64(2),
                WarHits: (int)reader.GetInt64(3),
                BonusHits: (int)reader.GetInt64(4),
                OutsideHits: (int)reader.GetInt64(5),
                AssistHits: (int)reader.GetInt64(6),
                Score: reader.GetInt64(7),
                Payout: reader.GetInt64(8),
                AverageSharePercent: reader.GetDouble(9)));
        }
        return players;
    }
}
