using Microsoft.Data.Sqlite;

namespace KucaMemoServer.Services;

/// <summary>
/// 내준 추천을 메모와 같은 SQLite 파일(Memos:DatabasePath)에 남긴다. 개인정보(계정·위치)는 저장하지 않는다.
/// V4 완료 판정(계획 거리의 80%)과 V6 집계(AI·규칙 추천 수락률)에 쓴다.
/// </summary>
public class RecommendationStore
{
    readonly string connectionString;

    public record Saved(string Id, string Source, int Minutes, string[] StopIds, int PlannedDistanceM, DateTimeOffset CreatedAt);

    public RecommendationStore(IConfiguration config, IWebHostEnvironment env)
    {
        string path = config["Memos:DatabasePath"] ?? Path.Combine(env.ContentRootPath, "memos.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS recommendations (
                id                 TEXT PRIMARY KEY,
                source             TEXT NOT NULL,
                minutes            INTEGER NOT NULL,
                stop_ids           TEXT NOT NULL,
                planned_distance_m INTEGER NOT NULL,
                created_at         INTEGER NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    SqliteConnection Open()
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }

    public void Add(Saved r)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO recommendations (id, source, minutes, stop_ids, planned_distance_m, created_at)
            VALUES ($id, $source, $minutes, $stops, $dist, $at)
            """;
        cmd.Parameters.AddWithValue("$id", r.Id);
        cmd.Parameters.AddWithValue("$source", r.Source);
        cmd.Parameters.AddWithValue("$minutes", r.Minutes);
        cmd.Parameters.AddWithValue("$stops", string.Join(',', r.StopIds));
        cmd.Parameters.AddWithValue("$dist", r.PlannedDistanceM);
        cmd.Parameters.AddWithValue("$at", r.CreatedAt.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    public Saved? Find(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, source, minutes, stop_ids, planned_distance_m, created_at FROM recommendations WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;
        string stops = reader.GetString(3);
        return new Saved(reader.GetString(0), reader.GetString(1), reader.GetInt32(2),
            stops.Length == 0 ? [] : stops.Split(','), reader.GetInt32(4),
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)));
    }
}
