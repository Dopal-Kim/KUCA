namespace KucaMemoServer.Models;

// 산책 음악 추천 (docs/api.md 의 "POST /api/music"). JSON 은 camelCase 로 오간다.

/// <summary>
/// POST /api/music 본문. 모두 선택이다.
/// 코스 카드에서 부르면 CourseTitle · CourseWhy · Places 를 같이 보내 그 코스 분위기에 맞춘다.
/// </summary>
/// <param name="Minutes">걸을 시간 (분), 곡 수를 정하지는 않는다</param>
/// <param name="Tastes">산책 취향 코드 (photo · quiet · food · unvisited · partner · exercise)</param>
/// <param name="Pace">slow · normal · fast (없으면 계정 취향)</param>
/// <param name="Count">곡 수 3~8 (기본 5)</param>
public record MusicRequest(
    int? Minutes = null,
    List<string>? Tastes = null,
    string? Pace = null,
    string? CourseTitle = null,
    string? CourseWhy = null,
    List<string>? Places = null,
    int? Count = null);

/// <param name="Why">이 곡을 고른 이유 한 줄 (없을 수 있음)</param>
public record MusicTrack(string Title, string Artist, string? Why);

/// <param name="Source">"ai" 또는 "fallback" (AI 없이 분위기별 목록에서 고름)</param>
/// <param name="Mood">calm · bright · energetic · rainy · night</param>
/// <param name="Theme">목록 전체 한 줄 (예: "맑은 가을 오후, 가볍게 걷기 좋은 노래")</param>
public record MusicResult(string Source, string Mood, string Theme, List<MusicTrack> Tracks);
