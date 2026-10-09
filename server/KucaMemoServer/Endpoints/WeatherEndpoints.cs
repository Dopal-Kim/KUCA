using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>
/// 캠퍼스 지금 날씨 (앱의 캐릭터 출현 조건용). docs/api.md 의 "날씨" 절.
/// </summary>
public static class WeatherEndpoints
{
    public static void MapWeatherEndpoints(this WebApplication app)
    {
        app.MapGet("/api/weather", async (KmaWeatherService weather, CancellationToken ct) =>
        {
            if (!weather.Configured)
                return Results.Json(new { error = "날씨 API 키가 설정되지 않았습니다 (Kma:ServiceKey)" }, statusCode: 503);
            var now = await weather.GetAsync(ct);
            return now is null
                ? Results.Json(new { error = "기상청 날씨를 받지 못했습니다" }, statusCode: 502)
                : Results.Ok(new { condition = now.Condition, observedAt = now.ObservedAt, source = "KMA" });
        }).WithTags("Weather");
    }
}
