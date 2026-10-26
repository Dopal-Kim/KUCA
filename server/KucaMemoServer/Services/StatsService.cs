namespace KucaMemoServer.Services;

/// <summary>
/// 관리자 집계 (C-V6, docs/api.md 의 "관리자 API › 지표"). MetricsStore 의 이벤트를 세어 비율을 낸다.
/// 비율은 분모가 0 이면 null. 소수 셋째 자리까지.
/// </summary>
public class StatsService(MetricsStore metrics, TimeProvider clock, IConfiguration config)
{
    public const int MaxDays = 90;

    public record Funnel(int Shown, int Started, int Finished, int Completed, double? AcceptRate, double? CompletionRate,
                         int Reviews, double? AvgCourseRating);
    public record WalkCounts(int Started, int Finished, int Completed, double? CompletionRate, int Reviews, double? AvgCourseRating);
    /// <param name="AcceptRateByReason">제안 화면의 매칭 이유가 AI 문장일 때 vs 틀 문장일 때 둘 다 수락한 비율</param>
    /// <param name="AcceptRateByPick">AI 가 고른 짝 vs 규칙이 고른 짝의 둘 다 수락한 비율</param>
    public record Matching(int Requests, int Proposed, int Met, int Walking, int Completed,
                           double? SuccessRate, double? CompletionRate, Dictionary<string, int> EndReasons,
                           Dictionary<string, double?> AcceptRateByReason, Dictionary<string, double?> AcceptRateByPick);
    public record AiUse(int Ai, int Fallback, double? FallbackRate);
    public record Stats(string From, string To, int Days,
                        Dictionary<string, Funnel> Recommendations, Dictionary<string, WalkCounts> Walks,
                        Matching Matching, Dictionary<string, AiUse> Ai);

    public Stats Get(int days)
    {
        DateTimeOffset to = clock.GetUtcNow(), from = to - TimeSpan.FromDays(days);
        List<MetricsStore.Row> rows = metrics.Since(from);
        int Count(string kind, string? source = null, int? value = null) =>
            rows.Count(r => r.Kind == kind && (source == null || r.Source == source) && (value == null || r.Value == value));
        double? Avg(string kind, string source) =>
            rows.Where(r => r.Kind == kind && r.Source == source && r.Value != null).Select(r => (double)r.Value!.Value)
                .DefaultIfEmpty(double.NaN).Average() is var a && double.IsNaN(a) ? null : Math.Round(a, 2);

        var recommendations = new[] { "ai", "fallback" }.ToDictionary(s => s, s =>
        {
            int shown = Count(MetricsStore.RecShown, s), started = Count(MetricsStore.WalkStarted, s);
            int finished = Count(MetricsStore.WalkFinished, s), completed = Count(MetricsStore.WalkFinished, s, 1);
            return new Funnel(shown, started, finished, completed, Rate(started, shown), Rate(completed, started),
                              Count(MetricsStore.Review, s), Avg(MetricsStore.Review, s));
        });

        var walks = new[] { "ai", "fallback", "free", "match" }.ToDictionary(s => s, s =>
        {
            int started = Count(MetricsStore.WalkStarted, s), completed = Count(MetricsStore.WalkFinished, s, 1);
            return new WalkCounts(started, Count(MetricsStore.WalkFinished, s), completed, Rate(completed, started),
                                  Count(MetricsStore.Review, s), Avg(MetricsStore.Review, s));
        });

        int requests = Count(MetricsStore.MatchRequest), met = Count(MetricsStore.MatchMet);
        int matchCompleted = Count(MetricsStore.MatchEnded, "completed");
        var endReasons = rows.Where(r => r.Kind == MetricsStore.MatchEnded && r.Source != null)
                             .GroupBy(r => r.Source!).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        var byReason = new[] { "ai", "fallback" }.ToDictionary(s => s, s =>
            Rate(Count(MetricsStore.MatchAnswer, s, 1), Count(MetricsStore.MatchAnswer, s)));
        var byPick = new[] { "ai", "rule" }.ToDictionary(s => s, s =>
            Rate(Count(MetricsStore.MatchPickAnswer, s, 1), Count(MetricsStore.MatchPickAnswer, s)));
        var matching = new Matching(requests, Count(MetricsStore.MatchProposed), met, Count(MetricsStore.MatchWalking),
                                    matchCompleted, Rate(met, requests), Rate(matchCompleted, met), endReasons, byReason, byPick);

        var ai = new[] { "recommend", "smalltalk", "moderation", "match_reason", "match_pick" }.ToDictionary(s => s, s =>
        {
            int yes = Count(MetricsStore.AiCall, s, 1), no = Count(MetricsStore.AiCall, s, 0);
            return new AiUse(yes, no, Rate(no, yes + no));
        });

        return new Stats(Iso(from), Iso(to), days, recommendations, walks, matching, ai);
    }

    /// <summary>운영 점검: 키가 들어 있는지(값은 안 보여 줌), 시연 설정, 최근 24시간 AI 대체 비율</summary>
    public object Health() => new
    {
        aiKey = Set("Ai:ApiKey") || Set("Recommend:ApiKey"),
        weatherKey = Set("Kma:ServiceKey"),
        kakaoAppId = Set("Kakao:AppId"),
        email = Set("Email:ConnectionString") || Set("Email:Password"),
        demoPartner = string.Equals(config["Match:DemoPartner"], "true", StringComparison.OrdinalIgnoreCase),
        devLoginOff = config["Auth:DevLogin"] != "true" && config["Email:DevEcho"] != "true",
        aiLast24h = Get(1).Ai,
    };

    bool Set(string key) => !string.IsNullOrWhiteSpace(config[key]);

    static double? Rate(int part, int whole) => whole == 0 ? null : Math.Round((double)part / whole, 3);

    static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
}
