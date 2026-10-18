using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 서버에 완료 · 리뷰 API 가 없을 때 기기에서 계산하는 완료 판정 · 보상 (docs/api_v2_완료_초안.md "기기 계산").
/// 걷기 1km 경험치는 걸을 때 이미 받았으니 여기서는 완료 · 리뷰 보상만 준다.
/// </summary>
public class LocalWalkService : IWalkService
{
    public const int CourseXp = 30, MateXp = 60, ReviewXp = 10, CompleteTickets = 1, CompleteGrowth = 10;
    public const float CourseRatio = 0.8f;
    public const int MateMeters = 500, MateMinutes = 10;

    /// <summary>완료 판정 (코스: 마지막 장소 도착 또는 거리 80%, 산책메이트: 500m 또는 10분)</summary>
    public static bool Judge(WalkCompleteRequest r, out string reason)
    {
        if (r.kind == WalkSession.Mate)
        {
            bool ok = r.meters >= MateMeters || r.minutes >= MateMinutes;
            reason = ok ? "산책메이트와 함께 걸었어요" : $"함께 {MateMeters}m 또는 {MateMinutes}분 이상 걸으면 완료예요";
            return ok;
        }
        if (r.stopsTotal > 0 && r.stopsDone >= r.stopsTotal)
        {
            reason = "마지막 장소에 도착했어요";
            return true;
        }
        if (r.plannedMeters > 0 && r.meters >= Mathf.RoundToInt(r.plannedMeters * CourseRatio))
        {
            reason = "코스 거리의 80% 이상 걸었어요";
            return true;
        }
        reason = "마지막 장소에 가거나 코스의 80% 이상 걸으면 완료예요";
        return false;
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
        if (request.courseStars <= 0 && request.partnerStars <= 0)
        {
            onDone(null, "별점을 골라 주세요");
            yield break;
        }
        if (request.comment != null && request.comment.Length > 60)
        {
            onDone(null, "한 줄은 60자까지예요");
            yield break;
        }
        onDone(new WalkReviewResult { xp = ReviewXp }, null);
    }
}
