using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지도 위 날씨 · 계절 화면 효과 (S1): 비 줄기 · 눈송이 · 봄 꽃잎 · 가을 낙엽이 내린다.
/// - 날씨는 KeyArtLook.ViewWeather (설정의 "날씨 효과" → 기상청), 계절은 KucaTheme.Current (설정의 계절 → 날짜)
/// - 지도 바로 위, 앱 화면(HUD 10 이상) 아래 캔버스(5)에 반투명 그림 두 겹을 흘려보낸다. 터치는 막지 않는다
/// - 셰이더 · 머티리얼 에셋 없이 기본 UI 로 그려 빌드에서 빠질 걱정이 없다
/// </summary>
public class WeatherFx : MonoBehaviour
{
    enum Kind { None, Rain, Snow, Petals, Leaves }

    /// <summary>UI 캡처처럼 화면이 매번 같아야 할 때 끈다</summary>
    public static bool Paused;

    struct Layer
    {
        public RawImage img;
        public Vector2 offset;
        public float scale;    // 그림 1 px 이 화면 몇 px 인지
        public Vector2 speed;  // 초당 그림 크기 배수
    }

    Layer near, far;
    Image tint;
    Kind kind = Kind.None;
    bool built;
    Texture2D rainTex, snowTex, petalTex, leafTex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (Object.FindAnyObjectByType<KeyArtLook>() == null) return;   // 키아트 지도가 있는 씬에서만
        var go = new GameObject("WeatherFx");
        DontDestroyOnLoad(go);
        go.AddComponent<WeatherFx>();
    }

    void Build()
    {
        built = true;
        Canvas canvas = UIKit.CreateCanvas(transform, "WeatherCanvas", 5);   // 지도 위, HUD(10) 아래
        Destroy(canvas.GetComponent<GraphicRaycaster>());
        tint = new GameObject("Tint", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Stretch(tint.rectTransform, canvas.transform);
        tint.raycastTarget = false;
        far = MakeLayer(canvas.transform, "Far");
        near = MakeLayer(canvas.transform, "Near");
        Show(Kind.None);
    }

    static Layer MakeLayer(Transform parent, string name)
    {
        var img = new GameObject(name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        Stretch(img.rectTransform, parent);
        img.raycastTarget = false;
        return new Layer { img = img };
    }

    static void Stretch(RectTransform r, Transform parent)
    {
        r.SetParent(parent, false);
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Kind Want()
    {
        if (Paused) return Kind.None;
        switch (KeyArtLook.ViewWeather)
        {
            case SpawnConditions.Weather.Rain: return Kind.Rain;
            case SpawnConditions.Weather.Snow: return Kind.Snow;
        }
        switch (KucaTheme.Current)
        {
            case KucaTheme.Season.Spring: return Kind.Petals;
            case KucaTheme.Season.Autumn: return Kind.Leaves;
        }
        return Kind.None;
    }

    void Update()
    {
        Kind want = Want();
        if (!built)
        {
            if (want == Kind.None) return;
            Build();
        }
        if (want != kind) Show(want);
        if (kind == Kind.None) return;
        float dt = Time.unscaledDeltaTime;
        float sway = Mathf.Sin(Time.unscaledTime * 0.7f) * 0.25f;   // 눈 · 꽃잎 · 낙엽이 좌우로 흔들림
        Move(ref near, dt, kind == Kind.Rain ? 0f : sway);
        Move(ref far, dt, kind == Kind.Rain ? 0f : -sway * 0.6f);
    }

    static void Move(ref Layer l, float dt, float sway)
    {
        if (l.img.texture == null) return;
        float w = Screen.width / (l.img.texture.width * l.scale), h = Screen.height / (l.img.texture.height * l.scale);
        l.offset += new Vector2(l.speed.x + l.speed.y * sway, l.speed.y) * dt;
        l.offset.x = Mathf.Repeat(l.offset.x, 1f);
        l.offset.y = Mathf.Repeat(l.offset.y, 1f);
        l.img.uvRect = new Rect(l.offset.x, l.offset.y, w, h);
    }

    void Show(Kind k)
    {
        kind = k;
        Texture2D tex = null;
        Color tintColor = Color.clear;
        switch (k)
        {
            case Kind.Rain:
                tex = rainTex ??= Streaks(48, 7);
                Set(ref near, tex, 2.4f, new Vector2(-1.9f * 0.18f, 1.9f), 0.55f);   // 줄기 기울기(0.18)와 같은 방향으로 떨어짐
                Set(ref far, tex, 1.4f, new Vector2(-1.3f * 0.18f, 1.3f), 0.35f);
                tintColor = new Color(0.30f, 0.38f, 0.48f, 0.10f);   // 비 오는 날 잿빛 공기
                break;
            case Kind.Snow:
                tex = snowTex ??= Dots(60, 11, new Color(1f, 1f, 1f), 1.4f, 3.4f, 1f);
                Set(ref near, tex, 2.2f, new Vector2(0.02f, 0.10f), 0.95f);
                Set(ref far, tex, 1.3f, new Vector2(0.01f, 0.07f), 0.6f);
                tintColor = new Color(0.92f, 0.95f, 1f, 0.07f);
                break;
            case Kind.Petals:
                tex = petalTex ??= Dots(16, 23, new Color(1f, 0.78f, 0.86f), 2.2f, 3.2f, 0.55f);
                Set(ref near, tex, 2.6f, new Vector2(0.05f, 0.06f), 0.85f);
                Set(ref far, tex, 1.6f, new Vector2(0.03f, 0.045f), 0.5f);
                break;
            case Kind.Leaves:
                tex = leafTex ??= Dots(12, 31, new Color(0.88f, 0.45f, 0.14f), 2.6f, 3.6f, 0.6f);
                Set(ref near, tex, 2.6f, new Vector2(0.04f, 0.07f), 0.85f);
                Set(ref far, tex, 1.6f, new Vector2(0.025f, 0.05f), 0.5f);
                break;
        }
        near.img.enabled = far.img.enabled = tex != null;
        tint.color = tintColor;
        tint.enabled = tintColor.a > 0f;
    }

    static void Set(ref Layer l, Texture2D tex, float scale, Vector2 speed, float alpha)
    {
        l.img.texture = tex;
        l.scale = scale;
        l.speed = speed;
        l.img.color = new Color(1f, 1f, 1f, alpha);
    }

    // ---------- 그림 (이음새 없이 반복되는 256 px) ----------

    const int N = 256;

    /// <summary>빈 칸도 효과 색(투명)으로 채운다: 검정(0,0,0,0)이면 확대 · 축소할 때 가장자리에 검은 테가 번짐</summary>
    static Texture2D NewTex(string name, Color c)
    {
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        var px = new Color32[N * N];
        System.Array.Fill(px, (Color32)new Color(c.r, c.g, c.b, 0f));
        t.SetPixels32(px);
        return t;
    }

    static void Blend(Color32[] px, int x, int y, Color c, float a)
    {
        int i = ((y % N + N) % N) * N + (x % N + N) % N;
        Color32 o = px[i];
        float na = Mathf.Max(o.a / 255f, a);
        px[i] = new Color(c.r, c.g, c.b, na);
    }

    /// <summary>비 줄기: 살짝 기울어진 2 px 선, 아래 끝이 진함 (1 px 은 밝은 바닥 위에서 안 보였음)</summary>
    static Texture2D Streaks(int count, int seed)
    {
        var rng = new System.Random(seed);
        var c = new Color(0.92f, 0.96f, 1f);
        Texture2D t = NewTex("Rain", c);
        Color32[] px = t.GetPixels32();
        for (int k = 0; k < count; k++)
        {
            int x0 = rng.Next(N), y0 = rng.Next(N), len = 16 + rng.Next(16);
            for (int j = 0; j < len; j++)
            {
                float u = j / (float)len, a = 0.2f + 0.65f * u;
                int x = x0 + Mathf.RoundToInt(j * 0.18f);
                Blend(px, x, y0 - j, c, a);
                Blend(px, x + 1, y0 - j, c, a * 0.35f);
            }
        }
        t.SetPixels32(px);
        t.Apply(false, true);
        return t;
    }

    /// <summary>눈송이 · 꽃잎 · 낙엽: 부드러운 점 (squash &lt; 1 이면 납작한 잎 모양)</summary>
    static Texture2D Dots(int count, int seed, Color c, float rMin, float rMax, float squash)
    {
        var rng = new System.Random(seed);
        Texture2D t = NewTex("Fx" + seed, c);
        Color32[] px = t.GetPixels32();
        for (int k = 0; k < count; k++)
        {
            int cx = rng.Next(N), cy = rng.Next(N);
            float r = rMin + (float)rng.NextDouble() * (rMax - rMin);
            float ang = (float)rng.NextDouble() * Mathf.PI, ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
            float shade = 0.85f + 0.3f * (float)rng.NextDouble();
            var col = new Color(Mathf.Min(1f, c.r * shade), Mathf.Min(1f, c.g * shade), Mathf.Min(1f, c.b * shade));
            int R = Mathf.CeilToInt(r) + 1;
            for (int dy = -R; dy <= R; dy++)
                for (int dx = -R; dx <= R; dx++)
                {
                    float u = (dx * ca + dy * sa) / r, v = (-dx * sa + dy * ca) / (r * squash);
                    float d = u * u + v * v;
                    if (d < 1f) Blend(px, cx + dx, cy + dy, col, 0.9f * (1f - d * d));
                }
        }
        t.SetPixels32(px);
        t.Apply(false, true);
        return t;
    }

    void OnDestroy()
    {
        foreach (Texture2D t in new[] { rainTex, snowTex, petalTex, leafTex })
            if (t != null) Destroy(t);
    }
}
