namespace KucaMemoServer.Models;

// 산책 추천 (docs/api.md 의 "산책 추천"). JSON 은 camelCase 로 오간다.

/// <summary>POST /api/recommendations 본문</summary>
public record RecommendRequest(
    int Minutes,
    List<string>? Tastes,
    double Lat,
    double Lon,
    List<string>? VisitedBuildingIds,
    List<string>? CandidateBuildingIds);

public record RecommendStop(string BuildingId, string Name, string Reason, int WalkMinutes, double Lat, double Lon);

/// <summary>Source: "ai" (AI 가 고름) 또는 "fallback" (AI 없이 규칙으로 고름)</summary>
public record RecommendResult(string Title, string Source, List<RecommendStop> Stops);
