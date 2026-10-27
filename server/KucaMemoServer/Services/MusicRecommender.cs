using System.Text.Json;
using KucaMemoServer.Models;

namespace KucaMemoServer.Services;

/// <summary>
/// 산책하며 들을 노래 추천. 취향 · 걷는 속도 · 지금 시간대 · 계절 · 날씨 (+ 코스 이름 · 이유 · 장소) 로 챗쿠 AI(IRouteAi)가 고른다.
/// 앱은 곡 이름으로 멜론 · Spotify · YouTube Music 검색을 열기만 한다 (재생 · 저작권 문제 없음, AI 가 곡명을 조금 틀려도 검색은 된다).
/// AI 가 실패하거나 형식이 틀리면 분위기별로 골라 둔 유명한 곡 (Data/music.json) 에서 뽑는다.
/// </summary>
public class MusicRecommender
{
    public const int DefaultCount = 5;
    public const int MinCount = 3;
    public const int MaxCount = 8;
    /// <summary>AI 에게 더 받는 곡 수: 같은 가수 · 형식이 틀린 곡을 빼도 count 를 채우게</summary>
    const int Spare = 4;
    public static readonly string[] Moods = ["calm", "bright", "energetic", "rainy", "night"];

    record Song(string Title, string Artist);

    readonly IRouteAi ai;
    readonly ICampusWeather weather;
    readonly TimeProvider clock;
    readonly MetricsStore metrics;
    readonly ILogger<MusicRecommender> log;
    readonly Dictionary<string, List<Song>> songs;
    readonly MusicHistory history;

