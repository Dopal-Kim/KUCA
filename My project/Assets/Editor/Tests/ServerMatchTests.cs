using NUnit.Framework;

/// <summary>서버 MatchState (docs/api.md "매칭") → 앱 MatchState 옮겨 담기 테스트</summary>
public class ServerMatchTests
{
    [Test]
    public void 대기_제안_수락은_앱_상태로()
    {
        Assert.AreEqual(MatchState.Idle, HttpMatchService.Parse("{\"state\":\"idle\"}").status);
        MatchState w = HttpMatchService.Parse("{\"state\":\"waiting\",\"matchId\":\"m_1\",\"waitingUntil\":\"2099-01-01T00:00:00Z\"}");
        Assert.AreEqual(MatchState.Waiting, w.status);
        Assert.Greater(w.waitSecondsLeft, 0);
        Assert.IsNull(w.partner);

        MatchState p = HttpMatchService.Parse("{\"state\":\"proposed\",\"matchId\":\"m_1\",\"partner\":{\"nickname\":\"민지\",\"commonTastes\":[\"photo\"]},\"myAnswer\":null}");
        Assert.AreEqual(MatchState.Proposed, p.status);
        Assert.AreEqual("민지", p.partner.nickname);
        Assert.AreEqual("photo", p.partner.commonTastes[0]);
        Assert.AreEqual(MatchState.Accepted, HttpMatchService.Parse("{\"state\":\"proposed\",\"myAnswer\":\"accepted\",\"partner\":{\"nickname\":\"민지\"}}").status);
    }

    [Test]
    public void 만남과_산책은_스팟_인상착의_산책ID를_옮긴다()
    {
        const string meeting = "{\"state\":\"meeting\",\"matchId\":\"m_1\",\"minutes\":30,\"partner\":{\"nickname\":\"민지\",\"appearance\":\"검은 후드\"}," +
            "\"meetSpot\":{\"buildingId\":\"way-9\",\"name\":\"학생회관\",\"lat\":37.2,\"lon\":127.0,\"meetSpecies\":\"pigeon\"}," +
            "\"meetBy\":\"2099-01-01T00:00:00Z\",\"smalltalk\":[\"a\",\"b\",\"c\"],\"myAppearance\":\"회색 맨투맨\",\"arrived\":{\"me\":true,\"partner\":false}}";
        MatchState m = HttpMatchService.Parse(meeting);
        Assert.AreEqual(MatchState.Matched, m.status);
        Assert.AreEqual("way-9", m.spot.buildingId);
        Assert.AreEqual("pigeon", m.spot.species);
        Assert.AreEqual("검은 후드", m.partnerLook);
        Assert.AreEqual("회색 맨투맨", m.myLook);
        Assert.AreEqual(3, m.smalltalk.Count);
        Assert.IsTrue(m.meArrived);
        Assert.IsFalse(string.IsNullOrEmpty(m.message)); // 나만 도착 → 기다린다는 안내

        MatchState walking = HttpMatchService.Parse("{\"state\":\"walking\",\"matchId\":\"m_1\",\"walkId\":\"w_7\",\"partner\":{\"nickname\":\"민지\"}}");
        Assert.AreEqual(MatchState.Walking, walking.status);
        Assert.AreEqual("w_7", walking.walkId);
    }

    [Test]
    public void 끝난_이유에_따라_완료_취소_시간초과()
    {
        Assert.AreEqual(MatchState.Ended, HttpMatchService.Parse("{\"state\":\"ended\",\"endReason\":\"completed\"}").status);
        Assert.AreEqual(MatchState.Expired, HttpMatchService.Parse("{\"state\":\"ended\",\"endReason\":\"no_partner\"}").status);
        MatchState cancel = HttpMatchService.Parse("{\"state\":\"ended\",\"endReason\":\"cancelled\",\"cancelledBy\":\"partner\"}");
        Assert.AreEqual(MatchState.Cancelled, cancel.status);
        StringAssert.Contains("상대", cancel.message);
        Assert.AreEqual(MatchState.Cancelled, HttpMatchService.Parse("{\"state\":\"ended\",\"endReason\":\"noshow\"}").status);
        Assert.IsNull(HttpMatchService.Parse("not json"));
    }
}
