namespace KucaMemoServer.Endpoints;

/// <summary>
/// 메모 API — 여러분이 채울 부분입니다.
/// 주소와 요청/응답 모양은 docs/api.md 를 그대로 따라야 앱과 연결됩니다.
/// 지금은 모두 501(아직 구현 안 됨)을 돌려줍니다.
/// </summary>
public static class MemoEndpoints
{
    public static void MapMemoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("Memos");

        // TODO(2단계): 건물의 메모 목록 (최신순)
        group.MapGet("/buildings/{buildingId}/memos", (string buildingId) =>
            Results.StatusCode(StatusCodes.Status501NotImplemented));

        // TODO(2단계 글만, 3단계 사진까지): 메모 작성. multipart/form-data 로 author, text, deviceId, photo(선택)를 받는다.
        group.MapPost("/buildings/{buildingId}/memos", (string buildingId) =>
            Results.StatusCode(StatusCodes.Status501NotImplemented))
            .DisableAntiforgery();

        // TODO(2단계): 메모 하나
        group.MapGet("/memos/{memoId}", (string memoId) =>
            Results.StatusCode(StatusCodes.Status501NotImplemented));

        // TODO(4단계): 메모 삭제. X-Device-Id 헤더가 작성자 기기와 같을 때만.
        group.MapDelete("/memos/{memoId}", (string memoId) =>
            Results.StatusCode(StatusCodes.Status501NotImplemented));
    }
}
