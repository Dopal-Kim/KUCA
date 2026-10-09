using KucaMemoServer.Models;
using Microsoft.Data.Sqlite;

namespace KucaMemoServer.Services;

/// <summary>
/// 메모를 SQLite 파일(기본: memos.db)에 저장한다. 서버를 껐다 켜도 메모가 남는다.
/// 파일 위치는 설정 "Memos:DatabasePath" 로 바꿀 수 있다 (테스트에서 임시 파일을 쓸 때).
/// </summary>
public class MemoStore
{
    readonly string connectionString;

    public MemoStore(IConfiguration config, IWebHostEnvironment env)
    {
        string path = config["Memos:DatabasePath"] ?? Path.Combine(env.ContentRootPath, "memos.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        CreateTable();
    }

    void CreateTable()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        // created_at 은 UTC Ticks(정수)로 저장해 정렬과 "이전 메모" 비교를 정확하게 한다.
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS memos (
                id          TEXT PRIMARY KEY,
                building_id TEXT NOT NULL,
                author      TEXT NOT NULL,
                text        TEXT NOT NULL,
                photo_url   TEXT,
                device_id   TEXT NOT NULL,
                created_at  INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS memos_building_created ON memos (building_id, created_at DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    SqliteConnection Open()
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>건물의 메모를 최신순으로. before 가 있으면 그 시각보다 이전 것만.</summary>
    public List<Memo> List(string buildingId, int limit, DateTime? before)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, building_id, author, text, photo_url, device_id, created_at
            FROM memos
            WHERE building_id = $building AND ($before IS NULL OR created_at < $before)
            ORDER BY created_at DESC, id DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$building", buildingId);
        cmd.Parameters.AddWithValue("$before", before.HasValue ? before.Value.ToUniversalTime().Ticks : DBNull.Value);
        cmd.Parameters.AddWithValue("$limit", limit);
        return ReadAll(cmd);
    }

    public Memo? Find(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, building_id, author, text, photo_url, device_id, created_at
            FROM memos WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$id", id);
        return ReadAll(cmd).FirstOrDefault();
    }

    public void Add(Memo memo)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO memos (id, building_id, author, text, photo_url, device_id, created_at)
            VALUES ($id, $building, $author, $text, $photo, $device, $created)
            """;
        cmd.Parameters.AddWithValue("$id", memo.Id);
        cmd.Parameters.AddWithValue("$building", memo.BuildingId);
        cmd.Parameters.AddWithValue("$author", memo.Author);
        cmd.Parameters.AddWithValue("$text", memo.Text);
        cmd.Parameters.AddWithValue("$photo", (object?)memo.PhotoUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$device", memo.DeviceId);
        cmd.Parameters.AddWithValue("$created", memo.CreatedAt.ToUniversalTime().Ticks);
        cmd.ExecuteNonQuery();
    }

    public bool Delete(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM memos WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>메모가 있는 건물별 메모 수 (웹 페이지용)</summary>
    public Dictionary<string, int> CountByBuilding()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT building_id, COUNT(*) FROM memos GROUP BY building_id";
        using var reader = cmd.ExecuteReader();
        var counts = new Dictionary<string, int>();
        while (reader.Read())
            counts[reader.GetString(0)] = reader.GetInt32(1);
        return counts;
    }

    static List<Memo> ReadAll(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<Memo>();
        while (reader.Read())
        {
            list.Add(new Memo
            {
                Id = reader.GetString(0),
                BuildingId = reader.GetString(1),
                Author = reader.GetString(2),
                Text = reader.GetString(3),
                PhotoUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                DeviceId = reader.GetString(5),
                CreatedAt = new DateTime(reader.GetInt64(6), DateTimeKind.Utc),
            });
        }
        return list;
    }
}
