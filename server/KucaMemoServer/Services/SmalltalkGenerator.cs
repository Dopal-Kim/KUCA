using System.Text.Json;

namespace KucaMemoServer.Services;

/// <summary>스몰토크 주제를 만든다. 실제 구현은 SmalltalkGenerator, 테스트에서는 가짜로 바꿔 끼울 수 있다.</summary>
public interface ISmalltalkGenerator
{
    /// <summary>주제 3개. AI 가 안 되면 A 의 예시(Data/smalltalk.json)에서 고른다.</summary>
    Task<(List<string> Topics, string Source)> GenerateAsync(SmalltalkInput input, CancellationToken ct);

    /// <summary>AI 없이 예시에서 바로 고른다 (상태 전환을 기다리게 하지 않으려고)</summary>
    List<string> Fallback(IEnumerable<string>? exclude = null);
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
    readonly ILogger<SmalltalkGenerator> log;
    readonly List<Example> examples;

    public SmalltalkGenerator(IRouteAi ai, IWebHostEnvironment env, ILogger<SmalltalkGenerator> log)
    {
        this.ai = ai;
        this.log = log;
        string path = Path.Combine(env.ContentRootPath, "Data", "smalltalk.json");
        examples = JsonSerializer.Deserialize<List<Example>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public IReadOnlyList<string> Examples => examples.Select(e => e.Text).ToList();

    public List<string> Fallback(IEnumerable<string>? exclude = null)
    {
        var used = new HashSet<string>(exclude ?? []);
        var pool = examples.Where(e => !used.Contains(e.Text)).ToList();
        if (pool.Count < Count)
            pool = examples.ToList(); // 다 썼으면 처음부터 다시
        // 주제(공강·학식…)가 겹치지 않게 하나씩, 모자라면 아무거나
        var picked = pool.OrderBy(_ => Random.Shared.Next()).GroupBy(e => e.Topic).Select(g => g.First())
                         .OrderBy(_ => Random.Shared.Next()).Take(Count).Select(e => e.Text).ToList();
        foreach (Example e in pool.OrderBy(_ => Random.Shared.Next()))
        {
            if (picked.Count >= Count) break;
            if (!picked.Contains(e.Text)) picked.Add(e.Text);
        }
        return picked;
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

    public async Task<(List<string> Topics, string Source)> GenerateAsync(SmalltalkInput input, CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return (Fallback(input.Already), "fallback");

        string system =
            "너는 경희대학교 국제캠퍼스에서 처음 만나 함께 산책할 두 학생에게 가벼운 첫 대화 주제를 주는 도우미야. " +
            "부담 없고 예의 바른 질문만 써. 외모 · 연애 · 정치 · 종교 · 연락처 · 사는 곳처럼 사적인 질문은 쓰지 마. " +
            "아래 예시와 비슷한 말투(존댓말, 한 문장 질문)로 써. " +
            $"반드시 JSON 하나만 답해: {{\"topics\": [\"질문\", \"질문\", \"질문\"]}} (각 {MaxLength}자 이내, 정확히 {Count}개)";
        string user =
            $"두 사람의 공통 취향: {(input.CommonTastes.Count > 0 ? string.Join(", ", input.CommonTastes.Select(t => TasteNames.GetValueOrDefault(t, t))) : "없음")}\n" +
            $"만나는 곳: {input.SpotName}{(input.SpotIntro is { Length: > 0 } intro ? $" ({intro})" : "")}\n" +
            $"날씨: {(input.Weather is { } w ? WeatherNames.GetValueOrDefault(w, w) : "모름")}\n" +
            "말투 예시:\n" + string.Join("\n", examples.Take(4).Select(e => "- " + e.Text)) +
            (input.Already.Count > 0 ? "\n이미 쓴 주제 (겹치지 않게):\n" + string.Join("\n", input.Already.Select(a => "- " + a)) : "");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string? answer = await ai.CompleteAsync(system, user, timeout.Token);
        if (Parse(answer) is { } topics)
            return (topics, "ai");
        log.LogWarning("스몰토크 AI 답을 쓰지 못해 예시로 대신합니다");
        return (Fallback(input.Already), "fallback");
    }

    /// <summary>{"topics":[…]} 에서 1~60자 질문 3개를 꺼낸다. 모자라거나 형식이 틀리면 null</summary>
    public static List<string>? Parse(string? answer)
    {
        if (answer == null)
            return null;
        int start = answer.IndexOf('{'), end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(answer[start..(end + 1)]);
            var topics = doc.RootElement.GetProperty("topics").EnumerateArray()
                .Select(t => t.GetString()?.Trim() ?? "")
                .Where(t => t.Length is > 0 and <= MaxLength && !t.Contains("http"))
                .Distinct().Take(Count).ToList();
            return topics.Count == Count ? topics : null;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }
}
