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
    const int TimeoutSeconds = 15;

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

    public IEnumerator GetMemos(string buildingId, Action<List<MemoData>, string> onDone)
    {
        using var req = UnityWebRequest.Get($"{baseUrl}/api/buildings/{UnityWebRequest.EscapeURL(buildingId)}/memos?limit=50");
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
        if (req.result != UnityWebRequest.Result.Success) { onDone(false, ErrorOf(req)); yield break; }
        onDone(true, null);
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
