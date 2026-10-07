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
    public enum Mode { Auto, Day, Sunset, Night }
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
    }

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

    [Header("계절 (Auto = 기기 날짜: 3~5 봄, 6~8 여름, 9~11 가을, 12~2 겨울)")]
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

    float nextAutoUpdate;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    void Update()
    {
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
        Season s = season;
        if (s == Season.Auto)
        {
            int m = System.DateTime.Now.Month;
            s = m >= 3 && m <= 5 ? Season.Spring : m >= 6 && m <= 8 ? Season.Summer : m >= 9 && m <= 11 ? Season.Autumn : Season.Winter;
        }
        switch (s)
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
        }
        float h = previewHour >= 0f ? previewHour : (float)System.DateTime.Now.TimeOfDay.TotalHours;
        return ForHour(h);
    }

    /// <summary>시각(0~24) → 프리셋. 밤 ~5:00, 새벽 노을 6:15, 낮 7:30~17:00, 저녁 노을 18:15, 밤 19:30~</summary>
    public Preset ForHour(float h)
    {
        if (h < 5f || h >= 19.5f) return night;
        if (h < 6.25f) return Preset.Lerp(night, sunset, Mathf.InverseLerp(5f, 6.25f, h));
        if (h < 7.5f) return Preset.Lerp(sunset, day, Mathf.InverseLerp(6.25f, 7.5f, h));
        if (h < 17f) return day;
        if (h < 18.25f) return Preset.Lerp(day, sunset, Mathf.InverseLerp(17f, 18.25f, h));
        return Preset.Lerp(sunset, night, Mathf.InverseLerp(18.25f, 19.5f, h));
    }

    public void Apply()
    {
        Preset p = Current();
        if (p == null) return;
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

        SeasonPalette sp = CurrentSeason();
        // leaf.x 가 0 이면 셰이더가 계절을 건너뛰므로 최소값을 둔다
        Shader.SetGlobalVector(LeafId, new Vector4(Mathf.Max(sp.leaf.x, 0.001f), sp.leaf.y, sp.leaf.z, 1f));
        Color bl = sp.blossom.linear;
        Shader.SetGlobalVector(BlossomId, new Vector4(bl.r, bl.g, bl.b, sp.blossomMix));
        Shader.SetGlobalVector(GrassId, sp.grass);
        Shader.SetGlobalFloat(AutumnId, sp.autumn);
        Shader.SetGlobalFloat(SnowId, sp.snow);

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
