using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>산책 음악 추천 API (docs/api.md 의 "POST /api/music"). 로그인은 선택이다 (로그인했으면 계정의 걷는 속도를 쓴다).</summary>
public static class MusicEndpoints
{
    public static void MapMusicEndpoints(this WebApplication app)
    {
        app.MapPost("/api/music", async (MusicRequest? request, HttpRequest http, AccountStore accounts,
                                         MusicRecommender music, CancellationToken ct) =>
        {
            request ??= new MusicRequest();
            if (MusicRecommender.Validate(request) is { } error)
                return Results.BadRequest(new { error });
            string pace = request.Pace ?? RecommendEndpoints.PaceOf(AuthEndpoints.Current(http, accounts)) ?? "normal";
            return Results.Ok(await music.RecommendAsync(request, pace, ct));
        }).WithTags("Music");
    }
}
