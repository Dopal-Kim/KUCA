using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KucaMemoServer.Models;

namespace KucaMemoServer.Services;

/// <summary>
/// 추천 v2 의 숫자는 모두 여기 둔다 (docs/api.md 의 "POST /api/recommendations (v2)").
/// [가정] 표시는 아직 팀이 정하지 않은 제안값이다.
/// </summary>
public static class RecommendRules
{
    public static readonly int[] AllowedMinutes = [15, 30, 60];
    public static readonly string[] AllowedTastes = ["photo", "quiet", "food", "unvisited", "partner", "exercise"];
    public static readonly string[] AllowedPaces = ["slow", "normal", "fast"];

    // 점수 = 취향 × 3 + 계절·시간 × 2 + 가까움 × 1 (라운드 문서 2-2)
    public const double TasteWeight = 3;
    public const double ContextWeight = 2;
    public const double NearWeight = 1;
    /// <summary>[가정] 비·눈이 오면 실내 스팟에 더하는 점수</summary>
    public const double IndoorOnBadWeather = 1;
    /// <summary>[가정] 이 거리(m)에서 가까움 점수가 0 이 된다</summary>
    public const double NearRangeM = 1000;
    /// <summary>[가정] 식후 걷기는 가까움을 이만큼 더 친다</summary>
    public const double AfterMealNearFactor = 2;
    /// <summary>[가정] 코스를 이을 때 다음 장소까지 이 거리(m)마다 점수 1 을 깎는다 (먼 곳으로 튀지 않게)</summary>
    public const double LegPenaltyM = 300;

    /// <summary>[가정] 걷는 속도 (m/분): slow · normal · fast</summary>
    public static int MetersPerMinute(string pace) => pace switch { "slow" => 60, "fast" => 90, _ => 75 };
    /// <summary>[가정] 직선거리 → 실제 걷는 거리 (길이 돌아가는 만큼)</summary>
    public const double PathFactor = 1.3;
    /// <summary>[가정] 걷는 시간은 고른 시간의 이 비율까지 (나머지는 둘러보는 시간)</summary>
    public const double WalkTimeRatio = 0.8;
    /// <summary>들를 곳 최대 개수: 15분 2 · 30분 3 · 60분 5</summary>
    public static int MaxStops(int minutes) => minutes <= 15 ? 2 : minutes <= 30 ? 3 : 5;
    /// <summary>[가정] AI 에게 주는 후보 수 (점수 상위)</summary>
    public const int AiCandidates = 10;
    /// <summary>위치 근처 후보 수 (가까운 순)</summary>
    public const int NearbyCandidates = 30;
    public const int MaxReasonLength = 60;
    /// <summary>AI 에게 부탁하는 이유 길이 (넘쳐도 MaxReasonLength 까지는 그대로 쓴다)</summary>
    public const int AskReasonLength = 40;

    /// <summary>
    /// 근거 (A-V2): Reynolds 외, Diabetologia 2016 (식사마다 10분 걷기 → 식후 혈당 평균 12% 낮음, 2형 당뇨 41명),
    /// Buffey 외, Sports Medicine 2022 (앉아 있는 중간 가벼운 걷기 → 식후 혈당이 오래 앉아 있을 때보다 낮음, 연구 7개 메타분석).
    /// 대상과 수치가 학생에게 그대로 맞지 않아 숫자는 쓰지 않는다.
    /// </summary>
    public const string AfterMealNote =
        "식사 뒤 가볍게 걸으면 식후 혈당이 완만하게 오르는 데 도움이 된다는 연구가 있어요. 일반 건강 정보이며 의료 조언이 아닙니다.";

    /// <summary>시간대 (앱 취향과 같은 기준, 한국 시각): 아침 6–11 · 낮 11–17 · 저녁 17–20 · 밤</summary>
    public static string TimeOfDay(int kstHour) => kstHour switch
    {
        >= 6 and < 11 => "morning",
        >= 11 and < 17 => "day",
        >= 17 and < 20 => "evening",
        _ => "night",
    };

    /// <summary>계절: 3–5 봄 · 6–8 여름 · 9–11 가을 · 12–2 겨울</summary>
    public static string Season(int month) => month switch
    {
        >= 3 and <= 5 => "spring",
        >= 6 and <= 8 => "summer",
        >= 9 and <= 11 => "autumn",
        _ => "winter",
    };
}

