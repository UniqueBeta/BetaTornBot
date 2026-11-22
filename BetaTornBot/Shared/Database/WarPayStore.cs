using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using WarPay;

namespace BetaTornBot.Shared;

/// <summary>The settings a result was calculated with. A saved result is reused only if these match.</summary>
internal sealed record WarSettings(
    int OutsideChainThreshold,
    double OutsideHitScore,
    double BonusHitScore,
    int AssistWindowSeconds,
    long TotalCacheValue);

/// <summary>Saves and loads wars, attacks and results. All the write/read SQL lives here.</summary>
internal sealed class WarPayStore
{
    private readonly string _path;

    public WarPayStore(string databasePath)
    {
        _path = databasePath;
    }

    /// <summary>Returns the saved result for the war, or null if there is none or it used different settings.</summary>
    public WarPayResult? TryLoadResult(long warId, long factionId, WarSettings settings)
    {
        using var conn = WarPayDatabase.Open(_path);

        long from, to;
        int attackCount;
        WarSettings saved;
        long totalScore;

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT w.start_time, w.end_time, w.attack_count,
       r.outside_chain_threshold, r.outside_hit_score, r.bonus_hit_score,
       r.assist_window_seconds, r.total_cache_value, r.total_score
FROM wars w
JOIN war_results r ON r.war_id = w.id
WHERE w.id = $id AND w.faction_id = $faction";
            cmd.Parameters.AddWithValue("$id", warId);
            cmd.Parameters.AddWithValue("$faction", factionId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            from = reader.GetInt64(0);
            to = reader.GetInt64(1);
            attackCount = (int)reader.GetInt64(2);
            saved = new WarSettings(
                OutsideChainThreshold: (int)reader.GetInt64(3),
                OutsideHitScore: reader.GetDouble(4),
                BonusHitScore: reader.GetDouble(5),
                AssistWindowSeconds: (int)reader.GetInt64(6),
                TotalCacheValue: reader.GetInt64(7));
            totalScore = reader.GetInt64(8);
        }

        if (saved != settings) return null;

        var players = new List<PlayerWarPay>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT player_id, name, share_percent, score, unsplit_score,
       war_hits, bonus_hits, outside_hits, shared_hits, assist_hits, payout
FROM war_players
WHERE war_id = $id
ORDER BY share_percent DESC, player_id";
            cmd.Parameters.AddWithValue("$id", warId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                players.Add(new PlayerWarPay
                {
                    Id = reader.GetInt64(0),
                    Name = reader.GetString(1),
                    SharePercent = reader.GetDouble(2),
                    Score = reader.GetInt64(3),
                    UnsplitScore = reader.GetInt64(4),
                    WarHits = (int)reader.GetInt64(5),
                    BonusHits = (int)reader.GetInt64(6),
                    OutsideHits = (int)reader.GetInt64(7),
                    SharedHits = (int)reader.GetInt64(8),
                    AssistHits = (int)reader.GetInt64(9),
                    Payout = reader.GetInt64(10),
                });
            }
        }

