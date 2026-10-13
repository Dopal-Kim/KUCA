using System.Text;
using System.Text.Json;
using KucaMemoServer.Models;

namespace KucaMemoServer.Services;

/// <summary>
/// 산책 코스 추천. 챗쿠 AI(IRouteAi)가 설정돼 있으면 AI 에게 후보 장소 목록을 주고 고르게 하고,
/// 키가 없거나 AI 가 실패·이상한 답을 주면 가까운 곳부터 고르는 규칙으로 대신한다 (Source = "fallback").
/// 후보는 앱이 보낸 경희스팟 ID(candidateBuildingIds), 없으면 지금 위치 근처의 이름 있는 건물.
/// </summary>
public class RouteRecommender(BuildingStore buildings, IRouteAi ai, ILogger<RouteRecommender> log)
{
    public static readonly int[] AllowedMinutes = [15, 30, 60];
    static readonly string[] AllowedTastes = ["photo", "quiet", "food", "unvisited"];
    const double WalkMetersPerMinute = 70;
    const string MainGate = "way-473963422"; // 캠퍼스 밖에 있으면 정문에서 출발하는 것으로 본다

    static readonly Dictionary<string, string> TasteNames = new()
    {
        ["photo"] = "사진 찍기 좋은 명소", ["quiet"] = "조용히 쉬거나 공부하기 좋은 곳",
        ["food"] = "밥·카페", ["unvisited"] = "아직 가 보지 않은 곳",
    };

    public static string? Validate(RecommendRequest r)
    {
        if (!AllowedMinutes.Contains(r.Minutes))
            return "minutes 는 15, 30, 60 중 하나여야 합니다.";
        if (r.Tastes?.Any(t => !AllowedTastes.Contains(t)) == true)
            return "tastes 는 photo, quiet, food, unvisited 만 쓸 수 있습니다.";
        if (r.Lat is < -90 or > 90 || r.Lon is < -180 or > 180)
            return "lat, lon 이 올바르지 않습니다.";
        return null;
    }

    record Candidate(Building Building, double Meters, bool Visited);

    public async Task<RecommendResult> RecommendAsync(RecommendRequest r, CancellationToken ct)
    {
        var visited = new HashSet<string>(r.VisitedBuildingIds ?? []);
        var tastes = (r.Tastes ?? []).Distinct().ToList();
        (double lat, double lon) = StartPoint(r);
        int maxStops = r.Minutes <= 15 ? 2 : r.Minutes <= 30 ? 3 : 5;

        List<Candidate> candidates = Candidates(r, lat, lon, visited);
        if (candidates.Count == 0)
            return new RecommendResult($"{r.Minutes}분 산책 코스", "fallback", []);

        if (ai.IsConfigured)
        {
            RecommendResult? fromAi = await AskAiAsync(r.Minutes, tastes, maxStops, lat, lon, candidates, ct);
            if (fromAi != null)
                return fromAi;
        }
        return Fallback(r.Minutes, tastes, maxStops, lat, lon, candidates);
    }

    (double lat, double lon) StartPoint(RecommendRequest r)
    {
        // 위치가 없거나 캠퍼스에서 3km 넘게 떨어져 있으면 정문에서 출발
        Building? gate = buildings.Find(MainGate);
        if (gate != null && (r.Lat == 0 && r.Lon == 0 || Meters(r.Lat, r.Lon, gate.Lat, gate.Lon) > 3000))
            return (gate.Lat, gate.Lon);
        return (r.Lat, r.Lon);
    }

    List<Candidate> Candidates(RecommendRequest r, double lat, double lon, HashSet<string> visited)
    {
        IEnumerable<Building> pool = r.CandidateBuildingIds is { Count: > 0 } ids
            ? ids.Distinct().Select(buildings.Find).OfType<Building>()
            : buildings.All.Where(b => b.Name != null && Meters(lat, lon, b.Lat, b.Lon) < 700);
        return pool.Where(b => b.Name != null)
                   .Select(b => new Candidate(b, Meters(lat, lon, b.Lat, b.Lon), visited.Contains(b.Id)))
                   .OrderBy(c => c.Meters)
                   .Take(30)
                   .ToList();
    }