/// <summary>
/// 산책 코스 추천 v2.
/// 1) 서버가 지금 시간대·계절·날씨를 정한다.
/// 2) 후보(앱이 보낸 ID, 없으면 경희스팟 25곳)마다 점수를 매긴다: 취향 × 3 + 계절·시간 × 2 + 가까움 × 1.
/// 3) 챗쿠 AI 가 있으면 점수 상위 후보 안에서 순서와 이유를 받는다. 이유가 소개글·상황과 맞지 않으면 규칙 문구로 바꾼다.
/// 4) AI 가 없거나 실패하면 점수와 거리로 규칙 코스를 만든다 (Source = "fallback").
/// 어느 쪽이든 걷는 시간이 고른 시간을 넘지 않게 자른다.
/// </summary>
public partial class RouteRecommender(
    BuildingStore buildings, SpotStore spots, IRouteAi ai, ICampusWeather weather,
    RecommendationStore saved, MetricsStore metrics, TimeProvider clock, ILogger<RouteRecommender> log)
{
    const string MainGate = "way-473963422"; // 캠퍼스 밖에 있으면 정문에서 출발하는 것으로 본다

    static readonly Dictionary<string, string> TasteNames = new()
    {
        ["photo"] = "사진 찍기 좋은 명소", ["quiet"] = "조용히 쉬거나 공부하기 좋은 곳",
        ["food"] = "밥·카페", ["unvisited"] = "아직 가 보지 않은 곳",
        ["partner"] = "파트너 경희몬과 함께 걷기", ["exercise"] = "운동 삼아 걷기",
    };
    static readonly Dictionary<string, string> Korean = new()
    {
        ["morning"] = "아침", ["day"] = "낮", ["evening"] = "저녁", ["night"] = "밤",
        ["spring"] = "봄", ["summer"] = "여름", ["autumn"] = "가을", ["winter"] = "겨울",
        ["clear"] = "맑음", ["cloudy"] = "흐림", ["rain"] = "비", ["snow"] = "눈",
    };

    public static string? Validate(RecommendRequest r)
    {
        if (!RecommendRules.AllowedMinutes.Contains(r.Minutes))
            return "minutes 는 15, 30, 60 중 하나여야 합니다.";
        if (r.Tastes?.Any(t => !RecommendRules.AllowedTastes.Contains(t)) == true)
            return "tastes 는 " + string.Join(", ", RecommendRules.AllowedTastes) + " 만 쓸 수 있습니다.";
        if (r.Pace != null && !RecommendRules.AllowedPaces.Contains(r.Pace))
            return "pace 는 slow, normal, fast 중 하나여야 합니다.";
        if (r.Lat is < -90 or > 90 || r.Lon is < -180 or > 180)
            return "lat, lon 이 올바르지 않습니다.";
        return null;
    }

    record Candidate(Building Building, Spot? Spot, double Meters, bool Visited, double Score);

    /// <summary>pace 는 엔드포인트가 정해서 넣는다 (요청 → 계정 취향 → normal)</summary>
    public async Task<RecommendResult> RecommendAsync(RecommendRequest r, string pace, CancellationToken ct)
    {
        DateTimeOffset now = clock.GetUtcNow();
        DateTimeOffset kst = now.ToOffset(TimeSpan.FromHours(9));
        var context = new RecommendContext(RecommendRules.TimeOfDay(kst.Hour), RecommendRules.Season(kst.Month),
                                           await weather.NowAsync(ct));

        var visited = new HashSet<string>(r.VisitedBuildingIds ?? []);
        var tastes = (r.Tastes ?? []).Distinct().ToList();
        (double lat, double lon) = StartPoint(r);
        var plan = new Plan(r.Minutes, pace, r.AfterMeal, lat, lon, tastes, context);

        List<Candidate> candidates = Candidates(r, plan, visited);
        string source = "fallback";
        string? title = null;
        List<RecommendStop>? stops = null;

        if (candidates.Count > 0 && ai.IsConfigured)
        {
            var fromAi = await AskAiAsync(plan, candidates, ct);
            if (fromAi != null)
                (title, stops, source) = (fromAi.Value.Title, fromAi.Value.Stops, "ai");
        }
        stops ??= Fallback(plan, candidates);
        title ??= r.AfterMeal ? $"식후 {r.Minutes}분 산책" : $"{Korean[context.Season]} {Korean[context.TimeOfDay]} {r.Minutes}분 산책";

        var result = new RecommendResult(
            "rec_" + Guid.NewGuid().ToString("N"), title, source, context, stops,
            stops.Sum(s => s.DistanceM), stops.Sum(s => s.WalkMinutes),
            r.AfterMeal ? RecommendRules.AfterMealNote : null);
        saved.Add(new RecommendationStore.Saved(result.Id, source, r.Minutes,
            stops.Select(s => s.BuildingId).ToArray(), result.TotalDistanceM, now));
        metrics.Record(MetricsStore.RecShown, source);
        metrics.Record(MetricsStore.AiCall, "recommend", source == "ai" ? 1 : 0);
        return result;
    }

    record Plan(int Minutes, string Pace, bool AfterMeal, double Lat, double Lon, List<string> Tastes, RecommendContext Context)
    {
        public int Speed => RecommendRules.MetersPerMinute(Pace);
        public double WalkBudgetMinutes => Minutes * RecommendRules.WalkTimeRatio;
        public int MaxStops => RecommendRules.MaxStops(Minutes);
    }

    (double lat, double lon) StartPoint(RecommendRequest r)
    {
        // 위치가 없거나 캠퍼스에서 3km 넘게 떨어져 있으면 정문에서 출발
        Building? gate = buildings.Find(MainGate);
        if (gate != null && (r.Lat == 0 && r.Lon == 0 || Meters(r.Lat, r.Lon, gate.Lat, gate.Lon) > 3000))
            return (gate.Lat, gate.Lon);
        return (r.Lat, r.Lon);
    }

    List<Candidate> Candidates(RecommendRequest r, Plan plan, HashSet<string> visited)
    {
        IEnumerable<Building> pool = r.CandidateBuildingIds is { Count: > 0 } ids
            ? ids.Distinct().Select(buildings.Find).OfType<Building>()
            : spots.All.Select(s => buildings.Find(s.Id)).OfType<Building>();
        return pool.Where(b => b.Name != null && spots.Find(b.Id)?.Restricted != true)
                   .Select(b => (b, d: Meters(plan.Lat, plan.Lon, b.Lat, b.Lon)))
                   .OrderBy(x => x.d)
                   .Take(RecommendRules.NearbyCandidates)
                   .Select(x =>
                   {
                       Spot? spot = spots.Find(x.b.Id);
                       bool wasVisited = visited.Contains(x.b.Id);
                       return new Candidate(x.b, spot, x.d, wasVisited, Score(spot, x.d, wasVisited, plan));
                   })
                   .ToList();
    }

    /// <summary>취향 × 3 + 계절·시간 × 2 + 가까움 × 1 (+ 비·눈일 때 실내)</summary>
    static double Score(Spot? spot, double meters, bool visited, Plan plan)
    {
        double taste = plan.Tastes.Count(t => t == "unvisited" ? !visited : spot?.Tastes?.Contains(t) == true);
        double context = (spot?.Seasons?.Contains(plan.Context.Season) == true ? 1 : 0)
                       + (spot?.Times?.Contains(plan.Context.TimeOfDay) == true ? 1 : 0);
        double near = Math.Max(0, 1 - meters / RecommendRules.NearRangeM)
                    * (plan.AfterMeal ? RecommendRules.AfterMealNearFactor : 1);
        double weatherBonus = plan.Context.Weather is "rain" or "snow" && spot?.Indoor == true ? RecommendRules.IndoorOnBadWeather : 0;
        return RecommendRules.TasteWeight * taste + RecommendRules.ContextWeight * context
             + RecommendRules.NearWeight * near + weatherBonus;
    }

    async Task<(string Title, List<RecommendStop> Stops)?> AskAiAsync(Plan plan, List<Candidate> candidates, CancellationToken ct)
    {
        List<Candidate> top = candidates.OrderByDescending(c => c.Score).Take(RecommendRules.AiCandidates).ToList();
        RecommendContext cx = plan.Context;
        string system =
            "너는 경희대학교 국제캠퍼스 산책 코스를 짜 주는 안내원이야. 주어진 후보 장소 안에서만 고르고, " +
            "걸어서 정해진 시간 안에 돌 수 있게 가까운 곳끼리 이어지는 순서로 골라. 점수가 높을수록 지금 상황에 잘 맞는 곳이야. " +
            "추천 이유는 그 장소의 소개, 지금 시간대·계절·날씨에 있는 내용만 써. 소개에 없는 사실(메뉴, 행사, 전망 등)을 지어내지 마. " +
            "산책하는 학생이 건물 앞을 지나가는 거야. '누구나 들어감'이 아닌 곳은 건물 안에 들어가라고 하지 마 " +
            "(강의실·실습실·작업실·공방·전시실·연구실·몇 층 등 건물 안 이야기 금지). 대신 건물 앞·근처를 지나며 할 수 있는 것을 짧게 써. " +
            $"이유는 {RecommendRules.AskReasonLength}자 안쪽 한 문장, 말끝은 '~요'로 (예: '~좋아요', '~있어요'). '~습니다'는 쓰지 마. " +
            $"반드시 JSON 하나만 답해: {{\"title\": \"코스 이름(20자 이내)\", \"stops\": [{{\"buildingId\": \"후보의 id\", \"reason\": \"추천 이유 한 문장({RecommendRules.AskReasonLength}자 이내, ~요)\"}}]}}";
        var user = new StringBuilder();
        user.AppendLine($"걸을 시간: {plan.Minutes}분 (들를 곳 최대 {plan.MaxStops}곳, 걷는 속도 분당 약 {plan.Speed}m)");
        user.AppendLine($"지금: {Korean[cx.Season]} {Korean[cx.TimeOfDay]}, 날씨 {(cx.Weather is { } w ? Korean.GetValueOrDefault(w, w) : "모름")}");
        if (plan.AfterMeal)
            user.AppendLine("식사 뒤 가볍게 걷는 산책이야. 가깝고 편한 곳 위주로.");
        user.AppendLine("하고 싶은 것: " + (plan.Tastes.Count > 0 ? string.Join(", ", plan.Tastes.Select(t => TasteNames[t])) : "상관없음"));
        user.AppendLine("후보 장소 (id | 이름 | 지금 위치에서 거리 | 가 본 적 | 실내 | 누구나 들어감 | 점수 | 소개):");
        foreach (Candidate c in top)
            user.AppendLine($"{c.Building.Id} | {c.Building.Name} | {c.Meters:F0}m | {(c.Visited ? "가 봄" : "안 가 봄")} | " +
                            $"{(c.Spot?.Indoor == true ? "실내" : "실외")} | {(c.Spot?.OpenInside == true ? "누구나 들어감" : "앞만 지나감")} | " +
                            $"{c.Score:F1} | {CleanIntro(c.Spot?.Intro)}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        string? answer = await ai.CompleteAsync(system, user.ToString(), timeout.Token);
        if (answer == null)
            return null;
        try
        {
            int start = answer.IndexOf('{'), end = answer.LastIndexOf('}');
            if (start < 0 || end <= start)
                throw new JsonException("JSON 이 없음");
            using JsonDocument doc = JsonDocument.Parse(answer[start..(end + 1)]);
            var byId = top.ToDictionary(c => c.Building.Id);
            var picked = new List<(Candidate C, string Reason)>();
            foreach (JsonElement s in doc.RootElement.GetProperty("stops").EnumerateArray())
            {
                string id = s.GetProperty("buildingId").GetString() ?? "";
                if (!byId.TryGetValue(id, out Candidate? c) || picked.Any(x => x.C == c))
                    continue; // 후보에 없는 곳·중복은 버린다
                string reason = s.TryGetProperty("reason", out JsonElement re) ? re.GetString()?.Trim() ?? "" : "";
                picked.Add((c, reason));
            }
            List<RecommendStop> stops = Walk(plan, picked, (c, reason) =>
            {
                reason = Polite(reason);
                return IsGrounded(reason, c, cx) && !GoesInside(reason, c) ? reason : RuleReason(c, plan);
            });
            if (stops.Count == 0)
                throw new JsonException("고른 장소가 없음");
            string title = doc.RootElement.TryGetProperty("title", out JsonElement t) && t.GetString() is { Length: > 0 } tt
                ? tt : $"{plan.Minutes}분 산책 코스";
            return (title, stops);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            log.LogWarning("챗쿠 답을 읽지 못해 규칙 추천으로 대신합니다: {Message}", e.Message);
            return null;
        }
    }

    /// <summary>점수가 높고 가까운 곳부터 잇는다. 걷는 시간이 예산을 넘는 곳은 건너뛴다.</summary>
    List<RecommendStop> Fallback(Plan plan, List<Candidate> candidates)
    {
        var left = new List<Candidate>(candidates);
        var order = new List<(Candidate C, string Reason)>();
        double atLat = plan.Lat, atLon = plan.Lon, minutes = 0;
        while (order.Count < plan.MaxStops && left.Count > 0)
        {
            Candidate? best = null;
            double bestValue = double.MinValue;
            foreach (Candidate c in left)
            {
                double d = Meters(atLat, atLon, c.Building.Lat, c.Building.Lon);
                if (minutes + LegMinutes(d, plan) > plan.WalkBudgetMinutes)
                    continue;
                double value = c.Score - d / RecommendRules.LegPenaltyM;
                if (value > bestValue) (best, bestValue) = (c, value);
            }
            // 시간 안에 닿는 곳이 하나도 없으면 가장 가까운 한 곳만
            if (best == null)
            {
                if (order.Count > 0) break;
                best = left.MinBy(c => c.Meters)!;
            }
            left.Remove(best);
            minutes += LegMinutes(Meters(atLat, atLon, best.Building.Lat, best.Building.Lon), plan);
            order.Add((best, ""));
            (atLat, atLon) = (best.Building.Lat, best.Building.Lon);
        }
        return Walk(plan, order, (c, _) => RuleReason(c, plan));
    }

    /// <summary>고른 순서대로 거리·시간을 붙인다. 걷는 시간 예산을 넘는 장소부터는 뺀다 (첫 곳은 남김).</summary>
    static List<RecommendStop> Walk(Plan plan, List<(Candidate C, string Reason)> order, Func<Candidate, string, string> reasonOf)
    {
        var stops = new List<RecommendStop>();
        double atLat = plan.Lat, atLon = plan.Lon, minutes = 0;
        foreach ((Candidate c, string reason) in order)
        {
            if (stops.Count == plan.MaxStops)
                break;
            double straight = Meters(atLat, atLon, c.Building.Lat, c.Building.Lon);
            int leg = LegMinutes(straight, plan);
            if (stops.Count > 0 && minutes + leg > plan.WalkBudgetMinutes)
                break;
            minutes += leg;
            stops.Add(new RecommendStop(c.Building.Id, c.Spot?.Name ?? c.Building.Name ?? c.Building.Id,
                Shorten(reasonOf(c, reason)), leg, (int)Math.Round(straight * RecommendRules.PathFactor),
                c.Building.Lat, c.Building.Lon));
            (atLat, atLon) = (c.Building.Lat, c.Building.Lon);
        }
        return stops;
    }

    static int LegMinutes(double straightMeters, Plan plan) =>
        Math.Max(1, (int)Math.Ceiling(straightMeters * RecommendRules.PathFactor / plan.Speed));

    /// <summary>AI 를 쓰지 않을 때의 이유: 날씨·안 가 본 곳·소개글 순으로 고른다.</summary>
    static string RuleReason(Candidate c, Plan plan)
    {
        string? weather = plan.Context.Weather;
        if (weather is "rain" or "snow" && c.Spot?.Indoor == true)
            return weather == "rain" ? "비가 와도 실내라 들르기 좋아요" : "눈이 와도 실내라 들르기 좋아요";
        if (plan.Tastes.Contains("unvisited") && !c.Visited)
            return "아직 가 보지 않은 곳이에요";
        string intro = CleanIntro(c.Spot?.Intro);
        // 소개에서 "~하기 좋다" 같은 추천 문장을 고른다. "출입 제한" 같은 주의 문장은 이유로 쓰지 않는다
        // 누구나 들어가는 곳이 아니면 건물 안 이야기 문장도 쓰지 않는다 (실사용자 피드백 10/24)
        string? pick = intro.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(sentence => !CautionWords.Any(sentence.Contains) && !GoesInside(sentence, c))
            .FirstOrDefault(sentence => sentence.Contains("좋"));
        if (pick != null)
            return Polite(pick); // 화면 말투(~요)에 맞춤
        return "가는 길에 들르기 좋아요";
    }

    /// <summary>
    /// 건물 안으로 들어가라는 이유인지 (실사용자 피드백 10/24: 다른 단과대 건물 안에 들어가 사진을 찍으라는 이유가 부담스럽다).
    /// 누구나 들어가 쉬는 곳 (spots.json openInside) 은 괜찮다.
    /// </summary>
    static bool GoesInside(string reason, Candidate c) =>
        c.Spot?.OpenInside != true && (InsideWords.Any(reason.Contains) || FloorWord().IsMatch(reason));

    /// <summary>건물 안 이야기에 나오는 말</summary>
    static readonly string[] InsideWords =
    [
        "안에서", "안으로", "안에 ", "들어가", "들어와", "실내에서", "내부",
        "강의실", "실습실", "작업실", "작업 공간", "공방", "전시실", "연구실", "연습실", "실험실", "동아리방", "열람실", "복도", "라운지",
    ];

    [GeneratedRegex(@"\d\s*층|지하")]
    private static partial Regex FloorWord();

    /// <summary>"~습니다" 같은 말끝을 화면 말투 "~요" 로 바꾼다 (자주 나오는 것만)</summary>
    public static string Polite(string s)
    {
        s = s.Trim();
        foreach ((string from, string to) in PoliteEndings)
            if (s.EndsWith(from + ".") || s.EndsWith(from))
                return s[..s.LastIndexOf(from, StringComparison.Ordinal)] + to;
        return s;
    }

    static readonly (string From, string To)[] PoliteEndings =
    [
        ("좋습니다", "좋아요"), ("있습니다", "있어요"), ("없습니다", "없어요"), ("됩니다", "돼요"), ("합니다", "해요"),
        ("드립니다", "드려요"), ("보입니다", "보여요"), ("어울립니다", "어울려요"),
        ("곳입니다", "곳이에요"), ("길입니다", "길이에요"), ("좋다", "좋아요"),
    ];

    /// <summary>이유로 쓰면 안 되는 주의 문장의 말</summary>
    static readonly string[] CautionWords = ["제한", "피하기", "주의", "들어갈 수는 없", "어두우니"];

    /// <summary>
    /// [가정] AI 이유가 근거 있는지 보는 간단한 검사: 이유의 한글 두 글자 조각 중 하나라도
    /// 장소 이름·소개, 지금 시간대·계절·날씨 말, 실내/실외에 나오면 통과. 흔한 말끝(좋아요 등)은 세지 않는다.
    /// </summary>
    static bool IsGrounded(string reason, Candidate c, RecommendContext cx)
    {
        if (reason.Length == 0)
            return false;
        var known = new StringBuilder();
        known.Append(c.Building.Name).Append(' ').Append(c.Spot?.Name).Append(' ')
             .Append(string.Join(' ', c.Spot?.Aliases ?? [])).Append(' ').Append(CleanIntro(c.Spot?.Intro)).Append(' ')
             .Append(Korean[cx.TimeOfDay]).Append(' ').Append(Korean[cx.Season]).Append(' ')
             .Append(cx.Weather is { } w ? Korean.GetValueOrDefault(w, "") : "").Append(' ')
             .Append(c.Spot?.Indoor == true ? "실내" : "야외 실외 바깥");
        string knownText = known.ToString();
        foreach (Match word in HangulWord().Matches(reason))
            for (int i = 0; i + 2 <= word.Value.Length; i++)
            {
                string bigram = word.Value.Substring(i, 2);
                if (!CommonBigrams.Contains(bigram) && knownText.Contains(bigram))
                    return true;
            }
        return false;
    }

    /// <summary>어느 장소 이유에나 나오는 말 조각 — 이것만 겹치면 근거로 치지 않는다</summary>
    static readonly HashSet<string> CommonBigrams =
    [
        "좋아", "아요", "좋은", "좋다", "해요", "에요", "이에", "예요", "있어", "어요", "있는", "있다", "하기", "기좋",
        "들르", "르기", "러요", "걷기", "산책", "곳이", "수있", "있게", "가기", "보기", "하는", "에서", "으로",
        "니다", "습니", "합니", "입니", "캠퍼", "퍼스",
    ];

    [GeneratedRegex(@"\p{IsHangulSyllables}+")]
    private static partial Regex HangulWord();

    [GeneratedRegex(@"\s*\[확인 필요[^\]]*\]")]
    private static partial Regex Unchecked();

    /// <summary>A 가 남긴 "[확인 필요]" 표시는 AI·화면에 보내지 않는다</summary>
    static string CleanIntro(string? intro) => intro == null ? "" : Unchecked().Replace(intro, "").Trim();

    static string Shorten(string s) => s.Length <= RecommendRules.MaxReasonLength ? s : s[..(RecommendRules.MaxReasonLength - 1)].TrimEnd() + "…";

    /// <summary>두 위경도 사이 직선거리 (m)</summary>
    static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
