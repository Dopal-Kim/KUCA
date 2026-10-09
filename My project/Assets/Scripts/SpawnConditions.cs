using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 시공간 출현 조건 (A-1). 시간대·계절·날씨에 따라 동물마다 나올 확률을 바꾼다.
/// - 순수 확률 가중: 조건이 맞으면 더 자주, 안 맞으면 드물게. 아예 안 나오게 막지는 않는다.
/// - 24종 모두 적용. 황금 동물은 더 뚜렷하게 (배수 폭이 크다).
/// - 날씨는 메모 서버의 GET /api/weather (기상청 초단기 실황·예보) 를 WeatherService 가 받아 온다. 모르면 날씨 배수는 1.
/// 배수 표는 아래 Rules 한 곳에서 고친다.
/// </summary>
public static class SpawnConditions
{
    public enum TimeSlot { Morning, Day, Evening, Night }
    public enum Season { Spring, Summer, Autumn, Winter }
    public enum Weather { Unknown, Clear, Cloudy, Rain, Snow }

    /// <summary>황금 동물: 배수가 이 값 이상이면 랜드마크에서 항상 황금으로 나오고, 낮을수록 파랑으로 바뀐다.</summary>
    public const float GoldFullMultiplier = 2.5f;

    // ---------- 지금 조건 ----------

    /// <summary>테스트용 강제 값 (null 이면 실제 값). CollectibleSpawner 인스펙터에서 바꾼다.</summary>
    public static TimeSlot? OverrideTime;
    public static Season? OverrideSeason;
    public static Weather? OverrideWeather;

    /// <summary>WeatherService 가 받아 온 실제 날씨</summary>
    public static Weather LiveWeather = Weather.Unknown;

    /// <summary>아침 06–11 · 낮 11–17 · 저녁 17–20 · 밤 20–06 (기기 시각)</summary>
    public static TimeSlot TimeSlotAt(DateTime t)
    {
        int h = t.Hour;
        return h >= 6 && h < 11 ? TimeSlot.Morning : h >= 11 && h < 17 ? TimeSlot.Day : h >= 17 && h < 20 ? TimeSlot.Evening : TimeSlot.Night;
    }

    /// <summary>3–5 봄 · 6–8 여름 · 9–11 가을 · 12–2 겨울 (KeyArtLook 의 지도 계절과 같은 규칙)</summary>
    public static Season SeasonAt(DateTime t)
    {
        int m = t.Month;
        return m >= 3 && m <= 5 ? Season.Spring : m >= 6 && m <= 8 ? Season.Summer : m >= 9 && m <= 11 ? Season.Autumn : Season.Winter;
    }

    public static TimeSlot CurrentTime => OverrideTime ?? TimeSlotAt(DateTime.Now);
    public static Season CurrentSeason => OverrideSeason ?? SeasonAt(DateTime.Now);
    public static Weather CurrentWeather => OverrideWeather ?? LiveWeather;

    // ---------- 배수 표 ----------

    public class Rule
    {
        public readonly float[] time = { 1f, 1f, 1f, 1f };
        public readonly float[] season = { 1f, 1f, 1f, 1f };
        public readonly float[] weather = { 1f, 1f, 1f, 1f, 1f };

        public Rule T(TimeSlot s, float m) { time[(int)s] = m; return this; }
        public Rule S(Season s, float m) { season[(int)s] = m; return this; }
        public Rule W(Weather w, float m) { weather[(int)w] = m; return this; }

        public float Multiplier(TimeSlot t, Season s, Weather w) => time[(int)t] * season[(int)s] * weather[(int)w];
    }

    static Rule R() => new Rule();

