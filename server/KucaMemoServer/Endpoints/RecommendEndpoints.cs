using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>산책 추천 API (docs/api.md 의 "산책 추천")</summary>
public static class RecommendEndpoints
{
    public static void MapRecommendEndpoints(this WebApplication app)
    {
        app.MapPost("/api/recommendations", async (RecommendRequest? request, RouteRecommender recommender, CancellationToken ct) =>
        {
            if (request is null)
                return Results.BadRequest(new { error = "본문(JSON)이 필요합니다." });
            if (RouteRecommender.Validate(request) is { } error)
                return Results.BadRequest(new { error });
            return Results.Ok(await recommender.RecommendAsync(request, ct));
        }).WithTags("Recommendations");
    }
}
