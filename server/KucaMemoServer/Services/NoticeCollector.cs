using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KucaMemoServer.Services;

/// <summary>공지를 모아 올 게시판 하나 (Data/notice_sources.json). buildingIds 는 그 단과대 · 학과가 쓰는 경희스팟</summary>
public record NoticeSource(string Id, string Name, string Kind, string ListUrl, string[] BuildingIds);

/// <summary>게시판 목록에서 읽은 글 한 줄</summary>
public record NoticeItem(string Title, string Url, string PostedAt);

/// <summary>한 번 모은 결과 (게시판마다 새로 넣은 수 · 오류)</summary>
public record NoticeCollectReport(DateTimeOffset StartedAt, List<NoticeCollectReport.Line> Sources)
{
    public record Line(string Source, int Listed, int Added, string? Error);
    public int Added => Sources.Sum(s => s.Added);
}

/// <summary>게시판 페이지를 읽는다. 테스트에서는 저장해 둔 HTML 로 바꿔 끼운다</summary>
public interface INoticePageFetcher
{
    Task<string?> GetAsync(string url, CancellationToken ct);
}

public class HttpNoticePageFetcher(HttpClient http, ILogger<HttpNoticePageFetcher> log) : INoticePageFetcher
{
    public async Task<string?> GetAsync(string url, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("KUCA-notice-collector/1.0 (+kuca6497@gmail.com)");
            using HttpResponseMessage res = await http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                log.LogWarning("공지 게시판을 읽지 못했어요 {Url}: {Status}", url, (int)res.StatusCode);
                return null;
            }
            return await res.Content.ReadAsStringAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("공지 게시판에 연결하지 못했어요 {Url}: {Message}", url, e.Message);
            return null;
        }
    }
}

/// <summary>
/// 경희스팟 건물의 단과대 · 학과 · 도서관 공지 게시판에서 최근 공지를 모아 장소 공지로 넣는다.
/// - 게시판마다 연결할 건물이 정해져 있다 (notice_sources.json). AI 는 요약만 한다 (NoticeClassifier).
/// - 서울캠퍼스 표시 글, 다른 사이트로 가는 학교 전체 공지, MaxAgeDays 보다 오래된 글, 이미 넣은 링크는 건너뛴다.
/// - 누군가 공지를 볼 때 (GET /api/notices) 마지막으로 모은 지 Interval 이 지났으면 뒤에서 한 번 모은다 (TriggerIfStale).
///   서버가 잠들어도 쓰는 사람이 있을 때 깨어나 모으면 되므로 따로 예약 작업이 필요 없다.
/// - 학교 사이트에 부담을 주지 않게 요청 사이를 띄우고, 한 번에 게시판마다 몇 건만 본문을 읽는다.
/// 설정: Notices:AutoCollect (기본 true), Notices:IntervalHours (기본 6), Notices:RequestDelayMs (기본 800)
/// </summary>
public partial class NoticeCollector
{
    public const int MaxAgeDays = 30;
    public const int MaxNewPerSource = 5;
    public const int MaxBodyChars = 1500;

    readonly IServiceScopeFactory scopes;
    readonly NoticeStore notices;
    readonly TimeProvider clock;
    readonly ILogger<NoticeCollector> log;
    readonly bool auto;
    readonly TimeSpan interval, delay;
    readonly SemaphoreSlim running = new(1, 1);
    DateTimeOffset lastStarted = DateTimeOffset.MinValue;

    public IReadOnlyList<NoticeSource> Sources { get; }
    public NoticeCollectReport? LastReport { get; private set; }

