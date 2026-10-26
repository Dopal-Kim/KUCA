using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버 매칭 API (docs/api.md "매칭"). 로그인 토큰을 붙여 부르고, 서버의 MatchState 를 앱 안의 MatchState 로 옮겨 담는다.
/// - 서버 state: idle · waiting · proposed · meeting · walking · ended → 앱: Idle · Waiting · Proposed/Accepted · Matched · Walking · Ended/Cancelled/Expired
/// - 서버에 매칭 API 가 없으면 (404) 그때부터 기기 안 가상 상대(MockMatchService)로 바꿔서 흐름이 끊기지 않게 한다.
/// </summary>
public class HttpMatchService : IMatchService
{
    readonly string baseUrl;
    readonly MockMatchService fallback;
    bool useFallback;
    const int Timeout = 20;

    // ---------- 서버 모양 ----------
    [Serializable] class ErrorBody { public string error; public string message; public string reason; }
    [Serializable] class QueueBody { public int minutes; public string matchGender; public string buildingId; }
    [Serializable] class TextBody { public string text; }
    [Serializable] class ReasonBody { public string reason; }
    [Serializable] class BuildingBody { public string buildingId; }
    [Serializable] class ReportBody { public string matchId; public string reason; public string text; }
    [Serializable] class BlockBody { public string matchId; }
    [Serializable] class SmalltalkBody { public List<string> smalltalk = new List<string>(); public List<TopicCard> smalltalkCards = new List<TopicCard>(); public string source; }
    [Serializable] class ServerPartner { public string nickname; public List<string> commonTastes = new List<string>(); public string appearance; public string whyMatched; }
    [Serializable] class ServerSpot { public string buildingId; public string name; public double lat, lon; public string meetSpecies; }
    [Serializable] class Arrived { public bool me, partner; }

    [Serializable]
    class ServerState
    {
        public string state;
        public string matchId;
        public int minutes;
        public ServerPartner partner;
        public string waitingUntil;
        public string respondBy;
        public string myAnswer;
        public ServerSpot meetSpot;
        public string meetBy;
        public List<string> smalltalk = new List<string>();
        public List<TopicCard> smalltalkCards = new List<TopicCard>();
        public MatchCourse course;
        public int nowSearching = -1;
        public string myAppearance;
        public Arrived arrived;
        public string walkId;
        public string endReason;
        public string cancelledBy;
    }

    MatchState last = new MatchState();
    readonly Queue<TopicCard> topics = new Queue<TopicCard>();   // "다음 주제" 로 받은 3개 중 아직 안 보여 준 것

