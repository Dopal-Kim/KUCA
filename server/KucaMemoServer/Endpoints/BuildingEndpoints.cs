using KucaMemoServer.Services;

namespace KucaMemoServer.Endpoints;

/// <summary>
/// 건물 API. 이미 완성된 예시이니 메모 API를 만들 때 이 모양을 참고하세요.
/// </summary>
public static class BuildingEndpoints
{
    public static void MapBuildingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/buildings").WithTags("Buildings");

        // 건물 목록. ?named=true 면 이름 있는 건물만.
        group.MapGet("/", (BuildingStore store, bool? named) =>
        {
            var list = named == true ? store.All.Where(b => b.Name != null) : store.All;
            return Results.Ok(list);
        });

        // 건물 하나. 없는 ID면 404.
        group.MapGet("/{buildingId}", (string buildingId, BuildingStore store) =>
        {
            var building = store.Find(buildingId);
            return building is null
                ? Results.NotFound(new { error = $"건물을 찾을 수 없습니다: {buildingId}" })
                : Results.Ok(building);
        });
    }
}
