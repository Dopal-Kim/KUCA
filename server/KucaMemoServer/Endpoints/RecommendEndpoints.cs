using System.Text.Json;
using KucaMemoServer.Models;
using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>산책 추천 API (docs/api.md 의 "POST /api/recommendations (v2)"). 로그인은 선택이다.</summary>
public static class RecommendEndpoints
{
    public static void MapRecommendEndpoints(this WebApplication app)
    {
        app.MapPost("/api/recommendations", async (RecommendRequest? request, HttpRequest http, AccountStore accounts,
                                                   RouteRecommender recommender, CancellationToken ct) =>
        {
            if (request is null)
                return Results.BadRequest(new { error = "본문(JSON)이 필요합니다." });
            if (RouteRecommender.Validate(request) is { } error)
                return Results.BadRequest(new { error });
            string pace = request.Pace ?? PaceOf(AuthEndpoints.Current(http, accounts)) ?? "normal";
            return Results.Ok(await recommender.RecommendAsync(request, pace, ct));
        }).WithTags("Recommendations");
    }

    /// <summary>계정 취향(WalkPreferences JSON)의 pace. 없거나 읽지 못하면 null</summary>
    internal static string? PaceOf(Account? account)
    {
        if (account?.PreferencesJson is not { Length: > 0 } json)
            return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("pace", out JsonElement p) && p.GetString() is { } pace
                   && RecommendRules.AllowedPaces.Contains(pace) ? pace : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
