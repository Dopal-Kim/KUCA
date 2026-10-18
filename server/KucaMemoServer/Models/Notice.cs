namespace KucaMemoServer.Models;

/// <summary>
/// 장소 공지 한 줄 (docs/api.md 의 PlaceNotice). 한 공지가 여러 경희스팟에 해당하면 장소마다 한 줄씩 나간다.
/// </summary>
public record PlaceNotice(string Id, string BuildingId, string BuildingName, string Title, string Summary, string Url, string PostedAt);

/// <summary>운영자가 붙여 넣는 공지 (POST /api/admin/notices 본문)</summary>
public record NewNotice(string? Title, string? Body, string? Url, string? PostedAt);

/// <summary>경희스팟 하나 (Data/spots.json). aliases 는 AI 가 안 될 때 이름 찾기에 쓰는 짧은 이름.</summary>
public record Spot(string Id, string Name, string[] Aliases);
