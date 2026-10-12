using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 없이 기기 안에서 고르는 기본 산책 코스와 예시 장소 공지.
/// 서버에 추천 API 가 없거나 AI 가 실패했을 때 HttpRouteService 가 대신 쓰고, 가짜 서버 모드에서는 이것만 쓴다.
/// 코스: 지금 위치에서 가까운 경희스팟부터, 고른 취향에 맞는 곳에 점수를 더 줘서 걸을 시간 안에 들를 만큼 고른다.
/// </summary>
public class LocalRouteService : IRouteService
{
    const float WalkMetersPerMinute = 70f;

    readonly Func<List<KyungHeeSpot>> spots;
    readonly Func<Vector3> player;

    // 취향별로 어울리는 경희스팟 (건물 ID). 대략적인 분류라 AI 추천이 붙으면 그쪽이 우선이다.
    static readonly Dictionary<string, string[]> TasteSpots = new Dictionary<string, string[]>
    {
        ["photo"] = new[] { "way-473963422", "way-474085534", "way-474531805", "way-474521125", "relation-8269760", "way-474085536", "way-474521122" },
        ["quiet"] = new[] { "way-474085534", "way-474521125", "way-474521122", "way-585696420", "way-474521121" },
        ["food"] = new[] { "way-474085535", "relation-6739958", "relation-6739964", "relation-8269760" },
    };

    static readonly Dictionary<string, string> TasteReason = new Dictionary<string, string>
    {
        ["unvisited"] = "아직 가 보지 않은 경희스팟이에요",
        ["photo"] = "사진 찍기 좋은 경희의 명소예요",
        ["quiet"] = "조용히 쉬거나 공부하기 좋아요",
        ["food"] = "밥이나 커피를 챙기기 좋아요",
    };

    public LocalRouteService(Func<List<KyungHeeSpot>> spots, Func<Vector3> player)
    {
        this.spots = spots;
        this.player = player;
    }

    public IEnumerator Recommend(RouteRequest request, Action<RouteResult, string> onDone)
    {
        yield return new WaitForSecondsRealtime(0.6f); // 너무 빨리 끝나 연출이 깜빡이지 않게
        List<KyungHeeSpot> candidates = spots().FindAll(s => s != null && s.Building != null);
        if (candidates.Count == 0)
        {
            onDone(null, "추천할 경희스팟을 찾지 못했어요");
            yield break;
        }
        var visited = new HashSet<string>(request.visitedBuildingIds ?? new List<string>());
        var tastes = new HashSet<string>(request.tastes ?? new List<string>());
        int maxStops = request.minutes <= 15 ? 2 : request.minutes <= 30 ? 3 : 5;
        float budget = request.minutes * WalkMetersPerMinute * 0.75f; // 들러서 구경할 시간을 남긴다

        var result = new RouteResult { title = $"{request.minutes}분 산책 코스", source = "fallback" };
        Vector3 at = player();
        float walked = 0f;
        while (result.stops.Count < maxStops && candidates.Count > 0)
        {
            KyungHeeSpot best = null;
            float bestScore = float.MinValue, bestDist = 0f;
            string bestTaste = null;
            foreach (KyungHeeSpot s in candidates)
            {
                float d = Vector3.Distance(Flat(at), Flat(s.transform.position));
                if (walked + d > budget && result.stops.Count > 0)
                    continue;
                string id = s.Building.buildingId;
                float score = -d / 150f;
                string matched = null;
                if (tastes.Contains("unvisited") && !visited.Contains(id)) { score += 2f; matched = "unvisited"; }
                foreach (string t in tastes)
                    if (TasteSpots.TryGetValue(t, out string[] ids) && Array.IndexOf(ids, id) >= 0)
                    {
                        score += 2.5f;
                        matched ??= t;
                    }
                if (score > bestScore)
                {
                    best = s;
                    bestScore = score;
                    bestDist = d;
                    bestTaste = matched;
                }
            }
            if (best == null)
                break;
            candidates.Remove(best);
            walked += bestDist;
            at = best.transform.position;
            result.stops.Add(new RouteStop
            {
                buildingId = best.Building.buildingId,
                name = best.Building.DisplayName,
                reason = bestTaste != null ? TasteReason[bestTaste] : "가는 길에 들르기 좋아요",
                walkMinutes = Mathf.Max(1, Mathf.CeilToInt(bestDist / WalkMetersPerMinute)),
            });
        }
        onDone(result, null);
    }

    public IEnumerator GetNotices(string buildingId, Action<List<PlaceNotice>, string> onDone)
    {
        yield return null;
        var asset = Resources.Load<TextAsset>("RouteSamples/PlaceNotices");
        var all = asset != null ? JsonUtility.FromJson<PlaceNoticeList>(asset.text).items : new List<PlaceNotice>();
        onDone(string.IsNullOrEmpty(buildingId) ? all : all.FindAll(n => n.buildingId == buildingId), null);
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
}
