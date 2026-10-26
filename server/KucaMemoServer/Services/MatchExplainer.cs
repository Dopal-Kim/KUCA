using System.Text.Json;

namespace KucaMemoServer.Services;

/// <summary>"왜 이 두 사람을 연결했는지" 한 줄. 실제 구현은 MatchExplainer, 테스트에서는 가짜로 바꿔 끼울 수 있다.</summary>
public interface IMatchExplainer
{
    Task<(string Text, string Source)> ExplainAsync(MatchReasonInput input, CancellationToken ct);
}

/// <summary>
/// AI 에게 주는 것은 두 사람이 겹치는 것뿐이다 (닉네임 · 성별 · 계정 · 위치는 보내지 않음).
/// </summary>
/// <param name="CommonTastes">겹치는 취향 코드 (photo · quiet …)</param>
/// <param name="CommonWalkTimes">겹치는 걷는 시간대 (morning · day · evening · night)</param>
/// <param name="SameTalkStyle">대화 스타일이 같으면 그 코드 (chatty · some · quiet), 다르면 null</param>
/// <param name="DistanceM">두 사람 건물 사이 거리 (m)</param>
/// <param name="Minutes">함께 걸을 시간 (30 · 60)</param>
public record MatchReasonInput(IReadOnlyList<string> CommonTastes, IReadOnlyList<string> CommonWalkTimes, string? SameTalkStyle, int DistanceM, int Minutes);

/// <summary>
/// 상대 제안 화면의 한 줄 이유를 챗쿠 AI(산책 추천과 같은 IRouteAi)로 쓴다. 짝을 정하는 건 여전히 서버 규칙이고,
/// AI 는 이미 정해진 짝을 사람 말투로 설명만 한다. 실패하면 겹치는 것으로 만든 틀 문장을 쓴다.
/// </summary>
public class MatchExplainer(IRouteAi ai, MetricsStore metrics, ILogger<MatchExplainer> log) : IMatchExplainer
{
    public const int MaxLength = 60;

    static readonly Dictionary<string, string> Tastes = new()
    {
        ["photo"] = "사진 찍기", ["quiet"] = "조용한 곳", ["food"] = "맛집·카페",
        ["unvisited"] = "새로운 곳 가 보기", ["partner"] = "경희몬과 걷기", ["exercise"] = "운동 삼아 걷기",
    };
    static readonly Dictionary<string, string> Times = new()
    {
        ["morning"] = "아침", ["day"] = "낮", ["evening"] = "저녁", ["night"] = "밤",
    };
    static readonly Dictionary<string, string> Talk = new()
    {
        ["chatty"] = "수다 떨며 걷기", ["some"] = "가끔 이야기하며 걷기", ["quiet"] = "조용히 함께 걷기",
    };

    public async Task<(string Text, string Source)> ExplainAsync(MatchReasonInput input, CancellationToken ct)
    {
        (string text, string source) = await ExplainCoreAsync(input, ct);
        metrics.Record(MetricsStore.AiCall, "match_reason", source == "ai" ? 1 : 0);
        return (text, source);
    }

    async Task<(string, string)> ExplainCoreAsync(MatchReasonInput input, CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return (Fallback(input), "fallback");

        string system =
            "너는 경희대학교 국제캠퍼스 산책 앱에서 처음 만나는 두 학생에게 '왜 둘을 연결했는지' 한 문장으로 알려 주는 안내원이야. " +
            "아래에 주어진 두 사람의 공통점만 써. 주어지지 않은 사실(전공, 나이, 외모, 성격 등)은 절대 지어내지 마. " +
            $"따뜻하고 설레는 존댓말 한 문장, {MaxLength}자 이내, 이모지는 쓰지 마. " +
            "반드시 JSON 하나만 답해: {\"reason\": \"…\"}";
        string user =
            $"공통 취향: {Names(input.CommonTastes, Tastes)}\n" +
            $"둘 다 즐겨 걷는 시간대: {Names(input.CommonWalkTimes, Times)}\n" +
            $"같은 대화 스타일: {(input.SameTalkStyle is { } t ? Talk.GetValueOrDefault(t, t) : "없음")}\n" +
            $"두 사람 사이 거리: 약 {input.DistanceM}m\n" +
            $"함께 걸을 시간: {input.Minutes}분";

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        if (Parse(await ai.CompleteAsync(system, user, timeout.Token)) is { } reason)
            return (reason, "ai");
        log.LogWarning("매칭 이유 AI 답을 쓰지 못해 틀 문장으로 대신합니다");
        return (Fallback(input), "fallback");
    }

    /// <summary>{"reason": "…"} 에서 1~60자 한 문장. 링크가 있거나 형식이 틀리면 null</summary>
    public static string? Parse(string? answer)
    {
        if (answer == null)
            return null;
        int start = answer.IndexOf('{'), end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(answer[start..(end + 1)]);
            string reason = doc.RootElement.GetProperty("reason").GetString()?.Trim() ?? "";
            return reason.Length is > 0 and <= MaxLength && !reason.Contains("http") ? reason : null;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>AI 를 못 쓸 때: 겹치는 것 중 가장 구체적인 것으로 틀 문장을 만든다</summary>
    public static string Fallback(MatchReasonInput input)
    {
        if (input.CommonTastes.Count > 0)
        {
            string liked = string.Join("·", input.CommonTastes.Take(2).Select(t => Tastes.GetValueOrDefault(t, t)));
            return $"둘 다 {liked}{Object(liked)} 좋아해서 연결했어요";
        }
        if (input.CommonWalkTimes.Count > 0)
            return $"둘 다 {Times.GetValueOrDefault(input.CommonWalkTimes[0], input.CommonWalkTimes[0])}에 걷는 걸 좋아해요";
        if (input.SameTalkStyle is { } t)
        {
            string style = Talk.GetValueOrDefault(t, t);
            return $"둘 다 {style}{Object(style)} 좋아해요";
        }
        if (input.DistanceM < 300)
            return "지금 가까이 있어서 금방 만날 수 있어요";
        return $"같은 {input.Minutes}분 산책을 찾고 있어서 연결했어요";
    }

    /// <summary>받침이 있으면 "을", 없으면 "를"</summary>
    static string Object(string word)
    {
        char last = word.Length > 0 ? word[^1] : '가';
        return last is >= '가' and <= '힣' && (last - '가') % 28 != 0 ? "을" : "를";
    }

    static string Names(IReadOnlyList<string> codes, Dictionary<string, string> names) =>
        codes.Count > 0 ? string.Join(", ", codes.Select(c => names.GetValueOrDefault(c, c))) : "없음";
}