        return new WarPayResult(players, totalScore)
        {
            FactionId = factionId,
            WarId = warId,
            From = from,
            To = to,
            AttackCount = attackCount,
        };
    }

    /// <summary>Returns the saved attack log for the war, or null if it hasn't been saved.</summary>
    public List<Attack>? TryLoadAttacks(long warId, long factionId)
    {
        using var conn = WarPayDatabase.Open(_path);

        using (var check = conn.CreateCommand())
        {
            // Wars saved before defender names were stored are treated as not saved, so they are fetched once more.
            check.CommandText = "SELECT has_defender_info FROM wars WHERE id = $id AND faction_id = $faction";
            check.Parameters.AddWithValue("$id", warId);
            check.Parameters.AddWithValue("$faction", factionId);
            if (check.ExecuteScalar() is not long hasDefenderInfo || hasDefenderInfo == 0) return null;
        }

        var attacks = new List<Attack>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, started, ended, attacker_id, attacker_name, attacker_faction_id,
       defender_id, result, respect_gain, chain, is_ranked_war, defender_name
FROM attacks
WHERE war_id = $id";
        cmd.Parameters.AddWithValue("$id", warId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            attacks.Add(new Attack(
                Id: reader.GetInt64(0),
                Started: reader.GetInt64(1),
                Ended: reader.GetInt64(2),
                AttackerId: ReadNullableLong(reader, 3),
                AttackerName: reader.GetString(4),
                AttackerFactionId: ReadNullableLong(reader, 5),
                DefenderId: ReadNullableLong(reader, 6),
                Result: reader.GetString(7),
                RespectGain: reader.GetDouble(8),
                Chain: (int)reader.GetInt64(9),
                IsRankedWar: reader.GetInt64(10) != 0,
                DefenderName: reader.GetString(11)));
        }
        return attacks;
    }

    /// <summary>Saves the war and its full attack log (replacing any earlier copy of the attacks).</summary>
    public void SaveAttacks(long warId, long factionId, long from, long to, IReadOnlyList<Attack> attacks)
    {
        using var conn = WarPayDatabase.Open(_path);
        using var tx = conn.BeginTransaction();

        // One database file per faction: a war id is the same for both sides of a war.
        using (var check = Command(conn, tx, "SELECT faction_id FROM wars WHERE id = $id"))
        {
            check.Parameters.AddWithValue("$id", warId);
            if (check.ExecuteScalar() is long existing && existing != factionId)
                throw new InvalidOperationException(
                    $"War {warId} is already saved for faction {existing}. Use one database file per faction.");
        }

        using (var cmd = Command(conn, tx, @"
INSERT INTO wars (id, faction_id, start_time, end_time, attack_count, has_defender_info)
VALUES ($id, $faction, $from, $to, $count, 1)
ON CONFLICT(id) DO UPDATE SET
    faction_id = excluded.faction_id,
    start_time = excluded.start_time,
    end_time = excluded.end_time,
    attack_count = excluded.attack_count,
    has_defender_info = 1"))
        {
            cmd.Parameters.AddWithValue("$id", warId);
            cmd.Parameters.AddWithValue("$faction", factionId);
            cmd.Parameters.AddWithValue("$from", from);
            cmd.Parameters.AddWithValue("$to", to);
            cmd.Parameters.AddWithValue("$count", attacks.Count);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = Command(conn, tx, "DELETE FROM attacks WHERE war_id = $id"))
        {
            cmd.Parameters.AddWithValue("$id", warId);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = Command(conn, tx, @"
INSERT OR REPLACE INTO attacks
    (war_id, id, started, ended, attacker_id, attacker_name, attacker_faction_id,
     defender_id, result, respect_gain, chain, is_ranked_war, defender_name)
VALUES
    ($war, $id, $started, $ended, $attacker, $attackerName, $attackerFaction,
     $defender, $result, $respect, $chain, $ranked, $defenderName)"))
        {
            var pWar = cmd.Parameters.Add("$war", SqliteType.Integer);
            var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
            var pStarted = cmd.Parameters.Add("$started", SqliteType.Integer);
            var pEnded = cmd.Parameters.Add("$ended", SqliteType.Integer);
            var pAttacker = cmd.Parameters.Add("$attacker", SqliteType.Integer);
            var pAttackerName = cmd.Parameters.Add("$attackerName", SqliteType.Text);
            var pAttackerFaction = cmd.Parameters.Add("$attackerFaction", SqliteType.Integer);
            var pDefender = cmd.Parameters.Add("$defender", SqliteType.Integer);
            var pResult = cmd.Parameters.Add("$result", SqliteType.Text);
            var pRespect = cmd.Parameters.Add("$respect", SqliteType.Real);
            var pChain = cmd.Parameters.Add("$chain", SqliteType.Integer);
            var pRanked = cmd.Parameters.Add("$ranked", SqliteType.Integer);
            var pDefenderName = cmd.Parameters.Add("$defenderName", SqliteType.Text);

            foreach (var a in attacks)
            {
                pWar.Value = warId;
                pId.Value = a.Id;
                pStarted.Value = a.Started;
                pEnded.Value = a.Ended;
                pAttacker.Value = ToDb(a.AttackerId);
                pAttackerName.Value = a.AttackerName ?? "";
                pAttackerFaction.Value = ToDb(a.AttackerFactionId);
                pDefender.Value = ToDb(a.DefenderId);
                pResult.Value = a.Result ?? "";
                pRespect.Value = a.RespectGain;
                pChain.Value = a.Chain;
                pRanked.Value = a.IsRankedWar ? 1 : 0;
                pDefenderName.Value = a.DefenderName ?? "";
                cmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    /// <summary>Saves (or replaces) the calculated result for a war. The war must already be saved.</summary>
    public void SaveResult(long warId, WarSettings settings, WarPayResult result)
    {
        using var conn = WarPayDatabase.Open(_path);
        using var tx = conn.BeginTransaction();

        using (var cmd = Command(conn, tx, "DELETE FROM war_players WHERE war_id = $id"))
        {
            cmd.Parameters.AddWithValue("$id", warId);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = Command(conn, tx, @"
INSERT OR REPLACE INTO war_results
    (war_id, outside_chain_threshold, outside_hit_score, bonus_hit_score,
     assist_window_seconds, total_cache_value, total_score, calculated_at)
VALUES
    ($id, $threshold, $outside, $bonus, $assistWindow, $cache, $total, $now)"))
        {
            cmd.Parameters.AddWithValue("$id", warId);
            cmd.Parameters.AddWithValue("$threshold", settings.OutsideChainThreshold);
            cmd.Parameters.AddWithValue("$outside", settings.OutsideHitScore);
            cmd.Parameters.AddWithValue("$bonus", settings.BonusHitScore);
            cmd.Parameters.AddWithValue("$assistWindow", settings.AssistWindowSeconds);
            cmd.Parameters.AddWithValue("$cache", settings.TotalCacheValue);
            cmd.Parameters.AddWithValue("$total", result.TotalScore);
            cmd.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            cmd.ExecuteNonQuery();
        }

        using (var cmd = Command(conn, tx, @"
INSERT INTO war_players
    (war_id, player_id, name, share_percent, score, unsplit_score,
     war_hits, bonus_hits, outside_hits, shared_hits, assist_hits, payout)
VALUES
    ($war, $player, $name, $share, $score, $unsplit,
     $warHits, $bonusHits, $outsideHits, $sharedHits, $assistHits, $payout)"))
        {
            var pWar = cmd.Parameters.Add("$war", SqliteType.Integer);
            var pPlayer = cmd.Parameters.Add("$player", SqliteType.Integer);
            var pName = cmd.Parameters.Add("$name", SqliteType.Text);
            var pShare = cmd.Parameters.Add("$share", SqliteType.Real);
            var pScore = cmd.Parameters.Add("$score", SqliteType.Integer);
            var pUnsplit = cmd.Parameters.Add("$unsplit", SqliteType.Integer);
            var pWarHits = cmd.Parameters.Add("$warHits", SqliteType.Integer);
            var pBonusHits = cmd.Parameters.Add("$bonusHits", SqliteType.Integer);
            var pOutsideHits = cmd.Parameters.Add("$outsideHits", SqliteType.Integer);
            var pSharedHits = cmd.Parameters.Add("$sharedHits", SqliteType.Integer);
            var pAssistHits = cmd.Parameters.Add("$assistHits", SqliteType.Integer);
            var pPayout = cmd.Parameters.Add("$payout", SqliteType.Integer);

            foreach (var p in result.Players)
            {
                pWar.Value = warId;
                pPlayer.Value = p.Id;
                pName.Value = p.Name ?? "";
                pShare.Value = p.SharePercent;
                pScore.Value = p.Score;
                pUnsplit.Value = p.UnsplitScore;
                pWarHits.Value = p.WarHits;
                pBonusHits.Value = p.BonusHits;
                pOutsideHits.Value = p.OutsideHits;
                pSharedHits.Value = p.SharedHits;
                pAssistHits.Value = p.AssistHits;
                pPayout.Value = p.Payout;
                cmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }

    private static SqliteCommand Command(SqliteConnection conn, SqliteTransaction tx, string sql)
    {
        var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd;
    }

    private static object ToDb(long? value) => value.HasValue ? value.Value : DBNull.Value;

    private static long? ReadNullableLong(SqliteDataReader reader, int index) =>
        reader.IsDBNull(index) ? null : reader.GetInt64(index);
}
