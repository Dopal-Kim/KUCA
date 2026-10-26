using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KucaMemoServer.Services;

namespace KucaMemoServer.Tests;

/// <summary>단과대 · 학과 · 도서관 공지 게시판에서 공지 모으기 (NoticeCollector). 게시판 HTML 은 실제 구조를 줄여 흉내 낸 것</summary>
public class NoticeCollectorTests : IDisposable
{
    const string EngList = "https://eng.khu.ac.kr/eng_kor/user/bbs/BMSR00040/list.do?menuNo=17100020";
    const string EngView = "https://eng.khu.ac.kr/eng_kor/user/bbs/BMSR00040/view.do?menuNo=17100020&boardId=";
    const string ForeignList = "https://foreign.khu.ac.kr/foreign_kor/user/bbs/BMSR00040/list.do?menuNo=16500028";
    const string LibraryList = "https://lib.khu.ac.kr/bbs/list/1";
    const string SportsList = "http://sports.khu.ac.kr/bbs/board.php?bo_table=05_01";
    const string Engineering = "way-455725718", EngineeringLab = "way-455728922", ForeignHall = "way-585696507";

    readonly TestServer server = new();
    readonly HttpClient client;

    public NoticeCollectorTests() => client = server.CreateClient();

    public void Dispose() => server.Dispose();

    static string Today(int daysAgo = 0) => DateTime.UtcNow.AddHours(9).AddDays(-daysAgo).ToString("yyyy-MM-dd");

    /// <summary>경희대 홈페이지 시스템 목록: 위쪽은 학교 전체 공지(다른 사이트 링크), 아래쪽이 이 게시판 글</summary>
    static string KhuList(params (string Id, string Tag, string Title, string Date)[] rows) =>
        """
        <table><tbody id="noticeTbody"><tr><td>대학</td><td class="tal">
          <a href="https://www.khu.ac.kr/kor/user/bbs/BMSR00040/view.do?menuNo=200316&boardId=1">학교 전체 공지</a></td><td>
        """ + Today() + "</td></tr></tbody></table><table><tbody>" +
        string.Concat(rows.Select(r => $"""
            <tr><td>1</td><td>일반</td><td class="tal"><a href="javascript:view('{r.Id}');">
              <span class="txtBox01 common">{r.Tag}</span> {r.Title}</a></td><td class="file"></td><td>{r.Date}</td></tr>
            """)) + "</tbody></table>";

    static string KhuView(string body) =>
        $"""<article class="bbs-view"><div class="bbs-view_c"><p>{body}</p></div></article><ul class="prev_next"><li>이전글 다른 글</li></ul>""";

    async Task<JsonElement> CollectAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/notices/collect");
        req.Headers.Add("X-Admin-Key", TestServer.AdminKey);
        HttpResponseMessage res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    async Task<JsonElement[]> ListAsync(string query = "") =>
        (await client.GetFromJsonAsync<JsonElement[]>("/api/notices?limit=50" + query))!;

    [Fact]
    public async Task 단과대_글을_그_건물에_넣고_서울_글_학교_전체_공지_오래된_글은_뺀다()
    {
        server.NoticePages.Pages[EngList] = KhuList(
            ("101", "All", "동계 계절학기 수업 운영 안내", Today(1)),
            ("102", "Seoul", "서울캠퍼스 주차 안내", Today(1)),
            ("103", "Global", "공학관 승강기 점검", Today(2)),
            ("104", "All", "지난 학기 공지", Today(60)));
        server.NoticePages.Pages[EngView + "101"] = KhuView("동계 계절학기는 12월 22일부터 열리며 수강신청은 11월에 합니다.");
        server.NoticePages.Pages[EngView + "103"] = KhuView("공학관 승강기 2호기를 10월 28일 오전에 점검합니다. 계단을 이용해 주세요.");

        await CollectAsync();

        JsonElement[] rows = await ListAsync($"&buildingId={Engineering}");
        string[] titles = rows.Select(r => r.GetProperty("title").GetString()!).ToArray();
        Assert.Equal(["동계 계절학기 수업 운영 안내", "공학관 승강기 점검"], titles);   // 캠퍼스 표시는 제목에서 뺌
        Assert.All(rows, r => Assert.StartsWith(EngView, r.GetProperty("url").GetString()));
        Assert.Equal(2, (await ListAsync($"&buildingId={EngineeringLab}")).Length);   // 공과대학 게시판 → 공학관 · 공학실험동
        Assert.DoesNotContain(server.NoticePages.Requested, u => u.EndsWith("boardId=102") || u.EndsWith("boardId=104"));
    }

    [Fact]
    public async Task 같은_제목이_여러_게시판에_있으면_한_공지에_건물만_더한다()
    {
        server.NoticePages.Pages[EngList] = KhuList(("201", "All", "Job Festival 안내", Today()));
        server.NoticePages.Pages[EngView + "201"] = KhuView("동문 멘토와 함께하는 Job Festival 을 11월 5일 학생회관에서 엽니다.");
        server.NoticePages.Pages[ForeignList] = KhuList(("301", "공통", "Job Festival 안내", Today()));

        await CollectAsync();

        JsonElement[] rows = (await ListAsync()).Where(r => r.GetProperty("title").GetString() == "Job Festival 안내").ToArray();
        Assert.Single(rows.Select(r => r.GetProperty("id").GetString()).Distinct());
        Assert.Contains(rows, r => r.GetProperty("buildingId").GetString() == ForeignHall);
        Assert.Contains(rows, r => r.GetProperty("buildingId").GetString() == Engineering);
    }

