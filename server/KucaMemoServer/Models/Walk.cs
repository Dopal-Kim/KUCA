namespace KucaMemoServer.Models;

// 산책 · 완료 · 리뷰 · 보상 (docs/api.md 의 "산책 · 완료 · 리뷰 · 보상"). JSON 은 camelCase.

public record WalkStartRequest(string? RecommendationId, int? Minutes);
public record WalkStarted(string Id, string StartedAt, int? PlannedDistanceM, List<string> Stops);

/// <summary>WeightKg (선택): 칼로리 범위를 좁힌다. Steps (선택): 휴대폰 걸음 센서로 잰 이번 산책 걸음 수. 둘 다 서버에 저장하지 않는다</summary>
public record WalkFinishRequest(int DistanceM, int DurationSec, List<string>? VisitedBuildingIds, double? WeightKg = null, int? Steps = null);
/// <summary>리뷰는 코스 별점 · 한 줄만 받는다 (상대 별점은 받지 않음, 10/24 결정)</summary>
public record WalkReviewRequest(int CourseRating, string? Text);

/// <param name="Kind">distance · walk · together · review</param>
public record XpItem(string Kind, int Xp);
public record WalkReward(int Tickets, int Xp, List<XpItem> Breakdown);

/// <summary>레벨 · 경험치. LevelXp 는 지금 레벨이 시작된 경험치, NextLevelXp 는 다음 레벨이 되는 경험치</summary>
public record Progress(int Level, int Xp, int LevelXp, int NextLevelXp, int TotalWalks, int TotalDistanceM, int TogetherWalks,
                       WeekGoal? Week = null);   // 이번 주 건강 목표 (WHO 150분)

public record WalkFinished(bool Completed, int DistanceM, WalkReward Reward, Progress Progress, HealthEstimate? Health = null);
public record ReviewSaved(WalkReward Reward, Progress Progress);
