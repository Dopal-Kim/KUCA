using System.Text.Json;

namespace KucaMemoServer.Services;

/// <summary>스몰토크 주제를 만든다. 실제 구현은 SmalltalkGenerator, 테스트에서는 가짜로 바꿔 끼울 수 있다.</summary>
public interface ISmalltalkGenerator
{
    /// <summary>주제 카드 3장. AI 가 안 되면 A 의 예시(Data/smalltalk.json)에서 고른다.</summary>
    Task<(List<SmalltalkCard> Cards, string Source)> GenerateAsync(SmalltalkInput input, CancellationToken ct);

    /// <summary>AI 없이 예시에서 바로 고른다 (상태 전환을 기다리게 하지 않으려고)</summary>
    List<SmalltalkCard> Fallback(IEnumerable<string>? exclude = null);
}

/// <summary>
/// 스몰토크 카드 한 장. 앱이 대화 카드처럼 꾸밀 수 있게 분류와 이모지를 붙인다.
/// Topic: 공강 · 학식 · 전공 · 캠퍼스 장소 · 계절 · 날씨 · 취향 · 대화(그 밖)
/// </summary>
public record SmalltalkCard(string Text, string Topic, string Emoji)
{
    static readonly Dictionary<string, string> Emojis = new()
    {
        ["공강"] = "☕", ["학식"] = "🍚", ["전공"] = "📚", ["캠퍼스 장소"] = "🏛️",
        ["계절"] = "🍂", ["날씨"] = "🌤️", ["취향"] = "✨", ["대화"] = "💬",
    };

    public static IReadOnlyCollection<string> Topics => Emojis.Keys;

    public static SmalltalkCard Of(string text, string? topic)
    {
        string t = topic != null && Emojis.ContainsKey(topic) ? topic : "대화";
        return new SmalltalkCard(text, t, Emojis[t]);
    }
}

/// <param name="CommonTastes">두 사람이 겹치는 취향 코드</param>
/// <param name="SpotName">만남 스팟 이름</param>
/// <param name="SpotIntro">만남 스팟 소개</param>
/// <param name="Weather">clear · cloudy · rain · snow, 모르면 null</param>
/// <param name="Already">이미 보여 준 주제 (겹치지 않게)</param>
public record SmalltalkInput(IReadOnlyList<string> CommonTastes, string SpotName, string? SpotIntro, string? Weather, IReadOnlyList<string> Already);

/// <summary>
/// 챗쿠 AI(산책 추천과 같은 IRouteAi)로 두 사람의 공통 취향 · 만남 스팟 · 날씨에 맞는 가벼운 첫 대화 주제를 만든다.
/// 실패하거나 형식이 틀리면 A 가 쓴 예시 20개(Data/smalltalk.json) 중에서 주제가 겹치지 않게 고른다.
/// </summary>
public class SmalltalkGenerator : ISmalltalkGenerator
{
    public const int Count = 3;
    public const int MaxLength = 60;

    record Example(string Topic, string Text);

    readonly IRouteAi ai;
    readonly MetricsStore metrics;
    readonly ILogger<SmalltalkGenerator> log;
    readonly List<Example> examples;

