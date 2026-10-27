using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using KucaMemoServer.Models;
using KucaMemoServer.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace KucaMemoServer.Tests;

/// <summary>
/// 말로 한 산책 바람 · 공지의 통제 / 행사 · 쪽지와 리뷰로 본 장소의 요즘 상태가 산책 추천에 반영되는지.
/// 실제 AI 는 부르지 않는다.
/// </summary>
public class WishAndPlaceApiTests : IDisposable
{
    // 경희스팟: 정문 · 중앙도서관(실내) · 학생회관(실내) · 공학관(실내) · 노천극장(바깥) · 체육대학관(바깥)
    const string Gate = "way-473963422", Library = "way-474085534", Union = "way-474085535", Engineering = "way-455725718",
                 Theater = "way-474531805", Pe = "way-455726113";
    static readonly string[] Spots = [Gate, Library, Union, Engineering, Theater, Pe];
    const double Lat = 37.2440, Lon = 127.0800;
    static readonly DateTimeOffset Today = new(2026, 11, 2, 14, 0, 0, TimeSpan.FromHours(9));

    /// <summary>시스템 문장으로 어떤 일인지 보고 정해 둔 답을 준다</summary>
    sealed class RoutingAi(string? wish = null, string? course = null, string? state = null) : IRouteAi
    {
        public bool IsConfigured => true;
        public List<string> Prompts { get; } = [];
        public Task<string?> CompleteAsync(string system, string user, CancellationToken ct)
        {
            lock (Prompts) Prompts.Add(user);
            return Task.FromResult(system.Contains("해석기") ? wish : system.Contains("요즘 모습") ? state : course);
        }
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    sealed class NoWeather : ICampusWeather
    {
        public Task<string?> NowAsync(CancellationToken ct) => Task.FromResult<string?>(null);
    }

    readonly List<IDisposable> owned = [];
    TestServer? last;

