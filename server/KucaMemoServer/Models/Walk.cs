namespace KucaMemoServer.Models;

// 산책 · 완료 · 리뷰 · 보상 (docs/api.md 의 "산책 · 완료 · 리뷰 · 보상"). JSON 은 camelCase.

public record WalkStartRequest(string? RecommendationId, int? Minutes);
public record WalkStarted(string Id, string StartedAt, int? PlannedDistanceM, List<string> Stops);

public record WalkFinishRequest(int DistanceM, int DurationSec, List<string>? VisitedBuildingIds);
public record WalkReviewRequest(int CourseRating, int? PartnerRating, string? Text);

/// <param name="Kind">distance · walk · together · review</param>
public record XpItem(string Kind, int Xp);
public record WalkReward(int Tickets, int Xp, List<XpItem> Breakdown);

/// <summary>레벨 · 경험치. LevelXp 는 지금 레벨이 시작된 경험치, NextLevelXp 는 다음 레벨이 되는 경험치</summary>
public record Progress(int Level, int Xp, int LevelXp, int NextLevelXp, int TotalWalks, int TotalDistanceM, int TogetherWalks);

public record WalkFinished(bool Completed, int DistanceM, WalkReward Reward, Progress Progress);
public record ReviewSaved(WalkReward Reward, Progress Progress);
