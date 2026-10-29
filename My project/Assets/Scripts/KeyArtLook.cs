using UnityEngine;

/// <summary>
/// 키아트 '햇살' 셰이더(KUCA/*, KUCAStylizedLighting.hlsl)의 전역 값과 시간대(낮·노을·밤).
/// - 모드 Auto: 기기 시계를 따라 새벽 노을 → 낮 → 저녁 노을 → 밤으로 부드럽게 넘어간다
/// - Day / Sunset / Night: 고정 (에디터 메뉴 KUCA → Time of Day)
/// 프리셋 값은 KUCA → Map Style → Apply Key Art 가 Assets/Art/KeyArt/KeyArtLook.json 의 modes 에서 채운다.
/// 해(Directional Light) 색·세기·방향, 안개, 하늘(Skybox), 환경광도 같이 바꾼다.
/// </summary>
[ExecuteAlways]
public class KeyArtLook : MonoBehaviour
{
    public enum Mode { Auto, Day, Sunset, Night, Morning }
    public enum Season { Auto, Spring, Summer, Autumn, Winter }

    [System.Serializable]
    public class SeasonPalette
    {
        [Tooltip("활엽 초록에 곱하는 색 (리니어 배율)")] public Vector3 leaf = Vector3.one;
        [Range(0f, 1f), Tooltip("활엽 단풍 정도")] public float autumn;
        [Tooltip("분홍 꽃잎(벚꽃)이 바뀔 색")] public Color blossom = new Color(0.98f, 0.76f, 0.84f);
        [Range(0f, 1f)] public float blossomMix;
        [Tooltip("잔디에 곱하는 색 (리니어 배율)")] public Vector3 grass = Vector3.one;
        [Range(0f, 1f)] public float snow;
        [Header("계절 빛 (S1)")]
        [Tooltip("햇빛 색에 곱함")] public Color light = Color.white;
        public float sunScale = 1f;
        [Tooltip("채도에 더함")] public float saturationAdd;
        [Tooltip("하늘색에 곱함")] public Color sky = Color.white;
        [Tooltip("잔디 위 무늬 색 (봄 꽃잎 · 가을 낙엽)")] public Color speckle = Color.white;
        [Range(0f, 1f)] public float speckleAmount;
        [Range(0f, 1f), Tooltip("겨울: 활엽수 잎 진 가지")] public float bare;
        [Range(0f, 1f), Tooltip("봄: 활엽수 일부가 꽃 핀 색 (blossom 색)")] public float bloom;
    }

    /// <summary>날씨가 빛 · 안개 · 하늘 · 바닥을 바꾸는 정도 (맑음 = 그대로). 어둡기는 지금 시간대 값에 곱해 밤에도 맞는다</summary>
    struct WeatherMod
    {
        public float sun, shadow, sat, fog, fogMix, skyMix, exposure, ambient, wet, snow;
        public Color tint;   // 흐린 하늘 · 안개 색 (지금 하늘 밝기에 곱함)
    }

    static WeatherMod ModFor(SpawnConditions.Weather w)
    {
        switch (w)
        {
            case SpawnConditions.Weather.Cloudy:
                return new WeatherMod { sun = 0.55f, shadow = 0.35f, sat = -0.14f, fog = 0.65f, fogMix = 0.6f, skyMix = 0.75f, exposure = 0.88f, ambient = 1.05f, tint = new Color(0.95f, 0.98f, 1.03f) };
            case SpawnConditions.Weather.Rain:
                return new WeatherMod { sun = 0.32f, shadow = 0.15f, sat = -0.2f, fog = 0.42f, fogMix = 0.8f, skyMix = 0.88f, exposure = 0.72f, ambient = 0.86f, wet = 1f, tint = new Color(0.86f, 0.92f, 1.0f) };
            case SpawnConditions.Weather.Snow:
                return new WeatherMod { sun = 0.5f, shadow = 0.3f, sat = -0.16f, fog = 0.5f, fogMix = 0.8f, skyMix = 0.8f, exposure = 0.95f, ambient = 1.08f, snow = 0.8f, tint = new Color(0.96f, 0.98f, 1.04f) };
            default:
                return new WeatherMod { sun = 1f, shadow = 1f, fog = 1f, exposure = 1f, ambient = 1f, tint = Color.white };
        }
    }

    // ---------- 날씨: 기상청(GET /api/weather → SpawnConditions.LiveWeather), 설정에서 고정 가능 ----------

    const string WeatherPrefKey = "map_weather";   // 없음 = 자동(기상청), 1~4 = 맑음 · 흐림 · 비 · 눈
    static bool weatherLoaded;
    static SpawnConditions.Weather? weatherOverride;

