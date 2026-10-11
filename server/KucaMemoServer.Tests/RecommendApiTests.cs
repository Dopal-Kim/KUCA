using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KucaMemoServer.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace KucaMemoServer.Tests;

/// <summary>산책 추천 API (docs/api.md 의 "산책 추천") 검증. 실제 AI 는 부르지 않는다.</summary>
public class RecommendApiTests : IDisposable
{
    // 앱의 경희스팟 일부 (Data/buildings.json 에 있음)
    static readonly string[] Spots = ["way-473963422", "way-474085534", "way-474085535", "way-455725718", "way-474531805", "relation-8269760"];
    const double Lat = 37.2440, Lon = 127.0800;

    /// <summary>정해 둔 답을 돌려주는 가짜 AI</summary>
    sealed class FakeAi(string? answer) : IRouteAi
    {
        public bool IsConfigured => true;
        public string? LastPrompt { get; private set; }
        public Task<string?> CompleteAsync(string system, string user, CancellationToken ct)
        {
            LastPrompt = user;
            return Task.FromResult(answer);
        }
    }

    readonly List<IDisposable> owned = [];

    HttpClient Client(IRouteAi? ai = null)
    {
        var server = new TestServer();
        owned.Add(server);
        if (ai == null)
            return server.CreateClient();
        var withAi = server.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(ai)));
        owned.Add(withAi);
        return withAi.CreateClient();
    }

    public void Dispose()
    {
        foreach (IDisposable d in owned) d.Dispose();
    }

    static object Body(int minutes = 30, string[]? tastes = null, string[]? visited = null) => new
    {
        minutes, tastes = tastes ?? ["unvisited"], lat = Lat, lon = Lon,
        visitedBuildingIds = visited ?? Array.Empty<string>(), candidateBuildingIds = Spots,
    };

    static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task 키가_없으면_규칙으로_후보_안에서_추천한다()
    {
        JsonElement r = await Ok(await Client().PostAsJsonAsync("/api/recommendations", Body(30)));
        Assert.Equal("fallback", r.GetProperty("source").GetString());
        JsonElement[] stops = r.GetProperty("stops").EnumerateArray().ToArray();
        Assert.InRange(stops.Length, 1, 3);
        foreach (JsonElement s in stops)
        {
            Assert.Contains(s.GetProperty("buildingId").GetString(), Spots);
            Assert.True(s.GetProperty("walkMinutes").GetInt32() >= 1);
            Assert.NotEqual(0, s.GetProperty("lat").GetDouble());
            Assert.False(string.IsNullOrEmpty(s.GetProperty("name").GetString()));
        }
    }

    [Fact]
    public async Task 안_가_본_곳을_고르면_가_본_곳보다_먼저_나온다()
    {
        string[] visited = Spots[..5];
        JsonElement r = await Ok(await Client().PostAsJsonAsync("/api/recommendations", Body(15, visited: visited)));
        Assert.Equal("relation-8269760", r.GetProperty("stops")[0].GetProperty("buildingId").GetString());
    }

    [Theory]
    [InlineData(20, "unvisited")]
    [InlineData(30, "shopping")]
    public async Task 잘못된_값은_400(int minutes, string taste)
    {
        var res = await Client().PostAsJsonAsync("/api/recommendations", Body(minutes, [taste]));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AI_답을_순서대로_쓰고_후보에_없는_곳은_버린다()
    {
        var ai = new FakeAi("""
            코스를 짰어요!
            ```json
            {"title":"노을 산책","stops":[
              {"buildingId":"way-474531805","reason":"노을이 예뻐요"},
              {"buildingId":"way-999","reason":"없는 곳"},
              {"buildingId":"way-474085534","reason":"조용해요"}]}
            ```
            """);
        JsonElement r = await Ok(await Client(ai).PostAsJsonAsync("/api/recommendations", Body(30, ["photo"])));
        Assert.Equal("ai", r.GetProperty("source").GetString());
        Assert.Equal("노을 산책", r.GetProperty("title").GetString());
        string[] ids = r.GetProperty("stops").EnumerateArray().Select(s => s.GetProperty("buildingId").GetString()!).ToArray();
        Assert.Equal(["way-474531805", "way-474085534"], ids);
        Assert.Equal("노을이 예뻐요", r.GetProperty("stops")[0].GetProperty("reason").GetString());
        Assert.Contains("사진 찍기 좋은 명소", ai.LastPrompt);
        Assert.Contains("중앙도서관", ai.LastPrompt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("미안해요, 지금은 답할 수 없어요")]
    [InlineData("{\"stops\":[{\"buildingId\":\"way-999\"}]}")]
    public async Task AI_가_실패하거나_엉뚱하면_규칙_추천(string? answer)
    {
        JsonElement r = await Ok(await Client(new FakeAi(answer)).PostAsJsonAsync("/api/recommendations", Body(30)));
        Assert.Equal("fallback", r.GetProperty("source").GetString());
        Assert.True(r.GetProperty("stops").GetArrayLength() > 0);
    }
}