    /// <summary>동물 id → 배수. 표에 없으면 1 (조건 없음). 이야기는 캐릭터 설정(05_Characters 기획서)에서 따왔다.</summary>
    public static readonly Dictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        // 초록 · 흔함 (부드럽게 0.3 ~ 3)
        { "pigeon", R().T(TimeSlot.Day, 1.6f).T(TimeSlot.Night, 0.4f).W(Weather.Rain, 0.6f) },                        // 학식 시간
        { "snail", R().T(TimeSlot.Morning, 1.5f).W(Weather.Rain, 3f).W(Weather.Cloudy, 1.5f).W(Weather.Clear, 0.7f)
                      .S(Season.Summer, 1.3f).S(Season.Winter, 0.3f) },                                             // 비 오는 1교시
        { "ant", R().T(TimeSlot.Day, 1.5f).T(TimeSlot.Night, 0.3f).S(Season.Summer, 1.8f).S(Season.Winter, 0.2f)
                    .W(Weather.Rain, 0.5f).W(Weather.Snow, 0.3f) },
        { "ladybug", R().S(Season.Spring, 2f).S(Season.Autumn, 1.2f).S(Season.Winter, 0.3f).W(Weather.Clear, 1.4f)
                        .W(Weather.Snow, 0.3f).T(TimeSlot.Night, 0.4f) },                                            // 벚꽃길

        // 파랑 · 드묾 (0.3 ~ 3)
        { "stray_cat", R().T(TimeSlot.Evening, 1.6f).T(TimeSlot.Night, 1.4f).W(Weather.Rain, 0.6f) },
        { "squirrel", R().S(Season.Autumn, 2.5f).S(Season.Winter, 0.6f).T(TimeSlot.Morning, 1.5f) },                // 도토리 철
        { "crow", R().T(TimeSlot.Morning, 1.6f).T(TimeSlot.Evening, 1.3f).W(Weather.Clear, 1.3f) },                   // 반짝이는 것
        { "raccoon_dog", R().T(TimeSlot.Night, 3f).T(TimeSlot.Evening, 1.4f).T(TimeSlot.Morning, 0.4f).T(TimeSlot.Day, 0.3f) }, // 야식

