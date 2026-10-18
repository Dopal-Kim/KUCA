using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>B-V2 추천 계산 테스트 (docs/api_v2_추천_초안.md 의 식)</summary>
public class RouteMathTests
{
    [Test]
    public void 칼로리는_MET_체중_시간()
    {
        // 보통 속도 4.5km/h 로 4.5km = 1시간 → 3.5 × 60kg × 1h = 210
        Assert.AreEqual(210, RouteMath.Kcal(4500f, "normal", 0f));
        Assert.AreEqual(245, RouteMath.Kcal(4500f, "normal", 70f));
        Assert.AreEqual(0, RouteMath.Kcal(0f, "normal", 0f));
    }

    [Test]
    public void 걸리는_시간은_속도로()
    {
        Assert.AreEqual(60, RouteMath.MinutesFor(4500f, "normal"));
        Assert.AreEqual(1, RouteMath.MinutesFor(10f, "normal"));
        Assert.Greater(RouteMath.MinutesFor(2000f, "slow"), RouteMath.MinutesFor(2000f, "fast"));
    }

    [Test]
    public void 식후_걷기_시간대()
    {
        Assert.AreEqual("점심", RouteMath.MealOf(new DateTime(2026, 10, 18, 12, 40, 0)));
        Assert.AreEqual("저녁", RouteMath.MealOf(new DateTime(2026, 10, 18, 18, 0, 0)));
        Assert.IsNull(RouteMath.MealOf(new DateTime(2026, 10, 18, 11, 29, 0)));
        Assert.IsNull(RouteMath.MealOf(new DateTime(2026, 10, 18, 15, 0, 0)));
    }

    [Test]
    public void 서버가_안_준_거리_시간_칼로리를_채우고_준_값은_그대로()
    {
        var r = new RouteResult
        {
            stops = new List<RouteStop> { new RouteStop(), new RouteStop { distanceMeters = 999 } },
        };
        var points = new List<Vector3> { Vector3.zero, new Vector3(300f, 0f, 400f), new Vector3(300f, 0f, 1400f) };
        RouteMath.Fill(r, points, "normal", 0f);
        Assert.AreEqual(500, r.stops[0].distanceMeters);   // 3-4-5 삼각형
        Assert.AreEqual(999, r.stops[1].distanceMeters);   // 서버 값 유지
        Assert.AreEqual(1499, r.totalMeters);
        Assert.AreEqual(RouteMath.MinutesFor(1499, "normal"), r.totalMinutes);
        Assert.AreEqual(RouteMath.Kcal(1499, "normal", 0f), r.kcal);

        var server = new RouteResult { stops = new List<RouteStop>(), totalMeters = 10, totalMinutes = 20, kcal = 30 };
        RouteMath.Fill(server, points, "normal", 0f);
        Assert.AreEqual(30, server.kcal);
    }
}
