namespace KucaMemoServer.Models;

/// <summary>
/// 캠퍼스 건물 하나. 앱의 3D 건물과 같은 OpenStreetMap 데이터에서 만들었다.
/// </summary>
/// <param name="Id">건물 ID. "way-775364189"처럼 OSM 종류-번호. 앱과 서버가 같은 값을 쓴다.</param>
/// <param name="Name">건물 이름 (예: "중앙도서관"). 이름이 없는 건물은 null.</param>
/// <param name="Type">OSM building 태그 (예: "university", "dormitory"). 없으면 null.</param>
/// <param name="Lat">건물 중심 위도</param>
/// <param name="Lon">건물 중심 경도</param>
public record Building(string Id, string? Name, string? Type, double Lat, double Lon);
