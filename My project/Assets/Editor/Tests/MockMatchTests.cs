using System.Collections;
using NUnit.Framework;

/// <summary>B-V3 가상 상대 흐름 테스트: 대기 → 제안 → 수락 → 만남 → 산책 → 끝, 거절 · 노쇼 · 신고</summary>
public class MockMatchTests
{
    float now;
    MockMatchService mock;
    MatchState last;

    [SetUp]
    public void SetUp()
    {
        now = 0f;
        mock = new MockMatchService(() => new MeetSpot { buildingId = "b1", name = "중앙도서관" }, () => now);
    }

    void Run(IEnumerator work)
    {
        while (work.MoveNext())
            if (work.Current is IEnumerator inner)
                Run(inner);
    }

    MatchState Status()
    {
        Run(mock.Status((s, _) => last = s));
        return last;
    }

    void Join() => Run(mock.Join(new MatchQueueRequest { minutes = 30, tastes = { "photo" } }, (s, _) => last = s));

    [Test]
    public void 대기에서_산책_끝까지()
    {
        Join();
        Assert.AreEqual(MatchState.Waiting, last.status);
        now = MockMatchService.ProposeAfter + 0.1f;
        Assert.AreEqual(MatchState.Proposed, Status().status);
        CollectionAssert.Contains(last.partner.commonTastes, "photo");

        Run(mock.Respond(last.matchId, true, (s, _) => last = s));
        Assert.AreEqual(MatchState.Accepted, last.status);
        now += MockMatchService.PartnerAcceptsAfter + 0.1f;
        Assert.AreEqual(MatchState.Matched, Status().status);
        Assert.AreEqual("중앙도서관", last.spot.name);
        Assert.AreEqual(3, last.smalltalk.Count);

        Run(mock.SetLook(last.matchId, "검은 후드", (s, _) => last = s));
        now += MockMatchService.PartnerLookAfter + 0.1f;
        Assert.AreEqual("검은 후드", Status().myLook);
        Assert.IsFalse(string.IsNullOrEmpty(last.partnerLook));

        Run(mock.StartWalk(last.matchId, (s, _) => last = s));
        Assert.AreEqual(MatchState.Walking, last.status);
        Run(mock.End(last.matchId, (s, _) => last = s));
        Assert.AreEqual(MatchState.Ended, last.status);
        Assert.IsTrue(string.IsNullOrEmpty(last.myLook)); // 끝나면 인상착의를 지운다
    }

    [Test]
    public void 거절하거나_시간이_지나면_끝난다()
    {
        Join();
        now = MockMatchService.ProposeAfter + 0.1f;
        Status();
        Run(mock.Respond(last.matchId, false, (s, _) => last = s));
        Assert.AreEqual(MatchState.Cancelled, last.status);

        Join();
        now += MockMatchService.ProposeAfter + 0.1f;
        Status();
        now += MockMatchService.DecideSeconds + 1f;
        Assert.AreEqual(MatchState.Expired, Status().status);
    }

    [Test]
    public void 만나고_10분_안에_시작하지_않으면_노쇼_취소()
    {
        Join();
        now = MockMatchService.ProposeAfter + 0.1f;
        Status();
        Run(mock.Respond(last.matchId, true, (s, _) => last = s));
        now += MockMatchService.PartnerAcceptsAfter + 0.1f;
        Assert.AreEqual(MatchState.Matched, Status().status);
        now += MockMatchService.NoShowSeconds + 1f;
        Assert.AreEqual(MatchState.Cancelled, Status().status);
    }

    [Test]
    public void 신고하면_매칭이_끝나고_이미_매칭_중이면_다시_못_들어간다()
    {
        Join();
        string error = null;
        Run(mock.Join(new MatchQueueRequest(), (s, e) => error = e));
        Assert.IsNotNull(error);
        now = MockMatchService.ProposeAfter + 0.1f;
        Status();
        Run(mock.Report(last.matchId, "rude", "", true, (s, _) => last = s));
        Assert.AreEqual(MatchState.Cancelled, last.status);
        Assert.IsFalse(last.Active);
    }
}