    public HttpMatchService(string baseUrl, MockMatchService fallback)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
        this.fallback = fallback;
    }

    public string Description => useFallback ? fallback.Description : "서버";

    UnityWebRequest Request(string method, string path, string json = null)
    {
        var req = new UnityWebRequest(baseUrl + path, method, new DownloadHandlerBuffer(),
            json != null ? new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)) : null);
        if (json != null) req.SetRequestHeader("Content-Type", "application/json");
        if (AuthService.HasToken) req.SetRequestHeader("Authorization", "Bearer " + AuthService.Token);
        req.timeout = Timeout;
        return req;
    }

    static string ErrorOf(UnityWebRequest req)
    {
        if (req.result == UnityWebRequest.Result.ConnectionError)
            return "서버에 연결할 수 없어요. 인터넷 연결을 확인해 주세요.";
        ErrorBody e = null;
        try { e = JsonUtility.FromJson<ErrorBody>(req.downloadHandler?.text ?? ""); }
        catch (ArgumentException) { }
        switch (req.responseCode)
        {
            case 401: return "로그인이 필요해요";
            case 403: return e?.reason == "not_ready" ? "닉네임 · 성별 설정을 먼저 마쳐 주세요" : "할 수 없는 매칭이에요";
            case 409: return !string.IsNullOrEmpty(e?.error) ? e.error : "지금은 할 수 없어요. 매칭 상태가 바뀌었어요";
            case 422: return "쓸 수 없는 내용이 있어요. 고쳐서 다시 보내 주세요";
            case 429: return !string.IsNullOrEmpty(e?.error) ? e.error : "주제를 다 봤어요. 이제 서로 이야기해 보세요";
        }
        if (!string.IsNullOrEmpty(e?.error)) return e.error;
        if (!string.IsNullOrEmpty(e?.message)) return e.message;
        return $"서버 오류 ({req.responseCode})";
    }

    /// <summary>서버 MatchState 를 돌려주는 요청. 404(API 없음)면 가상 상대로 바꿔서 alt 를 대신 부른다.</summary>
    IEnumerator Send(string method, string path, string json, Func<IEnumerator> alt, Action<MatchState, string> onDone,
        bool notFoundIsApiMissing = true)
    {
        if (useFallback) { yield return alt(); yield break; }
        if (!AuthService.HasToken) { onDone(null, "로그인이 필요해요"); yield break; }
        using var req = Request(method, path, json);
        yield return req.SendWebRequest();
        if (req.responseCode == 404 && notFoundIsApiMissing)
        {
            Debug.Log("[Match] 서버에 매칭 API 가 없어 가상 상대로 바꿔요");
            useFallback = true;
            yield return alt();
            yield break;
        }
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        MatchState s = Parse(req.downloadHandler.text);
        if (s == null) { onDone(null, "서버 응답을 읽지 못했어요"); yield break; }
        onDone(Remember(s), null);
    }

    MatchState Remember(MatchState s)
    {
        if (s.matchId != last.matchId)
            topics.Clear();
        last = s;
        return Copy(s);
    }

    /// <summary>JsonUtility 복사는 비어 있던 상대 · 스팟을 빈 객체로 채우므로 다시 비운다 (화면이 null 로 판단)</summary>
    static MatchState Copy(MatchState s)
    {
        MatchState c = JsonUtility.FromJson<MatchState>(JsonUtility.ToJson(s));
        if (s.partner == null) c.partner = null;
        if (s.spot == null) c.spot = null;
        if (!s.HasCourse) c.course = null;
        return c;
    }

    static int SecondsUntil(string iso)
    {
        if (string.IsNullOrEmpty(iso) || !DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset t))
            return 0;
        double seconds = Math.Ceiling((t - DateTimeOffset.UtcNow).TotalSeconds);
        return (int)Math.Clamp(seconds, 0d, 86400d);   // 하루 넘게는 세지 않는다 (int 넘침 방지)
    }

    /// <summary>서버 MatchState JSON → 앱 상태 (테스트에서도 씀). 읽지 못하면 null</summary>
    public static MatchState Parse(string json)
    {
        ServerState s = null;
        try { s = JsonUtility.FromJson<ServerState>(json); }
        catch (ArgumentException) { }
        return s == null || string.IsNullOrEmpty(s.state) ? null : ToApp(s);
    }

    /// <summary>서버 상태 → 앱 상태</summary>
    static MatchState ToApp(ServerState s)
    {
        var a = new MatchState
        {
            matchId = s.matchId,
            minutes = s.minutes,
            walkId = s.walkId,
            smalltalk = s.smalltalk ?? new List<string>(),
            cards = Cards(s.smalltalk, s.smalltalkCards),
            course = s.course != null && !string.IsNullOrEmpty(s.course.recommendationId) ? s.course : null,
            nowSearching = s.nowSearching,
            myLook = s.myAppearance,
            meArrived = s.arrived != null && s.arrived.me,
            partnerArrived = s.arrived != null && s.arrived.partner,
        };
        if (s.partner != null && !string.IsNullOrEmpty(s.partner.nickname))
        {
            a.partner = new MatchPartner
            {
                nickname = s.partner.nickname,
                commonTastes = s.partner.commonTastes ?? new List<string>(),
                whyMatched = s.partner.whyMatched,
            };
            a.partnerLook = s.partner.appearance;
        }
        if (s.meetSpot != null && !string.IsNullOrEmpty(s.meetSpot.buildingId))
            a.spot = new MeetSpot { buildingId = s.meetSpot.buildingId, name = s.meetSpot.name, lat = s.meetSpot.lat, lon = s.meetSpot.lon, species = s.meetSpot.meetSpecies };
        switch (s.state)
        {
            case "waiting":
                a.status = MatchState.Waiting;
                a.waitSecondsLeft = SecondsUntil(s.waitingUntil);
                break;
            case "proposed":
                a.status = s.myAnswer == "accepted" ? MatchState.Accepted : MatchState.Proposed;
                a.decideSecondsLeft = SecondsUntil(s.respondBy);
                break;
            case "meeting":
                a.status = MatchState.Matched;
                a.noShowSecondsLeft = SecondsUntil(s.meetBy);
                if (a.meArrived && !a.partnerArrived)
                    a.message = "도착했어요. 상대가 오면 산책이 시작돼요";
                break;
            case "walking":
                a.status = MatchState.Walking;
                break;
            case "ended":
                EndOf(s, a);
                break;
            default:
                a.status = MatchState.Idle;
                break;
        }
        return a;
    }

    /// <summary>주제 카드: 서버가 smalltalkCards 를 주면 그대로, 아니면 글만으로 만든다 (예전 서버)</summary>
    static List<TopicCard> Cards(List<string> texts, List<TopicCard> cards)
    {
        var result = new List<TopicCard>();
        if (cards != null)
            foreach (TopicCard c in cards)
                if (c != null && !string.IsNullOrEmpty(c.text)) result.Add(c);
        if (result.Count == 0 && texts != null)
            foreach (string t in texts)
                if (!string.IsNullOrEmpty(t)) result.Add(TopicCard.Plain(t));
        return result;
    }

    static void EndOf(ServerState s, MatchState a)
    {
        bool me = s.cancelledBy == "me";
        switch (s.endReason)
        {
            case "completed":
                a.status = MatchState.Ended;
                return;
            case "no_partner":
                a.status = MatchState.Expired;
                a.message = "대기 시간 안에 산책메이트를 찾지 못했어요";
                return;
            case "timeout":
                a.status = MatchState.Expired;
                a.message = "응답 시간이 지났어요";
                return;
            case "declined":
                a.status = MatchState.Cancelled;
                a.message = me ? "제안을 거절했어요" : "상대가 이번 산책을 거절했어요";
                return;
            case "noshow":
                a.status = MatchState.Cancelled;
                a.message = "10분 안에 만나지 못해 자동으로 취소됐어요";
                return;
            case "reported":
                a.status = MatchState.Cancelled;
                a.message = me ? "신고하고 차단했어요. 다시 매칭되지 않아요" : "매칭이 끝났어요";
                return;
            case "blocked":
                a.status = MatchState.Cancelled;
                a.message = me ? "차단했어요. 다시 매칭되지 않아요" : "매칭이 끝났어요";
                return;
            default:
                a.status = MatchState.Cancelled;
                a.message = me ? "매칭을 취소했어요" : "상대가 매칭을 취소했어요";
                return;
        }
    }

    string M(string matchId) => Uri.EscapeDataString(matchId ?? "");

    public IEnumerator Join(MatchQueueRequest request, Action<MatchState, string> onDone)
    {
        var body = new QueueBody { minutes = request.minutes, matchGender = request.sameGender ? "same" : "any", buildingId = request.buildingId };
        yield return Send("POST", "/api/match/queue", JsonUtility.ToJson(body), () => fallback.Join(request, onDone), onDone);
    }

    public IEnumerator Leave(Action<MatchState, string> onDone)
    {
        if (useFallback) { yield return fallback.Leave(onDone); yield break; }
        if (!AuthService.HasToken) { onDone(new MatchState(), null); yield break; }
        using var req = Request("DELETE", "/api/match/queue");
        yield return req.SendWebRequest();
        if (req.responseCode == 404) { useFallback = true; yield return fallback.Leave(onDone); yield break; }
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        onDone(Remember(new MatchState()), null);   // 204: 대기에서 빠짐
    }

    public IEnumerator Status(Action<MatchState, string> onDone) =>
        Send("GET", "/api/match", null, () => fallback.Status(onDone), onDone);

    public IEnumerator Respond(string matchId, bool accept, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{M(matchId)}/{(accept ? "accept" : "decline")}", "{}",
            () => fallback.Respond(matchId, accept, onDone), onDone, false);

    public IEnumerator SetLook(string matchId, string look, Action<MatchState, string> onDone) =>
        Send("PUT", $"/api/match/{M(matchId)}/appearance", JsonUtility.ToJson(new TextBody { text = look }),
            () => fallback.SetLook(matchId, look, onDone), onDone, false);

    /// <summary>만남 스팟 도착 (POST arrived). 둘 다 도착하면 서버가 walking 으로 바꾸고 산책 ID 를 준다</summary>
    public IEnumerator StartWalk(string matchId, Action<MatchState, string> onDone)
    {
        string building = last.matchId == matchId ? last.spot?.buildingId : null;
        yield return Send("POST", $"/api/match/{M(matchId)}/arrived", JsonUtility.ToJson(new BuildingBody { buildingId = building ?? "" }),
            () => fallback.StartWalk(matchId, onDone), onDone, false);
    }

    /// <summary>
    /// 산책 끝내기. 서버의 매칭 끝은 산책 완료 (POST /api/walks/{walkId}/finish, 완료 화면이 부름) 로 정해지므로
    /// 여기서는 서버를 부르지 않고 앱 상태만 끝낸다 (그 뒤 완료 화면이 산책을 서버에서 끝낸다).
    /// </summary>
    public IEnumerator End(string matchId, Action<MatchState, string> onDone)
    {
        if (useFallback) { yield return fallback.End(matchId, onDone); yield break; }
        yield return null;
        var s = new MatchState { status = MatchState.Ended, matchId = matchId, minutes = last.minutes, partner = last.partner, walkId = last.walkId };
        onDone(Remember(s), null);
    }

    public IEnumerator Cancel(string matchId, string reason, Action<MatchState, string> onDone)
    {
        string r = reason == "late" || reason == "changed_mind" ? reason : "other";
        yield return Send("POST", $"/api/match/{M(matchId)}/cancel", JsonUtility.ToJson(new ReasonBody { reason = r }),
            () => fallback.Cancel(matchId, reason, onDone), onDone, false);
    }

    /// <summary>신고하면 서버가 자동으로 차단한다. block 만 고르면 (신고 이유 없이) 차단만 한다</summary>
    public IEnumerator Report(string matchId, string reason, string detail, bool block, Action<MatchState, string> onDone)
    {
        if (useFallback) { yield return fallback.Report(matchId, reason, detail, block, onDone); yield break; }
        if (!AuthService.HasToken) { onDone(null, "로그인이 필요해요"); yield break; }
        string serverReason = reason switch { "rude" => "rude", "noshow" => "noshow", "personal" => "unsafe", _ => "other" };
        using (var req = Request("POST", "/api/reports", JsonUtility.ToJson(new ReportBody { matchId = matchId, reason = serverReason, text = detail ?? "" })))
        {
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        }
        // 신고는 자동 차단이라 따로 차단을 부르지 않는다. 바뀐 상태 (ended · reported) 를 다시 받아 온다
        var s = new MatchState { status = MatchState.Cancelled, matchId = matchId, message = "신고하고 차단했어요. 다시 매칭되지 않아요" };
        onDone(Remember(s), null);
    }

    public IEnumerator AgreeCourse(string matchId, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{M(matchId)}/course/agree", "{}", () => fallback.AgreeCourse(matchId, onDone), onDone, false);

    public IEnumerator NextCourse(string matchId, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{M(matchId)}/course/next", "{}", () => fallback.NextCourse(matchId, onDone), onDone, false);

    /// <summary>"다음 주제": 서버가 주는 3개를 하나씩 넘기고, 다 보면 다시 부른다 (매칭 하나에 5번까지)</summary>
    public IEnumerator NextTopic(string matchId, Action<TopicCard, string> onDone)
    {
        if (useFallback) { yield return fallback.NextTopic(matchId, onDone); yield break; }
        if (topics.Count == 0)
        {
            using var req = Request("POST", $"/api/match/{M(matchId)}/smalltalk", "{}");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
            SmalltalkBody t = null;
            try { t = JsonUtility.FromJson<SmalltalkBody>(req.downloadHandler.text); }
            catch (ArgumentException) { }
            if (t != null)
                foreach (TopicCard card in Cards(t.smalltalk, t.smalltalkCards))
                    topics.Enqueue(card);
            if (topics.Count == 0) { onDone(null, "서버 응답을 읽지 못했어요"); yield break; }
        }
        onDone(topics.Dequeue(), null);
    }
}
