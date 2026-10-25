using System.Net;
using System.Text;
using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>
/// 산책 기록 카카오톡 공유 (B, 10/25).
///   POST /api/walks/share  (로그인) 지도 그림 + 거리 · 시간 · 들른 곳 → 공유 페이지 주소 · 그림 주소
///   GET  /w/{id}           카톡 카드를 누르면 열리는 페이지 (og:image 로 카드 미리보기)
/// 페이지 글은 서버가 숫자 · 장소 이름 · 닉네임으로 만든다. 앱이 보내는 자유 글은 없어 내용 검사가 필요 없다.
/// </summary>
public static class WalkShareEndpoints
{
    public const long MaxImageBytes = 3 * 1024 * 1024;
    public const int MaxPerDay = 30;
    const int MaxStops = 8;

    public static void MapWalkShareEndpoints(this WebApplication app)
    {
        app.MapPost("/api/walks/share", async (HttpRequest request, AccountStore accounts, WalkShareStore shares,
                                               PhotoStore photos, BuildingStore buildings, SpotStore spots, TimeProvider clock) =>
        {
            if (AuthEndpoints.Current(request, accounts) is not { } user)
                return Results.Json(new { error = "로그인이 필요해요." }, statusCode: 401);
            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "multipart/form-data 로 보내 주세요." });
            DateTime now = clock.GetUtcNow().UtcDateTime;
            if (shares.CountSince(user.Id, now.AddDays(-1)) >= MaxPerDay)
                return Results.Json(new { error = "오늘은 공유를 너무 많이 했어요. 내일 다시 해 주세요." }, statusCode: 429);

            IFormCollection form;
            try { form = await request.ReadFormAsync(); }
            catch (InvalidDataException) { return Results.BadRequest(new { error = "그림이 너무 커요." }); }

            IFormFile? image = form.Files.GetFile("image");
            if (image is not { Length: > 0 } || image.Length > MaxImageBytes)
                return Results.BadRequest(new { error = "지도 그림(3MB 이하)이 필요해요." });
            string? ext = await PhotoStore.DetectExtensionAsync(image);
            if (ext is null)
                return Results.BadRequest(new { error = "지도 그림은 PNG 또는 JPEG 여야 해요." });
            if (!int.TryParse(form["meters"], out int meters) || meters is < 0 or > 100_000
                || !int.TryParse(form["minutes"], out int minutes) || minutes is < 0 or > 24 * 60)
                return Results.BadRequest(new { error = "meters · minutes 가 올바르지 않아요." });
            string kind = form["kind"].ToString() is "mate" or "free" ? form["kind"].ToString() : "course";

            // 들른 곳: 앱이 보낸 건물 ID 를 서버의 이름으로 바꾼다 (모르는 ID 는 버림)
            var names = form["buildingIds"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct().Take(MaxStops)
                .Select(id => spots.Find(id)?.Name ?? buildings.Find(id)?.Name)
                .OfType<string>().ToList();

            string id = Guid.NewGuid().ToString("N")[..12];
            string imageUrl = await photos.SaveAsync("walk_" + id, ext, image);
            string nickname = string.IsNullOrWhiteSpace(user.Nickname) ? "경희대 학생" : user.Nickname;
            shares.Add(new WalkShareStore.Share(id, user.Id, nickname, kind, meters, minutes, string.Join(" → ", names), imageUrl, now));
            return Results.Ok(new { id, pageUrl = $"/w/{id}", imageUrl, title = Title(nickname, kind), description = Summary(meters, minutes, string.Join(" → ", names)) });
        }).WithTags("Walks").DisableAntiforgery();

        app.MapGet("/w/{id}", (string id, HttpRequest request, WalkShareStore shares) =>
        {
            WalkShareStore.Share? s = shares.Find(id);
            if (s == null)
                return Results.Content(Page("산책 기록 없음", "", "", "<p class=\"empty\">공유한 산책 기록을 찾을 수 없어요.</p>"), "text/html; charset=utf-8", Encoding.UTF8, 404);
            string origin = Origin(request);
            string title = Title(s.Nickname, s.Kind);
            string desc = Summary(s.Meters, s.Minutes, s.Stops);
            var body = new StringBuilder();
            body.Append($"<h1>{Html(title)}</h1>");
            body.Append($"<p class=\"stats\">{Html(Distance(s.Meters))} · {s.Minutes}분</p>");
            if (s.Stops.Length > 0)
                body.Append($"<p class=\"stops\">{Html(s.Stops)}</p>");
            body.Append($"<img src=\"{Html(s.ImageUrl)}\" alt=\"걸은 길 지도\">");
            body.Append($"<p class=\"meta\">{KoreaDate(s.CreatedAt)} · 경희대학교 국제캠퍼스</p>");
            body.Append("<p class=\"about\"><b>KUCA</b> · 공강에 AI 가 걷기 좋은 캠퍼스 코스와 함께 걸을 학생을 이어 주는 경희대 국제캠퍼스 산책 앱</p>");
            return Results.Content(Page(title, desc, origin + s.ImageUrl, body.ToString()), "text/html; charset=utf-8", Encoding.UTF8);
        }).ExcludeFromDescription();
    }

    static string Title(string nickname, string kind) => kind switch
    {
        "mate" => $"{nickname}님이 산책메이트와 함께 걸었어요",
        "free" => $"{nickname}님의 자유 산책",
        _ => $"{nickname}님의 캠퍼스 산책 코스",
    };

    static string Summary(int meters, int minutes, string stops) =>
        $"{Distance(meters)} · {minutes}분" + (stops.Length > 0 ? $" · {stops}" : "");

    static string Distance(int m) => m >= 1000 ? $"{m / 1000.0:0.0}km" : $"{m}m";

    /// <summary>카드 그림은 절대 주소여야 한다. Azure 는 앞단에서 https 를 끝내므로 http 로 들어와도 https 로</summary>
    static string Origin(HttpRequest r)
    {
        string scheme = r.Host.Host.EndsWith(".azurewebsites.net") ? "https" : r.Scheme;
        return $"{scheme}://{r.Host}";
    }

    static string KoreaDate(DateTime utc)
    {
        DateTime k = utc.AddHours(9);
        return $"{k.Year}년 {k.Month}월 {k.Day}일";
    }

    static string Html(string s) => WebUtility.HtmlEncode(s);

    static string Page(string title, string description, string image, string body) => $$"""
        <!doctype html>
        <html lang="ko">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{{Html(title)}}</title>
        <meta property="og:title" content="{{Html(title)}}">
        <meta property="og:description" content="{{Html(description)}}">
        <meta property="og:image" content="{{Html(image)}}">
        <meta property="og:type" content="website">
        <style>
          :root { color-scheme: light; }
          body { max-width: 560px; margin: 0 auto; padding: 20px 16px 40px; background: #f9f9fc; color: #1c1c1f;
                 font: 16px/1.6 system-ui, -apple-system, "Apple SD Gothic Neo", "Malgun Gothic", sans-serif; }
          h1 { font-size: 22px; margin: 8px 0 4px; }
          .stats { font-size: 20px; font-weight: 700; color: #2a6bf2; margin: 0; }
          .stops { color: #555; margin: 4px 0 14px; }
          img { width: 100%; border-radius: 14px; display: block; }
          .meta, .empty { color: #888; font-size: 14px; }
          .about { margin-top: 24px; padding: 14px; background: #fff; border-radius: 12px; color: #444; font-size: 14px; }
        </style>
        </head>
        <body>
        {{body}}
        </body>
        </html>
        """;
}
