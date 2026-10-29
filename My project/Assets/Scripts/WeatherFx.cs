using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지도 위 날씨 · 계절 화면 효과 (S1): 비 · 눈 · 봄 꽃잎 · 가을 낙엽이 하나씩 따로 내린다.
/// - 비: 살짝 기운 가는 빗줄기가 빠르게 떨어지고, 바닥에 닿으면 작은 물결 동그라미가 퍼졌다 사라진다
/// - 눈: 크기 · 속도가 다른 눈송이가 좌우로 흔들리며 천천히 (큰 송이 = 가까이 = 조금 빠르게)
/// - 봄 꽃잎 · 가을 낙엽: 몇 장만 빙글빙글 돌며
/// - 번잡하지 않게 개수를 적게 두고, 전부 메시 하나(그리기 한 번)로 그린다
/// - 날씨는 KeyArtLook.ViewWeather (설정의 "지도 날씨" → 기상청), 계절은 KucaTheme.Current (설정의 계절 → 날짜)
/// - 지도 바로 위, 앱 화면(HUD 10 이상) 아래 캔버스(5). 터치는 막지 않는다. 셰이더 · 머티리얼 에셋 없이 기본 UI 로 그린다
/// </summary>
public class WeatherFx : MonoBehaviour
{
    enum Kind { None, Rain, Snow, Petals, Leaves }

    /// <summary>UI 캡처처럼 화면이 매번 같아야 할 때 끈다</summary>
    public static bool Paused;

    // 그림 칸 (가로 4칸 아틀라스, 전부 흰색 + 알파, 색은 정점 색으로)
    const int Streak = 0, Dot = 1, Ring = 2, Petal = 3;

    struct Bit
    {
        public Vector2 pos, vel, size;
        public float rot, spin, phase, sway, alpha, life, age;
        public int cell;
        public Color color;
    }

    readonly List<Bit> bits = new List<Bit>();
    readonly List<Bit> ripples = new List<Bit>();
    FxMesh mesh;
    Image tint;
    Kind kind = Kind.None;
    bool built;
    readonly System.Random rng = new System.Random(7);

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
        mesh = new GameObject("Bits", typeof(RectTransform), typeof(CanvasRenderer), typeof(FxMesh)).GetComponent<FxMesh>();
        Stretch(mesh.rectTransform, canvas.transform);
        mesh.raycastTarget = false;
        mesh.atlas = Atlas();
        mesh.bits = bits;
        mesh.ripples = ripples;
        Show(Kind.None);
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
        Step(Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        mesh.SetVerticesDirty();
    }

    float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    Vector2 Size => mesh.rectTransform.rect.size;   // 캔버스 단위 (기준 1080 × 1920)

    void Show(Kind k)
    {
        kind = k;
        bits.Clear();
        ripples.Clear();
        int count = k == Kind.Rain ? 46 : k == Kind.Snow ? 55 : k == Kind.Petals ? 18 : k == Kind.Leaves ? 15 : 0;
        for (int i = 0; i < count; i++)
            bits.Add(Spawn(true));
        tint.color = k == Kind.Rain ? new Color(0.30f, 0.38f, 0.48f, 0.10f)   // 비 오는 날 잿빛 공기
                   : k == Kind.Snow ? new Color(0.92f, 0.95f, 1f, 0.07f) : Color.clear;
        tint.enabled = tint.color.a > 0f;
        mesh.enabled = k != Kind.None;
    }

    /// <summary>새 조각 하나. anywhere = 처음 켤 때 화면 전체에 흩어 놓기 (아니면 화면 위에서 시작)</summary>
    Bit Spawn(bool anywhere)
    {
        Vector2 s = Size;
        var b = new Bit { color = Color.white, life = 1f };
        float x = R(-0.1f, 1.1f) * s.x;
        float y = anywhere ? R(0f, 1f) * s.y : s.y + R(10f, 120f);
        switch (kind)
        {
            case Kind.Rain:
                float speed = R(1500f, 2100f);
                b.vel = new Vector2(speed * 0.18f, -speed);   // 살짝 기울게 (오른쪽 아래로)
                b.size = new Vector2(R(3.5f, 5.5f), R(52f, 84f));
                b.rot = Mathf.Atan2(b.vel.x, -b.vel.y) * Mathf.Rad2Deg;   // 줄기를 떨어지는 방향으로 눕힘 (Quad 는 반시계 회전)
                b.alpha = R(0.32f, 0.55f);
                b.cell = Streak;
                b.color = new Color(0.90f, 0.95f, 1f);
                b.life = R(0.15f, 0.9f) * s.y;   // 이 높이에서 바닥에 닿음 (지도는 비스듬히 보이니 화면 곳곳)
                break;
            case Kind.Snow:
                float d = R(0f, 1f);   // 0 먼 송이 · 1 가까운 송이
                float r = Mathf.Lerp(9f, 24f, d * d);
                b.size = new Vector2(r, r);
                b.vel = new Vector2(R(-12f, 12f), -Mathf.Lerp(55f, 150f, d));
                b.sway = Mathf.Lerp(14f, 38f, d);
                b.phase = R(0f, 6.28f);
                b.spin = R(0.5f, 1.2f);   // 흔들림 빠르기
                b.alpha = Mathf.Lerp(0.62f, 0.95f, d);
                b.cell = Dot;
                break;
            default:   // 꽃잎 · 낙엽
                bool petal = kind == Kind.Petals;
                b.size = petal ? new Vector2(R(20f, 28f), R(12f, 16f)) : new Vector2(R(24f, 34f), R(14f, 19f));
                b.vel = new Vector2(R(12f, 40f), -R(50f, 95f));
                b.sway = R(26f, 48f);
                b.phase = R(0f, 6.28f);
                b.spin = R(0.6f, 1.1f);
                b.rot = R(0f, 360f);
                b.alpha = R(0.75f, 0.95f);
                b.cell = Petal;
                Color[] pal = petal
                    ? new[] { new Color(1f, 0.80f, 0.87f), new Color(1f, 0.90f, 0.93f), new Color(0.98f, 0.70f, 0.80f) }
                    : new[] { new Color(0.90f, 0.46f, 0.14f), new Color(0.80f, 0.26f, 0.10f), new Color(0.88f, 0.66f, 0.18f), new Color(0.58f, 0.36f, 0.20f) };
                b.color = pal[rng.Next(pal.Length)];
                break;
        }
        b.pos = new Vector2(x, y);
        return b;
    }