    public NoticeCollector(IServiceScopeFactory scopes, NoticeStore notices, TimeProvider clock, IConfiguration config,
                           IWebHostEnvironment env, ILogger<NoticeCollector> log)
    {
        this.scopes = scopes;
        this.notices = notices;
        this.clock = clock;
        this.log = log;
        auto = !bool.TryParse(config["Notices:AutoCollect"], out bool a) || a;
        interval = TimeSpan.FromHours(double.TryParse(config["Notices:IntervalHours"], out double h) && h > 0 ? h : 6);
        delay = TimeSpan.FromMilliseconds(int.TryParse(config["Notices:RequestDelayMs"], out int d) && d >= 0 ? d : 800);
        string path = Path.Combine(env.ContentRootPath, "Data", "notice_sources.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        Sources = File.Exists(path) ? JsonSerializer.Deserialize<List<NoticeSource>>(File.ReadAllText(path), options) ?? [] : [];
    }

    /// <summary>마지막으로 모은 지 오래됐으면 뒤에서 모으기 시작한다 (기다리지 않음)</summary>
    public void TriggerIfStale()
    {
        if (!auto || clock.GetUtcNow() - lastStarted < interval || running.CurrentCount == 0)
            return;
        _ = Task.Run(async () =>
        {
            try { await CollectAsync(CancellationToken.None); }
            catch (Exception e) { log.LogError(e, "공지 모으기 실패"); }
        });
    }

    /// <summary>모든 게시판에서 한 번 모은다. 이미 모으는 중이면 그 결과를 기다리지 않고 null</summary>
    public async Task<NoticeCollectReport?> CollectAsync(CancellationToken ct)
    {
        if (!await running.WaitAsync(0, ct))
            return null;
        try
        {
            lastStarted = clock.GetUtcNow();
            var report = new NoticeCollectReport(lastStarted, []);
            using IServiceScope scope = scopes.CreateScope();
            var fetcher = scope.ServiceProvider.GetRequiredService<INoticePageFetcher>();
            var classifier = scope.ServiceProvider.GetRequiredService<INoticeClassifier>();
            string oldest = KoreaDate(lastStarted.AddDays(-MaxAgeDays));
            foreach (NoticeSource source in Sources)
            {
                try
                {
                    report.Sources.Add(await CollectOneAsync(source, fetcher, classifier, oldest, ct));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    log.LogWarning(e, "공지 게시판 {Source} 를 처리하지 못했어요", source.Id);
                    report.Sources.Add(new(source.Id, 0, 0, e.Message));
                }
            }
            LastReport = report;
            log.LogInformation("공지 모으기: 새 공지 {Added}건", report.Added);
            return report;
        }
        finally
        {
            running.Release();
        }
    }

    async Task<NoticeCollectReport.Line> CollectOneAsync(NoticeSource source, INoticePageFetcher fetcher, INoticeClassifier classifier,
                                                          string oldest, CancellationToken ct)
    {
        string? list = await fetcher.GetAsync(source.ListUrl, ct);
        if (list == null)
            return new(source.Id, 0, 0, "목록을 읽지 못함");
        List<NoticeItem> items = ParseList(source, list);
        int added = 0;
        foreach (NoticeItem item in items.Where(i => string.CompareOrdinal(i.PostedAt, oldest) >= 0))
        {
            if (added >= MaxNewPerSource)
                break;
            if (notices.UrlExists(item.Url))
                continue;
            // 학교 전체 공지는 여러 단과대 게시판에 같은 제목으로 올라온다: 이미 있으면 이 단과대 건물만 더한다
            if (notices.FindByTitle(item.Title) is { } same)
            {
                notices.AddPlaces(same, source.BuildingIds);
                continue;
            }
            await Task.Delay(delay, ct);
            string? page = await fetcher.GetAsync(item.Url, ct);
            string body = page != null ? ParseBody(source.Kind, page) : "";
            string summary = "";
            if (body.Length < 20)
                body = item.Title;   // 포스터 그림만 있는 공지: 요약 없이 제목만 보인다
            else
            {
                NoticeClassification c = await classifier.ClassifyAsync(item.Title, body, ct);
                summary = c.Summary == item.Title ? "" : c.Summary;
            }
            notices.Add(Guid.NewGuid().ToString(), item.Title, body, item.Url, summary, item.PostedAt, source.BuildingIds);
            added++;
        }
        return new(source.Id, items.Count, added, null);
    }

    static string KoreaDate(DateTimeOffset t) => t.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");

    // ---------- 게시판 읽기 ----------

    public static List<NoticeItem> ParseList(NoticeSource source, string html) => source.Kind switch
    {
        "khu" => ParseKhuList(source.ListUrl, html),
        "library" => ParseLibraryList(source.ListUrl, html),
        "gnuboard" => ParseGnuList(html),
        _ => [],
    };

    public static string ParseBody(string kind, string html) => kind switch
    {
        "khu" => TextAfter(html, "class=\"bbs-view_c\""),
        "library" => TextAfter(html, "class=\"boardContent\""),
        "gnuboard" => TextAfter(html, "id=\"bo_v_con\""),
        _ => "",
    };

    /// <summary>
    /// 경희대 홈페이지 시스템 (BMSR 게시판). 이 게시판의 글은 javascript:view('번호') 로 열린다 → view.do?menuNo=…&amp;boardId=번호.
    /// 다른 사이트로 가는 줄 (학교 전체 공지를 함께 띄운 것) 과 Seoul 표시 글은 뺀다.
    /// </summary>
    static List<NoticeItem> ParseKhuList(string listUrl, string html)
    {
        var items = new List<NoticeItem>();
        string viewBase = listUrl.Replace("/list.do", "/view.do");
        foreach (Match tr in Row().Matches(html))
        {
            Match id = KhuView().Match(tr.Value);
            Match date = Date().Match(tr.Value);
            if (!id.Success || !date.Success)
                continue;
            if (CampusTag().Match(tr.Value) is { Success: true } tag && tag.Groups[1].Value is "Seoul" or "서울")
                continue;
            string title = Text(Anchor().Match(tr.Value).Groups[2].Value);
            title = CampusTagText().Replace(title, "").Trim();
            if (title.Length == 0)
                continue;
            items.Add(new(title, $"{viewBase}&boardId={id.Groups[1].Value}", date.Value.Replace('.', '-')));
        }
        return items;
    }

    /// <summary>중앙도서관 (서울 · 국제 · 전체 구분 칸). 서울 글은 뺀다. 링크의 jsessionid 는 지운다</summary>
    static List<NoticeItem> ParseLibraryList(string listUrl, string html)
    {
        var items = new List<NoticeItem>();
        var root = new Uri(listUrl);
        foreach (Match tr in Row().Matches(html))
        {
            Match a = LibraryLink().Match(tr.Value);
            Match date = Date().Match(tr.Value);
            if (!a.Success || !date.Success)
                continue;
            if (LibraryCampus().Match(tr.Value) is { Success: true } c && c.Groups[1].Value.Trim() == "서울")
                continue;
            string title = Text(a.Groups[2].Value);
            if (title.StartsWith("[서울]"))
                continue;
            items.Add(new(title, new Uri(root, a.Groups[1].Value).ToString(), date.Value.Replace('.', '-')));
        }
        return items;
    }

    /// <summary>체육대학 (그누보드). 날짜는 2026-10-23 (목록에 따라 26-10-23)</summary>
    static List<NoticeItem> ParseGnuList(string html)
    {
        var items = new List<NoticeItem>();
        foreach (Match tr in Row().Matches(html))
        {
            Match a = GnuLink().Match(tr.Value);
            Match date = Date().Match(tr.Value);
            Match shortDate = ShortDate().Match(tr.Value);
            if (!a.Success || !date.Success && !shortDate.Success)
                continue;
            string title = Text(a.Groups[2].Value);
            if (title.Length == 0)
                continue;
            string posted = date.Success ? date.Value.Replace('.', '-') : "20" + shortDate.Value;
            items.Add(new(title, WebUtility.HtmlDecode(a.Groups[1].Value), posted));
        }
        return items;
    }

    /// <summary>marker 가 있는 태그 뒤의 글자만 (style · script 빼고, 공백 정리, MaxBodyChars 까지)</summary>
    static string TextAfter(string html, string marker)
    {
        int i = html.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0)
            return "";
        int start = html.IndexOf('>', i) + 1;
        string part = html.Substring(start, Math.Min(html.Length - start, 12000));
        // 본문 뒤의 이전글 · 다음글 · 첨부 · 목록 버튼은 넣지 않는다
        foreach (string end in BodyEnds)
        {
            int e = part.IndexOf(end, StringComparison.Ordinal);
            if (e < 0) continue;
            int tag = part.LastIndexOf('<', e);   // 표시가 든 태그가 시작하는 곳에서 자른다
            part = part[..(tag >= 0 && !end.StartsWith('<') ? tag : e)];
        }
        part = StyleOrScript().Replace(part, " ");
        string text = Text(part);
        return text.Length <= MaxBodyChars ? text : text[..MaxBodyChars].TrimEnd() + "…";
    }

