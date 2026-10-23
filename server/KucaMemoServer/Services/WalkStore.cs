using Microsoft.Data.Sqlite;

namespace KucaMemoServer.Services;

/// <summary>
/// 산책 · 경험치 · 리뷰를 메모와 같은 SQLite 파일에 저장한다 (탈퇴할 때까지).
///   walks       계정마다 한 줄. 매칭 산책은 두 사람이 같은 id 를 쓰고 user_id 로 나뉜다
///   xp_events   받은 경험치 (산책 · 리뷰마다)
///   reviews     코스 별점 · 한 줄 (상대 별점은 받지 않는다)
/// </summary>
public class WalkStore
{
    readonly string connectionString;

    public record Walk(
        string Id, string UserId, string Kind, string? RecommendationId, int? Minutes, int? PlannedDistanceM,
        string[] Stops, string? PartnerId, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, bool Completed, bool Reviewed);

    public WalkStore(IConfiguration config, IWebHostEnvironment env)
    {
        string path = config["Memos:DatabasePath"] ?? Path.Combine(env.ContentRootPath, "memos.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        Exec("""
            CREATE TABLE IF NOT EXISTS walks (
                id                 TEXT NOT NULL,
                user_id            TEXT NOT NULL,
                kind               TEXT NOT NULL,
                recommendation_id  TEXT,
                minutes            INTEGER,
                planned_distance_m INTEGER,
                stops              TEXT NOT NULL DEFAULT '',
                partner_id         TEXT,
                started_at         INTEGER NOT NULL,
                finished_at        INTEGER,
                distance_m         INTEGER,
                duration_sec       INTEGER,
                completed          INTEGER NOT NULL DEFAULT 0,
                reviewed           INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (id, user_id)
            );
            CREATE TABLE IF NOT EXISTS xp_events (
                user_id    TEXT NOT NULL,
                walk_id    TEXT,
                kind       TEXT NOT NULL,
                xp         INTEGER NOT NULL,
                created_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS xp_events_user ON xp_events (user_id);
            CREATE TABLE IF NOT EXISTS reviews (
                walk_id        TEXT NOT NULL,
                user_id        TEXT NOT NULL,
                course_rating  INTEGER NOT NULL,
                text           TEXT,
                created_at     INTEGER NOT NULL,
                PRIMARY KEY (walk_id, user_id)
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

    T Scalar<T>(string sql, params (string Name, object? Value)[] args)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in args)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        object? v = cmd.ExecuteScalar();
        return v is null or DBNull ? default! : (T)Convert.ChangeType(v, typeof(T));
    }

    public void Start(string id, string userId, string kind, string? recommendationId, int? minutes, int? plannedDistanceM,
                      IEnumerable<string> stops, string? partnerId, DateTimeOffset at) =>
        Exec("""
            INSERT OR IGNORE INTO walks (id, user_id, kind, recommendation_id, minutes, planned_distance_m, stops, partner_id, started_at)
            VALUES ($id, $u, $k, $r, $m, $p, $s, $partner, $at)
            """,
            ("$id", id), ("$u", userId), ("$k", kind), ("$r", recommendationId), ("$m", minutes), ("$p", plannedDistanceM),
            ("$s", string.Join(',', stops)), ("$partner", partnerId), ("$at", at.ToUnixTimeSeconds()));

    /// <summary>산책 하나 (이 계정의 줄). 다른 계정의 산책이면 OwnerExists 로 구분한다</summary>
    public Walk? Find(string id, string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, user_id, kind, recommendation_id, minutes, planned_distance_m, stops, partner_id,
                   started_at, finished_at, completed, reviewed
            FROM walks WHERE id = $id AND user_id = $u
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$u", userId);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return null;
        string stops = r.GetString(6);
        return new Walk(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3),
            r.IsDBNull(4) ? null : r.GetInt32(4), r.IsDBNull(5) ? null : r.GetInt32(5),
            stops.Length == 0 ? [] : stops.Split(','), r.IsDBNull(7) ? null : r.GetString(7),
            DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(8)),
            r.IsDBNull(9) ? null : DateTimeOffset.FromUnixTimeSeconds(r.GetInt64(9)), r.GetInt64(10) == 1, r.GetInt64(11) == 1);
    }

    public bool Exists(string id) => Scalar<long>("SELECT COUNT(*) FROM walks WHERE id = $id", ("$id", id)) > 0;

    public void Finish(string id, string userId, int distanceM, int durationSec, bool completed, DateTimeOffset at) =>
        Exec("""
            UPDATE walks SET finished_at = $at, distance_m = $d, duration_sec = $s, completed = $c
            WHERE id = $id AND user_id = $u AND finished_at IS NULL
            """,
            ("$id", id), ("$u", userId), ("$d", distanceM), ("$s", durationSec), ("$c", completed ? 1 : 0), ("$at", at.ToUnixTimeSeconds()));

    public void AddXp(string userId, string? walkId, string kind, int xp, DateTimeOffset at) =>
        Exec("INSERT INTO xp_events (user_id, walk_id, kind, xp, created_at) VALUES ($u, $w, $k, $x, $at)",
             ("$u", userId), ("$w", walkId), ("$k", kind), ("$x", xp), ("$at", at.ToUnixTimeSeconds()));

    public void AddReview(string walkId, string userId, int courseRating, string? text, DateTimeOffset at)
    {
        Exec("""
            INSERT INTO reviews (walk_id, user_id, course_rating, text, created_at)
            VALUES ($w, $u, $c, $t, $at);
            UPDATE walks SET reviewed = 1 WHERE id = $w AND user_id = $u;
            """,
            ("$w", walkId), ("$u", userId), ("$c", courseRating), ("$t", text), ("$at", at.ToUnixTimeSeconds()));
    }

    public int TotalXp(string userId) => Scalar<int>("SELECT COALESCE(SUM(xp), 0) FROM xp_events WHERE user_id = $u", ("$u", userId));

    public (int Walks, int DistanceM, int Together) Totals(string userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(SUM(completed), 0), COALESCE(SUM(distance_m), 0), COALESCE(SUM(CASE WHEN kind = 'match' AND completed = 1 THEN 1 ELSE 0 END), 0)
            FROM walks WHERE user_id = $u AND finished_at IS NOT NULL
            """;
        cmd.Parameters.AddWithValue("$u", userId);
        using var r = cmd.ExecuteReader();
        r.Read();
        return (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2));
    }

    /// <summary>탈퇴: 그 계정의 산책 · 경험치 · 리뷰를 지운다</summary>
    public void Forget(string userId) =>
        Exec("""
            DELETE FROM walks WHERE user_id = $u;
            DELETE FROM xp_events WHERE user_id = $u;
            DELETE FROM reviews WHERE user_id = $u;
            """, ("$u", userId));
}
