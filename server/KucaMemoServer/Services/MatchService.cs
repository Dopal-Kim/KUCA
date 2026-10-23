using System.Globalization;
using System.Text.Json;
using KucaMemoServer.Models;

namespace KucaMemoServer.Services;

/// <summary>
/// 매칭 숫자는 모두 여기 둔다 (docs/api.md 의 "매칭"). [가정] 표시는 아직 팀이 정하지 않은 제안값이다.
/// 운영 시간은 두지 않는다 (24시간, 10/23 결정).
/// </summary>
public static class MatchRules
{
    public static readonly int[] AllowedMinutes = [30, 60];
    public static readonly string[] ReportReasons = ["noshow", "rude", "unsafe", "fake", "other"];
    public static readonly string[] CancelReasons = ["late", "changed_mind", "other"];

    /// <summary>[가정] 이 시간 안에 짝을 못 찾으면 끝 (no_partner)</summary>
    public static readonly TimeSpan WaitLimit = TimeSpan.FromMinutes(10);
    /// <summary>[가정] 상대 제안에 답할 시간</summary>
    public static readonly TimeSpan RespondLimit = TimeSpan.FromSeconds(60);
    /// <summary>노쇼 기준 (Q7 제안 10분): 둘 다 수락한 뒤 이 안에 둘 다 도착해야 한다</summary>
    public static readonly TimeSpan NoShowLimit = TimeSpan.FromMinutes(10);
    /// <summary>끝난 매칭을 "ended" 로 보여 주는 시간 (그 뒤 idle)</summary>
    public static readonly TimeSpan EndedVisibleFor = TimeSpan.FromHours(1);
    /// <summary>[가정] 두 사람이 산책 끝(POST /api/walks/{id}/finish)을 안 눌러도 고른 시간 + 30분이 지나면 매칭은 끝 (산책 기록은 남음)</summary>
    public static TimeSpan WalkingLimit(int minutes) => TimeSpan.FromMinutes(minutes + 30);

    /// <summary>[가정] 공통 취향 하나 = 건물 거리 100m 만큼 더 가깝게 친다</summary>
    public const double MetersPerCommonPreference = 100;
    /// <summary>[가정] 받은 상대 별점 평균이 이보다 낮으면(리뷰 3개 이상일 때) 짝 찾기에서 뺀다</summary>
    public const double MinPartnerRating = 2.0;
    public const int MinRatingsToJudge = 3;
    /// <summary>[가정] 신고를 이만큼 받은 사람은 짝 찾기에서 뺀다 (신고 보관 90일 동안)</summary>
    public const int ReportsToExclude = 3;
    /// <summary>[가정] 거절한 두 사람은 이 시간 동안 다시 짝지어 주지 않는다</summary>
    public static readonly TimeSpan DeclineCooldown = TimeSpan.FromHours(1);

    public const int MaxAppearanceLength = 60;
    public const int MaxReportTextLength = 300;
    /// <summary>[가정] 매칭 하나에서 "다음 주제" 를 부를 수 있는 횟수</summary>
    public const int MaxSmalltalkRefresh = 5;

    /// <summary>매칭 기록 보관 (Q8 제안)</summary>
    public static readonly TimeSpan RecordKeep = TimeSpan.FromDays(30);
    /// <summary>신고 보관 (Q8 제안)</summary>
    public static readonly TimeSpan ReportKeep = TimeSpan.FromDays(90);

    // 시연용 가상 상대 (설정 Match:DemoPartner=true 일 때만)
    public static readonly TimeSpan DemoDelay = TimeSpan.FromSeconds(5);
    public const string DemoUserId = "demo-partner";
    public const string DemoNickname = "체험메이트";
    public const string DemoAppearance = "노란 모자, 흰 티셔츠";
}

/// <summary>매칭 API 가 돌려줄 오류. 엔드포인트가 {error, reason} 과 상태 코드로 바꾼다.</summary>
public class MatchException(int status, string error, string? reason = null) : Exception(error)
{
    public int Status { get; } = status;
    public string? Reason { get; } = reason;
}