    static readonly string[] BodyEnds =
    [
        "class=\"prev_next\"", "</article>", "class=\"boardFile", "class=\"boardNavigation", "class=\"btnArea",
        "id=\"bo_v_file\"", "id=\"bo_v_link\"", "id=\"bo_v_act\"", "id=\"bo_v_share\"", "본문 내용 끝",
    ];

    static string Text(string html) =>
        Spaces().Replace(WebUtility.HtmlDecode(Tag().Replace(html, " ")), " ").Trim();

    [GeneratedRegex(@"<tr\b.*?</tr>", RegexOptions.Singleline)] private static partial Regex Row();
    [GeneratedRegex(@"<a\s[^>]*href=""([^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline)] private static partial Regex Anchor();
    [GeneratedRegex(@"href=""javascript:view\('(\d+)'\)")] private static partial Regex KhuView();
    [GeneratedRegex(@"<span class=""txtBox01[^""]*"">\s*(\w+)\s*</span>")] private static partial Regex CampusTag();
    [GeneratedRegex(@"^(All|Seoul|Global|공통|국제|서울)\s+")] private static partial Regex CampusTagText();
    [GeneratedRegex(@"20\d\d[-.]\d\d[-.]\d\d")] private static partial Regex Date();
    [GeneratedRegex(@"(?<!\d)\d\d-\d\d-\d\d(?!\d)")] private static partial Regex ShortDate();
    [GeneratedRegex(@"<a\s[^>]*href=""(/bbs/content/[\d_]+)[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline)] private static partial Regex LibraryLink();
    [GeneratedRegex(@"<td class=""subject"">(.*?)</td>", RegexOptions.Singleline)] private static partial Regex LibraryCampus();
    [GeneratedRegex(@"<a\s[^>]*href=""([^""]*wr_id=\d+[^""]*)""[^>]*>(.*?)</a>", RegexOptions.Singleline)] private static partial Regex GnuLink();
    [GeneratedRegex(@"<(style|script)\b.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)] private static partial Regex StyleOrScript();
    [GeneratedRegex(@"<[^>]+>")] private static partial Regex Tag();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
}