    public MusicRecommender(IRouteAi ai, ICampusWeather weather, TimeProvider clock, MetricsStore metrics,
                            IWebHostEnvironment env, ILogger<MusicRecommender> log, MusicHistory history)
    {
        this.history = history;
        this.ai = ai;
        this.weather = weather;
        this.clock = clock;
        this.metrics = metrics;
        this.log = log;
        string path = Path.Combine(env.ContentRootPath, "Data", "music.json");
        songs = JsonSerializer.Deserialize<Dictionary<string, List<Song>>>(File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    static readonly Dictionary<string, string> TasteNames = new()
    {
        ["photo"] = "사진 찍기", ["quiet"] = "조용한 곳", ["food"] = "맛집·카페",
        ["unvisited"] = "새로운 곳 가 보기", ["partner"] = "경희몬 파트너와 걷기", ["exercise"] = "운동",
    };
    static readonly Dictionary<string, string> Korean = new()
    {
        ["morning"] = "아침", ["day"] = "낮", ["evening"] = "저녁", ["night"] = "밤",
        ["spring"] = "봄", ["summer"] = "여름", ["autumn"] = "가을", ["winter"] = "겨울",
        ["clear"] = "맑음", ["cloudy"] = "흐림", ["rain"] = "비", ["snow"] = "눈",
        ["slow"] = "천천히", ["normal"] = "보통", ["fast"] = "빠르게",
        ["calm"] = "잔잔한", ["bright"] = "밝은", ["energetic"] = "신나는", ["rainy"] = "비 오는 날", ["night"] = "밤 산책",
    };

    /// <summary>분위기마다 AI 에게 주는 구체적인 기준 (energetic 인데 발라드가 섞였음)</summary>
    static readonly Dictionary<string, string> MoodGuide = new()
    {
        ["calm"] = "잔잔한 어쿠스틱 · 발라드 · 포크처럼 느린 곡",
        ["bright"] = "밝고 산뜻한 중간 빠르기 곡 (인디 팝 · 밝은 K-pop)",
        ["energetic"] = "빠르고 리듬이 강한 곡 (댄스 · 팝 · 록, 발라드는 빼)",
        ["rainy"] = "비 · 우산 · 흐린 날이 떠오르는 곡 (눈이 오면 눈 · 겨울 곡)",
        ["night"] = "밤 · 별 · 달이 떠오르는 차분한 곡 (R&B · 인디)",
    };

    /// <summary>계절마다 제목에 들어 있으면 빼는 말 (AI 가 가을에 "벚꽃 엔딩" · "봄봄봄" 을 자주 골랐음). 대소문자 무시</summary>
    static readonly Dictionary<string, string[]> OffSeason = new()
    {
        ["spring"] = ["가을", "겨울", "첫눈", "눈꽃", "눈의 꽃", "크리스마스", "캐롤", "autumn", "winter", "snow", "christmas"],
        ["summer"] = ["가을", "겨울", "첫눈", "눈꽃", "눈의 꽃", "크리스마스", "캐롤", "벚꽃", "autumn", "winter", "snow", "christmas"],
        ["autumn"] = ["봄", "벚꽃", "여름", "첫눈", "눈꽃", "눈의 꽃", "크리스마스", "캐롤", "spring", "summer", "christmas", "cherry"],
        ["winter"] = ["봄", "벚꽃", "여름", "가을", "spring", "summer", "autumn", "cherry"],
    };

    public static bool FitsSeason(string title, string? season) =>
        season == null || !OffSeason.TryGetValue(season, out string[]? words)
        || !words.Any(w => title.Contains(w, StringComparison.OrdinalIgnoreCase));

    public static string? Validate(MusicRequest r)
    {
        if (r.Tastes?.Any(t => !RecommendRules.AllowedTastes.Contains(t)) == true)
            return "tastes 는 " + string.Join(", ", RecommendRules.AllowedTastes) + " 만 쓸 수 있습니다.";
        if (r.Pace != null && !RecommendRules.AllowedPaces.Contains(r.Pace))
            return "pace 는 slow, normal, fast 중 하나예요.";
        if (r.Count is { } c && (c < MinCount || c > MaxCount))
            return $"count 는 {MinCount}~{MaxCount} 예요.";
        if (r.Minutes is { } m && (m < 1 || m > 600))
            return "minutes 가 올바르지 않아요.";
        if ((r.CourseTitle?.Length ?? 0) > 60 || (r.CourseWhy?.Length ?? 0) > 300 || (r.Places?.Count ?? 0) > 10
            || r.Places?.Any(p => p.Length > 40) == true)
            return "코스 정보가 너무 길어요.";
        return null;
    }

    /// <summary>
    /// 규칙으로 정한 분위기 (AI 가 없을 때의 목록 · AI 에게 주는 힌트): 비 · 눈 → rainy, 운동 · 빠르게 → energetic,
    /// 밤 → night, 조용한 곳 · 천천히 → calm, 그 밖 → bright
    /// </summary>
    public static string MoodOf(IReadOnlyCollection<string> tastes, string pace, string timeOfDay, string? weather)
    {
        if (weather is "rain" or "snow") return "rainy";
        if (tastes.Contains("exercise") || pace == "fast") return "energetic";
        if (timeOfDay == "night") return "night";
        if (tastes.Contains("quiet") || pace == "slow") return "calm";
        return "bright";
    }

    public async Task<MusicResult> RecommendAsync(MusicRequest r, string pace, CancellationToken ct)
    {
        DateTimeOffset kst = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(9));
        string timeOfDay = RecommendRules.TimeOfDay(kst.Hour), season = RecommendRules.Season(kst.Month);
        string? now = await weather.NowAsync(ct);
        var tastes = (IReadOnlyCollection<string>?)r.Tastes ?? [];
        string mood = MoodOf(tastes, pace, timeOfDay, now);
        int count = r.Count ?? DefaultCount;

        MusicResult? result = await AskAiAsync(r, pace, timeOfDay, season, now, mood, count, ct);
        metrics.Record(MetricsStore.AiCall, "music", result != null ? 1 : 0);
        return result ?? Fallback(mood, count, season, timeOfDay);
    }

    async Task<MusicResult?> AskAiAsync(MusicRequest r, string pace, string timeOfDay, string season, string? now,
                                        string mood, int count, CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return null;
        string system =
            "너는 경희대학교 국제캠퍼스를 산책하는 대학생에게 걸으며 듣기 좋은 노래를 골라 주는 도우미야. " +
            "실제로 발매된 유명한 곡만 골라. 곡 이름이나 가수가 확실하지 않으면 넣지 마 (지어내지 마). " +
            "한국 노래를 중심으로 외국 노래를 한두 곡 섞어. 같은 가수는 한 번만. 선정적이거나 욕설이 든 곡은 빼. " +
            "지금 계절과 맞지 않는 곡 (예: 가을에 벚꽃 · 봄 노래, 여름에 눈 노래) 은 빼. " +
            "한국 노래는 멜론에 나오는 한글 이름 그대로 써 (예: \"여행\" · \"볼빨간사춘기\", \"Travel\" · \"Bolbbalgan4\" 처럼 영어로 바꾸지 마). 외국 노래는 원래 이름. " +
            "누구나 아는 대표곡만 고르지 말고, 분위기에 맞는 덜 뻔한 곡도 섞어. " +
            "theme 은 목록 전체를 소개하는 한 줄 (25자 이내 한국어), why 는 곡마다 이 산책에 어울리는 이유 (25자 이내, ~요 로 끝나게). " +
            $"mood 는 {string.Join(", ", Moods)} 중 하나. " +
            $"반드시 JSON 하나만 답해: {{\"mood\": \"bright\", \"theme\": \"…\", \"tracks\": [{{\"title\": \"곡 이름\", \"artist\": \"가수\", \"why\": \"…\"}}]}} (정확히 {count + Spare}곡)";
        string tasteText = r.Tastes is { Count: > 0 } t ? string.Join(", ", t.Select(x => TasteNames.GetValueOrDefault(x, x))) : "정하지 않음";
        string user =
            $"지금: {Korean[season]} {Korean[timeOfDay]}, 날씨 {(now is { } w ? Korean.GetValueOrDefault(w, w) : "모름")}\n" +
            $"산책 취향: {tasteText}\n" +
            $"걷는 속도: {Korean[pace]}" + (r.Minutes is { } m ? $", {m}분 산책" : "") + "\n" +
            (r.CourseTitle is { Length: > 0 } title ? $"코스: {title}\n" : "") +
            (r.CourseWhy is { Length: > 0 } why ? $"코스 이유: {why}\n" : "") +
            (r.Places is { Count: > 0 } places ? $"들를 곳: {string.Join(", ", places)}\n" : "") +
            $"규칙으로 본 분위기: {Korean[mood]} ({mood}) — {MoodGuide[mood]}. 코스 분위기가 다르면 바꿔도 돼." +
            (history.Recent(12) is { Count: > 0 } recent ? "\n최근에 다른 학생에게 추천한 곡 (되도록 피해서): " + string.Join(", ", recent) : "");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        string? answer = await ai.CompleteAsync(system, user, timeout.Token);
        if (Parse(answer, count, season, history.Contains) is { } result)
        {
            history.Add(result.Tracks);
            return result;
        }
        log.LogWarning("음악 추천 AI 답을 쓰지 못해 기본 목록으로 대신합니다");
        return null;
    }

    /// <summary>{"mood","theme","tracks":[{title,artist,why}]} 을 읽는다. 곡이 count 개 안 되거나 형식이 틀리면 null</summary>
    /// <param name="season">있으면 제목이 계절에 안 맞는 곡을 뺀다</param>
    /// <param name="recent">있으면 최근에 추천한 곡을 뒤로 미룬다 (곡이 모자라면 그래도 쓴다)</param>
    public static MusicResult? Parse(string? answer, int count, string? season = null, Func<MusicTrack, bool>? recent = null)
    {
        if (answer == null)
            return null;
        int start = answer.IndexOf('{'), end = answer.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(answer[start..(end + 1)]);
            JsonElement root = doc.RootElement;
            string mood = root.TryGetProperty("mood", out JsonElement md) && md.GetString() is { } m && Moods.Contains(m) ? m : "bright";
            string theme = root.TryGetProperty("theme", out JsonElement th) ? Clip(th.GetString(), 40) ?? "" : "";
            var tracks = root.GetProperty("tracks").EnumerateArray()
                .Select(x => new MusicTrack(
                    Clip(x.GetProperty("title").GetString(), 60) ?? "",
                    Clip(x.GetProperty("artist").GetString(), 60) ?? "",
                    x.TryGetProperty("why", out JsonElement y) ? Clip(y.GetString(), 40) : null))
                .Where(x => x.Title.Length > 0 && x.Artist.Length > 0 && !x.Title.Contains("http") && !x.Artist.Contains("http"))
                .Where(x => FitsSeason(x.Title, season))
                .DistinctBy(x => ArtistKey(x.Artist))   // 같은 가수는 한 번만 (AI 가 자주 어김: 아이유 두 곡)
                .OrderBy(x => recent?.Invoke(x) == true ? 1 : 0)   // 안정 정렬: AI 가 준 순서는 그대로
                .Take(count).ToList();
            return tracks.Count == count ? new MusicResult("ai", mood, theme, tracks) : null;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>"아이유" · "IU" · "아이유 (IU)" 처럼 적어도 같은 가수로: 괄호 앞 이름, 공백 · 대소문자 무시. 영문 · 한글 이름이 다르면 못 잡는다</summary>
    static string ArtistKey(string artist)
    {
        int paren = artist.IndexOf('(');
        string name = paren > 0 ? artist[..paren] : artist;
        return name.Replace(" ", "").ToLowerInvariant() switch { "iu" => "아이유", "bts" => "방탄소년단", var k => k };
    }

    static string? Clip(string? s, int max) =>
        s?.Trim() is not { Length: > 0 } t ? null : t.Length <= max ? t : t[..(max - 1)].TrimEnd() + "…";

    MusicResult Fallback(string mood, int count, string season, string timeOfDay)
    {
        List<Song> pool = (songs.GetValueOrDefault(mood) ?? []).Where(x => FitsSeason(x.Title, season)).ToList();
        var picked = pool.OrderBy(_ => Random.Shared.Next()).Take(count).ToList();
        // 목록이 모자라면 밝은 곡으로 채운다
        foreach (Song s in (songs.GetValueOrDefault("bright") ?? []).OrderBy(_ => Random.Shared.Next()))
        {
            if (picked.Count >= count) break;
            if (!picked.Contains(s) && FitsSeason(s.Title, season)) picked.Add(s);
        }
        string theme = $"{Korean[season]} {Korean[timeOfDay]}, {Korean[mood]} 노래";
        return new MusicResult("fallback", mood, theme, picked.Select(s => new MusicTrack(s.Title, s.Artist, null)).ToList());
    }
}

/// <summary>최근에 추천한 곡 (메모리, 서버를 다시 켜면 비움). 같은 곡만 계속 나오지 않게 AI 에게 피할 곡으로 알려 준다</summary>
public class MusicHistory
{
    const int Keep = 40;
    readonly LinkedList<MusicTrack> recent = new();
    readonly object gate = new();

    static string Key(MusicTrack t) => (t.Title + "|" + t.Artist).Replace(" ", "").ToLowerInvariant();

    public void Add(IEnumerable<MusicTrack> tracks)
    {
        lock (gate)
            foreach (MusicTrack t in tracks)
            {
                recent.AddFirst(t);
                while (recent.Count > Keep) recent.RemoveLast();
            }
    }

    public bool Contains(MusicTrack t)
    {
        string k = Key(t);
        lock (gate) return recent.Any(r => Key(r) == k);
    }

    /// <summary>가장 최근 n곡 ("가수 — 곡")</summary>
    public List<string> Recent(int n)
    {
        lock (gate) return recent.DistinctBy(Key).Take(n).Select(t => $"{t.Artist} — {t.Title}").ToList();
    }
}
