using Microsoft.Data.Sqlite;

namespace KucaMemoServer.Services;

/// <summary>
/// 매칭에서 디스크에 남기는 것 (메모와 같은 SQLite 파일). 위치(건물) · 인상착의는 여기 저장하지 않는다.
///   match_records  누가 누구와 어디서 만났고 어떻게 끝났는지 (30일, 신고·차단·집계용)
///   blocks         차단 (탈퇴할 때까지)
///   reports        신고 (90일)
/// </summary>
public class MatchStore
{
    readonly string connectionString;

    public record MatchRecord(string Id, string UserA, string UserB, string? SpotId, DateTimeOffset CreatedAt, string? EndReason);

    public MatchStore(IConfiguration config, IWebHostEnvironment env)
    {
        string path = config["Memos:DatabasePath"] ?? Path.Combine(env.ContentRootPath, "memos.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        Exec("""
            CREATE TABLE IF NOT EXISTS match_records (
                id         TEXT PRIMARY KEY,
                user_a     TEXT NOT NULL,
                user_b     TEXT NOT NULL,
                spot_id    TEXT,
                created_at INTEGER NOT NULL,
                ended_at   INTEGER,
                end_reason TEXT
            );
            CREATE INDEX IF NOT EXISTS match_records_a ON match_records (user_a);
            CREATE INDEX IF NOT EXISTS match_records_b ON match_records (user_b);
            CREATE TABLE IF NOT EXISTS blocks (
                user_id    TEXT NOT NULL,
                blocked_id TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                PRIMARY KEY (user_id, blocked_id)
            );
            CREATE TABLE IF NOT EXISTS reports (
                id         TEXT PRIMARY KEY,
                match_id   TEXT NOT NULL,
                reporter   TEXT NOT NULL,
                target     TEXT NOT NULL,
                reason     TEXT NOT NULL,
                text       TEXT,
                created_at INTEGER NOT NULL
            );
            """);
    }

    SqliteConnection Open()
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }

    void Exec(string sql, params (string Name, object? Value)[] args)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void AddRecord(string id, string userA, string userB, DateTimeOffset at) =>
        Exec("INSERT INTO match_records (id, user_a, user_b, created_at) VALUES ($id, $a, $b, $at)",
             ("$id", id), ("$a", userA), ("$b", userB), ("$at", at.ToUnixTimeSeconds()));

    public void SetSpot(string id, string spotId) =>
        Exec("UPDATE match_records SET spot_id = $s WHERE id = $id", ("$id", id), ("$s", spotId));

    public void EndRecord(string id, string reason, DateTimeOffset at) =>
        Exec("UPDATE match_records SET end_reason = $r, ended_at = $at WHERE id = $id AND end_reason IS NULL",
             ("$id", id), ("$r", reason), ("$at", at.ToUnixTimeSeconds()));

    public MatchRecord? FindRecord(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, user_a, user_b, spot_id, created_at, end_reason FROM match_records WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        return r.Read()
            ? new MatchRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3),
                              DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(4)), r.IsDBNull(5) ? null : r.GetString(5))
            : null;
    }

    public void Block(string userId, string blockedId, DateTimeOffset at) =>
        Exec("INSERT OR IGNORE INTO blocks (user_id, blocked_id, created_at) VALUES ($u, $b, $at)",
             ("$u", userId), ("$b", blockedId), ("$at", at.ToUnixTimeSeconds()));

    /// <summary>둘 중 한쪽이라도 상대를 차단했으면 true</summary>
    public bool EitherBlocked(string a, string b)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM blocks WHERE (user_id = $a AND blocked_id = $b) OR (user_id = $b AND blocked_id = $a)";
        cmd.Parameters.AddWithValue("$a", a);
        cmd.Parameters.AddWithValue("$b", b);
        return cmd.ExecuteScalar() is not null;
    }

    public void AddReport(string matchId, string reporter, string target, string reason, string? text, DateTimeOffset at) =>
        Exec("INSERT INTO reports (id, match_id, reporter, target, reason, text, created_at) VALUES ($id, $m, $r, $t, $why, $text, $at)",
             ("$id", Guid.NewGuid().ToString()), ("$m", matchId), ("$r", reporter), ("$t", target),
             ("$why", reason), ("$text", text), ("$at", at.ToUnixTimeSeconds()));

    public int CountReports(string target)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM reports WHERE target = $t";
        cmd.Parameters.AddWithValue("$t", target);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>보관 기간이 지난 매칭 기록 · 신고를 지운다</summary>
    public void Purge(DateTimeOffset now) =>
        Exec("DELETE FROM match_records WHERE created_at < $m; DELETE FROM reports WHERE created_at < $r",
             ("$m", (now - MatchRules.RecordKeep).ToUnixTimeSeconds()), ("$r", (now - MatchRules.ReportKeep).ToUnixTimeSeconds()));

    /// <summary>탈퇴: 그 계정이 들어간 매칭 기록 · 차단 · 신고를 모두 지운다</summary>
    public void Forget(string userId) =>
        Exec("""
            DELETE FROM match_records WHERE user_a = $u OR user_b = $u;
            DELETE FROM blocks WHERE user_id = $u OR blocked_id = $u;
            DELETE FROM reports WHERE reporter = $u OR target = $u;
            """, ("$u", userId));
}
