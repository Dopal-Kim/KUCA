using System.Net;
using KucaMemoServer.Services;

namespace KucaMemoServer.Tests;

public class WeatherTests
{
    [Theory]
    [InlineData(37.5665, 126.9780, 60, 127)]   // 서울시청 (기상청 예시)
    [InlineData(37.2440, 127.0800, 62, 120)]   // 경희대 국제캠퍼스
    public void ToGrid_matches_kma_grid(double lat, double lon, int x, int y)
    {
        Assert.Equal((x, y), KmaWeatherService.ToGrid(lat, lon));
    }

    [Theory]
    [InlineData(0, 1, "clear")]
    [InlineData(0, 3, "cloudy")]
    [InlineData(0, 4, "cloudy")]
    [InlineData(1, 1, "rain")]
    [InlineData(5, null, "rain")]
    [InlineData(2, 4, "rain")]
    [InlineData(3, 4, "snow")]
    [InlineData(7, null, "snow")]
    [InlineData(0, null, "clear")]
    [InlineData(null, null, "unknown")]
    public void Classify_maps_pty_and_sky(int? pty, int? sky, string expected)
    {
        Assert.Equal(expected, KmaWeatherService.Classify(pty, sky));
    }

    [Fact]
    public async Task Weather_without_key_is_503()
    {
        using var server = new TestServer();
        using var client = server.CreateClient();
        var res = await client.GetAsync("/api/weather");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }
}
