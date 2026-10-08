using System.Net;
using System.Text;
using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>
/// 브라우저로 메모를 둘러보는 읽기 전용 페이지 (docs/api.md 의 "웹 페이지").
///   /                       메모가 있는 건물 목록과 건물별 메모 수
///   /buildings/{buildingId} 그 건물의 메모 목록 (사진, 작성자, 시간)
/// </summary>
public static class WebPages
{
    const int MemosPerPage = 50;

    public static void MapWebPages(this WebApplication app)
    {
        app.MapGet("/", (BuildingStore buildings, MemoStore memos) =>
        {
            var rows = memos.CountByBuilding()
                .Select(kv => (Id: kv.Key, Name: buildings.Find(kv.Key)?.Name ?? kv.Key, Count: kv.Value))
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ToList();

            var body = new StringBuilder("<h1>KUCA 장소 메모</h1>");
            if (rows.Count == 0)
            {
                body.Append("<p class=\"empty\">아직 메모가 없어요.</p>");
            }
            else
            {
                body.Append("<ul class=\"list\">");
                foreach (var r in rows)
                    body.Append($"<li><a href=\"/buildings/{Url(r.Id)}\"><span>{Html(r.Name)}</span><span class=\"count\">메모 {r.Count}</span></a></li>");
                body.Append("</ul>");
            }
            return Page("KUCA 장소 메모", body.ToString());
        }).ExcludeFromDescription();

        app.MapGet("/buildings/{buildingId}", (string buildingId, BuildingStore buildings, MemoStore memos) =>
        {
            Building? building = buildings.Find(buildingId);
            if (building is null)
                return Page("건물 없음", $"<p><a href=\"/\">← 건물 목록</a></p><p class=\"empty\">건물을 찾을 수 없어요: {Html(buildingId)}</p>", 404);

            string name = building.Name ?? building.Id;
            var body = new StringBuilder($"<p><a href=\"/\">← 건물 목록</a></p><h1>{Html(name)}</h1>");
            List<Memo> list = memos.List(buildingId, MemosPerPage, null);
            if (list.Count == 0)
                body.Append("<p class=\"empty\">아직 메모가 없어요.</p>");
            foreach (Memo m in list)
            {
                body.Append("<article>");
                body.Append($"<div class=\"meta\"><b>{Html(m.Author)}</b> · {KoreaTime(m.CreatedAt)}</div>");
                body.Append($"<p>{Html(m.Text)}</p>");
                if (m.PhotoUrl is not null)
                    body.Append($"<img src=\"{Html(m.PhotoUrl)}\" alt=\"메모 사진\" loading=\"lazy\">");
                body.Append("</article>");
            }
            return Page(name, body.ToString());
        }).ExcludeFromDescription();
    }

    /// <summary>한국 시간으로 "10월 6일 14:12" (한국은 서머타임이 없어 +9시간 고정)</summary>
    static string KoreaTime(DateTime utc)
    {
        DateTime kst = utc.ToUniversalTime().AddHours(9);
        return $"{kst.Year}년 {kst.Month}월 {kst.Day}일 {kst:HH:mm}";
    }

    static string Html(string s) => WebUtility.HtmlEncode(s);

    static string Url(string s) => Uri.EscapeDataString(s);

    static IResult Page(string title, string body, int status = 200) => Results.Content($$"""
        <!doctype html>
        <html lang="ko">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{Html(title)}}</title>
        <style>
          :root { color-scheme: light dark; --line: #8884; --muted: #888; }
          body { max-width: 640px; margin: 0 auto; padding: 16px; font: 15px/1.6 system-ui, -apple-system, "Apple SD Gothic Neo", "Malgun Gothic", sans-serif; }
          h1 { font-size: 20px; }
          a { color: inherit; }
          .list { list-style: none; padding: 0; }
          .list li { border-top: 1px solid var(--line); }
          .list a { display: flex; justify-content: space-between; padding: 10px 2px; text-decoration: none; }
          .count, .meta, .empty { color: var(--muted); }
          article { border-top: 1px solid var(--line); padding: 12px 0; }
          article p { margin: 4px 0; white-space: pre-wrap; overflow-wrap: anywhere; }
          img { max-width: 100%; border-radius: 6px; }
        </style>
        </head>
        <body>
        {{body}}
        </body>
        </html>
        """, "text/html; charset=utf-8", Encoding.UTF8, status);
}