    /// <summary>설정에서 고정한 날씨 효과 (null = 기상청 날씨). 지도 모습만 바꾸고 경희펫 출현 규칙에는 쓰지 않는다</summary>
    public static SpawnConditions.Weather? WeatherOverride
    {
        get
        {
            if (!weatherLoaded)
            {
                weatherLoaded = true;
                int v = PlayerPrefs.GetInt(WeatherPrefKey, 0);
                weatherOverride = v >= 1 && v <= 4 ? (SpawnConditions.Weather)v : (SpawnConditions.Weather?)null;
            }
            return weatherOverride;
        }
        set
        {
            weatherLoaded = true;
            weatherOverride = value;
            if (value.HasValue) PlayerPrefs.SetInt(WeatherPrefKey, (int)value.Value);
            else PlayerPrefs.DeleteKey(WeatherPrefKey);
            PlayerPrefs.Save();
        }
    }

    /// <summary>지도에 보일 날씨: 고정값 → 기상청 → 모르면 맑음</summary>
    public static SpawnConditions.Weather ViewWeather =>
        WeatherOverride ?? (SpawnConditions.CurrentWeather == SpawnConditions.Weather.Unknown ? SpawnConditions.Weather.Clear : SpawnConditions.CurrentWeather);

    /// <summary>지도에 보일 계절 (인스펙터 고정 → 설정 · 날짜, 강조색과 같은 KucaTheme.Current)</summary>
    public Season ViewSeason => season != Season.Auto ? season : (Season)((int)KucaTheme.Current + 1);

    [System.Serializable]
    public class Preset
    {
        public Color sunColor = new Color(1f, 0.93f, 0.8f);
        public float sunIntensity = 0.72f, sunPitch = 45f, sunYaw = 60f;
        [Tooltip("환경광 (리니어 배율)")]
        public Vector3 skyAmbient = new Vector3(0.56f, 0.62f, 0.74f), groundAmbient = new Vector3(0.66f, 0.62f, 0.50f);
        [Range(0f, 1f)] public float shadowStrength = 0.7f;
        [Range(0f, 1f)] public float wrap = 0.35f;
        public Vector3 shadowTint = new Vector3(0.80f, 0.86f, 1.06f);
        public float warmTop = 0.16f, rim = 0.22f, saturation = 1.12f, wash = 0.10f;
        public Color washColor = new Color(1f, 0.94f, 0.78f);
        public Color fogColor = new Color(0.88f, 0.93f, 0.98f);
        public float fogStart = 900f, fogEnd = 2600f;
        [Range(0f, 1f), Tooltip("창문·가로등·바닥 불빛 (밤 = 1)")] public float night;
        public Color skyTint = new Color(0.48f, 0.68f, 0.96f);
        public float skyExposure = 1.3f;

