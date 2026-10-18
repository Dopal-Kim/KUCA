using System;
using System.Collections;
using UnityEngine;

// 산책 완료 · 리뷰 · 보상 (B-V4). docs/api_v2_완료_초안.md 와 같은 모양 (JsonUtility 로 그대로 읽는다).

[Serializable]
public class WalkCompleteRequest
{
    /// <summary>"course" (산책 추천 코스) · "mate" (산책메이트와 함께)</summary>
    public string kind = WalkSession.Course;
    public string matchId = "";
    public int meters, minutes, kcal;
    public int plannedMeters, stopsDone, stopsTotal;
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
    /// <summary>산책메이트 별점 1~5 (코스면 0). 상대에게 보이지 않고 매칭 품질에만</summary>
    public int partnerStars;
    public int courseStars;
    public string comment = "";
}

[Serializable]
public class WalkReviewResult
{
    public int xp;
}

/// <summary>완료 · 리뷰 서버. HTTP(HttpWalkService) 와 기기 계산(LocalWalkService) 이 같은 모양을 따른다.</summary>
public interface IWalkService
{
    /// <summary>score: 보상을 더하기 전 경험치 (기기 계산이 레벨을 셈)</summary>
    IEnumerator Complete(WalkCompleteRequest request, int score, Action<WalkCompleteResult, string> onDone);
    IEnumerator Review(string walkId, WalkReviewRequest request, Action<WalkReviewResult, string> onDone);
}

/// <summary>
/// 지금 하고 있는 산책 하나 (코스 또는 산책메이트). 시작할 때의 걸은 거리 · 시각을 기억해 두었다가
/// 끝날 때 이번 산책에서 걸은 거리 · 시간 · 칼로리를 계산한다.
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

    static float startMeters, startTime;

    static GameProgress Progress
    {
        get
        {
            var collect = UnityEngine.Object.FindAnyObjectByType<CollectController>();
            return collect != null ? collect.Progress : null;
        }
    }

    public static void BeginCourse(string title, int plannedMeters, int stops) =>
        Begin(Course, title, plannedMeters, stops, "", null);

    public static void BeginMate(string matchId, string partnerNickname) =>
        Begin(Mate, $"{partnerNickname ?? "산책메이트"}님과 함께", 0, 0, matchId, partnerNickname);

    static void Begin(string kind, string title, int planned, int stops, string matchId, string nickname)
    {
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

    /// <summary>산책을 끝내고 서버에 보낼 내용을 만든다</summary>
    public static WalkCompleteRequest Finish(int stopsDone)
    {
        string pace = WalkPreferences.Load().pace;
        float meters = WalkedMeters;
        var r = new WalkCompleteRequest
        {
            kind = Kind ?? Course,
            matchId = MatchId ?? "",
            meters = Mathf.RoundToInt(meters),
            minutes = Mathf.Max(0, Mathf.RoundToInt((Time.realtimeSinceStartup - startTime) / 60f)),
            kcal = RouteMath.Kcal(meters, string.IsNullOrEmpty(pace) ? "normal" : pace, 0f),
            plannedMeters = PlannedMeters,
            stopsDone = stopsDone,
            stopsTotal = StopsTotal,
        };
        Active = false;
        return r;
    }

    public static void Abandon() => Active = false;
}