/// <summary>
/// 실시간 산책 매칭. 대기열 · 진행 중인 매칭은 메모리에만 둔다 (위치 · 인상착의를 디스크에 남기지 않으려고).
/// 타이머 대신, 요청이 올 때마다 Tick 에서 시간을 보고 만료 · 노쇼 처리와 짝 찾기를 한다 (앱이 3초마다 폴링).
/// 매칭 기록 · 차단 · 신고만 MatchStore(SQLite)에 남긴다.
/// </summary>
public class MatchService(
    MatchStore store, WalkStore walks, BuildingStore buildings, SpotStore spots, ICampusWeather weather,
    TimeProvider clock, IConfiguration config)
{
    sealed record Prefs(HashSet<string> Tastes, HashSet<string> WalkTimes, string? TalkStyle, string? MatchGender);

    sealed class Waiting
    {
        public required string UserId, Nickname, Gender, MatchGender, BuildingId;
        public required int Minutes;
        public required Prefs Prefs;
        public required DateTimeOffset Since, Until;
    }

    sealed class Side
    {
        public required string UserId, Nickname, Gender, MatchGender, BuildingId;
        public required Prefs Prefs;
        public bool Accepted, Arrived, Finished;
        public string? Appearance;
        public bool IsDemo => UserId == MatchRules.DemoUserId;
    }

    sealed class Match
    {
        public required string Id;
        public required int Minutes;
        public required Side A, B;
        public string State = "proposed";
        public DateTimeOffset RespondBy, MeetBy, WalkUntil;
        public Spot? Spot;
        public Building? SpotBuilding;
        public List<string> Smalltalk = [];
        public string SmalltalkSource = "fallback";
        public readonly List<string> Shown = [];
        public int Refreshes, Version;
        public string? WalkId;
        public Side Me(string userId) => A.UserId == userId ? A : B;
        public Side Other(string userId) => A.UserId == userId ? B : A;
        public IEnumerable<Side> Sides => [A, B];
    }

    sealed record Ended(string? MatchId, string Reason, string? CancelledBy, DateTimeOffset At);

    readonly object gate = new();
    readonly Dictionary<string, Waiting> waiting = [];
    readonly Dictionary<string, Match> matches = [];
    readonly Dictionary<string, string> current = [];   // 계정 → 진행 중인 매칭 ID
    readonly Dictionary<string, Ended> ended = [];
    readonly Dictionary<string, DateTimeOffset> declined = [];  // "a|b" → 거절한 시각

    bool DemoOn => string.Equals(config["Match:DemoPartner"], "true", StringComparison.OrdinalIgnoreCase);
    DateTimeOffset Now => clock.GetUtcNow();

    // ───── 대기열 ─────

    public MatchState Join(Account user, MatchJoinRequest? body)
    {
        if (body == null || !MatchRules.AllowedMinutes.Contains(body.Minutes))
            throw new MatchException(400, "minutes 는 30 또는 60 이어야 합니다.");
        Prefs prefs = ParsePrefs(user.PreferencesJson);
        string matchGender = body.MatchGender ?? prefs.MatchGender ?? "any";
        if (matchGender is not ("same" or "any"))
            throw new MatchException(400, "matchGender 는 same 또는 any 여야 합니다.");
        if (string.IsNullOrWhiteSpace(body.BuildingId) || buildings.Find(body.BuildingId) == null)
            throw new MatchException(400, "buildingId 가 없거나 캠퍼스 건물이 아닙니다.");

        DateTimeOffset now = Now;
        store.Purge(now);
        lock (gate)
        {
            Tick(now);
            if (waiting.ContainsKey(user.Id) || current.ContainsKey(user.Id))
                throw new MatchException(409, "이미 매칭을 기다리거나 진행 중이에요.");
            ended.Remove(user.Id);
            waiting[user.Id] = new Waiting
            {
                UserId = user.Id, Nickname = user.Nickname ?? "익명", Gender = user.Gender ?? "",
                MatchGender = matchGender, BuildingId = body.BuildingId, Minutes = body.Minutes, Prefs = prefs,
                Since = now, Until = now + MatchRules.WaitLimit,
            };
            Tick(now);
            return View(user.Id);
        }
    }

    /// <summary>대기 취소. 대기 중이 아니어도 그냥 넘어간다.</summary>
    public void Leave(string userId)
    {
        lock (gate)
            waiting.Remove(userId);
    }

    public MatchState Get(string userId)
    {
        lock (gate)
        {
            Tick(Now);
            return View(userId);
        }
    }

    // ───── 제안 · 수락 · 거절 ─────

    public async Task<MatchState> AcceptAsync(string userId, string matchId, ISmalltalkGenerator smalltalk, CancellationToken ct)
    {
        Match m;
        int version;
        lock (gate)
        {
            Tick(Now);
            m = Own(userId, matchId);
            if (m.State != "proposed")
                throw new MatchException(409, "지금은 수락할 수 없어요.");
            m.Me(userId).Accepted = true;
            if (!(m.A.Accepted && m.B.Accepted))
                return View(userId);
            StartMeeting(m, smalltalk);
            version = m.Version;
        }
        await ReplaceWithAiSmalltalkAsync(m, version, smalltalk, ct);
        lock (gate)
            return View(userId);
    }

    public MatchState Decline(string userId, string matchId)
    {
        lock (gate)
        {
            DateTimeOffset now = Now;
            Tick(now);
            Match m = Own(userId, matchId);
            if (m.State != "proposed")
                throw new MatchException(409, "지금은 거절할 수 없어요.");
            declined[PairKey(m.A.UserId, m.B.UserId)] = now;
            Remove(m);
            store.EndRecord(m.Id, "declined", now);
            ended[userId] = new Ended(m.Id, "declined", userId, now);
            Side other = m.Other(userId);
            if (!other.IsDemo)
                Requeue(other, m.Minutes, now);  // 상대는 다시 찾기
            return View(userId);
        }
    }

    // ───── 만남 ─────

    public async Task<MatchState> SetAppearanceAsync(string userId, string matchId, string? text, IContentModerator moderator, CancellationToken ct)
    {
        string value = (text ?? "").Trim();
        if (value.Length is 0 or > MatchRules.MaxAppearanceLength)
            throw new MatchException(400, $"인상착의는 1~{MatchRules.MaxAppearanceLength}자로 써 주세요.");
        lock (gate)
        {
            Tick(Now);
            if (Own(userId, matchId).State != "meeting")
                throw new MatchException(409, "만나러 가는 중에만 쓸 수 있어요.");
        }
        // 메모와 같은 내용 검사. 검토 대기를 기다릴 시간이 없으니 애매함도 거절한다.
        ModerationResult check = await moderator.CheckAsync(value, null, ct);
        if (check.Verdict != Verdict.Allow)
            throw new MatchException(422, check.Verdict == Verdict.Block ? check.UserMessage : "이 표현은 쓸 수 없어요. 옷차림만 간단히 적어 주세요.",
                                     check.Reason ?? ModerationReasons.Abusive);
        lock (gate)
        {
            Tick(Now);
            Match m = Own(userId, matchId);
            if (m.State != "meeting")
                throw new MatchException(409, "만나러 가는 중에만 쓸 수 있어요.");
            m.Me(userId).Appearance = value;
            return View(userId);
        }
    }

    public async Task<SmalltalkResult> NextSmalltalkAsync(string userId, string matchId, ISmalltalkGenerator smalltalk, CancellationToken ct)
    {
        SmalltalkInput input;
        lock (gate)
        {
            Tick(Now);
            Match m = Own(userId, matchId);
            if (m.State is not ("meeting" or "walking"))
                throw new MatchException(409, "만난 뒤에만 쓸 수 있어요.");
            if (m.Refreshes >= MatchRules.MaxSmalltalkRefresh)
                throw new MatchException(429, "이번 매칭의 주제를 다 봤어요. 이제 편하게 이야기해 보세요!");
            m.Refreshes++;
            input = Input(m, null);
        }
        input = input with { Weather = await weather.NowAsync(ct) };
        (List<string> topics, string source) = await smalltalk.GenerateAsync(input, ct);
        lock (gate)
        {
            if (matches.TryGetValue(matchId, out Match? m))
            {
                m.Smalltalk = topics;
                m.SmalltalkSource = source;
                m.Shown.AddRange(topics);
                m.Version++;
            }
        }
        return new SmalltalkResult(topics, source);
    }

    public MatchState Arrived(string userId, string matchId, string? buildingId)
    {
        lock (gate)
        {
            DateTimeOffset now = Now;
            Tick(now);
            Match m = Own(userId, matchId);
            if (m.State != "meeting")
                throw new MatchException(409, "만나러 가는 중이 아니에요.");
            if (buildingId != m.Spot!.Id)
                throw new MatchException(400, "만남 스팟의 buildingId 를 보내 주세요.");
            m.Me(userId).Arrived = true;
            if (m.A.Arrived && m.B.Arrived)
            {
                m.State = "walking";
                m.WalkId = "w_" + Guid.NewGuid().ToString("N");
                m.WalkUntil = now + MatchRules.WalkingLimit(m.Minutes);
                // 두 사람 각자의 산책 기록 (같은 walkId). 끝내기 · 리뷰는 산책 API 로
                foreach (Side s in m.Sides.Where(s => !s.IsDemo))
                    walks.Start(m.WalkId, s.UserId, "match", null, m.Minutes, null, [], m.Other(s.UserId).UserId, now);
            }
            return View(userId);
        }
    }

    /// <summary>산책 API 가 매칭 산책을 끝냈을 때 부른다. 두 사람 모두 끝내면(가상 상대는 끝낸 것으로 봄) 매칭이 completed</summary>
    public void WalkFinished(string userId, string walkId)
    {
        lock (gate)
        {
            if (!current.TryGetValue(userId, out string? id) || !matches.TryGetValue(id, out Match? m) || m.WalkId != walkId)
                return;
            m.Me(userId).Finished = true;
            if (m.Sides.All(s => s.Finished || s.IsDemo))
                End(m, "completed", null, Now);
        }
    }

    public MatchState Cancel(string userId, string matchId, string? reason)
    {
        if (reason != null && !MatchRules.CancelReasons.Contains(reason))
            throw new MatchException(400, "reason 은 late, changed_mind, other 중 하나예요.");
        lock (gate)
        {
            Tick(Now);
            Match m = Own(userId, matchId);
            if (m.State == "proposed")
                return Decline(userId, matchId);
            End(m, "cancelled", userId, Now);
            return View(userId);
        }
    }

    // ───── 안전 ─────

    public void Report(string userId, ReportRequest? body)
    {
        string reason = body?.Reason ?? "";
        if (!MatchRules.ReportReasons.Contains(reason))
            throw new MatchException(400, "reason 은 " + string.Join(", ", MatchRules.ReportReasons) + " 중 하나예요.");
        string? text = body?.Text?.Trim();
        if (text?.Length > MatchRules.MaxReportTextLength)
            throw new MatchException(400, $"text 는 {MatchRules.MaxReportTextLength}자 이내로 써 주세요.");
        string target = PartnerInRecord(userId, body?.MatchId);
        DateTimeOffset now = Now;
        store.AddReport(body!.MatchId!, userId, target, reason, string.IsNullOrEmpty(text) ? null : text, now);
        store.Block(userId, target, now);
        EndIfActive(body.MatchId!, "reported", userId, now);
    }

    public void Block(string userId, BlockRequest? body)
    {
        string target = PartnerInRecord(userId, body?.MatchId);
        DateTimeOffset now = Now;
        store.Block(userId, target, now);
        EndIfActive(body!.MatchId!, "blocked", userId, now);
    }

    /// <summary>탈퇴: 대기열 · 진행 중인 매칭에서 빼고, 남은 기록을 지운다</summary>
    public void ForgetAccount(string userId)
    {
        lock (gate)
        {
            waiting.Remove(userId);
            if (current.TryGetValue(userId, out string? id) && matches.TryGetValue(id, out Match? m))
                End(m, "cancelled", userId, Now);
            ended.Remove(userId);
        }
        store.Forget(userId);
    }

    string PartnerInRecord(string userId, string? matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId) || store.FindRecord(matchId) is not { } record)
            throw new MatchException(404, "매칭 기록을 찾을 수 없어요.");
        if (record.UserA != userId && record.UserB != userId)
            throw new MatchException(403, "내 매칭이 아니에요.", "not_yours");
        return record.UserA == userId ? record.UserB : record.UserA;
    }

    void EndIfActive(string matchId, string reason, string byUser, DateTimeOffset now)
    {
        lock (gate)
            if (matches.TryGetValue(matchId, out Match? m))
                End(m, reason, byUser, now);
    }

    // ───── 내부: 시간 처리 · 짝 찾기 ─────

    /// <summary>만료 · 노쇼를 처리하고, 기다리는 사람끼리 짝을 짓는다. 반드시 lock 안에서 부른다.</summary>
    void Tick(DateTimeOffset now)
    {
        foreach ((string user, Ended e) in ended.ToList())
            if (now - e.At > MatchRules.EndedVisibleFor)
                ended.Remove(user);
        foreach ((string key, DateTimeOffset at) in declined.ToList())
            if (now - at > MatchRules.DeclineCooldown)
                declined.Remove(key);

        foreach (Waiting w in waiting.Values.Where(w => now > w.Until).ToList())
        {
            waiting.Remove(w.UserId);
            ended[w.UserId] = new Ended(null, "no_partner", null, now);
        }

        foreach (Match m in matches.Values.ToList())
        {
            if (m.State == "proposed" && now > m.RespondBy)
            {
                // 응답 없음: 수락한 쪽은 다시 대기, 답하지 않은 쪽은 끝
                Remove(m);
                store.EndRecord(m.Id, "timeout", now);
                foreach (Side s in m.Sides.Where(s => !s.IsDemo))
                    if (s.Accepted) Requeue(s, m.Minutes, now);
                    else ended[s.UserId] = new Ended(m.Id, "timeout", null, now);
            }
            else if (m.State == "meeting" && now > m.MeetBy)
                End(m, "noshow", null, now);
            else if (m.State == "walking" && now > m.WalkUntil)
                End(m, "completed", null, now);
        }

        foreach (Waiting w in waiting.Values.OrderBy(w => w.Since).ToList())
        {
            if (!waiting.ContainsKey(w.UserId))
                continue; // 이번 Tick 에서 이미 짝이 됨
            Waiting? best = waiting.Values
                .Where(o => o.UserId != w.UserId && Compatible(w, o))
                .OrderBy(o => Cost(w, o))
                .FirstOrDefault();
            if (best != null)
                Propose(ToSide(w), ToSide(best), w.Minutes, now);
            else if (DemoOn && now - w.Since >= MatchRules.DemoDelay)
                Propose(ToSide(w), DemoSide(w), w.Minutes, now);
        }
    }

    bool Compatible(Waiting a, Waiting b)
    {
        if (a.Minutes != b.Minutes)
            return false;
        if ((a.MatchGender == "same" || b.MatchGender == "same") && a.Gender != b.Gender)
            return false;
        if (declined.ContainsKey(PairKey(a.UserId, b.UserId)))
            return false;
        if (store.EitherBlocked(a.UserId, b.UserId))
            return false;
        return Trusted(a.UserId) && Trusted(b.UserId);
    }

    /// <summary>신고가 쌓였거나 상대 별점이 낮은 사람은 짝 찾기에서 뺀다</summary>
    bool Trusted(string userId)
    {
        if (store.CountReports(userId) >= MatchRules.ReportsToExclude)
            return false;
        (double average, int count) = walks.PartnerRating(userId);
        return count < MatchRules.MinRatingsToJudge || average >= MatchRules.MinPartnerRating;
    }

    /// <summary>건물 거리(m) − 공통 취향 × 100m. 작을수록 좋은 짝</summary>
    double Cost(Waiting a, Waiting b)
    {
        Building x = buildings.Find(a.BuildingId)!, y = buildings.Find(b.BuildingId)!;
        return Geo.Meters(x.Lat, x.Lon, y.Lat, y.Lon) - MatchRules.MetersPerCommonPreference * CommonScore(a.Prefs, b.Prefs);
    }

    static int CommonScore(Prefs a, Prefs b) =>
        a.Tastes.Intersect(b.Tastes).Count() + a.WalkTimes.Intersect(b.WalkTimes).Count()
        + (a.TalkStyle is { Length: > 0 } && a.TalkStyle == b.TalkStyle ? 1 : 0);

    void Propose(Side a, Side b, int minutes, DateTimeOffset now)
    {
        var m = new Match
        {
            Id = "m_" + Guid.NewGuid().ToString("N"), Minutes = minutes, A = a, B = b,
            RespondBy = now + MatchRules.RespondLimit,
        };
        matches[m.Id] = m;
        foreach (Side s in m.Sides.Where(s => !s.IsDemo))
        {
            waiting.Remove(s.UserId);
            current[s.UserId] = m.Id;
        }
        store.AddRecord(m.Id, a.UserId, b.UserId, now);
    }

    void StartMeeting(Match m, ISmalltalkGenerator smalltalk)
    {
        DateTimeOffset now = Now;
        (m.Spot, m.SpotBuilding) = PickSpot(m.A.BuildingId, m.B.BuildingId);
        m.State = "meeting";
        m.MeetBy = now + MatchRules.NoShowLimit;
        m.Smalltalk = smalltalk.Fallback();  // AI 답이 오기 전까지 보여 줄 예시
        m.SmalltalkSource = "fallback";
        m.Shown.AddRange(m.Smalltalk);
        foreach (Side s in m.Sides.Where(s => s.IsDemo))
            (s.Appearance, s.Arrived) = (MatchRules.DemoAppearance, true);
        store.SetSpot(m.Id, m.Spot.Id);
    }

    /// <summary>둘 다 수락한 직후 AI 주제를 받아, 그사이 다른 주제로 바뀌지 않았으면 바꿔 넣는다</summary>
    async Task ReplaceWithAiSmalltalkAsync(Match m, int version, ISmalltalkGenerator smalltalk, CancellationToken ct)
    {
        SmalltalkInput input;
        lock (gate)
            input = Input(m, null);
        input = input with { Weather = await weather.NowAsync(ct) };
        (List<string> topics, string source) = await smalltalk.GenerateAsync(input with { Already = [] }, ct);
        if (source != "ai")
            return;
        lock (gate)
        {
            if (m.Version != version || !matches.ContainsKey(m.Id))
                return;
            m.Smalltalk = topics;
            m.SmalltalkSource = source;
            m.Shown.AddRange(topics);
            m.Version++;
        }
    }

    SmalltalkInput Input(Match m, string? weatherNow) =>
        new(m.A.Prefs.Tastes.Intersect(m.B.Prefs.Tastes).OrderBy(t => t).ToList(),
            m.Spot?.Name ?? "", m.Spot?.Intro, weatherNow, m.Shown.ToList());

    /// <summary>만남 스팟(meetSpecies 가 있는 경희스팟) 중 두 사람이 걷는 거리의 큰 쪽이 가장 작은 곳</summary>
    (Spot, Building) PickSpot(string buildingA, string buildingB)
    {
        Building a = buildings.Find(buildingA)!, b = buildings.Find(buildingB)!;
        return spots.All
            .Where(s => s.MeetSpecies != null)
            .Select(s => (Spot: s, Building: buildings.Find(s.Id)))
            .Where(x => x.Building != null)
            .Select(x =>
            {
                double da = Geo.Meters(a.Lat, a.Lon, x.Building!.Lat, x.Building.Lon);
                double db = Geo.Meters(b.Lat, b.Lon, x.Building.Lat, x.Building.Lon);
                return (x.Spot, Building: x.Building!, Far: Math.Max(da, db), Sum: da + db);
            })
            .OrderBy(x => x.Far).ThenBy(x => x.Sum)
            .Select(x => (x.Spot, x.Building))
            .First();
    }

    void End(Match m, string reason, string? byUser, DateTimeOffset now)
    {
        Remove(m);
        store.EndRecord(m.Id, reason, now);
        foreach (Side s in m.Sides.Where(s => !s.IsDemo))
            ended[s.UserId] = new Ended(m.Id, reason, byUser, now);
        // 인상착의 · 위치는 메모리에만 있었으므로 매칭과 함께 사라진다
    }

    void Remove(Match m)
    {
        matches.Remove(m.Id);
        foreach (Side s in m.Sides)
            if (current.TryGetValue(s.UserId, out string? id) && id == m.Id)
                current.Remove(s.UserId);
    }

    void Requeue(Side s, int minutes, DateTimeOffset now)
    {
        current.Remove(s.UserId);
        waiting[s.UserId] = new Waiting
        {
            UserId = s.UserId, Nickname = s.Nickname, Gender = s.Gender, MatchGender = s.MatchGender,
            BuildingId = s.BuildingId, Minutes = minutes, Prefs = s.Prefs, Since = now, Until = now + MatchRules.WaitLimit,
        };
    }

    Match Own(string userId, string matchId)
    {
        if (current.TryGetValue(userId, out string? id) && id == matchId && matches.TryGetValue(id, out Match? m))
            return m;
        if (matches.TryGetValue(matchId, out _))
            throw new MatchException(403, "내 매칭이 아니에요.", "not_yours");
        if (store.FindRecord(matchId) is { } record)
            throw record.UserA == userId || record.UserB == userId
                ? new MatchException(409, "이미 끝난 매칭이에요.")
                : new MatchException(403, "내 매칭이 아니에요.", "not_yours");
        throw new MatchException(404, "매칭을 찾을 수 없어요.");
    }

    static Side ToSide(Waiting w) => new()
    {
        UserId = w.UserId, Nickname = w.Nickname, Gender = w.Gender, MatchGender = w.MatchGender,
        BuildingId = w.BuildingId, Prefs = w.Prefs,
    };

    /// <summary>시연용 가상 상대: 기다리는 사람과 같은 건물·성별·취향, 이미 수락한 상태</summary>
    static Side DemoSide(Waiting w) => new()
    {
        UserId = MatchRules.DemoUserId, Nickname = MatchRules.DemoNickname, Gender = w.Gender, MatchGender = "any",
        BuildingId = w.BuildingId, Prefs = w.Prefs, Accepted = true,
    };

    static string PairKey(string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"{a}|{b}" : $"{b}|{a}";

    // ───── 내부: 응답 모양 ─────

    MatchState View(string userId)
    {
        if (waiting.TryGetValue(userId, out Waiting? w))
            return new MatchState("waiting", Minutes: w.Minutes, WaitingSince: Iso(w.Since), WaitingUntil: Iso(w.Until));

        if (current.TryGetValue(userId, out string? id) && matches.TryGetValue(id, out Match? m))
        {
            Side me = m.Me(userId), other = m.Other(userId);
            var common = me.Prefs.Tastes.Intersect(other.Prefs.Tastes).OrderBy(t => t).ToList();
            if (m.State == "proposed")
                return new MatchState("proposed", m.Id, m.Minutes,
                    Partner: new MatchPartner(other.Nickname, common, null),
                    RespondBy: Iso(m.RespondBy), MyAnswer: me.Accepted ? "accepted" : null);
            return new MatchState(m.State, m.Id, m.Minutes,
                Partner: new MatchPartner(other.Nickname, common, other.Appearance),
                MyAnswer: "accepted",
                MeetSpot: new MeetSpotView(m.Spot!.Id, m.Spot.Name, m.SpotBuilding!.Lat, m.SpotBuilding.Lon, m.Spot.MeetSpecies),
                MeetBy: Iso(m.MeetBy),
                Smalltalk: m.Smalltalk, SmalltalkSource: m.SmalltalkSource,
                MyAppearance: me.Appearance,
                Arrived: new MatchArrived(me.Arrived, other.Arrived),
                WalkId: m.WalkId);
        }

        if (ended.TryGetValue(userId, out Ended? e))
            return new MatchState("ended", e.MatchId, EndReason: e.Reason,
                CancelledBy: e.CancelledBy == null ? null : e.CancelledBy == userId ? "me" : "partner");

        return new MatchState("idle");
    }

    static string Iso(DateTimeOffset t) => t.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>계정의 산책 취향 (앱 WalkPreferences JSON). 읽지 못하면 빈 취향</summary>
    static Prefs ParsePrefs(string? json)
    {
        var empty = new Prefs([], [], null, null);
        if (string.IsNullOrWhiteSpace(json))
            return empty;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            HashSet<string> List(string name) => root.TryGetProperty(name, out JsonElement a) && a.ValueKind == JsonValueKind.Array
                ? a.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToHashSet()
                : [];
            string? Text(string name) => root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String
                && v.GetString() is { Length: > 0 } s ? s : null;
            return new Prefs(List("tastes"), List("walkTimes"), Text("talkStyle"), Text("matchGender") is "same" or "any" ? Text("matchGender") : null);
        }
        catch (JsonException)
        {
            return empty;
        }
    }
}

/// <summary>위경도 계산</summary>
public static class Geo
{
    /// <summary>두 위경도 사이 직선거리 (m)</summary>
    public static double Meters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
