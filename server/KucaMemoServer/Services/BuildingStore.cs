using System.Text.Json;
using KucaMemoServer.Models;

namespace KucaMemoServer.Services;

/// <summary>
/// Data/buildings.json 을 서버 시작 때 한 번 읽어 메모리에 들고 있는다.
/// 건물 목록은 바뀌지 않으므로 DB에 넣지 않는다.
/// </summary>
public class BuildingStore
{
    readonly Dictionary<string, Building> byId;

    public IReadOnlyList<Building> All { get; }

    public BuildingStore(IWebHostEnvironment env)
    {
        string path = Path.Combine(env.ContentRootPath, "Data", "buildings.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        All = JsonSerializer.Deserialize<List<Building>>(File.ReadAllText(path), options) ?? new List<Building>();
        byId = All.ToDictionary(b => b.Id);
    }

    public Building? Find(string id) => byId.GetValueOrDefault(id);
}
