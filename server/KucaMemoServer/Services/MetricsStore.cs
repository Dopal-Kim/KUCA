using Microsoft.Data.Sqlite;

namespace KucaMemoServer.Services;

/// <summary>
/// 지표 이벤트 (C-V6). 누가 했는지는 남기지 않고 "무슨 일 · 어떤 종류 · 언제" 만 센다 (탈퇴해도 지울 개인정보가 없음).
/// 관리자 집계(GET /api/admin/stats)가 이걸 읽어 AI 추천 vs 규칙 추천 수락률 · 완주율, 매칭 성사율을 낸다.
/// </summary>
public class MetricsStore
{
    // kind 목록 (source 는 kind 마다 뜻이 다름)
    public const string RecShown = "rec_shown";             // source: ai · fallback
    public const string WalkStarted = "walk_started";       // source: ai · fallback (추천 코스) · free (자유) · match
    public const string WalkFinished = "walk_finished";     // source: 위와 같음, value: 1 완료 · 0 미완료
    public const string Review = "review";                  // source: 위와 같음, value: 코스 별점
    public const string MatchRequest = "match_request";     // source: 15 · 30 · 60
    public const string MatchProposed = "match_proposed";
    public const string MatchMet = "match_met";             // 둘 다 수락
    public const string MatchWalking = "match_walking";     // 둘 다 도착
    public const string MatchEnded = "match_ended";         // source: endReason
    public const string MatchAnswer = "match_answer";       // source: 매칭 이유 ai · fallback, value: 1 둘 다 수락 · 0 거절/응답 없음
    public const string AiCall = "ai_call";                 // source: recommend · smalltalk · moderation · match_reason, value: 1 AI · 0 대체

    readonly string connectionString;
    readonly TimeProvider clock;

    public record Row(string Kind, string? Source, int? Value);

    public MetricsStore(IConfiguration config, IWebHostEnvironment env, TimeProvider clock)
    {
        this.clock = clock;
        string path = config["Memos:DatabasePath"] ?? Path.Combine(env.ContentRootPath, "memos.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS metric_events (
                kind       TEXT NOT NULL,
                source     TEXT,
                value      INTEGER,
                created_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS metric_events_at ON metric_events (created_at);
            """;
        cmd.ExecuteNonQuery();
    }

    SqliteConnection Open()
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }

    public void Record(string kind, string? source = null, int? value = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO metric_events (kind, source, value, created_at) VALUES ($k, $s, $v, $at)";
        cmd.Parameters.AddWithValue("$k", kind);
        cmd.Parameters.AddWithValue("$s", (object?)source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$at", clock.GetUtcNow().ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    public List<Row> Since(DateTimeOffset from)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT kind, source, value FROM metric_events WHERE created_at >= $from";
        cmd.Parameters.AddWithValue("$from", from.ToUnixTimeSeconds());
        using var r = cmd.ExecuteReader();
        var rows = new List<Row>();
        while (r.Read())
            rows.Add(new Row(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetInt32(2)));
        return rows;
    }
}
