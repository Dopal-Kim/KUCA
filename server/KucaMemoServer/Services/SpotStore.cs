using System.Text.Json;
using KucaMemoServer.Models;

namespace KucaMemoServer.Services;

/// <summary>
/// 경희스팟 25곳 (Data/spots.json). 공지를 연결할 장소 후보다.
/// 앱의 KyungHeeSpots 목록과 같아야 한다 — 한쪽을 바꾸면 다른 쪽도 같이 바꾼다 (docs/api.md).
/// </summary>
public class SpotStore
{
    readonly Dictionary<string, Spot> byId;

    public IReadOnlyList<Spot> All { get; }

    public SpotStore(IWebHostEnvironment env)
    {
        string path = Path.Combine(env.ContentRootPath, "Data", "spots.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        All = JsonSerializer.Deserialize<List<Spot>>(File.ReadAllText(path), options) ?? new List<Spot>();
        byId = All.ToDictionary(s => s.Id);
    }

    public Spot? Find(string id) => byId.GetValueOrDefault(id);
}
