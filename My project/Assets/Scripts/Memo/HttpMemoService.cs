using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 실제 메모 서버(server/, docs/api.md)와 통신한다.
/// </summary>
public class HttpMemoService : IMemoService
{
    readonly string baseUrl;
    // Azure 무료 요금제(F1)는 잠들었다 깨어나는 첫 요청이 느려서 넉넉히 기다린다.
    const int TimeoutSeconds = 40;

    public HttpMemoService(string baseUrl) => this.baseUrl = baseUrl.TrimEnd('/');

    public string Description => baseUrl;

    [Serializable] class ErrorBody { public string error; }

    static string ErrorOf(UnityWebRequest req)
    {
        if (req.result == UnityWebRequest.Result.ConnectionError)
            return "서버에 연결할 수 없어요. 와이파이와 서버 주소를 확인해 주세요.";
        try
        {
            var body = JsonUtility.FromJson<ErrorBody>(req.downloadHandler?.text ?? "");
            if (!string.IsNullOrEmpty(body?.error))
                return body.error;
        }
        catch (ArgumentException) { }
        return $"서버 오류 ({req.responseCode})";
    }

    /// <summary>"192.168.0.5:5080" 처럼 쓴 주소에 http:// 를 붙이고 끝의 / 를 뗀다.</summary>
    public static string Normalize(string url)
    {
        url = (url ?? "").Trim().TrimEnd('/');
        if (url.Length > 0 && !url.StartsWith("http://") && !url.StartsWith("https://"))
            url = "http://" + url;
        return url;
    }

    /// <summary>GET /api/health 로 서버가 살아 있는지 확인한다.</summary>
    public static IEnumerator Check(string url, Action<bool, string> onDone)
    {
        using var req = UnityWebRequest.Get(Normalize(url) + "/api/health");
        req.timeout = 40;
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
            onDone(true, null);
        else
            onDone(false, req.result == UnityWebRequest.Result.ConnectionError
                ? "연결할 수 없어요. 같은 와이파이인지, 서버가 켜져 있는지 확인해 주세요."
                : $"서버 응답 오류 ({req.responseCode})");
    }

    public IEnumerator GetMemos(string buildingId, string deviceId, Action<List<MemoData>, string> onDone)
    {
        using var req = UnityWebRequest.Get($"{baseUrl}/api/buildings/{UnityWebRequest.EscapeURL(buildingId)}/memos?limit=50");
        if (!string.IsNullOrEmpty(deviceId))
            req.SetRequestHeader("X-Device-Id", deviceId); // likedByMe 를 받으려고
        req.timeout = TimeoutSeconds;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        // 응답은 JSON 배열이라 JsonUtility가 읽을 수 있게 감싼다.
        var list = JsonUtility.FromJson<MemoDataList>("{\"items\":" + req.downloadHandler.text + "}");
        onDone(list.items, null);
    }

    public IEnumerator CreateMemo(string buildingId, NewMemo memo, Action<MemoData, string> onDone)
    {
        var form = new WWWForm();
        form.AddField("author", memo.author ?? "");
        form.AddField("text", memo.text ?? "");
        form.AddField("deviceId", memo.deviceId ?? "");
        if (memo.photoJpg != null && memo.photoJpg.Length > 0)
            form.AddBinaryData("photo", memo.photoJpg, "photo.jpg", "image/jpeg");

        using var req = UnityWebRequest.Post($"{baseUrl}/api/buildings/{UnityWebRequest.EscapeURL(buildingId)}/memos", form);
        req.timeout = TimeoutSeconds * 2;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        onDone(JsonUtility.FromJson<MemoData>(req.downloadHandler.text), null);
    }

    public IEnumerator DeleteMemo(string memoId, string deviceId, Action<bool, string> onDone)
    {
        using var req = UnityWebRequest.Delete($"{baseUrl}/api/memos/{UnityWebRequest.EscapeURL(memoId)}");
        req.SetRequestHeader("X-Device-Id", deviceId);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = TimeoutSeconds;
        yield return req.SendWebRequest();
        if (req.responseCode == 404) { onDone(false, MemoData.NotFound); yield break; }
        if (req.result != UnityWebRequest.Result.Success) { onDone(false, ErrorOf(req)); yield break; }
        onDone(true, null);
    }

    public IEnumerator GetMemo(string memoId, string deviceId, Action<MemoData, string> onDone)
    {
        using var req = UnityWebRequest.Get($"{baseUrl}/api/memos/{UnityWebRequest.EscapeURL(memoId)}");
        if (!string.IsNullOrEmpty(deviceId))
            req.SetRequestHeader("X-Device-Id", deviceId);
        req.timeout = TimeoutSeconds;
        yield return req.SendWebRequest();
        if (req.responseCode == 404) { onDone(null, MemoData.NotFound); yield break; }
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        onDone(JsonUtility.FromJson<MemoData>(req.downloadHandler.text), null);
    }

    public string PublicPhotoUrl(MemoData memo) =>
        memo == null || !memo.HasPhoto ? null : memo.photoUrl.StartsWith("http") ? memo.photoUrl : baseUrl + memo.photoUrl;

    public string PublicPageUrl(string buildingId) => $"{baseUrl}/buildings/{UnityWebRequest.EscapeURL(buildingId)}";

    [Serializable] class LikeBody { public int likeCount; public bool likedByMe; }

    /// <summary>PUT(누르기) / DELETE(취소) /api/memos/{id}/like</summary>
    public IEnumerator SetLike(string memoId, string deviceId, bool like, Action<int, bool, string> onDone)
    {
        string url = $"{baseUrl}/api/memos/{UnityWebRequest.EscapeURL(memoId)}/like";
        using var req = new UnityWebRequest(url, like ? "PUT" : "DELETE", new DownloadHandlerBuffer(), null);
        req.SetRequestHeader("X-Device-Id", deviceId);
        req.timeout = TimeoutSeconds;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { onDone(0, !like, ErrorOf(req)); yield break; }
        var body = JsonUtility.FromJson<LikeBody>(req.downloadHandler.text);
        onDone(body.likeCount, body.likedByMe, null);
    }

    public IEnumerator LoadPhoto(MemoData memo, Action<Texture2D, string> onDone)
    {
        using var req = UnityWebRequestTexture.GetTexture(baseUrl + memo.photoUrl);
        req.timeout = TimeoutSeconds;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { onDone(null, ErrorOf(req)); yield break; }
        onDone(DownloadHandlerTexture.GetContent(req), null);
    }
}
