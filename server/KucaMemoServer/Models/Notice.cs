namespace KucaMemoServer.Models;

/// <summary>
/// 장소 공지 한 줄 (docs/api.md 의 PlaceNotice). 한 공지가 여러 경희스팟에 해당하면 장소마다 한 줄씩 나간다.
/// </summary>
public record PlaceNotice(string Id, string BuildingId, string BuildingName, string Title, string Summary, string Url, string PostedAt);

/// <summary>운영자가 붙여 넣는 공지 (POST /api/admin/notices 본문)</summary>
public record NewNotice(string? Title, string? Body, string? Url, string? PostedAt);

/// <summary>
/// 경희스팟 하나 (Data/spots.json). aliases 는 AI 가 안 될 때 이름 찾기에 쓰는 짧은 이름.
/// 나머지는 A 가 채우는 추천·매칭용 정보 (docs/api.md 의 "경희스팟 정보"). 없으면 null/false.
/// </summary>
public record Spot(
    string Id,
    string Name,
    string[] Aliases,
    string? MeetSpecies = null,   // 만남 스팟이면 그 자리에 세울 경희몬 ID
    string? Intro = null,         // 2~3줄 소개. AI 추천 이유는 이 안의 사실만 쓴다
    string[]? Seasons = null,     // spring · summer · autumn · winter
    string[]? Times = null,       // morning · day · evening · night
    bool Indoor = false,
    string[]? Tastes = null,      // 어울리는 취향 코드 (photo · quiet · food · …)
    bool Restricted = false,      // 거주자 · 관계자만 드나드는 곳 (기숙사 등). 추천 후보에서 빠진다
    bool OpenInside = false,      // 누구나 들어가 쉬는 실내 (도서관 · 학생회관 · 라운지 · 체육관). 아니면 추천 이유에 건물 안 이야기를 쓰지 않는다
    bool Noisy = false,           // 찻길 옆이거나 늘 붐벼 걸으며 이야기하기 어려운 곳 (큰길 80 m 안 · 식당가)
    bool Hill = false);           // 가는 길에 힘든 오르막이 있는 곳 (사용자 표시 10/27: 천문대 · 선승관 · 국제대학)