    async Task<RecommendResult?> AskAiAsync(int minutes, List<string> tastes, int maxStops, double lat, double lon,
        List<Candidate> candidates, CancellationToken ct)
    {
        const string system =
            "너는 경희대학교 국제캠퍼스 산책 코스를 짜 주는 안내원이야. 주어진 후보 장소 안에서만 고르고, " +
            "걸어서 정해진 시간 안에 돌 수 있게 가까운 곳끼리 이어지는 순서로 골라. " +
            "반드시 JSON 하나만 답해: {\"title\": \"코스 이름(20자 이내)\", \"stops\": [{\"buildingId\": \"후보의 id\", \"reason\": \"추천 이유 한 문장(40자 이내, 존댓말)\"}]}";
        var user = new StringBuilder();
        user.AppendLine($"걸을 시간: {minutes}분 (들를 곳 최대 {maxStops}곳, 걷는 속도 분당 약 70m)");
        user.AppendLine("하고 싶은 것: " + (tastes.Count > 0 ? string.Join(", ", tastes.Select(t => TasteNames[t])) : "상관없음"));
        user.AppendLine("후보 장소 (id | 이름 | 지금 위치에서 거리 | 가 본 적 있는지):");
        foreach (Candidate c in candidates)
            user.AppendLine($"{c.Building.Id} | {c.Building.Name} | {c.Meters:F0}m | {(c.Visited ? "가 봄" : "안 가 봄")}");

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
            var byId = candidates.ToDictionary(c => c.Building.Id);
            var stops = new List<RecommendStop>();
            double atLat = lat, atLon = lon;
            foreach (JsonElement s in doc.RootElement.GetProperty("stops").EnumerateArray())
            {
                string id = s.GetProperty("buildingId").GetString() ?? "";
                if (!byId.TryGetValue(id, out Candidate? c) || stops.Any(x => x.BuildingId == id))
                    continue; // 후보에 없는 곳·중복은 버린다
                string reason = s.TryGetProperty("reason", out JsonElement re) ? re.GetString() ?? "" : "";
                stops.Add(Stop(c.Building, reason.Length > 0 ? reason : "가는 길에 들르기 좋아요", atLat, atLon));
                (atLat, atLon) = (c.Building.Lat, c.Building.Lon);
                if (stops.Count == maxStops) break;
            }
            if (stops.Count == 0)
                throw new JsonException("고른 장소가 없음");
            string title = doc.RootElement.TryGetProperty("title", out JsonElement t) && t.GetString() is { Length: > 0 } tt
                ? tt : $"{minutes}분 산책 코스";
            return new RecommendResult(title, "ai", stops);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            log.LogWarning("챗쿠 답을 읽지 못해 규칙 추천으로 대신합니다: {Message}", e.Message);
            return null;
        }
    }

    RecommendResult Fallback(int minutes, List<string> tastes, int maxStops, double lat, double lon, List<Candidate> candidates)
    {
        bool preferNew = tastes.Contains("unvisited");
        double budget = minutes * WalkMetersPerMinute * 0.75;
        var left = new List<Candidate>(candidates);
        var stops = new List<RecommendStop>();
        double atLat = lat, atLon = lon, walked = 0;
        while (stops.Count < maxStops && left.Count > 0)
        {
            Candidate? best = null;
            double bestScore = double.MinValue, bestDist = 0;
            foreach (Candidate c in left)
            {
                double d = Meters(atLat, atLon, c.Building.Lat, c.Building.Lon);
                if (walked + d > budget && stops.Count > 0) continue;
                double score = -d / 150 + (preferNew && !c.Visited ? 2 : 0);
                if (score > bestScore) (best, bestScore, bestDist) = (c, score, d);
            }
            if (best == null) break;
            left.Remove(best);
            walked += bestDist;
            stops.Add(Stop(best.Building, preferNew && !best.Visited ? "아직 가 보지 않은 곳이에요" : "가는 길에 들르기 좋아요", atLat, atLon));
            (atLat, atLon) = (best.Building.Lat, best.Building.Lon);
        }
        return new RecommendResult($"{minutes}분 산책 코스", "fallback", stops);
    }

    static RecommendStop Stop(Building b, string reason, double fromLat, double fromLon) =>
        new(b.Id, b.Name ?? b.Id, reason, Math.Max(1, (int)Math.Ceiling(Meters(fromLat, fromLon, b.Lat, b.Lon) / WalkMetersPerMinute)), b.Lat, b.Lon);

    /// <summary>두 위경도 사이 거리 (m)</summary>
    static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
