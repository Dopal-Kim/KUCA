using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>자유 산책 · 산책 기록: 걸은 길 줄이기, 기록 한 줄 만들기</summary>
public class WalkHistoryTests
{
    [Test]
    public void 길_점은_최대_개수로_줄이고_처음과_끝은_남긴다()
    {
        var line = new List<Vector3>();
        for (int i = 0; i < 1000; i++)
            line.Add(new Vector3(i, 0f, 0f));
        List<Vector3> thin = WalkTrack.Thin(line, 300);
        Assert.AreEqual(300, thin.Count);
        Assert.AreEqual(line[0], thin[0]);
        Assert.AreEqual(line[999], thin[299]);
        Assert.AreEqual(5, WalkTrack.Thin(line.GetRange(0, 5), 300).Count);
    }

    [Test]
    public void 기록은_거리_시간_길을_담는다()
    {
        var walk = new WalkCompleteRequest
        {
            kind = WalkSession.Course, meters = 820, minutes = 12, title = WalkSession.FreeTitle,
            track = new List<Vector3> { new Vector3(1.234f, 9f, 2f), new Vector3(10f, 3f, -5.55f) },
        };
        WalkHistory.Entry e = WalkHistory.FromWalk(walk, new System.DateTime(2026, 10, 24, 15, 12, 0));
        Assert.AreEqual("자유 산책", e.title);
        Assert.AreEqual(820, e.meters);
        Assert.AreEqual(2, e.Track.Count);
        Assert.AreEqual(1.2f, e.Track[0].x, 0.001f);
        Assert.AreEqual(-5.6f, e.Track[1].z, 0.06f);
        Assert.AreEqual(0f, e.Track[0].y);   // 높이는 저장하지 않음 (지도에 그릴 때 땅 높이)
        Assert.AreEqual(15, e.EndedAt.Hour);
    }

    [Test]
    public void 자유_산책은_코스_색과_다른_색으로_그린다()
    {
        Color free = WalkCompleteScreen.LineColor(WalkSession.Course, 0, 0);
        Color course = WalkCompleteScreen.LineColor(WalkSession.Course, 3, 1200);
        Assert.AreEqual(FreeWalk.TrackColor, free);
        Assert.AreNotEqual(free, course);
    }
}
