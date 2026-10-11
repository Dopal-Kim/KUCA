using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 서버(C)의 산책 추천·장소 공지 API. 서버에 아직 API 가 없거나(404) AI 가 실패하면
/// 기기 안의 기본 추천(LocalRouteService)으로 바꿔서 화면이 깨지지 않게 한다.
/// </summary>
public class HttpRouteService : IRouteService
{
    readonly string baseUrl;
    readonly LocalRouteService fallback;
    const int RecommendTimeout = 30; // AI 가 코스를 짜는 시간 + 무료 요금제 서버가 깨어나는 시간

    public HttpRouteService(string baseUrl, LocalRouteService fallback)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
        this.fallback = fallback;
    }

    public IEnumerator Recommend(RouteRequest request, Action<RouteResult, string> onDone)
    {
        using var req = new UnityWebRequest($"{baseUrl}/api/recommendations", "POST",
            new DownloadHandlerBuffer(), new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(request))));
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = RecommendTimeout;
        yield return req.SendWebRequest();
        RouteResult result = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { result = JsonUtility.FromJson<RouteResult>(req.downloadHandler.text); }
            catch (ArgumentException) { }
        }
        if (result != null && result.stops != null && result.stops.Count > 0)
        {
            onDone(result, null);
            yield break;
        }
        Debug.Log($"[Route] 서버 추천을 못 받아 기본 추천으로 ({req.responseCode} {req.error})");
        yield return fallback.Recommend(request, onDone);
    }

    public IEnumerator GetNotices(string buildingId, Action<List<PlaceNotice>, string> onDone)
    {
        string url = $"{baseUrl}/api/notices" + (string.IsNullOrEmpty(buildingId) ? "" : $"?buildingId={UnityWebRequest.EscapeURL(buildingId)}");
        using var req = UnityWebRequest.Get(url);
        req.timeout = 20;
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            PlaceNoticeList list = null;
            try { list = JsonUtility.FromJson<PlaceNoticeList>("{\"items\":" + req.downloadHandler.text + "}"); }
            catch (ArgumentException) { }
            if (list?.items != null)
            {
                onDone(list.items, null);
                yield break;
            }
        }
        if (req.responseCode == 404 || req.result != UnityWebRequest.Result.Success)
        {
            // 서버에 공지 API 가 아직 없으면 예시 공지를 보여 준다.
            yield return fallback.GetNotices(buildingId, onDone);
            yield break;
        }
        onDone(new List<PlaceNotice>(), "공지를 불러오지 못했어요");
    }
}
