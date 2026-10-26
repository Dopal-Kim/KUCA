namespace KucaMemoServer.Models;

// 산책 추천 (docs/api.md 의 "POST /api/recommendations (v2)"). JSON 은 camelCase 로 오간다.

/// <summary>POST /api/recommendations 본문. v1 필드만 보내도 된다 (Pace·AfterMeal 은 v2, 선택).</summary>
public record RecommendRequest(
    int Minutes,
    List<string>? Tastes,
    double Lat,
    double Lon,
    List<string>? VisitedBuildingIds,
    List<string>? CandidateBuildingIds,
    string? Pace = null,          // "slow" · "normal" · "fast" (없으면 계정 취향, 그것도 없으면 normal)
    bool AfterMeal = false,       // 식후 걷기 버튼으로 요청
    string? Wish = null);         // 말로 한 산책 바람 (200자 이내). 있으면 해석해서 시간 · 취향 · 피할 것에 반영

/// <param name="WalkMinutes">앞 장소(첫 곳은 지금 위치)에서 걸어서 몇 분</param>
/// <param name="DistanceM">앞 장소에서 걷는 거리 (m)</param>
public record RecommendStop(string BuildingId, string Name, string Reason, int WalkMinutes, int DistanceM, double Lat, double Lon);

/// <summary>서버가 판단한 지금 상황. Weather 는 GET /api/weather 의 condition, 모르면 null</summary>
public record RecommendContext(string TimeOfDay, string Season, string? Weather);

/// <summary>Source: "ai" (AI 가 고름) 또는 "fallback" (AI 없이 규칙으로 고름)</summary>
public record RecommendResult(
    string Id,
    string Title,
    string Source,
    RecommendContext Context,
    List<RecommendStop> Stops,
    int TotalDistanceM,
    int TotalMinutes,
    string? HealthNote,
    RecommendWish? Wish = null);

/// <summary>바람을 어떻게 이해했는지 (앱이 "이렇게 이해했어요" 로 보여 준다). Source: "ai" 또는 "fallback"</summary>
public record RecommendWish(string Summary, string Source, int? Minutes, List<string> Want, List<string> Avoid);
