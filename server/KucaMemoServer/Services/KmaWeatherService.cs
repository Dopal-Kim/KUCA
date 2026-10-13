using System.Globalization;
using System.Text.Json;

namespace KucaMemoServer.Services;

/// <summary>
/// 캠퍼스 지금 날씨 (기상청 단기예보 조회서비스: 초단기실황 PTY + 초단기예보 SKY).
/// 앱의 캐릭터 출현 조건(SpawnConditions)에 쓰인다. 결과는 10분 동안 재사용한다.
/// API 키는 설정 Kma:ServiceKey (Azure 앱 설정 이름 Kma__ServiceKey, 로컬은 dotnet user-secrets). 저장소에 넣지 않는다.
/// 공공데이터포털의 Decoding 키와 Encoding 키 모두 받는다 (% 가 들어 있으면 이미 인코딩된 것으로 본다).
/// </summary>
public sealed class KmaWeatherService
{
    public const string BaseUrl = "https://apis.data.go.kr/1360000/VilageFcstInfoService_2.0";
    static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);

    readonly IHttpClientFactory http;
    readonly ILogger<KmaWeatherService> log;
    readonly string? key;
    readonly SemaphoreSlim gate = new(1, 1);
    WeatherNow? cached;
    DateTimeOffset cachedAt;

    public int Nx { get; }
    public int Ny { get; }
    public bool Configured => !string.IsNullOrWhiteSpace(key);

    public KmaWeatherService(IHttpClientFactory http, IConfiguration config, ILogger<KmaWeatherService> log)
    {
        this.http = http;
        this.log = log;
        key = config["Kma:ServiceKey"]?.Trim();
        // 기본 위치: 경희대 국제캠퍼스 (격자 62, 120)
        double lat = config.GetValue("Kma:Latitude", 37.2440);
        double lon = config.GetValue("Kma:Longitude", 127.0800);
        (Nx, Ny) = ToGrid(lat, lon);
    }

    public record WeatherNow(string Condition, string ObservedAt);

    /// <summary>지금 날씨. 기상청이 실패하면 마지막으로 받은 값 (없으면 null).</summary>
    public async Task<WeatherNow?> GetAsync(CancellationToken ct)
    {
        if (!Configured)
            return null;
        if (cached != null && DateTimeOffset.UtcNow - cachedAt < CacheFor)
            return cached;
        await gate.WaitAsync(ct);
        try
        {
            if (cached != null && DateTimeOffset.UtcNow - cachedAt < CacheFor)
                return cached;
            DateTime kst = DateTime.UtcNow.AddHours(9);
            int? pty = await FetchPtyAsync(kst, ct);
            int? sky = await FetchSkyAsync(kst, ct);
            string condition = Classify(pty, sky);
            if (condition == "unknown")
                return cached;
            cached = new WeatherNow(condition, DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            cachedAt = DateTimeOffset.UtcNow;
            return cached;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException)
        {
            log.LogWarning("기상청 날씨 조회 실패: {Message}", e.Message);
            return cached;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 강수형태(PTY)와 하늘상태(SKY)를 앱이 쓰는 4가지로 줄인다.
    /// PTY: 0 없음, 1 비, 2 비/눈, 3 눈, 5 빗방울, 6 빗방울눈날림, 7 눈날림 · SKY: 1 맑음, 3 구름많음, 4 흐림
    /// </summary>
    public static string Classify(int? pty, int? sky) => pty switch
    {
        3 or 7 => "snow",
        1 or 2 or 5 or 6 => "rain",
        _ => sky switch
        {
            1 => "clear",
            3 or 4 => "cloudy",
            _ => pty == 0 ? "clear" : "unknown",
        },
    };

    /// <summary>위경도 → 기상청 격자 (Lambert Conformal Conic, 기상청 공식 변환)</summary>
    public static (int X, int Y) ToGrid(double lat, double lon)
    {
        const double RE = 6371.00877, GRID = 5.0, SLAT1 = 30.0, SLAT2 = 60.0, OLON = 126.0, OLAT = 38.0;
        const double XO = 43, YO = 136, DEG = Math.PI / 180.0;
        double re = RE / GRID, slat1 = SLAT1 * DEG, slat2 = SLAT2 * DEG, olon = OLON * DEG, olat = OLAT * DEG;
        double sn = Math.Log(Math.Cos(slat1) / Math.Cos(slat2)) /
                    Math.Log(Math.Tan(Math.PI * 0.25 + slat2 * 0.5) / Math.Tan(Math.PI * 0.25 + slat1 * 0.5));
        double sf = Math.Pow(Math.Tan(Math.PI * 0.25 + slat1 * 0.5), sn) * Math.Cos(slat1) / sn;
        double ro = re * sf / Math.Pow(Math.Tan(Math.PI * 0.25 + olat * 0.5), sn);
        double ra = re * sf / Math.Pow(Math.Tan(Math.PI * 0.25 + lat * DEG * 0.5), sn);
        double theta = lon * DEG - olon;
        if (theta > Math.PI) theta -= 2 * Math.PI;
        if (theta < -Math.PI) theta += 2 * Math.PI;
        theta *= sn;
        return ((int)Math.Floor(ra * Math.Sin(theta) + XO + 0.5), (int)Math.Floor(ro - ra * Math.Cos(theta) + YO + 0.5));
    }

    /// <summary>초단기실황: 매시 정각 자료가 40분쯤 나온다.</summary>
    async Task<int?> FetchPtyAsync(DateTime kst, CancellationToken ct)
    {
        DateTime b = kst.Minute < 40 ? kst.AddHours(-1) : kst;
        using JsonDocument doc = await GetAsync("getUltraSrtNcst", b.ToString("yyyyMMdd"), b.ToString("HH") + "00", ct);
        foreach (JsonElement item in Items(doc))
            if (item.GetProperty("category").GetString() == "PTY" && int.TryParse(item.GetProperty("obsrValue").GetString(), out int v))
                return v;
        return null;
    }

    /// <summary>초단기예보: 매시 30분 자료가 45분쯤 나온다. 가장 가까운 시각의 SKY.</summary>
    async Task<int?> FetchSkyAsync(DateTime kst, CancellationToken ct)
    {
        DateTime b = kst.Minute < 45 ? kst.AddHours(-1) : kst;
        using JsonDocument doc = await GetAsync("getUltraSrtFcst", b.ToString("yyyyMMdd"), b.ToString("HH") + "30", ct);
        string? best = null;
        int? sky = null;
        foreach (JsonElement item in Items(doc))
        {
            if (item.GetProperty("category").GetString() != "SKY")
                continue;
            string when = item.GetProperty("fcstDate").GetString() + item.GetProperty("fcstTime").GetString();
            if ((best == null || string.CompareOrdinal(when, best) < 0) && int.TryParse(item.GetProperty("fcstValue").GetString(), out int v))
            {
                best = when;
                sky = v;
            }
        }
        return sky;
    }

    async Task<JsonDocument> GetAsync(string operation, string baseDate, string baseTime, CancellationToken ct)
    {
        string serviceKey = key!.Contains('%') ? key : Uri.EscapeDataString(key);
        string url = $"{BaseUrl}/{operation}?serviceKey={serviceKey}&pageNo=1&numOfRows=100&dataType=JSON" +
                     $"&base_date={baseDate}&base_time={baseTime}&nx={Nx}&ny={Ny}";
        HttpClient client = http.CreateClient("kma");
        client.Timeout = TimeSpan.FromSeconds(10);
        string body = await client.GetStringAsync(url, ct);
        // 키 오류 등은 JSON 이 아닌 XML 로 온다 → JsonException
        JsonDocument doc = JsonDocument.Parse(body);
        string? code = doc.RootElement.GetProperty("response").GetProperty("header").GetProperty("resultCode").GetString();
        if (code != "00")
        {
            doc.Dispose();
            throw new InvalidOperationException($"기상청 응답 코드 {code}");
        }
        return doc;
    }

    static IEnumerable<JsonElement> Items(JsonDocument doc) =>
        doc.RootElement.GetProperty("response").GetProperty("body").GetProperty("items").GetProperty("item").EnumerateArray();
}
