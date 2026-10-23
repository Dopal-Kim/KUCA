namespace KucaMemoServer.Services;

/// <summary>추천이 쓰는 지금 캠퍼스 날씨. 테스트에서는 가짜로 바꿔 끼운다.</summary>
public interface ICampusWeather
{
    /// <summary>clear · cloudy · rain · snow, 모르면 null</summary>
    Task<string?> NowAsync(CancellationToken ct);
}

/// <summary>기상청(KmaWeatherService, 10분 캐시)으로 날씨를 본다. 키가 없거나 실패하면 null.</summary>
public class KmaCampusWeather(KmaWeatherService kma) : ICampusWeather
{
    public async Task<string?> NowAsync(CancellationToken ct) => (await kma.GetAsync(ct))?.Condition;
}
