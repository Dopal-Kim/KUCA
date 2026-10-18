using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버 매칭 API (docs/api_v2_매칭_초안.md, 정식 명세는 C). 로그인 토큰을 붙여 부른다.
/// 서버에 아직 API 가 없으면 (404) 그때부터 기기 안 가상 상대(MockMatchService)로 바꿔서 흐름이 끊기지 않게 한다.
/// </summary>
public class HttpMatchService : IMatchService
{
    readonly string baseUrl;
    readonly MockMatchService fallback;
    bool useFallback;
    const int Timeout = 20;

    [Serializable] class ErrorBody { public string error; public string message; }
    [Serializable] class LookBody { public string look; }
    [Serializable] class ReasonBody { public string reason; }
    [Serializable] class ReportBody { public string reason; public string detail; public bool block; }
    [Serializable] class TopicBody { public string topic; }

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
        try
        {
            var e = JsonUtility.FromJson<ErrorBody>(req.downloadHandler?.text ?? "");
            if (!string.IsNullOrEmpty(e?.error)) return e.error;
            if (!string.IsNullOrEmpty(e?.message)) return e.message;
        }
        catch (ArgumentException) { }
        return $"서버 오류 ({req.responseCode})";
    }

    /// <summary>MatchState 를 돌려주는 요청. 404 면 가상 상대로 바꿔서 alt 를 대신 부른다.</summary>
    IEnumerator Send(string method, string path, string json, Func<IEnumerator> alt, Action<MatchState, string> onDone)
    {
        if (useFallback) { yield return alt(); yield break; }
        using var req = Request(method, path, json);
        yield return req.SendWebRequest();
        if (req.responseCode == 404)
        {
            Debug.Log("[Match] 서버에 매칭 API 가 없어 가상 상대로 바꿔요");
            useFallback = true;
            yield return alt();
            yield break;
        }
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        MatchState s = null;
        try { s = JsonUtility.FromJson<MatchState>(req.downloadHandler.text); }
        catch (ArgumentException) { }
        onDone(s, s == null ? "서버 응답을 읽지 못했어요" : null);
    }

    public IEnumerator Join(MatchQueueRequest request, Action<MatchState, string> onDone) =>
        Send("POST", "/api/match/queue", JsonUtility.ToJson(request), () => fallback.Join(request, onDone), onDone);

    public IEnumerator Leave(Action<MatchState, string> onDone) =>
        Send("DELETE", "/api/match/queue", null, () => fallback.Leave(onDone), onDone);

    public IEnumerator Status(Action<MatchState, string> onDone) =>
        Send("GET", "/api/match", null, () => fallback.Status(onDone), onDone);

    public IEnumerator Respond(string matchId, bool accept, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{Uri.EscapeDataString(matchId ?? "")}/{(accept ? "accept" : "decline")}", "{}",
            () => fallback.Respond(matchId, accept, onDone), onDone);

    public IEnumerator SetLook(string matchId, string look, Action<MatchState, string> onDone) =>
        Send("PUT", $"/api/match/{Uri.EscapeDataString(matchId ?? "")}/look", JsonUtility.ToJson(new LookBody { look = look }),
            () => fallback.SetLook(matchId, look, onDone), onDone);

    public IEnumerator StartWalk(string matchId, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{Uri.EscapeDataString(matchId ?? "")}/start", "{}", () => fallback.StartWalk(matchId, onDone), onDone);

    public IEnumerator End(string matchId, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{Uri.EscapeDataString(matchId ?? "")}/end", "{}", () => fallback.End(matchId, onDone), onDone);

    public IEnumerator Cancel(string matchId, string reason, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{Uri.EscapeDataString(matchId ?? "")}/cancel", JsonUtility.ToJson(new ReasonBody { reason = reason }),
            () => fallback.Cancel(matchId, reason, onDone), onDone);

    public IEnumerator Report(string matchId, string reason, string detail, bool block, Action<MatchState, string> onDone) =>
        Send("POST", $"/api/match/{Uri.EscapeDataString(matchId ?? "")}/report",
            JsonUtility.ToJson(new ReportBody { reason = reason, detail = detail, block = block }),
            () => fallback.Report(matchId, reason, detail, block, onDone), onDone);

    public IEnumerator NextTopic(string matchId, Action<string, string> onDone)
    {
        if (useFallback) { yield return fallback.NextTopic(matchId, onDone); yield break; }
        using var req = Request("POST", $"/api/match/{Uri.EscapeDataString(matchId ?? "")}/topic", "{}");
        yield return req.SendWebRequest();
        if (req.responseCode == 404) { useFallback = true; yield return fallback.NextTopic(matchId, onDone); yield break; }
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        TopicBody t = null;
        try { t = JsonUtility.FromJson<TopicBody>(req.downloadHandler.text); }
        catch (ArgumentException) { }
        onDone(t?.topic, t == null ? "서버 응답을 읽지 못했어요" : null);
    }
}
