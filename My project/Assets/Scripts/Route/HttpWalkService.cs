using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버 산책 · 완료 · 리뷰 API (docs/api.md "산책 · 완료 · 리뷰 · 보상"). 로그인 토큰을 붙여 부른다.
/// - 서버가 완료를 판정하고 보상(경험치 · 완료 티켓)을 정한다. 앱은 받은 값을 기기 저장에 더한다.
/// - 서버 산책 ID 가 없을 때 (시작을 못 알림 · 서버 없는 모드 · 로그인 안 함) 는 기기 계산(LocalWalkService)으로 판정한다.
/// </summary>
public class HttpWalkService : IWalkService
{
    readonly string baseUrl;
    readonly LocalWalkService fallback;

    [Serializable] class ErrorBody { public string error; public string reason; }
    [Serializable] class BeginBody { public string recommendationId; public int minutes; }
    [Serializable] class BeginResult { public string id; }
    [Serializable] class FinishBody { public int distanceM; public int durationSec; public List<string> visitedBuildingIds = new List<string>(); }
    [Serializable] class RewardPart { public string kind; public int xp; }
    [Serializable] class Reward { public int tickets; public int xp; public List<RewardPart> breakdown = new List<RewardPart>(); }
    [Serializable] class FinishResult { public bool completed; public int distanceM; public Reward reward; }
    [Serializable] class ReviewResult { public Reward reward; }

    public HttpWalkService(string baseUrl, LocalWalkService fallback)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
        this.fallback = fallback;
    }

    UnityWebRequest Post(string path, string json)
    {
        var req = new UnityWebRequest(baseUrl + path, "POST", new DownloadHandlerBuffer(), new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)));
        req.SetRequestHeader("Content-Type", "application/json");
        if (AuthService.HasToken) req.SetRequestHeader("Authorization", "Bearer " + AuthService.Token);
        req.timeout = 20;
        return req;
    }

    static string ErrorOf(UnityWebRequest req)
    {
        if (req.result == UnityWebRequest.Result.ConnectionError)
            return "서버에 연결할 수 없어요. 인터넷 연결을 확인해 주세요.";
        switch (req.responseCode)
        {
            case 401: return "로그인이 필요해요";
            case 409: return "이미 처리된 산책이에요";
            case 422: return "한 줄에 쓸 수 없는 내용이 있어요. 고쳐서 다시 보내 주세요";
        }
        try
        {
            var e = JsonUtility.FromJson<ErrorBody>(req.downloadHandler?.text ?? "");
            if (!string.IsNullOrEmpty(e?.error)) return e.error;
        }
        catch (ArgumentException) { }
        return $"서버 오류 ({req.responseCode})";
    }

    static bool OnServer(string walkId) => !string.IsNullOrEmpty(walkId) && !walkId.StartsWith("local_");

    public IEnumerator Begin(string recommendationId, int minutes, Action<string, string> onDone)
    {
        if (!AuthService.HasToken)
        {
            yield return fallback.Begin(recommendationId, minutes, onDone);
            yield break;
        }
        // 기기 추천(ID 없음)은 자유 산책으로 시작한다
        var body = new BeginBody { recommendationId = recommendationId ?? "", minutes = minutes };
        using var req = Post("/api/walks", JsonUtility.ToJson(body));   // 빈 추천 ID 는 서버가 자유 산책으로 본다
        yield return req.SendWebRequest();
        BeginResult r = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { r = JsonUtility.FromJson<BeginResult>(req.downloadHandler.text); }
            catch (ArgumentException) { }
        }
        if (r == null || string.IsNullOrEmpty(r.id))
        {
            Debug.Log($"[Walk] 서버에 산책 시작을 못 알려 기기에서 판정해요 ({req.responseCode} {req.error})");
            yield return fallback.Begin(recommendationId, minutes, onDone);
            yield break;
        }
        onDone(r.id, null);
    }

    public IEnumerator Complete(WalkCompleteRequest request, int score, Action<WalkCompleteResult, string> onDone)
    {
        if (!OnServer(request.walkId) || !AuthService.HasToken)
        {
            yield return fallback.Complete(request, score, onDone);
            yield break;
        }
        var body = new FinishBody { distanceM = request.meters, durationSec = request.durationSec, visitedBuildingIds = request.visitedBuildingIds };
        using var req = Post($"/api/walks/{Uri.EscapeDataString(request.walkId)}/finish", JsonUtility.ToJson(body));
        yield return req.SendWebRequest();
        if (req.responseCode == 404)
        {
            // 서버가 이 산책을 모름 (오래되어 지워짐 등): 기기에서 판정
            yield return fallback.Complete(request, score, onDone);
            yield break;
        }
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        WalkCompleteResult r = ParseFinish(request, req.downloadHandler.text, score);
        onDone(r, r == null ? "서버 응답을 읽지 못했어요" : null);
    }

    /// <summary>서버 finish 응답 JSON → 앱 보상 (테스트에서도 씀). 읽지 못하면 null</summary>
    public static WalkCompleteResult ParseFinish(WalkCompleteRequest request, string json, int score)
    {
        FinishResult f = null;
        try { f = JsonUtility.FromJson<FinishResult>(json); }
        catch (ArgumentException) { }
        return f == null ? null : ToResult(request, f, score);
    }

    /// <summary>
    /// 서버 완료 응답 → 앱 보상. 걸은 거리 경험치(breakdown 의 distance)는 걸을 때 앱이 이미 더했으니 뺀다.
    /// 파트너 진화 경험치는 앱 규칙(완료 10)이고, 레벨은 HUD 와 같은 기기 경험치로 센다.
    /// </summary>
    static WalkCompleteResult ToResult(WalkCompleteRequest request, FinishResult f, int score)
    {
        int xp = 0;
        if (f.reward != null)
        {
            if (f.reward.breakdown != null && f.reward.breakdown.Count > 0)
            {
                foreach (RewardPart part in f.reward.breakdown)
                    if (part != null && part.kind != "distance")
                        xp += part.xp;
            }
            else
                xp = f.reward.xp;
        }
        var r = new WalkCompleteResult
        {
            walkId = request.walkId,
            completed = f.completed,
            reason = LocalWalkService.Reason(request, f.completed),
            xp = xp,
            tickets = f.reward != null ? f.reward.tickets : 0,
            growth = f.completed ? LocalWalkService.CompleteGrowth : 0,
        };
        GameHUD.LevelOf(score + r.xp, out r.level, out r.xpInLevel, out r.xpNeeded);
        return r;
    }

    public IEnumerator Review(string walkId, WalkReviewRequest request, Action<WalkReviewResult, string> onDone)
    {
        // 기기에서 판정한 산책 (local_) 은 서버가 모르니 리뷰도 기기에서
        if (!OnServer(walkId) || !AuthService.HasToken)
        {
            yield return fallback.Review(walkId, request, onDone);
            yield break;
        }
        using var req = Post($"/api/walks/{Uri.EscapeDataString(walkId)}/review", JsonUtility.ToJson(request));
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        ReviewResult r = null;
        try { r = JsonUtility.FromJson<ReviewResult>(req.downloadHandler.text); }
        catch (ArgumentException) { }
        onDone(r != null ? new WalkReviewResult { xp = r.reward != null ? r.reward.xp : 0 } : null, r == null ? "서버 응답을 읽지 못했어요" : null);
    }
}