    public SmalltalkGenerator(IRouteAi ai, MetricsStore metrics, IWebHostEnvironment env, ILogger<SmalltalkGenerator> log)
    {
        this.ai = ai;
        this.metrics = metrics;
        this.log = log;
        string path = Path.Combine(env.ContentRootPath, "Data", "smalltalk.json");
        examples = JsonSerializer.Deserialize<List<Example>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public IReadOnlyList<string> Examples => examples.Select(e => e.Text).ToList();

    public List<SmalltalkCard> Fallback(IEnumerable<string>? exclude = null)
    {
        var used = new HashSet<string>(exclude ?? []);
        var pool = examples.Where(e => !used.Contains(e.Text)).ToList();
        if (pool.Count < Count)
            pool = examples.ToList(); // 다 썼으면 처음부터 다시
        // 주제(공강·학식…)가 겹치지 않게 하나씩, 모자라면 아무거나
        var picked = pool.OrderBy(_ => Random.Shared.Next()).GroupBy(e => e.Topic).Select(g => g.First())
                         .OrderBy(_ => Random.Shared.Next()).Take(Count).ToList();
        foreach (Example e in pool.OrderBy(_ => Random.Shared.Next()))
        {
            if (picked.Count >= Count) break;
            if (!picked.Contains(e)) picked.Add(e);
        }
        return picked.Select(e => SmalltalkCard.Of(e.Text, e.Topic)).ToList();
    }

    static readonly Dictionary<string, string> TasteNames = new()
    {
        ["photo"] = "사진 찍기", ["quiet"] = "조용한 곳", ["food"] = "맛집·카페",
        ["unvisited"] = "새로운 곳 가 보기", ["partner"] = "경희몬 파트너와 걷기", ["exercise"] = "운동",
    };
    static readonly Dictionary<string, string> WeatherNames = new()
    {
        ["clear"] = "맑음", ["cloudy"] = "흐림", ["rain"] = "비", ["snow"] = "눈",
    };

    public async Task<(List<SmalltalkCard> Cards, string Source)> GenerateAsync(SmalltalkInput input, CancellationToken ct)
    {
        var result = await GenerateCoreAsync(input, ct);
        metrics.Record(MetricsStore.AiCall, "smalltalk", result.Source == "ai" ? 1 : 0);
        return result;
    }

    async Task<(List<SmalltalkCard> Cards, string Source)> GenerateCoreAsync(SmalltalkInput input, CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return (Fallback(input.Already), "fallback");

        string system =
            "너는 경희대학교 국제캠퍼스에서 처음 만나 함께 산책할 두 학생에게 가벼운 첫 대화 주제를 주는 도우미야. " +
            "부담 없고 예의 바른 질문만 써. 외모 · 연애 · 정치 · 종교 · 연락처 · 사는 곳처럼 사적인 질문은 쓰지 마. " +
            "아래 예시와 비슷한 말투(존댓말, 한 문장 질문)로 써. " +
            $"topic 은 {string.Join(", ", SmalltalkCard.Topics)} 중 하나로 붙여. " +
            $"반드시 JSON 하나만 답해: {{\"topics\": [{{\"topic\": \"학식\", \"text\": \"질문\"}}, …]}} (각 {MaxLength}자 이내, 정확히 {Count}개)";
        string user =
            $"두 사람의 공통 취향: {(input.CommonTastes.Count > 0 ? string.Join(", ", input.CommonTastes.Select(t => TasteNames.GetValueOrDefault(t, t))) : "없음")}\n" +
            $"만나는 곳: {input.SpotName}{(input.SpotIntro is { Length: > 0 } intro ? $" ({intro})" : "")}\n" +
            $"날씨: {(input.Weather is { } w ? WeatherNames.GetValueOrDefault(w, w) : "모름")}\n" +
            "말투 예시:\n" + string.Join("\n", examples.Take(4).Select(e => "- " + e.Text)) +
            (input.Already.Count > 0 ? "\n이미 쓴 주제 (겹치지 않게):\n" + string.Join("\n", input.Already.Select(a => "- " + a)) : "");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string? answer = await ai.CompleteAsync(system, user, timeout.Token);
        if (Parse(answer) is { } cards)
            return (cards, "ai");
        log.LogWarning("스몰토크 AI 답을 쓰지 못해 예시로 대신합니다");
        return (Fallback(input.Already), "fallback");
    }

    /// <summary>
    /// {"topics":[{"topic","text"}…]} 에서 1~60자 질문 3개를 꺼낸다 (예전 모양인 문자열 배열도 받음).
    /// 모자라거나 형식이 틀리면 null. 모르는 topic 은 "대화"
    /// </summary>
    public static List<SmalltalkCard>? Parse(string? answer)
    {
        if (answer == null)
            return null;
        int start = answer.IndexOf('{'), end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(answer[start..(end + 1)]);
            var cards = doc.RootElement.GetProperty("topics").EnumerateArray()
                .Select(t => t.ValueKind == JsonValueKind.Object
                    ? SmalltalkCard.Of(t.GetProperty("text").GetString()?.Trim() ?? "", t.TryGetProperty("topic", out JsonElement tp) ? tp.GetString() : null)
                    : SmalltalkCard.Of(t.GetString()?.Trim() ?? "", null))
                .Where(c => c.Text.Length is > 0 and <= MaxLength && !c.Text.Contains("http"))
                .DistinctBy(c => c.Text).Take(Count).ToList();
            return cards.Count == Count ? cards : null;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