        public static Preset Lerp(Preset a, Preset b, float t)
        {
            return new Preset
            {
                sunColor = Color.Lerp(a.sunColor, b.sunColor, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                sunPitch = Mathf.Lerp(a.sunPitch, b.sunPitch, t),
                sunYaw = Mathf.LerpAngle(a.sunYaw, b.sunYaw, t),
                skyAmbient = Vector3.Lerp(a.skyAmbient, b.skyAmbient, t),
                groundAmbient = Vector3.Lerp(a.groundAmbient, b.groundAmbient, t),
                shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t),
                wrap = Mathf.Lerp(a.wrap, b.wrap, t),
                shadowTint = Vector3.Lerp(a.shadowTint, b.shadowTint, t),
                warmTop = Mathf.Lerp(a.warmTop, b.warmTop, t),
                rim = Mathf.Lerp(a.rim, b.rim, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                wash = Mathf.Lerp(a.wash, b.wash, t),
                washColor = Color.Lerp(a.washColor, b.washColor, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t),
                fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t),
                night = Mathf.Lerp(a.night, b.night, t),
                skyTint = Color.Lerp(a.skyTint, b.skyTint, t),
                skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t),
            };
        }
    }

    public Mode mode = Mode.Auto;
    public Preset day = new Preset();
    public Preset sunset = new Preset();
    public Preset night = new Preset();
    [Tooltip("아침 (A-1 출현 시간대와 맞춤): 낮게 뜬 옅은 금빛 해, 긴 그림자, 옅은 아침 안개")]
    public Preset morning = MorningDefault();

    /// <summary>씬에 아침 값이 저장돼 있지 않을 때 쓰는 기본값 (Art/Concept/06_A_Round/A1_분위기_time_morning.png 기준)</summary>
    static Preset MorningDefault() => new Preset
    {
        sunColor = new Color(1f, 0.88f, 0.68f), sunIntensity = 0.9f, sunPitch = 22f, sunYaw = 80f,
        skyAmbient = new Vector3(0.58f, 0.64f, 0.74f), groundAmbient = new Vector3(0.60f, 0.58f, 0.50f),
        shadowStrength = 0.6f, wrap = 0.38f, shadowTint = new Vector3(0.82f, 0.90f, 1.10f),
        warmTop = 0.2f, rim = 0.32f, saturation = 1.1f, wash = 0.18f, washColor = new Color(1f, 0.92f, 0.76f),
        fogColor = new Color(0.93f, 0.94f, 0.92f), fogStart = 600f, fogEnd = 2200f, night = 0f,
        skyTint = new Color(0.70f, 0.78f, 0.95f), skyExposure = 1.25f,
    };

    [Header("계절 (Auto = 앱 설정의 계절, 설정이 자동이면 기기 날짜: 3~5 봄, 6~8 여름, 9~11 가을, 12~2 겨울)")]
    public Season season = Season.Auto;
    public SeasonPalette spring = new SeasonPalette();
    public SeasonPalette summer = new SeasonPalette();
    public SeasonPalette autumn = new SeasonPalette();
    public SeasonPalette winter = new SeasonPalette();

    [Header("밤 불빛")]
    public Color glowColor = new Color(1f, 0.80f, 0.48f);
    public float glowStrength = 1.6f;
    [Range(0f, 1f)] public float litWindows = 0.55f;
    public float groundLight = 0.9f;

    [Tooltip("밤 빛 지도 (KeyArtLights.png): 등 아래 웅덩이 + 넓게 번지는 빛")]
    public Texture2D lightMap;
    public Vector2 mapSize = new Vector2(1418f, 1548f);

    [Header("바꿀 대상 (Apply Key Art 가 채움)")]
    public Light sun;
    public Material skybox;

    [Tooltip("Auto 모드를 에디터에서 볼 때 쓸 시각 (0~24, 음수면 기기 시계)")]
    public float previewHour = -1f;

    static readonly int SkyId = Shader.PropertyToID("_KucaSkyAmbient");
    static readonly int GroundId = Shader.PropertyToID("_KucaGroundAmbient");
    static readonly int ShadowId = Shader.PropertyToID("_KucaShadowStrength");
    static readonly int WrapId = Shader.PropertyToID("_KucaWrap");
    static readonly int TintId = Shader.PropertyToID("_KucaShadowTint");
    static readonly int WarmId = Shader.PropertyToID("_KucaWarmTop");
    static readonly int RimId = Shader.PropertyToID("_KucaRim");
    static readonly int SatId = Shader.PropertyToID("_KucaSaturation");
    static readonly int WashId = Shader.PropertyToID("_KucaWash");
    static readonly int WashColorId = Shader.PropertyToID("_KucaWashColor");
    static readonly int NightId = Shader.PropertyToID("_KucaNight");
    static readonly int GlowColorId = Shader.PropertyToID("_KucaGlowColor");
    static readonly int GlowStrengthId = Shader.PropertyToID("_KucaGlowStrength");
    static readonly int LitId = Shader.PropertyToID("_KucaLitWindows");
    static readonly int GroundLightId = Shader.PropertyToID("_KucaGroundLight");
    static readonly int LightMapId = Shader.PropertyToID("_KucaLightMap");
    static readonly int MapSizeId = Shader.PropertyToID("_KucaMapSize");
    static readonly int LeafId = Shader.PropertyToID("_KucaSeasonLeaf");
    static readonly int BlossomId = Shader.PropertyToID("_KucaSeasonBlossom");
    static readonly int GrassId = Shader.PropertyToID("_KucaSeasonGrass");
    static readonly int AutumnId = Shader.PropertyToID("_KucaAutumn");
    static readonly int SnowId = Shader.PropertyToID("_KucaSnow");
    static readonly int SpeckleId = Shader.PropertyToID("_KucaSpeckle");
    static readonly int BareId = Shader.PropertyToID("_KucaBare");
    static readonly int WetId = Shader.PropertyToID("_KucaWet");
    static readonly int BloomId = Shader.PropertyToID("_KucaBloom");

    float nextAutoUpdate;
    Season appliedSeason;
    SpawnConditions.Weather appliedWeather;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    void Update()
    {
        // 설정에서 계절 · 날씨를 바꾸거나 기상청 날씨가 새로 오면 바로 다시 칠한다
        if (ViewSeason != appliedSeason || ViewWeather != appliedWeather)
        {
            Apply();
            return;
        }
        // Auto 는 시각에 따라 천천히 바뀌므로 몇 초마다만 다시 계산
        if (mode == Mode.Auto && Time.realtimeSinceStartup >= nextAutoUpdate)
        {
            nextAutoUpdate = Time.realtimeSinceStartup + 5f;
            Apply();
        }
    }

    /// <summary>계절을 바꾸고 바로 적용</summary>
    public void SetSeason(Season s)
    {
        season = s;
        Apply();
    }

    public SeasonPalette CurrentSeason()
    {
        switch (ViewSeason)
        {
            case Season.Summer: return summer;
            case Season.Autumn: return autumn;
            case Season.Winter: return winter;
            default: return spring;
        }
    }

    /// <summary>모드를 바꾸고 바로 적용 (게임 UI·디버그 버튼에서 불러도 된다)</summary>
    public void SetMode(Mode m)
    {
        mode = m;
        Apply();
    }

    public Preset Current()
    {
        switch (mode)
        {
            case Mode.Day: return day;
            case Mode.Sunset: return sunset;
            case Mode.Night: return night;
            case Mode.Morning: return morning;
        }
        // 출현 조건 테스트로 시간대를 강제했으면 지도 빛도 그 시간대로 (CollectibleSpawner 인스펙터)
        if (SpawnConditions.OverrideTime is SpawnConditions.TimeSlot slot)
            return PresetFor(slot);
        float h = previewHour >= 0f ? previewHour : (float)System.DateTime.Now.TimeOfDay.TotalHours;
        return ForHour(h);
    }

    /// <summary>출현 시간대(SpawnConditions.TimeSlot)의 대표 프리셋</summary>
    public Preset PresetFor(SpawnConditions.TimeSlot slot)
    {
        switch (slot)
        {
            case SpawnConditions.TimeSlot.Morning: return morning;
            case SpawnConditions.TimeSlot.Evening: return sunset;
            case SpawnConditions.TimeSlot.Night: return night;
            default: return day;
        }
    }

    /// <summary>
    /// 시각(0~24) → 프리셋. 캐릭터 출현 시간대(아침 06–11 · 낮 11–17 · 저녁 17–20 · 밤 20–06)와 맞추고,
    /// 경계 앞뒤로 천천히 섞는다: 밤 ~5:00 → 아침 6:00~10:00 → 낮 11:00~16:30 → 노을 17:30~19:30 → 밤 20:30~
    /// </summary>
    public Preset ForHour(float h)
    {
        if (h < 5f || h >= 20.5f) return night;
        if (h < 6f) return Preset.Lerp(night, morning, Mathf.InverseLerp(5f, 6f, h));
        if (h < 10f) return morning;
        if (h < 11f) return Preset.Lerp(morning, day, Mathf.InverseLerp(10f, 11f, h));
        if (h < 16.5f) return day;
        if (h < 17.5f) return Preset.Lerp(day, sunset, Mathf.InverseLerp(16.5f, 17.5f, h));
        if (h < 19.5f) return sunset;
        return Preset.Lerp(sunset, night, Mathf.InverseLerp(19.5f, 20.5f, h));
    }

    public void Apply()
    {
        Preset p = Current();
        if (p == null) return;
        appliedSeason = ViewSeason;
        appliedWeather = ViewWeather;
        p = Styled(p, CurrentSeason(), ModFor(appliedWeather));
        ApplyShaderGlobals(p);
        ApplyRest(p);
    }

    static float Luma(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

    /// <summary>시간대 프리셋에 계절 빛과 날씨를 얹은 사본 (원래 프리셋은 그대로)</summary>
    static Preset Styled(Preset p, SeasonPalette sp, WeatherMod w)
    {
        Preset q = Preset.Lerp(p, p, 0f);
        q.sunColor = p.sunColor * sp.light;
        q.sunIntensity = p.sunIntensity * sp.sunScale * w.sun;
        q.shadowStrength = p.shadowStrength * w.shadow;
        q.saturation = p.saturation + sp.saturationAdd + w.sat;
        q.warmTop = p.warmTop * w.sun;
        q.rim = p.rim * Mathf.Lerp(0.5f, 1f, w.sun);
        q.wash = p.wash * w.sun;
        q.skyAmbient = p.skyAmbient * w.ambient;
        q.groundAmbient = p.groundAmbient * w.ambient;
        // 흐림 · 비 · 눈: 하늘과 안개를 지금 밝기 그대로 잿빛으로 (밤 하늘이 밝아지지 않게)
        Color sky = p.skyTint * sp.sky;
        q.skyTint = Color.Lerp(sky, Luma(sky) * w.tint, w.skyMix);
        q.skyExposure = p.skyExposure * w.exposure;
        q.fogColor = Color.Lerp(p.fogColor, Luma(p.fogColor) * w.tint, w.fogMix);
        q.fogStart = p.fogStart * w.fog;
        q.fogEnd = p.fogEnd * w.fog;
        return q;
    }

    /// <summary>
    /// 3D 미리보기(NeutralAmbient)가 그리는 동안만 낮 값으로 바꿨다가 되돌릴 때 쓴다: 캐릭터 · 경희펫 셰이더(PlayerToy · CreatureToy)는
    /// 주변광 프로브가 아니라 이 전역 값을 읽어, 밤 캡처 미리보기가 낮보다 약 20% 어두웠음 (REVIEW_v22_day)
    /// </summary>
    public void ApplyShaderGlobals(Preset p)
    {
        Shader.SetGlobalVector(SkyId, p.skyAmbient);
        Shader.SetGlobalVector(GroundId, p.groundAmbient);
        Shader.SetGlobalFloat(ShadowId, p.shadowStrength);
        Shader.SetGlobalFloat(WrapId, p.wrap);
        Shader.SetGlobalVector(TintId, p.shadowTint);
        Shader.SetGlobalFloat(WarmId, p.warmTop);
        Shader.SetGlobalFloat(RimId, p.rim);
        Shader.SetGlobalFloat(SatId, p.saturation);
        Shader.SetGlobalFloat(WashId, p.wash);
        Shader.SetGlobalColor(WashColorId, p.washColor.linear);
        Shader.SetGlobalFloat(NightId, p.night);
        Shader.SetGlobalColor(GlowColorId, glowColor.linear);
        Shader.SetGlobalFloat(GlowStrengthId, glowStrength);
        Shader.SetGlobalFloat(LitId, litWindows);
        Shader.SetGlobalFloat(GroundLightId, groundLight);
        if (lightMap != null) Shader.SetGlobalTexture(LightMapId, lightMap);
        Shader.SetGlobalVector(MapSizeId, lightMap != null ? new Vector4(mapSize.x, mapSize.y, 0f, 0f) : Vector4.zero);
    }

    void ApplyRest(Preset p)
    {
        SeasonPalette sp = CurrentSeason();
        // leaf.x 가 0 이면 셰이더가 계절을 건너뛰므로 최소값을 둔다
        Shader.SetGlobalVector(LeafId, new Vector4(Mathf.Max(sp.leaf.x, 0.001f), sp.leaf.y, sp.leaf.z, 1f));
        Color bl = sp.blossom.linear;
        Shader.SetGlobalVector(BlossomId, new Vector4(bl.r, bl.g, bl.b, sp.blossomMix));
        Shader.SetGlobalVector(GrassId, sp.grass);
        Shader.SetGlobalFloat(AutumnId, sp.autumn);
        WeatherMod w = ModFor(appliedWeather);
        Shader.SetGlobalFloat(SnowId, Mathf.Max(sp.snow, w.snow));
        Color sk = sp.speckle.linear;
        Shader.SetGlobalVector(SpeckleId, new Vector4(sk.r, sk.g, sk.b, sp.speckleAmount));
        Shader.SetGlobalFloat(BareId, sp.bare);
        Shader.SetGlobalFloat(BloomId, sp.bloom);
        Shader.SetGlobalFloat(WetId, w.wet);

        if (sun != null)
        {
            sun.color = p.sunColor;
            sun.intensity = p.sunIntensity;
            sun.transform.rotation = Quaternion.Euler(p.sunPitch, p.sunYaw, 0f);
        }
        if (RenderSettings.fog)
        {
            RenderSettings.fogColor = p.fogColor;
            RenderSettings.fogStartDistance = p.fogStart;
            RenderSettings.fogEndDistance = p.fogEnd;
        }
        // 키아트 셰이더 밖(캐릭터 등 URP Lit)도 비슷한 환경광 (리니어 배율 → 감마 색)
        var skyC = new Color(p.skyAmbient.x, p.skyAmbient.y, p.skyAmbient.z);
        var groundC = new Color(p.groundAmbient.x, p.groundAmbient.y, p.groundAmbient.z);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = skyC.gamma;
        RenderSettings.ambientEquatorColor = Color.Lerp(skyC, groundC, 0.5f).gamma;
        RenderSettings.ambientGroundColor = groundC.gamma;
        if (skybox != null)
        {
            skybox.SetColor("_SkyTint", p.skyTint);
            skybox.SetFloat("_Exposure", p.skyExposure);
        }
    }
}
