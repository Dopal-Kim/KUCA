using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 산책 완료 · 리뷰 · 보상 (B-V4). 서버 모양은 docs/api.md "산책 · 완료 · 리뷰 · 보상" (HttpWalkService 가 옮겨 담는다).
// 아래 클래스는 앱 안에서 쓰는 모양이고, 서버가 없을 때는 LocalWalkService 가 같은 모양으로 계산한다.

/// <summary>끝난 산책 하나 (WalkSession.Finish 가 만든다)</summary>
[Serializable]
public class WalkCompleteRequest
{
    /// <summary>"course" (산책 추천 코스) · "mate" (산책메이트와 함께)</summary>
    public string kind = WalkSession.Course;
    public string matchId = "";
    /// <summary>서버 산책 ID (POST /api/walks 또는 매칭의 walkId). 없거나 local_ 이면 기기에서 판정</summary>
    public string walkId = "";
    public int meters, minutes, durationSec;
    public int plannedMeters, stopsDone, stopsTotal;
    /// <summary>코스에서 들른 경희스팟 건물 ID</summary>
    public List<string> visitedBuildingIds = new List<string>();
}

[Serializable]
public class WalkCompleteResult
{
    public string walkId;
    public bool completed;
    public string reason;
    public int xp, tickets, growth;
    public int level, xpInLevel, xpNeeded;
}

[Serializable]
public class WalkReviewRequest
{
    /// <summary>코스 별점 1~5 (필수). 리뷰는 코스만 평가한다 (10/24 결정, 상대 별점 없음)</summary>
    public int courseRating;
    /// <summary>한 줄 0~100자 (내용 검사)</summary>
    public string text = "";
    public const int MaxText = 100;
}

[Serializable]
public class WalkReviewResult
{
    public int xp;
}

/// <summary>완료 · 리뷰 서버. HTTP(HttpWalkService) 와 기기 계산(LocalWalkService) 이 같은 모양을 따른다.</summary>
public interface IWalkService
{
    /// <summary>혼자 걷는 산책 시작 (POST /api/walks). onDone(산책 ID, 에러)</summary>
    IEnumerator Begin(string recommendationId, int minutes, Action<string, string> onDone);
    /// <summary>score: 보상을 더하기 전 경험치 (기기 계산이 레벨을 셈)</summary>
    IEnumerator Complete(WalkCompleteRequest request, int score, Action<WalkCompleteResult, string> onDone);
    IEnumerator Review(string walkId, WalkReviewRequest request, Action<WalkReviewResult, string> onDone);
}

/// <summary>
/// 지금 하고 있는 산책 하나 (코스 또는 산책메이트). 시작할 때의 걸은 거리 · 시각을 기억해 두었다가
/// 끝날 때 이번 산책에서 걸은 거리 · 시간을 계산한다.
/// </summary>
public static class WalkSession
{
    public const string Course = "course", Mate = "mate";

    public static bool Active { get; private set; }
    public static string Kind { get; private set; }
    public static string Title { get; private set; }
    public static string MatchId { get; private set; }
    public static string PartnerNickname { get; private set; }
    public static int PlannedMeters { get; private set; }
    public static int StopsTotal { get; private set; }
    /// <summary>서버 산책 ID (시작 응답이 오기 전 · 서버가 없으면 빈 값)</summary>
    public static string WalkId { get; private set; }

    static float startMeters, startTime;
    static int session;

    static GameProgress Progress
    {
        get
        {
            var collect = UnityEngine.Object.FindAnyObjectByType<CollectController>();
            return collect != null ? collect.Progress : null;
        }
    }

    public static void BeginCourse(string title, int plannedMeters, int stops) =>
        Begin(Course, title, plannedMeters, stops, "", null, "");

    /// <summary>walkId: 매칭 서버가 두 사람 각각에게 만든 산책 ID (MatchState.walkId)</summary>
    public static void BeginMate(string matchId, string partnerNickname, string walkId) =>
        Begin(Mate, $"{partnerNickname ?? "산책메이트"}님과 함께", 0, 0, matchId, partnerNickname, walkId);

    /// <summary>지금 산책에 서버 산책 ID 를 붙이는 콜백 (응답이 늦게 와서 다른 산책이 시작됐으면 무시)</summary>
    public static Action<string, string> WalkIdSetter()
    {
        int mine = session;
        return (id, error) =>
        {
            if (mine == session && Active && !string.IsNullOrEmpty(id))
                WalkId = id;
        };
    }

    static void Begin(string kind, string title, int planned, int stops, string matchId, string nickname, string walkId)
    {
        session++;
        WalkId = walkId ?? "";
        Active = true;
        Kind = kind;
        Title = title;
        PlannedMeters = planned;
        StopsTotal = stops;
        MatchId = matchId ?? "";
        PartnerNickname = nickname;
        startMeters = Progress != null ? Progress.walkedMeters : 0f;
        startTime = Time.realtimeSinceStartup;
    }

    public static float WalkedMeters => Active && Progress != null ? Mathf.Max(0f, Progress.walkedMeters - startMeters) : 0f;

    /// <summary>산책을 끝내고 서버에 보낼 내용을 만든다. visited: 코스에서 들른 건물 ID (산책메이트는 null)</summary>
    public static WalkCompleteRequest Finish(List<string> visited)
    {
        float seconds = Mathf.Max(0f, Time.realtimeSinceStartup - startTime);
        var r = new WalkCompleteRequest
        {
            kind = Kind ?? Course,
            matchId = MatchId ?? "",
            walkId = WalkId ?? "",
            meters = Mathf.RoundToInt(WalkedMeters),
            minutes = Mathf.RoundToInt(seconds / 60f),
            durationSec = Mathf.RoundToInt(seconds),
            plannedMeters = PlannedMeters,
            stopsDone = visited?.Count ?? 0,
            stopsTotal = StopsTotal,
            visitedBuildingIds = visited != null ? new List<string>(visited) : new List<string>(),
        };
        Active = false;
        return r;
    }

    public static void Abandon() => Active = false;
}
