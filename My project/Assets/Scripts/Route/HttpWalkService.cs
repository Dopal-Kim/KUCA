using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버 완료 · 리뷰 API (docs/api_v2_완료_초안.md, 정식 명세는 C). 로그인 토큰을 붙여 부른다.
/// 서버에 아직 API 가 없으면 (404) 기기 계산(LocalWalkService)으로 바꾼다.
/// </summary>
public class HttpWalkService : IWalkService
{
    readonly string baseUrl;
    readonly LocalWalkService fallback;
    bool useFallback;

    [Serializable] class ErrorBody { public string error; }

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
        try
        {
            var e = JsonUtility.FromJson<ErrorBody>(req.downloadHandler?.text ?? "");
            if (!string.IsNullOrEmpty(e?.error)) return e.error;
        }
        catch (ArgumentException) { }
        return $"서버 오류 ({req.responseCode})";
    }

    public IEnumerator Complete(WalkCompleteRequest request, int score, Action<WalkCompleteResult, string> onDone)
    {
        if (!useFallback)
        {
            using var req = Post("/api/walks/complete", JsonUtility.ToJson(request));
            yield return req.SendWebRequest();
            if (req.responseCode != 404)
            {
                if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
                WalkCompleteResult r = null;
                try { r = JsonUtility.FromJson<WalkCompleteResult>(req.downloadHandler.text); }
                catch (ArgumentException) { }
                onDone(r, r == null ? "서버 응답을 읽지 못했어요" : null);
                yield break;
            }
            Debug.Log("[Walk] 서버에 완료 API 가 없어 기기에서 계산해요");
            useFallback = true;
        }
        yield return fallback.Complete(request, score, onDone);
    }

    public IEnumerator Review(string walkId, WalkReviewRequest request, Action<WalkReviewResult, string> onDone)
    {
        // 기기에서 계산한 산책 (local_) 은 서버가 모르니 리뷰도 기기에서
        if (!useFallback && walkId != null && !walkId.StartsWith("local_"))
        {
            using var req = Post($"/api/walks/{Uri.EscapeDataString(walkId)}/review", JsonUtility.ToJson(request));
            yield return req.SendWebRequest();
            if (req.responseCode != 404)
            {
                if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
                WalkReviewResult r = null;
                try { r = JsonUtility.FromJson<WalkReviewResult>(req.downloadHandler.text); }
                catch (ArgumentException) { }
                onDone(r, r == null ? "서버 응답을 읽지 못했어요" : null);
                yield break;
            }
            useFallback = true;
        }
        yield return fallback.Review(walkId, request, onDone);
    }
}