    WebApplicationFactory<Program> Server(IRouteAi? ai = null)
    {
        var server = new TestServer();
        last = server;
        owned.Add(server);
        var configured = server.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            if (ai != null)
                s.AddSingleton(ai);
            s.AddSingleton<ICampusWeather>(new NoWeather());
            s.AddSingleton<TimeProvider>(new FixedClock(Today));
        }));
        owned.Add(configured);
        return configured;
    }

    public void Dispose()
    {
        foreach (IDisposable d in owned) d.Dispose();
    }

    static object Body(string? wish, int minutes = 30, string[]? candidates = null) => new
    {
        minutes, tastes = Array.Empty<string>(), lat = Lat, lon = Lon, visitedBuildingIds = Array.Empty<string>(),
        candidateBuildingIds = candidates ?? Spots, wish,
    };

    static async Task<JsonElement> Recommend(HttpClient client, object body)
    {
        var res = await client.PostAsJsonAsync("/api/recommendations", body);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    static string[] StopIds(JsonElement r) => r.GetProperty("stops").EnumerateArray().Select(s => s.GetProperty("buildingId").GetString()!).ToArray();

    // ---------- 바람 해석 (말 찾기) ----------

    [Fact]
    public void 말_찾기는_시간_취향_바라는_것_피할_것을_뽑는다()
    {
        SpotStore spots = new(new TestEnv());
        WalkWish w = WishRules.Parse("그늘지고 조용한 데 20분, 오르막은 빼고 천천히", spots);
        Assert.Equal(15, w.Minutes);
        Assert.Contains("quiet", w.Tastes);
        Assert.Contains("그늘", w.Want);
        Assert.Contains("오르막", w.Avoid);
        Assert.Equal("slow", w.Pace);
        Assert.Equal("fallback", w.Source);
        Assert.Contains("오르막 피하기", w.Summary);
    }

    [Fact]
    public void 말한_장소는_넣거나_빼고_실내_바람도_안다()
    {
        SpotStore spots = new(new TestEnv());
        WalkWish w = WishRules.Parse("비 와서 실내로 한 시간, 도서관 말고 학생회관은 들르고 싶어", spots);
        Assert.Equal(60, w.Minutes);
        Assert.True(w.Indoor);
        Assert.Contains(Library, w.AvoidPlaces);
        Assert.Contains(Union, w.WantPlaces);
        Assert.DoesNotContain(Library, w.WantPlaces);
    }

    [Fact]
    public void AI_해석은_목록_밖_코드와_출입_제한_장소를_버린다()
    {
        SpotStore spots = new(new TestEnv());
        WalkWish? w = WishInterpreter.Parse(
            """{"minutes": 20, "tastes": ["quiet", "shopping"], "indoor": null, "afterMeal": true, "pace": "run", "want": ["그늘", "아주아주아주긴바라는모습"], "avoid": ["붐빔"], "wantPlaces": ["relation-8269760", "way-474531805"], "avoidPlaces": ["way-없음"]}""",
            "아무 말", spots);
        Assert.NotNull(w);
        Assert.Equal(15, w!.Minutes);
        Assert.Equal(["quiet"], w.Tastes);
        Assert.Null(w.Pace);
        Assert.True(w.AfterMeal);
        Assert.Equal(["그늘"], w.Want);
        Assert.Equal([Theater], w.WantPlaces);   // 우정원(기숙사)은 출입 제한이라 버림
        Assert.Empty(w.AvoidPlaces);
        Assert.Equal("ai", w.Source);
    }

    // ---------- 추천에 반영 ----------

    [Fact]
    public async Task AI_가_없어도_바람을_해석해_돌려주고_뺄_곳은_뺀다()
    {
        HttpClient client = Server().CreateClient();
        JsonElement r = await Recommend(client, Body("도서관 말고 노천극장 들러서 30분", minutes: 15));
        JsonElement wish = r.GetProperty("wish");
        Assert.Equal("fallback", wish.GetProperty("source").GetString());
        Assert.Equal(30, wish.GetProperty("minutes").GetInt32());
        Assert.Contains("노천극장 들르기", wish.GetProperty("summary").GetString());
        string[] ids = StopIds(r);
        Assert.DoesNotContain(Library, ids);
        Assert.Contains(Theater, ids);
        Assert.Equal("말한 곳이라 코스에 넣었어요",
            r.GetProperty("stops").EnumerateArray().First(s => s.GetProperty("buildingId").GetString() == Theater).GetProperty("reason").GetString());
    }

    [Fact]
    public async Task 실내_바람이면_실내_장소가_먼저다()
    {
        HttpClient client = Server().CreateClient();
        JsonElement r = await Recommend(client, Body("비 와서 실내만 15분", candidates: [Theater, Pe, Library]));
        Assert.Equal(Library, StopIds(r)[0]);
    }

    [Fact]
    public async Task 바람이_200자를_넘으면_400()
    {
        HttpClient client = Server().CreateClient();
        var res = await client.PostAsJsonAsync("/api/recommendations", Body(new string('가', 201)));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task AI_는_바람을_해석하고_원래_문장과_함께_코스를_고른다()
    {
        var ai = new RoutingAi(
            wish: """{"minutes": 15, "tastes": ["quiet"], "indoor": true, "afterMeal": false, "pace": null, "want": ["조용"], "avoid": [], "wantPlaces": [], "avoidPlaces": []}""",
            course: $$"""{"title": "조용한 실내 산책", "stops": [{"buildingId": "{{Library}}", "reason": "열람실이 모인 조용한 곳이라 좋아요"}]}""");
        HttpClient client = Server(ai).CreateClient();
        JsonElement r = await Recommend(client, Body("머리 좀 식히고 싶어"));
        Assert.Equal("ai", r.GetProperty("source").GetString());
        Assert.Equal("ai", r.GetProperty("wish").GetProperty("source").GetString());
        Assert.Equal(15, r.GetProperty("wish").GetProperty("minutes").GetInt32());
        Assert.Equal([Library], StopIds(r));
        string coursePrompt = ai.Prompts.Last(p => p.Contains("후보 장소"));
        Assert.Contains("<wish>머리 좀 식히고 싶어</wish>", coursePrompt);
        Assert.Contains("15분 · 조용한 곳 · 실내 위주 · 조용", coursePrompt);
    }

    [Fact]
    public async Task 같은_바람은_한_번만_해석한다()
    {
        var ai = new RoutingAi(wish: """{"minutes": 30, "tastes": [], "want": [], "avoid": []}""");
        HttpClient client = Server(ai).CreateClient();
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Recommend(client, Body("가볍게 걷고 싶어"))));
        Assert.Equal(1, ai.Prompts.Count(p => p.Contains("<wish>") && !p.Contains("후보 장소")));
    }

    // ---------- 판단 → 사실 비교 → 경로 (AI 우선) ----------

    static object Visits(string wish, params (string Id, int Count)[] counts) => new
    {
        minutes = 30, tastes = Array.Empty<string>(), lat = Lat, lon = Lon, visitedBuildingIds = Array.Empty<string>(),
        candidateBuildingIds = Spots, wish, visitCounts = counts.Select(c => new { buildingId = c.Id, count = c.Count }),
    };

    [Fact]
    public void 말_찾기도_판단_비중과_판단_문장을_만든다()
    {
        SpotStore spots = new(new TestEnv());
        WalkWish w = WishRules.Parse("안 가 본 새로운 곳, 경치 좋은 데로", spots);
        Assert.Equal(3, w.Priorities.Novelty);
        Assert.Equal(3, w.Priorities.Scenery);
        Assert.Equal(0, w.Priorities.Familiar);
        Assert.Contains("안 가 본 곳", w.Judgment);
        Assert.Contains("경관", w.Judgment);
    }

    [Fact]
    public void AI_판단은_비중을_0에서_3으로_자르고_금지어_판단은_버린다()
    {
        SpotStore spots = new(new TestEnv());
        WalkWish? w = WishInterpreter.Parse(
            """{"minutes": 30, "judgment": "기분 전환이 필요해 보여 안 가 본 곳 위주로 골라요", "priorities": {"novelty": 7, "familiar": -2, "scenery": 2}}""", "x", spots);
        Assert.Equal("기분 전환이 필요해 보여 안 가 본 곳 위주로 골라요", w!.Judgment);
        Assert.Equal((3, 0, 2, 1), (w.Priorities.Novelty, w.Priorities.Familiar, w.Priorities.Scenery, w.Priorities.Quiet));
        WalkWish? bad = WishInterpreter.Parse("""{"judgment": "시발 그냥 아무데나 가요"}""", "x", spots);
        Assert.DoesNotContain("시발", bad!.Judgment);
    }

    [Fact]
    public async Task 익숙한_곳을_원하면_자주_간_곳이_들어가고_이유와_코스_이유가_방문_횟수를_말한다()
    {
        HttpClient client = Server().CreateClient();
        JsonElement r = await Recommend(client, Visits("늘 가던 익숙한 곳으로 30분", (Theater, 5)));
        Assert.Contains(Theater, StopIds(r));
        string reason = r.GetProperty("stops").EnumerateArray().First(s => s.GetProperty("buildingId").GetString() == Theater).GetProperty("reason").GetString()!;
        Assert.Contains("자주 들렀던 곳", reason);
        Assert.Contains("자주 가던 곳", r.GetProperty("why").GetString());
        Assert.Contains("자주 가던 곳", r.GetProperty("wish").GetProperty("judgment").GetString());
    }

    [Fact]
    public async Task 새로운_곳을_원하면_자주_간_곳은_뒤로_밀린다()
    {
        HttpClient client = Server().CreateClient();
        JsonElement fresh = await Recommend(client, Visits("안 가 본 새로운 곳으로 15분", (Library, 9), (Union, 9)));
        Assert.DoesNotContain(Library, StopIds(fresh));
        Assert.DoesNotContain(Union, StopIds(fresh));
    }

    [Fact]
    public async Task AI_에는_점수_없이_후보_전체와_사실_판단_비중을_준다()
    {
        var ai = new RoutingAi(
            wish: """{"minutes": 30, "judgment": "경치 좋은 곳 위주로 골라요", "priorities": {"scenery": 3}}""",
            course: $$"""{"title": "노을 코스", "why": "노을 경관이 있는 노천극장을 넣었어요", "stops": [{"buildingId": "{{Theater}}", "reason": "저녁 노을에 계단에 앉아 쉬기 좋아요"}]}""");
        HttpClient client = Server(ai).CreateClient();
        JsonElement r = await Recommend(client, new { minutes = 30, tastes = Array.Empty<string>(), lat = Lat, lon = Lon, wish = "경치 좋은 데" });
        string prompt = ai.Prompts.Last(p => p.Contains("후보 장소"));
        Assert.True(prompt.Split('\n').Count(l => l.StartsWith("way-") || l.StartsWith("relation-")) > 10);   // 상위 10곳으로 자르지 않음
        Assert.DoesNotContain("점수", prompt);
        Assert.Contains("판단: 경치 좋은 곳 위주로 골라요", prompt);
        Assert.Contains("경관 3", prompt);
        Assert.Contains("안 가 봄", prompt);
        Assert.Equal("노을 경관이 있는 노천극장을 넣었어요", r.GetProperty("why").GetString());
    }

    [Fact]
    public async Task AI_코스_이유가_사실과_겹치지_않으면_비중으로_만든_이유를_쓴다()
    {
        var ai = new RoutingAi(
            wish: """{"minutes": 30, "judgment": "경치 좋은 곳 위주로 골라요", "priorities": {"scenery": 3}}""",
            course: $$"""{"title": "코스", "why": "분수 쇼와 불꽃놀이를 볼 수 있어요", "stops": [{"buildingId": "{{Theater}}", "reason": "저녁 노을에 계단에 앉아 쉬기 좋아요"}]}""");
        HttpClient client = Server(ai).CreateClient();
        JsonElement r = await Recommend(client, Body("경치 좋은 데"));
        Assert.Contains("경관 좋은 곳", r.GetProperty("why").GetString());
    }

    // ---------- 공지: 통제 · 행사 ----------

    [Fact]
    public void 공지_말_찾기는_통제와_기간을_뽑고_전화번호는_날짜로_안_본다()
    {
        NoticeClassification c = NoticeEvents.Fill(new NoticeClassification([], "", "fallback"),
            "체육대학관 보수공사로 출입 통제 안내", "11월 1일부터 11.5 까지 출입이 제한됩니다. 문의 031-201-2114");
        Assert.Equal(NoticeKinds.Closure, c.Kind);
        Assert.Equal($"{Today.Year}-11-01", c.StartsOn);
        Assert.Equal($"{Today.Year}-11-05", c.EndsOn);

        // "공사" 만으로는 통제가 아니다 (한국전력공사)
        Assert.NotEqual(NoticeKinds.Closure, NoticeEvents.Fill(new NoticeClassification([], "", "fallback"), "한국전력공사 채용 안내", "").Kind);
    }

    [Fact]
    public void AI_공지_날짜는_원문에_있을_때만_믿는다()
    {
        SpotStore spots = new(new TestEnv());
        string Answer(string starts) => JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = $$"""{"buildingIds": ["{{Pe}}"], "summary": "보수 공사", "kind": "closure", "startsOn": "{{starts}}", "endsOn": null}""" } } },
        });
        NoticeClassification? ok = AiNoticeClassifier.ParseResponse(Answer("2026-11-03"), spots, "11월 3일 체육대학관 출입 통제");
        Assert.Equal(("closure", "2026-11-03", "2026-11-03"), (ok!.Kind, ok.StartsOn, ok.EndsOn));
        NoticeClassification? made = AiNoticeClassifier.ParseResponse(Answer("2026-12-25"), spots, "체육대학관 출입 통제");
        Assert.Null(made!.StartsOn);
    }

    [Fact]
    public async Task 오늘_출입_통제인_곳은_추천에서_빠지고_행사_중인_곳은_이유에_나온다()
    {
        WebApplicationFactory<Program> server = Server();
        HttpClient client = server.CreateClient();
        var notices = server.Services.GetRequiredService<NoticeStore>();
        notices.Add("n1", "체육대학관 보수공사로 출입 통제", "11월 1일 ~ 11월 5일", "https://example.com/1", "공사", "2026-10-30", [Pe]);
        notices.AddEvent("n1", [Pe], NoticeKinds.Closure, "2026-11-01", "2026-11-05");
        notices.Add("n2", "노천극장 가을 버스킹", "11월 2일 저녁", "https://example.com/2", "버스킹", "2026-10-30", [Theater]);
        notices.AddEvent("n2", [Theater], NoticeKinds.Event, "2026-11-02", "2026-11-02");
        notices.Add("n3", "지난 공사", "10월 1일", "https://example.com/3", "끝남", "2026-09-20", [Library]);
        notices.AddEvent("n3", [Library], NoticeKinds.Closure, "2026-10-01", "2026-10-01");

        JsonElement r = await Recommend(client, Body(null, candidates: [Pe, Theater, Library]));
        string[] ids = StopIds(r);
        Assert.DoesNotContain(Pe, ids);
        Assert.Contains(Library, ids);   // 지난 통제는 상관없음
        Assert.Contains("버스킹", r.GetProperty("stops").EnumerateArray().First(s => s.GetProperty("buildingId").GetString() == Theater)
                                     .GetProperty("reason").GetString());
    }

    // ---------- 쪽지 · 리뷰 · 사진 → 요즘 상태 ----------

    [Fact]
    public void 요즘_상태_AI_답은_재료가_없는_장소와_근거_없는_요약을_버린다()
    {
        var posts = new Dictionary<string, List<string>> { [Theater] = ["계단에 단풍이 한창이에요", "[사진] 단풍 든 나무 아래 계단"] };
        Dictionary<string, PlaceState>? s = PlaceStateService.Parse(
            $$"""{"places": [{"id": "{{Theater}}", "summary": "단풍이 한창이에요", "good": ["단풍", "야경"], "bad": []}, {"id": "{{Library}}", "summary": "조용해요", "good": [], "bad": []}]}""", posts);
        Assert.Equal([Theater], s!.Keys);
        Assert.Equal("단풍이 한창이에요", s[Theater].Summary);
        Assert.Equal(["단풍"], s[Theater].Good);   // "야경" 은 재료 글에 없음

        Dictionary<string, PlaceState>? made = PlaceStateService.Parse(
            $$"""{"places": [{"id": "{{Theater}}", "summary": "분수 쇼가 멋있어요", "good": [], "bad": []}]}""", posts);
        Assert.Null(made![Theater].Summary);
    }

    [Fact]
    public void 말_찾기는_좋은_점과_불편한_점만_채운다()
    {
        PlaceState s = PlaceStateService.RuleState(["입구 공사 가림막 때문에 돌아가야 해요", "점심엔 사람 많아요", "단풍은 예뻐요"]);
        Assert.Null(s.Summary);
        Assert.Equal(["단풍"], s.Good);
        Assert.Equal(["공사 중", "붐빔"], s.Bad);
    }

    [Fact]
    public async Task 쪽지와_사진_설명이_요즘_상태가_되어_바람과_추천_이유에_쓰인다()
    {
        var ai = new RoutingAi(state: $$"""{"places": [{"id": "{{Theater}}", "summary": "계단에 단풍이 한창이에요", "good": ["단풍"], "bad": []}]}""");
        WebApplicationFactory<Program> server = Server(ai);
        HttpClient client = server.CreateClient();
        var memos = server.Services.GetRequiredService<MemoStore>();
        memos.Add(new Memo
        {
            Id = "m1", BuildingId = Theater, Author = "원준", Text = "노천극장 계단 단풍 진짜 예뻐요", DeviceId = "d",
            CreatedAt = Today.UtcDateTime.AddDays(-1), PhotoCaption = "단풍 든 나무 아래 계단",
        });
        await server.Services.GetRequiredService<PlaceStateService>().RefreshAsync(CancellationToken.None);
        Assert.Contains(ai.Prompts, p => p.Contains("[사진] 단풍 든 나무 아래 계단") && !p.Contains("원준"));   // 닉네임은 보내지 않는다

        // 바람 해석 · 코스는 AI 답이 없어(null) 규칙으로: 바람 "단풍" 이 요즘 상태에 있어 이유가 된다
        JsonElement r = await Recommend(client, Body("단풍 보러 30분", candidates: [Theater, Library, Pe]));
        JsonElement theater = r.GetProperty("stops").EnumerateArray().First(s => s.GetProperty("buildingId").GetString() == Theater);
        Assert.Contains("단풍", theater.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task AI_가_사진을_못_보면_사진_쪽지는_검토_대기다()
    {
        HttpClient client = Server().CreateClient();
        last!.Moderator.Next = null;   // 금지어 검사(대체)로만 판단 → 사진은 못 봄
        var form = new MultipartFormDataContent
        {
            { new StringContent("원준"), "author" }, { new StringContent("단풍 예뻐요"), "text" }, { new StringContent("d1"), "deviceId" },
        };
        var file = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0xFF, 0xD9]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "photo", "a.jpg");
        var res = await client.PostAsync($"/api/buildings/{Theater}/memos", form);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        Assert.Equal("pending", (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public void 내용_검사_AI_는_사진_설명을_함께_준다()
    {
        string body = JsonSerializer.Serialize(new
        {
            choices = new[] { new { message = new { content = """{"verdict": "allow", "reason": "none", "note": "", "caption": "단풍 든 나무 아래 계단"}""" } } },
        });
        Assert.Equal("단풍 든 나무 아래 계단", AiModerator.ParseResponse(body)!.Caption);
    }

    /// <summary>SpotStore 를 서버 없이 만들 때 (Data/spots.json 이 있는 서버 폴더)</summary>
    sealed class TestEnv : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = FindServerRoot();
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "KucaMemoServer";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string EnvironmentName { get; set; } = "Testing";

        static string FindServerRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "KucaMemoServer", "Data", "spots.json");
                if (File.Exists(candidate))
                    return Path.Combine(dir.FullName, "KucaMemoServer");
            }
            throw new DirectoryNotFoundException("KucaMemoServer/Data/spots.json");
        }
    }
}
