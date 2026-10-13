using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using KucaMemoServer.Models;
using KucaMemoServer.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace KucaMemoServer.Tests;

/// <summary>장소 공지 API (docs/api.md 의 "GET /api/notices", "관리자 API › 공지 넣기")</summary>
public class NoticeApiTests : IDisposable
{
    const string Library = "way-474085534";     // 중앙도서관
    const string StudentHall = "way-474085535"; // 학생회관
    const string Engineering = "way-455725718"; // 공학관

    readonly TestServer server = new();
    readonly HttpClient client;

    public NoticeApiTests() => client = server.CreateClient();

    public void Dispose() => server.Dispose();

    Task<HttpResponseMessage> PostNoticeAsync(object body, string? adminKey = TestServer.AdminKey)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/notices")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        if (adminKey != null) req.Headers.Add("X-Admin-Key", adminKey);
        return client.SendAsync(req);
    }

    static object Notice(string title = "중간고사 기간 열람실 연장", string body = "시험 기간 동안 제1열람실을 밤 12시까지 엽니다.",
                         string url = "https://www.khu.ac.kr/notice/1", string? postedAt = "2026-10-08") =>
        new { title, body, url, postedAt };

    async Task<JsonElement[]> ListAsync(string query = "") =>
        (await client.GetFromJsonAsync<JsonElement[]>("/api/notices" + query))!;

    [Fact]
    public async Task 공지를_넣으면_AI_가_고른_장소와_요약으로_저장되고_앱이_받아_간다()
    {
        server.NoticeClassifier.Next = new NoticeClassification(new[] { Library }, "제1열람실이 밤 12시까지 열려요.", "ai");

        var res = await PostNoticeAsync(Notice());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<JsonElement>();
        string id = created.GetProperty("id").GetString()!;
        Assert.Equal(new[] { Library }, created.GetProperty("buildingIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("ai", created.GetProperty("source").GetString());

        var row = (await ListAsync()).Single();
        Assert.Equal(id, row.GetProperty("id").GetString());
        Assert.Equal(Library, row.GetProperty("buildingId").GetString());
        Assert.Equal("중앙도서관", row.GetProperty("buildingName").GetString());
        Assert.Equal("중간고사 기간 열람실 연장", row.GetProperty("title").GetString());
        Assert.Equal("제1열람실이 밤 12시까지 열려요.", row.GetProperty("summary").GetString());
        Assert.Equal("https://www.khu.ac.kr/notice/1", row.GetProperty("url").GetString());
        Assert.Equal("2026-10-08", row.GetProperty("postedAt").GetString());
        Assert.Equal(7, row.EnumerateObject().Count()); // 본문(body)은 앱에 보내지 않는다
    }

    [Fact]
    public async Task 여러_장소면_장소마다_한_줄이고_buildingId_로_거른다()
    {
        server.NoticeClassifier.Next = new NoticeClassification(new[] { Library, StudentHall }, "요약", "ai");
        await PostNoticeAsync(Notice());

        var all = await ListAsync();
        Assert.Equal(2, all.Length);
        Assert.Single(all.Select(r => r.GetProperty("id").GetString()).Distinct());

        var onlyHall = await ListAsync($"?buildingId={StudentHall}");
        Assert.Equal(StudentHall, onlyHall.Single().GetProperty("buildingId").GetString());
        Assert.Empty(await ListAsync($"?buildingId={Engineering}"));
    }

    [Fact]
    public async Task 관련_장소가_없으면_저장하지_않는다()
    {
        server.NoticeClassifier.Next = new NoticeClassification(Array.Empty<string>(), "장학금 안내", "ai");

        var res = await PostNoticeAsync(Notice(title: "2학기 장학금 신청 안내"));

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("saved").GetBoolean());
        Assert.Equal("no_place", body.GetProperty("reason").GetString());
        Assert.Empty(await ListAsync());
    }

    [Fact]
    public async Task 최신순이고_limit_로_자른다()
    {
        server.NoticeClassifier.Next = new NoticeClassification(new[] { Library }, "요약", "ai");
        await PostNoticeAsync(Notice(title: "옛 공지", url: "https://a/1", postedAt: "2026-09-01"));
        await PostNoticeAsync(Notice(title: "새 공지", url: "https://a/2", postedAt: "2026-10-10"));
        await PostNoticeAsync(Notice(title: "중간 공지", url: "https://a/3", postedAt: "2026-10-01"));

        Assert.Equal(new[] { "새 공지", "중간 공지", "옛 공지" }, (await ListAsync()).Select(r => r.GetProperty("title").GetString()));
        Assert.Equal(2, (await ListAsync("?limit=2")).Length);
        Assert.Single(await ListAsync("?limit=0"));
    }

    [Fact]
    public async Task 같은_원문_링크는_409_이고_AI_를_다시_부르지_않는다()
    {
        server.NoticeClassifier.Next = new NoticeClassification(new[] { Library }, "요약", "ai");
        await PostNoticeAsync(Notice());
        int calls = server.NoticeClassifier.Calls;

        var again = await PostNoticeAsync(Notice(title: "제목만 다름"));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(calls, server.NoticeClassifier.Calls);
    }

    [Theory]
    [InlineData("", "본문", "https://a/1", null, "title")]
    [InlineData("제목", "", "https://a/1", null, "body")]
    [InlineData("제목", "본문", "", null, "url")]
    [InlineData("제목", "본문", "www.khu.ac.kr", null, "url")]
    [InlineData("제목", "본문", "javascript:alert(1)", null, "url")]
    [InlineData("제목", "본문", "https://a/1", "10월 8일", "postedAt")]
    public async Task 규칙에_맞지_않으면_400과_필드_이름(string title, string body, string url, string? postedAt, string field)
    {
        var res = await PostNoticeAsync(new { title, body, url, postedAt });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Contains(field, (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    [Fact]
    public async Task 게시일이_없으면_오늘_한국_날짜()
    {
        server.NoticeClassifier.Next = new NoticeClassification(new[] { Library }, "요약", "ai");
        await PostNoticeAsync(new { title = "제목", body = "본문", url = "https://a/1" });

        string expected = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd");
        Assert.Equal(expected, (await ListAsync()).Single().GetProperty("postedAt").GetString());
    }

    [Fact]
    public async Task 관리자_키가_없으면_공지를_넣을_수_없다()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await PostNoticeAsync(Notice(), adminKey: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostNoticeAsync(Notice(), adminKey: "wrong")).StatusCode);
        Assert.Equal(0, server.NoticeClassifier.Calls);
    }

    [Fact]
    public async Task 공지를_지우면_모든_장소에서_사라진다()
    {
        server.NoticeClassifier.Next = new NoticeClassification(new[] { Library, StudentHall }, "요약", "ai");
        var res = await PostNoticeAsync(Notice());
        string id = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var del = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/notices/{id}");
        del.Headers.Add("X-Admin-Key", TestServer.AdminKey);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(del)).StatusCode);
        Assert.Empty(await ListAsync());

        var again = new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/notices/{id}");
        again.Headers.Add("X-Admin-Key", TestServer.AdminKey);
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(again)).StatusCode);
    }

    [Fact]
    public async Task AI_가_없을_때는_제목_본문의_경희스팟_이름으로_연결한다()
    {
        // FakeNoticeClassifier 의 기본값이 이름 찾기 (실제 서버에서 AI 키가 없거나 장애일 때와 같음)
        var res = await PostNoticeAsync(Notice(title: "공학관 엘리베이터 점검", body: "10월 15일 공학관 1호기 엘리베이터를 점검합니다."));

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("fallback", created.GetProperty("source").GetString());
        Assert.Equal(new[] { Engineering }, created.GetProperty("buildingIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal("10월 15일 공학관 1호기 엘리베이터를 점검합니다.", created.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task 공지는_서버를_다시_켜도_남는다()
    {
        server.NoticeClassifier.Next = new NoticeClassification(new[] { Library }, "요약", "ai");
        await PostNoticeAsync(Notice());

        using var second = server.WithWebHostBuilder(_ => { });
        var rows = await second.CreateClient().GetFromJsonAsync<JsonElement[]>("/api/notices");
        Assert.Single(rows!);
    }
}

/// <summary>AiNoticeClassifier 응답 해석과 이름 찾기 대체</summary>
public class NoticeClassifierTests
{
    static readonly SpotStore Spots = LoadSpots();

    // 서버 프로젝트의 Data/spots.json 을 그대로 쓴다
    static SpotStore LoadSpots() => new(new FakeEnv(FindServerDir()));

    sealed class FakeEnv(string root) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    static string ChatResponse(string content) =>
        new JsonObject
        {
            ["choices"] = new JsonArray { new JsonObject { ["message"] = new JsonObject { ["content"] = content } } },
        }.ToJsonString();

    [Fact]
    public void 경희스팟_목록은_25곳이고_모두_캠퍼스_건물_목록에_있다()
    {
        Assert.Equal(25, Spots.All.Count);
        var buildings = new BuildingStore(new FakeEnv(FindServerDir()));
        Assert.All(Spots.All, s => Assert.Equal(s.Name, buildings.Find(s.Id)?.Name));
    }

    static string FindServerDir()
    {
        string dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "KucaMemoServer", "Data", "spots.json")))
            dir = Path.GetDirectoryName(dir)!;
        return Path.Combine(dir, "KucaMemoServer");
    }

    [Fact]
    public void 응답에서_목록에_있는_장소만_최대_3곳_겹치지_않게_고른다()
    {
        string content = """
            ```json
            {"buildingIds": ["way-474085534", "way-999", "way-474085534", "way-474085535", "way-455725718", "way-455726113"], "summary": "  열람실 연장  "}
            ```
            """;
        var result = AiNoticeClassifier.ParseResponse(ChatResponse(content), Spots);

        Assert.NotNull(result);
        Assert.Equal(new[] { "way-474085534", "way-474085535", "way-455725718" }, result!.BuildingIds);
        Assert.Equal("열람실 연장", result.Summary);
        Assert.Equal("ai", result.Source);
    }

    [Fact]
    public void 빈_배열이면_관련_장소_없음()
    {
        var result = AiNoticeClassifier.ParseResponse(ChatResponse("""{"buildingIds": [], "summary": "장학금 안내"}"""), Spots);
        Assert.NotNull(result);
        Assert.Empty(result!.BuildingIds);
    }

    [Fact]
    public void 긴_요약은_100자로_자른다()
    {
        var result = AiNoticeClassifier.ParseResponse(ChatResponse($$"""{"buildingIds": [], "summary": "{{new string('가', 150)}}"}"""), Spots);
        Assert.Equal(100, result!.Summary.Length);
        Assert.EndsWith("…", result.Summary);
    }

    [Theory]
    [InlineData("모르겠어요")]
    [InlineData("""{"summary": "장소 없음"}""")]
    [InlineData("""{"buildingIds": ["way-474085534"], "summary": ""}""")]
    [InlineData("""{"buildingIds": "way-474085534", "summary": "x"}""")]
    public void 형식이_틀리면_null(string content) => Assert.Null(AiNoticeClassifier.ParseResponse(ChatResponse(content), Spots));

    [Theory]
    [InlineData("중앙도서관 휴관 안내", "", new[] { "way-474085534" })]
    [InlineData("도서관 휴관", "", new[] { "way-474085534" })]                                     // 짧은 이름
    [InlineData("예술디자인대학 도예관 공사", "", new[] { "way-474521123", "way-474085536" })]   // 긴 이름이 먼저
    [InlineData("제2기숙사 정전", "", new[] { "relation-6739964", "relation-6739958" })]
    [InlineData("2학기 장학금 신청", "학사 일정을 확인하세요.", new string[0])]
    public void 이름_찾기(string title, string body, string[] expected)
    {
        var result = KeywordNoticeClassifier.Classify(title, body, Spots.All);
        Assert.Equal(expected, result.BuildingIds);
        Assert.Equal("fallback", result.Source);
    }

    [Fact]
    public async Task 키가_없으면_AI_를_부르지_않고_이름_찾기()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var handler = new CountingHandler();
        var classifier = new AiNoticeClassifier(new HttpClient(handler), Spots, config, NullLogger<AiNoticeClassifier>.Instance);

        var result = await classifier.ClassifyAsync("공학관 점검", "본문");

        Assert.Equal(0, handler.Count);
        Assert.Equal("fallback", result.Source);
        Assert.Equal(new[] { "way-455725718" }, result.BuildingIds);
    }

    [Fact]
    public async Task 게이트웨이에_경희스팟_목록과_공지를_보낸다()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Recommend:ApiKey"] = "shared-key",
        }).Build();
        var handler = new CountingHandler(ChatResponse("""{"buildingIds": ["way-474085534"], "summary": "요약"}"""));
        var classifier = new AiNoticeClassifier(new HttpClient(handler), Spots, config, NullLogger<AiNoticeClassifier>.Instance);

        var result = await classifier.ClassifyAsync("열람실 연장", "제1열람실");

        Assert.Equal("ai", result.Source);
        Assert.Equal("shared-key", handler.LastRequest!.Headers.Authorization!.Parameter);
        var body = JsonNode.Parse(handler.LastBody!)!;
        Assert.Equal(AiModerator.DefaultModel, body["model"]!.GetValue<string>());
        Assert.Contains("way-474085534: 중앙도서관", body["messages"]![0]!["content"]!.GetValue<string>());
        Assert.Contains("<notice>", body["messages"]![1]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task 서버_오류면_이름_찾기로_대체()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:ApiKey"] = "k" }).Build();
        var handler = new CountingHandler(status: HttpStatusCode.BadGateway);
        var classifier = new AiNoticeClassifier(new HttpClient(handler), Spots, config, NullLogger<AiNoticeClassifier>.Instance);

        var result = await classifier.ClassifyAsync("학생회관 식당 운영 시간", "본문");

        Assert.Equal(1, handler.Count);
        Assert.Equal("fallback", result.Source);
        Assert.Equal(new[] { "way-474085535" }, result.BuildingIds);
    }

    sealed class CountingHandler(string? body = null, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public int Count;
        public HttpRequestMessage? LastRequest;
        public string? LastBody;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(body ?? "", Encoding.UTF8, "application/json") };
        }
    }
}