        // 황금 · 가장 드묾 (뚜렷하게 0.15 ~ 4, 랜드마크 근처에서만)
        { "space_rabbit", R().T(TimeSlot.Night, 2.5f).T(TimeSlot.Day, 0.6f).W(Weather.Clear, 1.8f).W(Weather.Rain, 0.5f) },
        { "library_owl", R().T(TimeSlot.Night, 3f).T(TimeSlot.Evening, 1.5f).T(TimeSlot.Morning, 0.3f).S(Season.Winter, 1.4f) },
        { "plaza_duck", R().W(Weather.Rain, 3.5f).W(Weather.Cloudy, 1.5f).W(Weather.Clear, 0.7f)
                           .S(Season.Summer, 1.6f).S(Season.Winter, 0.3f) },                                        // 비모자
        { "gate_magpie", R().T(TimeSlot.Morning, 3f).T(TimeSlot.Night, 0.2f) },                                        // 아침의 좋은 소식
        { "lion_cub", R().T(TimeSlot.Morning, 2f).T(TimeSlot.Day, 2f).T(TimeSlot.Night, 0.2f).W(Weather.Clear, 1.5f).W(Weather.Rain, 0.4f) },
        { "clock_rooster", R().T(TimeSlot.Morning, 4f).T(TimeSlot.Night, 0.2f) },                                      // 새벽 시계탑
        { "art_chameleon", R().S(Season.Autumn, 2.5f).S(Season.Spring, 1.5f).S(Season.Winter, 0.5f).W(Weather.Clear, 1.3f) }, // 단풍 색
        { "ceramics_mole", R().W(Weather.Cloudy, 2f).W(Weather.Rain, 1.5f).W(Weather.Clear, 0.6f).T(TimeSlot.Evening, 1.5f) },
        { "observatory_squirrel", R().T(TimeSlot.Night, 4f).T(TimeSlot.Day, 0.15f).T(TimeSlot.Morning, 0.3f)
                                     .W(Weather.Clear, 2.5f).W(Weather.Cloudy, 0.5f).W(Weather.Rain, 0.3f) },        // 맑은 밤하늘
        { "amphitheater_frog", R().W(Weather.Rain, 3f).S(Season.Summer, 2.5f).S(Season.Winter, 0.2f)
                                  .T(TimeSlot.Evening, 2f).W(Weather.Snow, 0.2f) },                                  // 저녁 공연
        { "stadium_jindo", R().S(Season.Spring, 3.5f).S(Season.Winter, 0.7f).T(TimeSlot.Day, 2f).T(TimeSlot.Night, 0.3f)
                              .W(Weather.Clear, 1.5f).W(Weather.Snow, 1.5f) },                                       // 벚꽃 트랙
        { "language_parrot", R().T(TimeSlot.Day, 2f).T(TimeSlot.Morning, 1.5f).T(TimeSlot.Night, 0.3f) },
        { "dorm_hamster", R().T(TimeSlot.Night, 3f).T(TimeSlot.Evening, 1.5f).T(TimeSlot.Morning, 0.5f)
                             .S(Season.Winter, 1.8f).W(Weather.Rain, 1.3f).W(Weather.Snow, 2f) },                    // 이불 속
        { "electronics_hedgehog", R().T(TimeSlot.Night, 2.5f).T(TimeSlot.Evening, 1.5f).S(Season.Winter, 0.4f) },     // 밤샘 실습, 겨울잠
        { "business_fox", R().T(TimeSlot.Morning, 2f).T(TimeSlot.Day, 1.6f).T(TimeSlot.Night, 0.4f).S(Season.Autumn, 1.5f) },
        { "multimedia_meerkat", R().T(TimeSlot.Evening, 3f).W(Weather.Clear, 2f).W(Weather.Rain, 0.4f) },             // 노을 촬영
    };

    /// <summary>지금 조건에서 이 동물의 출현 배수 (1 = 보통)</summary>
    public static float Multiplier(string species)
    {
        if (species == null || !Rules.TryGetValue(species, out Rule r))
            return 1f;
        return r.Multiplier(CurrentTime, CurrentSeason, CurrentWeather);
    }

    /// <summary>후보 중 하나를 지금 조건의 배수만큼 가중해서 고른다.</summary>
    public static string PickWeighted(IList<string> candidates)
    {
        if (candidates == null || candidates.Count == 0)
            return null;
        float total = 0f;
        foreach (string id in candidates)
            total += Multiplier(id);
        float r = UnityEngine.Random.value * total;
        foreach (string id in candidates)
        {
            r -= Multiplier(id);
            if (r <= 0f)
                return id;
        }
        return candidates[candidates.Count - 1];
    }

    /// <summary>황금 동물이 랜드마크에서 지금 황금으로 나올 확률 (0~1)</summary>
    public static float GoldChance(string species) => Mathf.Clamp01(Multiplier(species) / GoldFullMultiplier);

    /// <summary>
    /// 도감 힌트용: 이 동물이 잘 나오는 조건 (배수 1.5 이상), 큰 순서. 예: ("밤", 4), ("맑음", 2.5)
    /// </summary>
    public static List<(string label, float multiplier)> FavoredConditions(string species)
    {
        var list = new List<(string, float)>();
        if (species == null || !Rules.TryGetValue(species, out Rule r))
            return list;
        string[] times = { "아침", "낮", "저녁", "밤" };
        string[] seasons = { "봄", "여름", "가을", "겨울" };
        string[] weathers = { "", "맑음", "흐림", "비", "눈" };
        for (int i = 0; i < 4; i++) if (r.time[i] >= 1.5f) list.Add((times[i], r.time[i]));
        for (int i = 0; i < 4; i++) if (r.season[i] >= 1.5f) list.Add((seasons[i], r.season[i]));
        for (int i = 1; i < 5; i++) if (r.weather[i] >= 1.5f) list.Add((weathers[i], r.weather[i]));
        list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        return list;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        OverrideTime = null;
        OverrideSeason = null;
        OverrideWeather = null;
        LiveWeather = Weather.Unknown;
    }
}