    void Step(float dt)
    {
        Vector2 s = Size;
        for (int i = 0; i < bits.Count; i++)
        {
            Bit b = bits[i];
            b.age += dt;
            if (kind == Kind.Rain)
            {
                b.pos += b.vel * dt;
                if (b.pos.y < b.life)
                {
                    if (ripples.Count < 12 && rng.NextDouble() < 0.4)   // 닿은 자리에 물결 (가끔만)
                        ripples.Add(new Bit { pos = b.pos, cell = Ring, life = R(0.45f, 0.7f), size = new Vector2(R(26f, 40f), 0f), color = new Color(0.92f, 0.96f, 1f), alpha = 0.45f });
                    b = Spawn(false);
                }
            }
            else
            {
                b.pos.y += b.vel.y * dt;
                b.pos.x += (b.vel.x + Mathf.Cos(b.age * b.spin * 2f + b.phase) * b.sway) * dt;
                if (kind != Kind.Snow) b.rot += Mathf.Sin(b.age * b.spin * 1.6f + b.phase) * 140f * dt;   // 빙글빙글
                if (b.pos.y < -40f || b.pos.x > s.x + 60f || b.pos.x < -60f) b = Spawn(false);
            }
            bits[i] = b;
        }
        for (int i = ripples.Count - 1; i >= 0; i--)
        {
            Bit r = ripples[i];
            r.age += dt;
            if (r.age >= r.life) ripples.RemoveAt(i);
            else ripples[i] = r;
        }
    }

    // ---------- 그림 (가로 4칸 64 px: 빗줄기 · 둥근 점 · 물결 고리 · 잎) ----------

    static Texture2D Atlas()
    {
        const int C = 64, W = C * 4;
        var t = new Texture2D(W, C, TextureFormat.RGBA32, false) { name = "WeatherFx", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[W * C];
        var a = new float[4];
        for (int y = 0; y < C; y++)
            for (int x = 0; x < C; x++)
            {
                float u = (x + 0.5f) / C * 2f - 1f, v = (y + 0.5f) / C * 2f - 1f, d = Mathf.Sqrt(u * u + v * v);
                a[Streak] = Mathf.Exp(-u * u / 0.08f) * Mathf.Clamp01((1f - v) * 0.5f) * Mathf.Clamp01((1f - Mathf.Abs(v)) * 6f);   // 아래(떨어지는 쪽) 끝이 진함
                a[Dot] = Mathf.Pow(Mathf.Clamp01(1f - d * d), 2f);
                a[Ring] = Mathf.Exp(-Mathf.Pow((d - 0.75f) / 0.09f, 2f));
                float pd = u * u + v * v / 0.36f;
                a[Petal] = Mathf.Clamp01(1f - pd) * Mathf.Clamp01(1.2f - pd * 0.4f);
                for (int c = 0; c < 4; c++)
                    px[y * W + c * C + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a[c]));   // 빈 칸도 흰색(투명): 가장자리 검은 테 방지
            }
        t.SetPixels32(px);
        t.Apply(false, true);
        return t;
    }

    void OnDestroy()
    {
        if (mesh != null && mesh.atlas != null) Destroy(mesh.atlas);
    }

    /// <summary>조각 전부를 사각형 메시 하나로</summary>
    class FxMesh : MaskableGraphic
    {
        public Texture2D atlas;
        public List<Bit> bits, ripples;

        public override Texture mainTexture => atlas != null ? atlas : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (bits == null) return;
            Vector2 origin = rectTransform.rect.min;
            foreach (Bit b in bits)
                Quad(vh, origin + b.pos, b.size, b.rot, b.cell, b.color, b.alpha);
            foreach (Bit r in ripples)
            {
                float t = r.age / r.life;   // 0 → 1: 커지며 옅어짐
                float w = r.size.x * (0.3f + 0.7f * t);
                Quad(vh, origin + r.pos, new Vector2(w, w * 0.45f), 0f, Ring, r.color, r.alpha * (1f - t));   // 땅에 눕힌 납작한 고리
            }
        }

        static void Quad(VertexHelper vh, Vector2 c, Vector2 size, float rotDeg, int cell, Color col, float alpha)
        {
            const float inset = 0.5f / 256f;
            float u0 = cell * 0.25f + inset, u1 = (cell + 1) * 0.25f - inset;
            float a = rotDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            Vector2 ax = new Vector2(ca, sa) * (size.x * 0.5f), ay = new Vector2(-sa, ca) * (size.y * 0.5f);   // 반시계 rotDeg 회전
            col.a = alpha;
            int i = vh.currentVertCount;
            vh.AddVert(c - ax - ay, col, new Vector2(u0, 0f));
            vh.AddVert(c - ax + ay, col, new Vector2(u0, 1f));
            vh.AddVert(c + ax + ay, col, new Vector2(u1, 1f));
            vh.AddVert(c + ax - ay, col, new Vector2(u1, 0f));
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}
