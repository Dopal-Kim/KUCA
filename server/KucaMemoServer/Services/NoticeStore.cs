using KucaMemoServer.Models;
using Microsoft.Data.Sqlite;

namespace KucaMemoServer.Services;

/// <summary>
/// 장소 공지를 메모와 같은 SQLite 파일(Memos:DatabasePath)에 저장한다.
///   notices        공지 한 건 (원문 링크는 겹치지 않는다)
///   notice_places  공지 ↔ 경희스팟 연결 (한 공지에 최대 3곳)
///   notice_events  출입 통제 · 행사 공지가 해당하는 장소와 기간 (산책 추천이 피하거나 더 친다)
/// </summary>
public class NoticeStore
{
    readonly string connectionString;

    public NoticeStore(IConfiguration config, IWebHostEnvironment env)
    {
        string path = config["Memos:DatabasePath"] ?? Path.Combine(env.ContentRootPath, "memos.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        CreateTables();
    }

    SqliteConnection Open()
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }

    void CreateTables()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS notices (
                id         TEXT PRIMARY KEY,
                title      TEXT NOT NULL,
                body       TEXT NOT NULL,
                url        TEXT NOT NULL UNIQUE,
                summary    TEXT NOT NULL,
                posted_at  TEXT NOT NULL,
                created_at INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS notice_places (
                notice_id   TEXT NOT NULL,
                building_id TEXT NOT NULL,
                PRIMARY KEY (notice_id, building_id)
            );
            CREATE INDEX IF NOT EXISTS notice_places_building ON notice_places (building_id);
            CREATE TABLE IF NOT EXISTS notice_events (
                notice_id   TEXT NOT NULL,
                building_id TEXT NOT NULL,
                kind        TEXT NOT NULL,
                starts_on   TEXT,
                ends_on     TEXT,
                PRIMARY KEY (notice_id, building_id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public bool UrlExists(string url)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM notices WHERE url = $url";
        cmd.Parameters.AddWithValue("$url", url);
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>같은 제목의 공지 ID (여러 게시판에 함께 올라온 학교 전체 공지를 한 번만 넣으려고)</summary>
    public string? FindByTitle(string title)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM notices WHERE title = $title LIMIT 1";
        cmd.Parameters.AddWithValue("$title", title);
        return cmd.ExecuteScalar() as string;
    }

    /// <summary>이미 있는 공지에 장소를 더한다 (있던 연결은 그대로)</summary>
    public void AddPlaces(string id, IEnumerable<string> buildingIds)
    {
        using var conn = Open();
        foreach (string buildingId in buildingIds)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO notice_places (notice_id, building_id) VALUES ($id, $building)";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$building", buildingId);
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>공지와 장소 연결을 한 번에 저장한다.</summary>
    public void Add(string id, string title, string body, string url, string summary, string postedAt, IEnumerable<string> buildingIds)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO notices (id, title, body, url, summary, posted_at, created_at)
            VALUES ($id, $title, $body, $url, $summary, $posted, $created)
            """;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$title", title);
        cmd.Parameters.AddWithValue("$body", body);
        cmd.Parameters.AddWithValue("$url", url);
        cmd.Parameters.AddWithValue("$summary", summary);
        cmd.Parameters.AddWithValue("$posted", postedAt);
        cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.Ticks);
        cmd.ExecuteNonQuery();

        foreach (string buildingId in buildingIds)
        {
            cmd.Parameters.Clear();
            cmd.CommandText = "INSERT OR IGNORE INTO notice_places (notice_id, building_id) VALUES ($id, $building)";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$building", buildingId);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>
    /// 장소별 공지 줄, 최신순 (게시일 → 넣은 시각). buildingId 가 있으면 그 장소만.
    /// 건물 이름은 호출하는 쪽이 SpotStore 로 채운다.
    /// </summary>
    public List<(string Id, string BuildingId, string Title, string Summary, string Url, string PostedAt)> List(string? buildingId, int limit, string? postedSince = null)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT n.id, p.building_id, n.title, n.summary, n.url, n.posted_at
            FROM notices n JOIN notice_places p ON p.notice_id = n.id
            WHERE ($building IS NULL OR p.building_id = $building) AND ($since IS NULL OR n.posted_at >= $since)
            ORDER BY n.posted_at DESC, n.created_at DESC, p.building_id
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$building", (object?)buildingId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$since", (object?)postedSince ?? DBNull.Value);
        using var reader = cmd.ExecuteReader();
        var rows = new List<(string, string, string, string, string, string)>();
        while (reader.Read())
            rows.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
        return rows;
    }

    /// <summary>통제 · 행사 공지의 장소와 기간을 남긴다 (general 은 남기지 않음)</summary>
    public void AddEvent(string noticeId, IEnumerable<string> buildingIds, string kind, string? startsOn, string? endsOn)
    {
        if (kind == NoticeKinds.General)
            return;
        using var conn = Open();
        foreach (string buildingId in buildingIds)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT OR REPLACE INTO notice_events (notice_id, building_id, kind, starts_on, ends_on)
                VALUES ($id, $building, $kind, $starts, $ends)
                """;
            cmd.Parameters.AddWithValue("$id", noticeId);
            cmd.Parameters.AddWithValue("$building", buildingId);
            cmd.Parameters.AddWithValue("$kind", kind);
            cmd.Parameters.AddWithValue("$starts", (object?)startsOn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$ends", (object?)endsOn ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public record ActiveEvent(string BuildingId, string Kind, string Title, string Summary);

    /// <summary>
    /// 오늘(yyyy-MM-dd) 해당하는 통제 · 행사. 기간이 있으면 그 안일 때, 기간이 없으면 게시일부터 undatedDays 일 동안.
    /// </summary>
    public List<ActiveEvent> ActiveEvents(string today, int undatedDays = 14)
    {
        string since = DateOnly.ParseExact(today, "yyyy-MM-dd").AddDays(-undatedDays).ToString("yyyy-MM-dd");
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT e.building_id, e.kind, n.title, n.summary
            FROM notice_events e JOIN notices n ON n.id = e.notice_id
            WHERE (e.starts_on IS NOT NULL AND e.starts_on <= $today AND COALESCE(e.ends_on, e.starts_on) >= $today)
               OR (e.starts_on IS NULL AND n.posted_at >= $since AND n.posted_at <= $today)
            ORDER BY n.posted_at DESC
            """;
        cmd.Parameters.AddWithValue("$today", today);
        cmd.Parameters.AddWithValue("$since", since);
        using var reader = cmd.ExecuteReader();
        var rows = new List<ActiveEvent>();
        while (reader.Read())
            rows.Add(new ActiveEvent(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));
        return rows;
    }

    /// <summary>공지를 모든 장소에서 지운다. 없으면 false.</summary>
    public bool Delete(string id)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.CommandText = "DELETE FROM notice_places WHERE notice_id = $id";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DELETE FROM notice_events WHERE notice_id = $id";
        cmd.ExecuteNonQuery();
        cmd.CommandText = "DELETE FROM notices WHERE id = $id";
        bool deleted = cmd.ExecuteNonQuery() > 0;
        tx.Commit();
        return deleted;
    }
}
