using System.Collections;
using NUnit.Framework;

/// <summary>B-V4 완료 판정 · 보상 테스트 (docs/api_v2_완료_초안.md "기기 계산")</summary>
public class WalkCompleteTests
{
    static void Run(IEnumerator work)
    {
        while (work.MoveNext())
            if (work.Current is IEnumerator inner)
                Run(inner);
    }

    [Test]
    public void 코스는_마지막_장소_도착_또는_거리_80퍼센트면_완료()
    {
        var r = new WalkCompleteRequest { kind = WalkSession.Course, plannedMeters = 1000, stopsTotal = 3 };
        r.stopsDone = 3; r.meters = 100;
        Assert.IsTrue(LocalWalkService.Judge(r, out _));
        r.stopsDone = 1; r.meters = 800;
        Assert.IsTrue(LocalWalkService.Judge(r, out _));
        r.meters = 799;
        Assert.IsFalse(LocalWalkService.Judge(r, out _));
    }

    [Test]
    public void 산책메이트는_500m_또는_10분이면_완료()
    {
        var r = new WalkCompleteRequest { kind = WalkSession.Mate };
        r.meters = 500; Assert.IsTrue(LocalWalkService.Judge(r, out _));
        r.meters = 100; r.minutes = 10; Assert.IsTrue(LocalWalkService.Judge(r, out _));
        r.minutes = 9; Assert.IsFalse(LocalWalkService.Judge(r, out _));
    }

    [Test]
    public void 완료_보상과_레벨()
    {
        var local = new LocalWalkService();
        WalkCompleteResult course = null, mate = null, fail = null;
        Run(local.Complete(new WalkCompleteRequest { kind = WalkSession.Course, stopsDone = 2, stopsTotal = 2 }, 90, (r, _) => course = r));
        Run(local.Complete(new WalkCompleteRequest { kind = WalkSession.Mate, meters = 600 }, 0, (r, _) => mate = r));
        Run(local.Complete(new WalkCompleteRequest { kind = WalkSession.Course, stopsTotal = 3, plannedMeters = 1000 }, 0, (r, _) => fail = r));
        Assert.AreEqual(LocalWalkService.CourseXp, course.xp);
        Assert.AreEqual(1, course.tickets);
        Assert.AreEqual(2, course.level); // 90 + 30 = 120 → 레벨 2 (100 에서 오름)
        Assert.AreEqual(20, course.xpInLevel);
        Assert.AreEqual(LocalWalkService.MateXp, mate.xp);
        Assert.IsFalse(fail.completed);
        Assert.AreEqual(0, fail.xp);
    }

    [Test]
    public void 리뷰는_별점이_있어야_하고_10XP()
    {
        var local = new LocalWalkService();
        WalkReviewResult ok = null;
        string error = null;
        Run(local.Review("local_1", new WalkReviewRequest(), (r, e) => error = e));
        Assert.IsNotNull(error);
        Run(local.Review("local_1", new WalkReviewRequest { courseStars = 4 }, (r, _) => ok = r));
        Assert.AreEqual(LocalWalkService.ReviewXp, ok.xp);
    }
}