    [Fact]
    public async Task 이미_넣은_링크는_다시_읽지_않고_포스터만_있는_글은_요약_없이_제목만()
    {
        server.NoticePages.Pages[EngList] = KhuList(("401", "All", "축제 포스터", Today()));
        server.NoticePages.Pages[EngView + "401"] = KhuView("<img src=\"poster.png\">");

        await CollectAsync();
        int calls = server.NoticeClassifier.Calls;
        await CollectAsync();

        JsonElement row = (await ListAsync($"&buildingId={Engineering}")).Single();
        Assert.Equal("", row.GetProperty("summary").GetString());
        Assert.Equal(0, calls);   // 글자가 없으면 AI 를 부르지 않는다
        Assert.Single(server.NoticePages.Requested, u => u == EngView + "401");
    }

    [Fact]
    public async Task 게시판을_읽지_못해도_다른_게시판은_계속하고_결과에_남긴다()
    {
        server.NoticePages.Pages[EngList] = KhuList(("501", "All", "공학관 소식", Today()));
        server.NoticePages.Pages[EngView + "501"] = KhuView("공학관 1층 라운지를 새로 꾸몄습니다. 많이 이용해 주세요.");

        JsonElement report = await CollectAsync();

        JsonElement[] lines = report.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(1, lines.Single(l => l.GetProperty("source").GetString() == "eng").GetProperty("added").GetInt32());
        Assert.Equal("목록을 읽지 못함", lines.Single(l => l.GetProperty("source").GetString() == "cls").GetProperty("error").GetString());
    }

    [Fact]
    public void 도서관은_서울_글을_빼고_jsessionid_를_지운다()
    {
        var source = new NoticeSource("library", "중앙도서관", "library", LibraryList, ["way-474085534"]);
        string html = $"""
            <tr><td class="subject"> 국제 </td><td class="title"><a href="/bbs/content/1_100;jsessionid=ABC">열람실 공사 안내</a></td><td class="reportDate"> {Today()} </td></tr>
            <tr><td class="subject"> 서울 </td><td class="title"><a href="/bbs/content/1_101;jsessionid=ABC">[서울] 학습 환경</a></td><td class="reportDate"> {Today()} </td></tr>
            <tr><td class="subject"> 전체 </td><td class="title"><a href="/bbs/content/1_102">DB 오류 복구</a></td><td class="reportDate"> {Today()} </td></tr>
            """;
        List<NoticeItem> items = NoticeCollector.ParseList(source, html);
        Assert.Equal(["열람실 공사 안내", "DB 오류 복구"], items.Select(i => i.Title));
        Assert.Equal("https://lib.khu.ac.kr/bbs/content/1_100", items[0].Url);
    }

    [Fact]
    public void 체육대학_게시판_글과_본문을_읽는다()
    {
        var source = new NoticeSource("sports", "체육대학", "gnuboard", SportsList, ["way-455726113"]);
        string html = """
            <tr class="bo_notice"><td class="td_subject"><div class="bo_tit">
              <a href="http://sports.khu.ac.kr/bbs/board.php?bo_table=05_01&amp;wr_id=2428"> 동계 계절학기 운영 안내 </a></div></td>
              <td class="td_datetime">2026-10-22</td></tr>
            """;
        NoticeItem item = NoticeCollector.ParseList(source, html).Single();
        Assert.Equal("동계 계절학기 운영 안내", item.Title);
        Assert.Equal("http://sports.khu.ac.kr/bbs/board.php?bo_table=05_01&wr_id=2428", item.Url);
        Assert.Equal("2026-10-22", item.PostedAt);
        string body = NoticeCollector.ParseBody("gnuboard",
            """<div id="bo_v_con"><style>p{}</style><p>계절학기 강좌를 &nbsp;안내합니다.</p></div><section id="bo_v_file">첨부</section>""");
        Assert.Equal("계절학기 강좌를 안내합니다.", body);
    }

    [Fact]
    public async Task 오래된_공지는_앱에_보내지_않는다()
    {
        using var windowed = server.WithWebHostBuilder(b => b.UseSetting("Notices:ShowDays", "45"));
        HttpClient c = windowed.CreateClient();
        server.NoticePages.Pages[EngList] = KhuList(("601", "All", "새 공지", Today()));
        server.NoticePages.Pages[EngView + "601"] = KhuView("새로 올라온 공지의 본문입니다. 충분히 긴 글이에요.");
        var req = new HttpRequestMessage(HttpMethod.Post, "/api/admin/notices/collect");
        req.Headers.Add("X-Admin-Key", TestServer.AdminKey);
        await c.SendAsync(req);
        var old = new HttpRequestMessage(HttpMethod.Post, "/api/admin/notices")
        {
            Content = JsonContent.Create(new { title = "옛 공지", body = "아주 오래된 공지입니다.", url = "https://www.khu.ac.kr/old/1", postedAt = Today(100) }),
        };
        old.Headers.Add("X-Admin-Key", TestServer.AdminKey);
        server.NoticeClassifier.Next = new NoticeClassification([Engineering], "옛 요약", "ai");
        await c.SendAsync(old);

        string[] titles = (await c.GetFromJsonAsync<JsonElement[]>("/api/notices?limit=50"))!
            .Select(r => r.GetProperty("title").GetString()!).ToArray();
        Assert.Contains("새 공지", titles);
        Assert.DoesNotContain("옛 공지", titles);
    }
}
