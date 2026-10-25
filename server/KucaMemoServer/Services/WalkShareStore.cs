using Microsoft.Data.Sqlite;

namespace KucaMemoServer.Services;

/// <summary>
/// 카카오톡으로 공유한 산책 기록 (walk_shares). 공유 버튼을 눌렀을 때만 만든다.
/// 글은 서버가 숫자 · 장소 이름 · 계정 닉네임으로 만들고 (앱이 보낸 자유 글은 없음), 지도 그림은 사진 폴더에 둔다.
/// </summary>
public class WalkShareStore
{
    readonly string connectionString;

    public record Share(string Id, string UserId, string Nickname, string Kind, int Meters, int Minutes,
                        string Stops, string ImageUrl, DateTime CreatedAt);

    public WalkShareStore(IConfiguration config, IWebHostEnvironment env)
    {
        string path = config["Memos:DatabasePath"] ?? Path.Combine(env.ContentRootPath, "memos.db");
        connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS walk_shares (
                id         TEXT PRIMARY KEY,
                user_id    TEXT NOT NULL,
                nickname   TEXT NOT NULL,
                kind       TEXT NOT NULL,
                meters     INTEGER NOT NULL,
                minutes    INTEGER NOT NULL,
                stops      TEXT NOT NULL,
                image_url  TEXT NOT NULL,
                created_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS walk_shares_user ON walk_shares (user_id, created_at);
            """;
        cmd.ExecuteNonQuery();
    }

    SqliteConnection Open()
    {
        var conn = new SqliteConnection(connectionString);
        conn.Open();
        return conn;
    }

    public void Add(Share s)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO walk_shares (id, user_id, nickname, kind, meters, minutes, stops, image_url, created_at)
            VALUES ($id, $u, $n, $k, $m, $min, $s, $img, $t)
            """;
        cmd.Parameters.AddWithValue("$id", s.Id);
        cmd.Parameters.AddWithValue("$u", s.UserId);
        cmd.Parameters.AddWithValue("$n", s.Nickname);
        cmd.Parameters.AddWithValue("$k", s.Kind);
        cmd.Parameters.AddWithValue("$m", s.Meters);
        cmd.Parameters.AddWithValue("$min", s.Minutes);
        cmd.Parameters.AddWithValue("$s", s.Stops);
        cmd.Parameters.AddWithValue("$img", s.ImageUrl);
        cmd.Parameters.AddWithValue("$t", s.CreatedAt.Ticks);
        cmd.ExecuteNonQuery();
    }

    public Share? Find(string id)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, user_id, nickname, kind, meters, minutes, stops, image_url, created_at FROM walk_shares WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return null;
        return new Share(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetInt32(5),
                         r.GetString(6), r.GetString(7), new DateTime(r.GetInt64(8), DateTimeKind.Utc));
    }

    /// <summary>since 뒤에 이 계정이 만든 공유 수 (하루 상한)</summary>
    public int CountSince(string userId, DateTime since)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM walk_shares WHERE user_id = $u AND created_at >= $t";
        cmd.Parameters.AddWithValue("$u", userId);
        cmd.Parameters.AddWithValue("$t", since.Ticks);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    /// <summary>회원 탈퇴: 그 계정의 공유를 지우고 지도 그림 주소를 돌려준다 (사진 폴더에서 지우게)</summary>
    public List<string> Forget(string userId)
    {
        var images = new List<string>();
        using var conn = Open();
        using (var q = conn.CreateCommand())
        {
            q.CommandText = "SELECT image_url FROM walk_shares WHERE user_id = $u";
            q.Parameters.AddWithValue("$u", userId);
            using var r = q.ExecuteReader();
            while (r.Read()) images.Add(r.GetString(0));
        }
        using var d = conn.CreateCommand();
        d.CommandText = "DELETE FROM walk_shares WHERE user_id = $u";
        d.Parameters.AddWithValue("$u", userId);
        d.ExecuteNonQuery();
        return images;
    }
}
