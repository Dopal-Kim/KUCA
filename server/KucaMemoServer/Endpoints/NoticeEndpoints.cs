using KucaMemoServer.Models;
using KucaMemoServer.Services;
using static KucaMemoServer.Endpoints.MemoEndpoints;

namespace KucaMemoServer.Endpoints;

/// <summary>
/// 장소 공지 (docs/api.md 의 "GET /api/notices", "관리자 API › 공지 넣기").
/// 앱은 GET 으로 받아 가고, 운영자는 관리자 API 로 넣는다 (AdminEndpoints 의 X-Admin-Key 검사를 같이 쓴다).
/// </summary>
public static class NoticeEndpoints
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 5000;

    /// <summary>앱용: 장소별 공지, 최신순. ?buildingId= 로 그 장소만, ?limit=1~50 (기본 20).</summary>
    public static void MapNoticeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/notices", (string? buildingId, int? limit, NoticeStore notices, SpotStore spots) =>
        {
            int take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
            var rows = notices.List(string.IsNullOrWhiteSpace(buildingId) ? null : buildingId.Trim(), take)
                .Select(r => new PlaceNotice(r.Id, r.BuildingId, spots.Find(r.BuildingId)?.Name ?? r.BuildingId,
                                             r.Title, r.Summary, r.Url, r.PostedAt));
            return Results.Ok(rows);
        }).WithTags("Notices");
    }

    /// <summary>관리자용: 공지 넣기·지우기. AdminEndpoints 가 만든 /api/admin 그룹(키 검사 포함)에 붙인다.</summary>
    public static void MapNoticeAdmin(this RouteGroupBuilder admin)
    {
        admin.MapPost("/notices", async (HttpRequest request, NoticeStore notices, INoticeClassifier classifier) =>
        {
            if (!request.HasJsonContentType())
                return Error(StatusCodes.Status400BadRequest, "본문은 JSON 이어야 합니다 (Content-Type: application/json)");

            NewNotice? input;
            try
            {
                input = await request.ReadFromJsonAsync<NewNotice>();
            }
            catch (System.Text.Json.JsonException)
            {
                return Error(StatusCodes.Status400BadRequest, "JSON 형식이 올바르지 않습니다");
            }

            string title = input?.Title?.Trim() ?? "";
            string body = input?.Body?.Trim() ?? "";
            string url = input?.Url?.Trim() ?? "";
            string? postedAt = input?.PostedAt?.Trim();

            if (title.Length is 0 or > MaxTitleLength)
                return Error(StatusCodes.Status400BadRequest, $"title 은 1~{MaxTitleLength}자여야 합니다");
            if (body.Length is 0 or > MaxBodyLength)
                return Error(StatusCodes.Status400BadRequest, $"body 는 1~{MaxBodyLength}자여야 합니다");
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return Error(StatusCodes.Status400BadRequest, "url 은 http:// 또는 https:// 로 시작하는 원문 링크여야 합니다");
            if (string.IsNullOrEmpty(postedAt))
                postedAt = KoreaToday();
            else if (!DateOnly.TryParseExact(postedAt, "yyyy-MM-dd", out _))
                return Error(StatusCodes.Status400BadRequest, "postedAt 은 YYYY-MM-DD 형식이어야 합니다");

            if (notices.UrlExists(url))
                return Error(StatusCodes.Status409Conflict, "같은 원문 링크의 공지가 이미 있습니다");

            NoticeClassification result = await classifier.ClassifyAsync(title, body, request.HttpContext.RequestAborted);
            if (result.BuildingIds.Count == 0)
                return Results.Ok(new { saved = false, reason = "no_place", source = result.Source });

            string id = Guid.NewGuid().ToString();
            notices.Add(id, title, body, url, result.Summary, postedAt, result.BuildingIds);
            return Results.Created($"/api/notices?buildingId={Uri.EscapeDataString(result.BuildingIds[0])}",
                new { id, buildingIds = result.BuildingIds, summary = result.Summary, source = result.Source });
        });

        admin.MapDelete("/notices/{noticeId}", (string noticeId, NoticeStore notices) =>
            notices.Delete(noticeId)
                ? Results.NoContent()
                : Error(StatusCodes.Status404NotFound, $"공지를 찾을 수 없습니다: {noticeId}"));
    }

    /// <summary>오늘 날짜 (한국 시간, 서머타임 없음 +9)</summary>
    static string KoreaToday() => DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd");
}
