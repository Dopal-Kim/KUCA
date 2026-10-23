using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 서버 산책이 없을 때 (서버 없는 모드 · 시작을 서버에 못 알림) 기기에서 계산하는 완료 판정 · 보상.
/// 기준은 서버(docs/api.md "산책 · 완료 · 리뷰 · 보상")와 같다. 걷기 1km 경험치는 걸을 때 이미 받았으니 여기서는 완료 · 리뷰 보상만 준다.
/// </summary>
public class LocalWalkService : IWalkService
{
    public const int CourseXp = 30, MateXp = 60, ReviewXp = 10, CompleteTickets = 1, CompleteGrowth = 10;
    public const float CourseRatio = 0.8f;
    /// <summary>산책메이트 · 자유 산책은 이만큼 걸으면 완료 (서버와 같음)</summary>
    public const int MateMeters = 500;

    /// <summary>완료 판정 (코스: 마지막 장소 도착 또는 거리 80%, 산책메이트: 500m)</summary>
    public static bool Judge(WalkCompleteRequest r, out string reason)
    {
        bool done = Completed(r);
        reason = Reason(r, done);
        return done;
    }

    public static bool Completed(WalkCompleteRequest r)
    {
        if (r.kind == WalkSession.Mate || (r.stopsTotal <= 0 && r.plannedMeters <= 0))
            return r.meters >= MateMeters;
        return (r.stopsTotal > 0 && r.stopsDone >= r.stopsTotal)
            || (r.plannedMeters > 0 && r.meters >= Mathf.RoundToInt(r.plannedMeters * CourseRatio));
    }

    /// <summary>완료 화면에 보일 판정 이유 (서버 판정에도 같은 문구를 쓴다)</summary>
    public static string Reason(WalkCompleteRequest r, bool completed)
    {
        if (r.kind == WalkSession.Mate)
            return completed ? "산책메이트와 함께 걸었어요" : $"함께 {MateMeters}m 이상 걸으면 완료예요";
        if (r.stopsTotal <= 0 && r.plannedMeters <= 0)   // 코스 없이 걸은 자유 산책
            return completed ? $"{MateMeters}m 이상 걸었어요" : $"{MateMeters}m 이상 걸으면 완료예요";
        if (!completed)
            return "마지막 장소에 가거나 코스의 80% 이상 걸으면 완료예요";
        return r.stopsTotal > 0 && r.stopsDone >= r.stopsTotal ? "마지막 장소에 도착했어요" : "코스 거리의 80% 이상 걸었어요";
    }

    public IEnumerator Begin(string recommendationId, int minutes, Action<string, string> onDone)
    {
        yield return null;
        onDone("local_" + Guid.NewGuid().ToString("N").Substring(0, 8), null);
    }

    public IEnumerator Complete(WalkCompleteRequest request, int score, Action<WalkCompleteResult, string> onDone)
    {
        yield return null;
        var r = new WalkCompleteResult { walkId = "local_" + Guid.NewGuid().ToString("N").Substring(0, 8) };
        r.completed = Judge(request, out r.reason);
        if (r.completed)
        {
            r.xp = request.kind == WalkSession.Mate ? MateXp : CourseXp;
            r.tickets = CompleteTickets;
            r.growth = CompleteGrowth;
        }
        GameHUD.LevelOf(score + r.xp, out r.level, out r.xpInLevel, out r.xpNeeded);
        onDone(r, null);
    }

    public IEnumerator Review(string walkId, WalkReviewRequest request, Action<WalkReviewResult, string> onDone)
    {
        yield return null;
        if (request.courseRating <= 0)
        {
            onDone(null, "별점을 골라 주세요");
            yield break;
        }
        if (request.text != null && request.text.Length > WalkReviewRequest.MaxText)
        {
            onDone(null, $"한 줄은 {WalkReviewRequest.MaxText}자까지예요");
            yield break;
        }
        onDone(new WalkReviewResult { xp = ReviewXp }, null);
    }
}
