using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

// 산책 추천 · 장소 공지 (B-4). docs/api.md 의 "산책 추천", "장소 공지" 와 같은 모양 (JsonUtility 로 그대로 읽는다).
// v2 칸 (B-V2) 은 docs/api.md "POST /api/recommendations (v2)". 응답에 없으면 (기기 추천) 앱이 RouteMath 로 채운다.
// 시간대 · 계절 · 날씨는 서버가 정하고, 칼로리는 다루지 않는다 (10/23 결정).

/// <summary>POST /api/recommendations 본문</summary>
[Serializable]
public class RouteRequest
{
    /// <summary>걸을 시간 (분): 15, 30, 60</summary>
    public int minutes;
    /// <summary>"photo"(사진 명소) · "quiet"(조용한 곳) · "food"(맛집·카페) · "unvisited"(안 가 본 곳)</summary>
    public List<string> tastes = new List<string>();
    /// <summary>지금 위치 (GPS)</summary>
    public double lat, lon;
    /// <summary>이미 가 본 경희스팟 건물 ID</summary>
    public List<string> visitedBuildingIds = new List<string>();
    /// <summary>추천 후보 (앱의 경희스팟 건물 ID). 서버는 이 안에서만 고른다</summary>
    public List<string> candidateBuildingIds = new List<string>();

    // ---------- v2 ----------
    /// <summary>걷는 속도 "slow" · "normal" · "fast" (취향 체크리스트)</summary>
    public string pace = "normal";
    /// <summary>식후 걷기 버튼으로 요청함 (가까운 곳 위주, 응답에 healthNote)</summary>
    public bool afterMeal;
}

[Serializable]
public class RouteStop
{
    public string buildingId;
    public string name;
    /// <summary>추천 이유 한 문장</summary>
    public string reason;
    /// <summary>앞 장소(첫 곳은 지금 위치)에서 걸어서 몇 분</summary>
    public int walkMinutes;
    public double lat, lon;
    /// <summary>v2: 앞 장소(첫 곳은 지금 위치)에서 여기까지 걷는 거리 (m)</summary>
    public int distanceM;
}

/// <summary>서버가 추천에 쓴 상황 (시간대 · 계절 · 날씨)</summary>
[Serializable]
public class RouteContext
{
    /// <summary>morning · day · evening · night</summary>
    public string timeOfDay;
    /// <summary>spring · summer · autumn · winter</summary>
    public string season;
    /// <summary>clear · cloudy · rain · snow (모르면 빈 값)</summary>
    public string weather;
}

[Serializable]
public class RouteResult
{
    public string title;
    public List<RouteStop> stops = new List<RouteStop>();
    /// <summary>"ai"(AI 추천) 또는 "fallback"(AI 없이 기본 규칙으로 고름)</summary>
    public string source;

    // ---------- v2 ----------
    /// <summary>추천 ID. 이 코스로 산책을 시작할 때 POST /api/walks 에 넣는다 (기기 추천은 빈 값)</summary>
    public string id;
    public RouteContext context;
    /// <summary>코스 전체 걷는 거리 (m) · 시간 (분)</summary>
    public int totalDistanceM;
    public int totalMinutes;
    /// <summary>식후 걷기 안내 (afterMeal 일 때만, 의료 조언 아님 문구 포함)</summary>
    public string healthNote;
}

/// <summary>학교 공지 중 특정 장소와 관련된 것 (GET /api/notices)</summary>
[Serializable]
public class PlaceNotice
{
    public string id;
    public string buildingId;
    public string buildingName;
    public string title;
    /// <summary>한두 문장 요약</summary>
    public string summary;
    /// <summary>원문 링크</summary>
    public string url;
    /// <summary>게시일 (yyyy-MM-dd)</summary>
    public string postedAt;
}

[Serializable]
public class PlaceNoticeList { public List<PlaceNotice> items = new List<PlaceNotice>(); }

public interface IRouteService
{
    IEnumerator Recommend(RouteRequest request, Action<RouteResult, string> onDone);
    /// <summary>buildingId 가 null 이면 장소와 연결된 모든 공지 (최신순)</summary>
    IEnumerator GetNotices(string buildingId, Action<List<PlaceNotice>, string> onDone);
}
