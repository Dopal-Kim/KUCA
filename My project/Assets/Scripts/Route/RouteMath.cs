using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 추천 v2 (B-V2) 계산: 걷는 속도 · 시간 · 칼로리 · 식후 걷기 시간대.
/// 식은 docs/api_v2_추천_초안.md 와 같다. 서버가 값을 주면 그 값을 쓰고, 없으면 (v1 서버 · 기기 추천) 여기서 채운다.
/// </summary>
public static class RouteMath
{
    /// <summary>체중을 받지 않았을 때 쓰는 값 (kg)</summary>
    public const float DefaultWeightKg = 60f;

    public static float SpeedKmh(string pace) => pace switch { "slow" => 3.2f, "fast" => 5.6f, _ => 4.5f };

    public static float Met(string pace) => pace switch { "slow" => 2.8f, "fast" => 4.3f, _ => 3.5f };

    public static int MinutesFor(float meters, string pace) =>
        meters <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(meters / 1000f / SpeedKmh(pace) * 60f));

    /// <summary>kcal = MET × 체중(kg) × 걸린 시간(h)</summary>
    public static int Kcal(float meters, string pace, float weightKg)
    {
        float kg = weightKg > 0f ? weightKg : DefaultWeightKg;
        float hours = meters / 1000f / SpeedKmh(pace);
        return Mathf.RoundToInt(Met(pace) * kg * hours);
    }

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
    /// 응답에 v2 칸 (거리 · 시간 · 칼로리) 이 없으면 채운다.
    /// points: [0] 지금 위치, 나머지는 stops 와 같은 순서의 지도 위치 (월드 단위 = m)
    /// </summary>
    public static void Fill(RouteResult r, IList<Vector3> points, string pace, float weightKg)
    {
        if (r == null || r.stops == null)
            return;
        float total = 0f;
        for (int i = 0; i < r.stops.Count && i + 1 < points.Count; i++)
        {
            RouteStop s = r.stops[i];
            Vector3 a = points[i], b = points[i + 1];
            float d = new Vector2(a.x - b.x, a.z - b.z).magnitude;
            if (s.distanceMeters <= 0)
                s.distanceMeters = Mathf.RoundToInt(d);
            total += s.distanceMeters;
        }
        if (r.totalMeters <= 0)
            r.totalMeters = Mathf.RoundToInt(total);
        if (r.totalMinutes <= 0)
            r.totalMinutes = MinutesFor(r.totalMeters, pace);
        if (r.kcal <= 0)
            r.kcal = Kcal(r.totalMeters, pace, weightKg);
    }
}
