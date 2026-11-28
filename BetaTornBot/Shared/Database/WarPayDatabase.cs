using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace WarPay;

/// <summary>Opens the SQLite file and makes sure the tables exist.</summary>
internal static class WarPayDatabase
{
    // One row per saved war. The attack log is kept so results can be recalculated without the API.
    private const string Schema = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS wars (
    id            INTEGER PRIMARY KEY,
    faction_id    INTEGER NOT NULL,
    start_time    INTEGER NOT NULL,
    end_time      INTEGER NOT NULL,
    attack_count  INTEGER NOT NULL,
    -- 0 for wars saved before defender names were stored; those attack logs get fetched again once.
    has_defender_info INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS attacks (
    war_id              INTEGER NOT NULL REFERENCES wars(id) ON DELETE CASCADE,
    id                  INTEGER NOT NULL,
    started             INTEGER NOT NULL,
    ended               INTEGER NOT NULL,
    attacker_id         INTEGER,
    attacker_name       TEXT    NOT NULL,
    attacker_faction_id INTEGER,
    defender_id         INTEGER,
    result              TEXT    NOT NULL,
    respect_gain        REAL    NOT NULL,
    chain               INTEGER NOT NULL,
    is_ranked_war       INTEGER NOT NULL,
    defender_name       TEXT    NOT NULL DEFAULT '',
    PRIMARY KEY (war_id, id)
);

-- The settings a result was calculated with, so a saved result is only reused when they match.
CREATE TABLE IF NOT EXISTS war_results (
    war_id                  INTEGER PRIMARY KEY REFERENCES wars(id) ON DELETE CASCADE,
    outside_chain_threshold INTEGER NOT NULL,
    outside_hit_score       REAL    NOT NULL,
    bonus_hit_score         REAL    NOT NULL,
    assist_window_seconds   INTEGER NOT NULL,
    total_cache_value       INTEGER NOT NULL,
    total_score             INTEGER NOT NULL,
    calculated_at           INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS war_players (
    war_id        INTEGER NOT NULL REFERENCES wars(id) ON DELETE CASCADE,
    player_id     INTEGER NOT NULL,
    name          TEXT    NOT NULL,
    share_percent REAL    NOT NULL,
    score         INTEGER NOT NULL,
    unsplit_score INTEGER NOT NULL,
    war_hits      INTEGER NOT NULL,
    bonus_hits    INTEGER NOT NULL,
    outside_hits  INTEGER NOT NULL,
    shared_hits   INTEGER NOT NULL,
    assist_hits   INTEGER NOT NULL,
    payout        INTEGER NOT NULL,
    PRIMARY KEY (war_id, player_id)
);

CREATE INDEX IF NOT EXISTS idx_war_players_player ON war_players(player_id);
CREATE INDEX IF NOT EXISTS idx_wars_start ON wars(start_time);
";

    /// <summary>Opens a new connection (creating the file and tables if needed). The caller disposes it.</summary>
    public static SqliteConnection Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A database path is required.", nameof(path));

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            DefaultTimeout = 30,
        };

        var connection = new SqliteConnection(builder.ToString());
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = Schema;
            command.ExecuteNonQuery();

            // Database files created by an earlier version don't have the newer columns yet.
            EnsureColumn(connection, "wars", "has_defender_info", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "attacks", "defender_name", "TEXT NOT NULL DEFAULT ''");
            EnsureColumn(connection, "attacks", "defender_faction_id", "INTEGER");
            EnsureColumn(connection, "war_results", "hospital_bonus_score", "REAL NOT NULL DEFAULT 0");
            EnsureColumn(connection, "war_results", "assist_score_fraction", "REAL NOT NULL DEFAULT 0");
            EnsureColumn(connection, "war_players", "hospital_hits", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "war_results", "outside_chain_saving_hit_score", "REAL NOT NULL DEFAULT 3");
            EnsureColumn(connection, "war_results", "outside_chain_saving_seconds_left", "INTEGER NOT NULL DEFAULT 0");
            EnsureColumn(connection, "war_players", "chain_saving_hits", "INTEGER NOT NULL DEFAULT 0");

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    // Table and column names here are fixed strings from this file, never user input.
    private static void EnsureColumn(SqliteConnection connection, string table, string column, string definition)
    {
        using (var info = connection.CreateCommand())
        {
            info.CommandText = $"PRAGMA table_info({table})";
            using var reader = info.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        try
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            alter.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.Message.Contains("duplicate column", StringComparison.OrdinalIgnoreCase))
        {
            // Another process added it between our check and the ALTER. That's fine.
        }
    }
}
