namespace KucaMemoServer.Models;

// 실시간 산책 매칭 (docs/api.md 의 "매칭"). JSON 은 camelCase, 시각은 ISO 8601 UTC 문자열.

public record MatchJoinRequest(int Minutes, string? MatchGender, string? BuildingId);
public record AppearanceRequest(string? Text);
public record ArrivedRequest(string? BuildingId);
public record MatchCancelRequest(string? Reason);
public record ReportRequest(string? MatchId, string? Reason, string? Text);
public record BlockRequest(string? MatchId);

/// <summary>상대에게서 보이는 것은 닉네임 · 공통 취향 · 인상착의뿐이다 (성별 · 계정 · 건물은 안 보임)</summary>
public record MatchPartner(string Nickname, List<string> CommonTastes, string? Appearance);
public record MeetSpotView(string BuildingId, string Name, double Lat, double Lon, string? MeetSpecies);
public record MatchArrived(bool Me, bool Partner);
public record SmalltalkResult(List<string> Smalltalk, string Source);

/// <summary>
/// GET /api/match 가 돌려주는 지금 상태. State 에 따라 채워지는 필드가 다르다 (나머지는 null).
/// idle · waiting · proposed · meeting · walking · ended
/// </summary>
public record MatchState(
    string State,
    string? MatchId = null,
    int? Minutes = null,
    string? WaitingSince = null,
    string? WaitingUntil = null,
    MatchPartner? Partner = null,
    string? RespondBy = null,
    string? MyAnswer = null,
    MeetSpotView? MeetSpot = null,
    string? MeetBy = null,
    List<string>? Smalltalk = null,
    string? SmalltalkSource = null,
    string? MyAppearance = null,
    MatchArrived? Arrived = null,
    string? WalkId = null,
    string? EndReason = null,
    string? CancelledBy = null);
