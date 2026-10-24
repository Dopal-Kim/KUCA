using System.Collections;
using NUnit.Framework;

/// <summary>B-V4 완료 판정 · 보상 테스트 (기기 계산은 docs/api.md "산책 · 완료" 의 서버 기준과 같다)</summary>
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
    public void 산책메이트와_자유_산책은_500m면_완료()
    {
        var r = new WalkCompleteRequest { kind = WalkSession.Mate };
        r.meters = 500; Assert.IsTrue(LocalWalkService.Judge(r, out _));
        r.meters = 499; r.minutes = 30; Assert.IsFalse(LocalWalkService.Judge(r, out _));
        var free = new WalkCompleteRequest { kind = WalkSession.Course, meters = 600 };
        Assert.IsTrue(LocalWalkService.Judge(free, out _));
    }

    [Test]
    public void 서버_완료_응답에서_걸은_거리_경험치는_빼고_더한다()
    {
        // 걸은 거리 경험치는 걸을 때 앱이 이미 더했다. 완료 30 + 함께 60 만 더하고, 티켓은 서버 값
        const string json = "{\"completed\":true,\"distanceM\":1620,\"reward\":{\"tickets\":1,\"xp\":106," +
            "\"breakdown\":[{\"kind\":\"distance\",\"xp\":16},{\"kind\":\"walk\",\"xp\":30},{\"kind\":\"together\",\"xp\":60}]}}";
        var req = new WalkCompleteRequest { kind = WalkSession.Mate, walkId = "w_1", meters = 1620 };
        WalkCompleteResult r = HttpWalkService.ParseFinish(req, json, 0);
        Assert.IsTrue(r.completed);
        Assert.AreEqual(90, r.xp);
        Assert.AreEqual(1, r.tickets);
        Assert.AreEqual(LocalWalkService.CompleteGrowth, r.growth);
        Assert.AreEqual("w_1", r.walkId);

        WalkCompleteResult fail = HttpWalkService.ParseFinish(req, "{\"completed\":false,\"reward\":{\"tickets\":0,\"xp\":3,\"breakdown\":[{\"kind\":\"distance\",\"xp\":3}]}}", 0);
        Assert.IsFalse(fail.completed);
        Assert.AreEqual(0, fail.xp);
        Assert.AreEqual(0, fail.growth);
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
    public void 레벨은_다음까지_100에서_10씩_늘어난다()
    {
        GameHUD.LevelOf(209, out int lv, out int inLv, out int need);
        Assert.AreEqual((2, 109, 110), (lv, inLv, need));
        GameHUD.LevelOf(330, out lv, out inLv, out need);
        Assert.AreEqual((4, 0, 130), (lv, inLv, need));   // 100 + 110 + 120
    }

    [Test]
    public void 리뷰는_별점이_있어야_하고_10XP()
    {
        var local = new LocalWalkService();
        WalkReviewResult ok = null;
        string error = null;
        Run(local.Review("local_1", new WalkReviewRequest(), (r, e) => error = e));
        Assert.IsNotNull(error);
        Run(local.Review("local_1", new WalkReviewRequest { courseRating = 4 }, (r, _) => ok = r));
        Assert.AreEqual(LocalWalkService.ReviewXp, ok.xp);
    }
}
