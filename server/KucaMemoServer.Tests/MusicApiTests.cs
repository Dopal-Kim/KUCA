using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KucaMemoServer.Models;
using KucaMemoServer.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace KucaMemoServer.Tests;

/// <summary>산책 음악 추천 API (docs/api.md 의 "POST /api/music") 검증. 실제 AI 는 부르지 않는다.</summary>
public class MusicApiTests : IDisposable
{
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

    sealed class FakeWeather(string? condition) : ICampusWeather
    {
        public Task<string?> NowAsync(CancellationToken ct) => Task.FromResult(condition);
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>가을 오후 (2026-10-27 15:00 한국 시각)</summary>
    static readonly DateTimeOffset AutumnDay = new(2026, 10, 27, 15, 0, 0, TimeSpan.FromHours(9));

    readonly List<IDisposable> owned = [];

    HttpClient Client(IRouteAi? ai = null, string? weather = "clear", DateTimeOffset? now = null)
    {
        var server = new TestServer();
        owned.Add(server);
        WebApplicationFactory<Program> configured = server.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            if (ai != null)
                s.AddSingleton(ai);
            s.AddSingleton<ICampusWeather>(new FakeWeather(weather));
            s.AddSingleton<TimeProvider>(new FixedClock(now ?? AutumnDay));
        }));
        owned.Add(configured);
        return configured.CreateClient();
    }

    public void Dispose()
    {
        foreach (IDisposable d in owned) d.Dispose();
    }

    static async Task<JsonElement> Ok(HttpResponseMessage res)
    {
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    const string FiveSongs = """
        ```json
        {"mood":"calm","theme":"조용한 가을 산책길","tracks":[
          {"title":"밤편지","artist":"아이유","why":"잔잔해서 걷기 좋아요"},
          {"title":"모든 날, 모든 순간","artist":"폴킴","why":"가을 길과 어울려요"},
          {"title":"거리에서","artist":"성시경","why":"천천히 걷기 좋아요"},
          {"title":"Don't Know Why","artist":"Norah Jones","why":"조용한 길에 맞아요"},
          {"title":"바람이 분다","artist":"이소라","why":"바람 부는 날 좋아요"}]}
        ```
        """;

    [Fact]
    public async Task AI_가_고른_곡을_돌려준다()
    {
        var ai = new FakeAi(FiveSongs);
        JsonElement r = await Ok(await Client(ai).PostAsJsonAsync("/api/music",
            new { minutes = 30, tastes = new[] { "quiet" }, courseTitle = "가을 조용한 캠퍼스길", places = new[] { "중앙도서관" } }));

        Assert.Equal("ai", r.GetProperty("source").GetString());
        Assert.Equal("calm", r.GetProperty("mood").GetString());
        Assert.Equal(5, r.GetProperty("tracks").GetArrayLength());
        Assert.Equal("아이유", r.GetProperty("tracks")[0].GetProperty("artist").GetString());
        // 코스 · 취향 · 지금 상황이 AI 에게 간다
        Assert.Contains("가을 조용한 캠퍼스길", ai.LastPrompt);
        Assert.Contains("중앙도서관", ai.LastPrompt);
        Assert.Contains("조용한 곳", ai.LastPrompt);
        Assert.Contains("가을 낮", ai.LastPrompt);
    }

    [Fact]
    public async Task 곡_수를_정할_수_있다()
    {
        JsonElement r = await Ok(await Client(new FakeAi(FiveSongs)).PostAsJsonAsync("/api/music", new { count = 3 }));
        Assert.Equal(3, r.GetProperty("tracks").GetArrayLength());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("곡을 추천할 수 없어요")]
    [InlineData("""{"tracks":[{"title":"밤편지","artist":"아이유"}]}""")]   // 곡이 모자람
    public async Task AI_가_안_되면_분위기별_기본_목록(string? answer)
    {
        JsonElement r = await Ok(await Client(new FakeAi(answer)).PostAsJsonAsync("/api/music", new { tastes = new[] { "exercise" } }));
        Assert.Equal("fallback", r.GetProperty("source").GetString());
        Assert.Equal("energetic", r.GetProperty("mood").GetString());
        Assert.Equal(MusicRecommender.DefaultCount, r.GetProperty("tracks").GetArrayLength());
        Assert.All(r.GetProperty("tracks").EnumerateArray(), t =>
        {
            Assert.False(string.IsNullOrEmpty(t.GetProperty("title").GetString()));
            Assert.False(string.IsNullOrEmpty(t.GetProperty("artist").GetString()));
        });
    }

    [Fact]
    public async Task 키가_없어도_본문_없이_부르면_기본_목록()
    {
        var res = await Client().PostAsync("/api/music", JsonContent.Create(new { }));
        JsonElement r = await Ok(res);
        Assert.Equal("fallback", r.GetProperty("source").GetString());
        Assert.False(string.IsNullOrEmpty(r.GetProperty("theme").GetString()));
    }

    [Theory]
    [InlineData("rain", "day", new string[0], "normal", "rainy")]
    [InlineData("clear", "day", new[] { "exercise" }, "normal", "energetic")]
    [InlineData("clear", "night", new string[0], "normal", "night")]
    [InlineData("clear", "day", new[] { "quiet" }, "normal", "calm")]
    [InlineData("clear", "day", new string[0], "slow", "calm")]
    [InlineData(null, "morning", new[] { "photo" }, "normal", "bright")]
    public void 분위기_규칙(string? weather, string timeOfDay, string[] tastes, string pace, string mood) =>
        Assert.Equal(mood, MusicRecommender.MoodOf(tastes, pace, timeOfDay, weather));

    [Theory]
    [InlineData("""{"tastes":["dance"]}""")]
    [InlineData("""{"pace":"run"}""")]
    [InlineData("""{"count":20}""")]
    public async Task 잘못된_요청은_400(string body)
    {
        var res = await Client().PostAsync("/api/music", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public void 같은_가수는_한_곡만()
    {
        MusicResult? r = MusicRecommender.Parse("""
            {"tracks":[{"title":"너의 의미","artist":"아이유"},{"title":"가을 아침","artist":"IU"},
                       {"title":"밤편지","artist":"아이유 (IU)"},{"title":"거리에서","artist":"성시경"},{"title":"Yellow","artist":"Coldplay"}]}
            """, 3);
        Assert.NotNull(r);
        Assert.Equal(["아이유", "성시경", "Coldplay"], r!.Tracks.Select(t => t.Artist));
    }

    [Fact]
    public void 링크_곡명은_버린다() =>
        Assert.Null(MusicRecommender.Parse("""{"tracks":[{"title":"http://x","artist":"a"},{"title":"b","artist":"c"},{"title":"d","artist":"e"}]}""", 3));
}
