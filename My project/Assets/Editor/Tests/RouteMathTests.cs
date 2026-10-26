using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>B-V2 추천 계산 테스트 (docs/api.md 추천 v2 와 같은 식)</summary>
public class RouteMathTests
{
    [Test]
    public void 걸리는_시간은_서버와_같은_속도로()
    {
        // 보통 75m/분, 느림 60, 빠름 90
        Assert.AreEqual(20, RouteMath.MinutesFor(1500f, "normal"));
        Assert.AreEqual(25, RouteMath.MinutesFor(1500f, "slow"));
        Assert.AreEqual(1, RouteMath.MinutesFor(10f, "normal"));
        Assert.AreEqual(0, RouteMath.MinutesFor(0f, "normal"));
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
    public void 서버가_안_준_거리_시간을_채우고_준_값은_그대로()
    {
        var r = new RouteResult
        {
            stops = new List<RouteStop> { new RouteStop(), new RouteStop { distanceM = 999 } },
        };
        var points = new List<Vector3> { Vector3.zero, new Vector3(300f, 0f, 400f), new Vector3(300f, 0f, 1400f) };
        RouteMath.Fill(r, points, "normal");
        Assert.AreEqual(500, r.stops[0].distanceM);   // 3-4-5 삼각형
        Assert.AreEqual(999, r.stops[1].distanceM);   // 서버 값 유지
        Assert.AreEqual(1499, r.totalDistanceM);
        Assert.AreEqual(RouteMath.MinutesFor(1499, "normal"), r.totalMinutes);

        var server = new RouteResult { stops = new List<RouteStop>(), totalDistanceM = 10, totalMinutes = 20 };
        RouteMath.Fill(server, points, "normal");
        Assert.AreEqual(10, server.totalDistanceM);
        Assert.AreEqual(20, server.totalMinutes);
    }

    [Test]
    public void 서버_추천_응답을_그대로_읽는다()
    {
        const string json = "{\"id\":\"rec_1\",\"title\":\"노을 코스\",\"source\":\"ai\",\"context\":{\"timeOfDay\":\"evening\",\"season\":\"autumn\",\"weather\":\"rain\"}," +
            "\"stops\":[{\"buildingId\":\"way-1\",\"name\":\"도서관\",\"reason\":\"창가\",\"walkMinutes\":6,\"distanceM\":420,\"lat\":37.2,\"lon\":127.0}]," +
            "\"totalDistanceM\":1850,\"totalMinutes\":18,\"healthNote\":null}";
        RouteResult r = JsonUtility.FromJson<RouteResult>(json);
        Assert.AreEqual("rec_1", r.id);
        Assert.AreEqual(420, r.stops[0].distanceM);
        Assert.AreEqual(1850, r.totalDistanceM);
        Assert.AreEqual("비", RouteMath.WeatherName(r.context.weather));
        Assert.IsNull(RouteMath.WeatherName(null));
    }

    [Test]
    public void 순서만_다르거나_한쪽이_포함되면_같은_코스()
    {
        var abc = new HashSet<string> { "A", "B", "C" };
        Assert.IsTrue(RoutePlanner.SamePlaces(abc, new HashSet<string> { "C", "B", "A" }));   // 순서만 다름
        Assert.IsTrue(RoutePlanner.SamePlaces(new HashSet<string> { "A", "C" }, abc));       // 가볍게 걷는 코스가 일부만
        Assert.IsFalse(RoutePlanner.SamePlaces(abc, new HashSet<string> { "A", "B", "D" }));  // 한 곳이라도 다르면 다른 코스
    }
}
