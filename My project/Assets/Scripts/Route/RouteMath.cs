using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 추천 v2 (B-V2) 계산: 걷는 속도 · 시간 · 식후 걷기 시간대 · 날씨 이름.
/// 서버(docs/api.md 추천 v2)가 값을 주면 그 값을 쓰고, 없으면 (기기 추천) 여기서 같은 식으로 채운다.
/// </summary>
public static class RouteMath
{
    /// <summary>걷는 속도 (m/분). 서버와 같은 값: slow 60 · normal 75 · fast 90</summary>
    public static float MetersPerMinute(string pace) => pace switch { "slow" => 60f, "fast" => 90f, _ => 75f };

    public static int MinutesFor(float meters, string pace) =>
        meters <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(meters / MetersPerMinute(pace)));

    /// <summary>서버 날씨 코드 → 화면 글자 (모르면 null)</summary>
    public static string WeatherName(string code) => code switch
    {
        "clear" => "맑음", "cloudy" => "흐림", "rain" => "비", "snow" => "눈", _ => null,
    };

    /// <summary>식후 걷기를 권하는 시간대: 점심 11:30~14:00, 저녁 17:30~20:00. 아니면 null</summary>
    public static string MealOf(DateTime now)
    {
        int m = now.Hour * 60 + now.Minute;
        if (m >= 11 * 60 + 30 && m < 14 * 60) return "점심";
        if (m >= 17 * 60 + 30 && m < 20 * 60) return "저녁";
        return null;
    }

    public const string HealthNote = "일반 건강 정보이며 의료 조언이 아니에요";

    public static string Distance(float meters) => meters >= 1000f ? $"{meters / 1000f:0.0}km" : $"{meters:0}m";

    /// <summary>
    /// 응답에 v2 칸 (거리 · 시간) 이 없으면 채운다.
    /// points: [0] 지금 위치, 나머지는 stops 와 같은 순서의 지도 위치 (월드 단위 = m)
    /// </summary>
    public static void Fill(RouteResult r, IList<Vector3> points, string pace)
    {
        if (r == null || r.stops == null)
            return;
        float total = 0f;
        for (int i = 0; i < r.stops.Count && i + 1 < points.Count; i++)
        {
            RouteStop s = r.stops[i];
            Vector3 a = points[i], b = points[i + 1];
            float d = new Vector2(a.x - b.x, a.z - b.z).magnitude;
            if (s.distanceM <= 0)
                s.distanceM = Mathf.RoundToInt(d);
            total += s.distanceM;
        }
        if (r.totalDistanceM <= 0)
            r.totalDistanceM = Mathf.RoundToInt(total);
        if (r.totalMinutes <= 0)
            r.totalMinutes = MinutesFor(r.totalDistanceM, pace);
    }
}
