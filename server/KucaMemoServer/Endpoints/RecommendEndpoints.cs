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
                                                   RouteRecommender recommender, HealthService health, WishInterpreter wishes, CancellationToken ct) =>
        {
            if (request is null)
                return Results.BadRequest(new { error = "본문(JSON)이 필요합니다." });
            if (RouteRecommender.Validate(request) is { } error)
                return Results.BadRequest(new { error });
            if (!HealthRules.ValidWeight(request.WeightKg))
                return Results.BadRequest(new { error = $"weightKg 는 {HealthRules.MinWeightKg}~{HealthRules.MaxWeightKg} 입니다." });
            // 알아들을 수 없거나 서로 부딪치는 바람이면 코스를 지어내지 않고 다시 묻는다 (해석은 10분 기억해 추천에서 다시 부르지 않음)
            if (!string.IsNullOrWhiteSpace(request.Wish) && (await wishes.InterpretAsync(request.Wish, ct)).Unclear is { } unclear)
                return Results.Json(new { error = unclear, code = "wish_unclear" }, statusCode: 422);
            Account? user = AuthEndpoints.Current(http, accounts);
            string pace = request.Pace ?? PaceOf(user) ?? "normal";
            WeekGoal? week = user == null ? null : health.Week(user.Id);   // 로그인했으면 이번 주 목표에 맞춰
            return Results.Ok(await recommender.RecommendAsync(request, pace, ct, week: week));
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
