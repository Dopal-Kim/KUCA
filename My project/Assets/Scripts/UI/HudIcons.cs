using UnityEngine;

/// <summary>
/// HUD 아이콘과 장식 스프라이트를 코드로 그린다 (부호 거리 함수 + 안티에일리어싱).
/// 단색 아이콘은 흰색으로 그려서 Image.color 로 색을 입힌다.
/// 좌표는 픽셀 단위, 원점은 왼쪽 아래.
/// </summary>
public static class HudIcons
{
    static Sprite circle, pill, rounded, glowRing, softDisk, needle, bag, shirt, gear, bell, hexagon, chevron;

    public static Sprite Circle => circle != null ? circle : circle = Paint(128, 0, p => p.Circle(64, 64, 63.5f));
    /// <summary>9-slice 알약/둥근 카드 (반지름 = 테두리)</summary>
    public static Sprite Pill => pill != null ? pill : pill = Paint(128, 60, p => p.Box(64, 64, 63.5f, 63.5f, 60f));
    public static Sprite Rounded => rounded != null ? rounded : rounded = Paint(64, 18, p => p.Box(32, 32, 31.5f, 31.5f, 16f));

    /// <summary>바깥으로 은은하게 번지는 빛 고리</summary>
    public static Sprite GlowRing => glowRing != null ? glowRing : glowRing = PaintGlowRing(256, 92f, 7f, 22f);
    /// <summary>가운데가 밝고 가장자리로 사라지는 원</summary>
    public static Sprite SoftDisk => softDisk != null ? softDisk : softDisk = PaintSoftDisk(128);

    /// <summary>나침반 바늘 (북쪽 빨강, 남쪽 회색) — 색이 들어 있으므로 흰색 그대로 쓴다.</summary>
    public static Sprite Needle => needle != null ? needle : needle = Paint(128, 0, p =>
    {
        p.Polygon(new Color(0.88f, 0.22f, 0.24f), new Vector2(64, 118), new Vector2(49, 64), new Vector2(79, 64));
        p.Polygon(new Color(0.72f, 0.76f, 0.84f), new Vector2(64, 10), new Vector2(79, 64), new Vector2(49, 64));
        p.Circle(64, 64, 7f, new Color(0.11f, 0.16f, 0.39f));
    });

    public static Sprite Bag => bag != null ? bag : bag = Paint(128, 0, p =>
    {
        p.Ring(64, 88, 19f, 9f);
        p.Box(64, 56, 42f, 36f, 14f);
        p.Erase(q => q.SegmentD(42, 60, 86, 60, 6f));
        p.Box(64, 40, 16f, 9f, 4f, erase: true);
    });

    public static Sprite Shirt => shirt != null ? shirt : shirt = Paint(128, 0, p =>
    {
        p.Box(64, 54, 28f, 38f, 6f);
        p.Segment(46, 88, 18, 62, 22f);
        p.Segment(82, 88, 110, 62, 22f);
        p.Box(64, 86, 32f, 10f, 8f);
        p.Erase(q => q.CircleD(64, 100, 13f));
    });

    public static Sprite Gear => gear != null ? gear : gear = Paint(128, 0, p =>
    {
        p.Circle(64, 64, 36f);
        // 가운데를 지나는 막대 4개 = 톱니 8개
        for (int i = 0; i < 4; i++)
            p.Box(64, 64, 10f, 52f, 4f, angle: i * 45f);
        p.Erase(q => q.CircleD(64, 64, 15f));
    });

    public static Sprite Bell => bell != null ? bell : bell = Paint(128, 0, p =>
    {
        p.Circle(64, 76, 28f);
        p.Polygon(Color.white, new Vector2(36, 76), new Vector2(92, 76), new Vector2(102, 40), new Vector2(26, 40));
        p.Box(64, 38, 44f, 7f, 7f);
        p.Circle(64, 24, 10f);
        p.Circle(64, 106, 8f);
    });

    /// <summary>금색 테두리 + 남색 육각형 엠블럼</summary>
    public static Sprite Hexagon => hexagon != null ? hexagon : hexagon = Paint(128, 0, p =>
    {
        p.Polygon(new Color(0.85f, 0.72f, 0.45f), Hex(64, 64, 62f));
        p.Polygon(new Color(0.11f, 0.16f, 0.39f), Hex(64, 64, 52f));
    });

    public static Sprite Chevron => chevron != null ? chevron : chevron = Paint(128, 0, p =>
    {
        p.Segment(50, 98, 82, 64, 14f);
        p.Segment(82, 64, 50, 30, 14f);
    });


    // ---------- 메뉴 아이콘 (흰색, Image.color 로 색 입힘) ----------

    static readonly System.Collections.Generic.Dictionary<string, Sprite> cache = new System.Collections.Generic.Dictionary<string, Sprite>();

    static Sprite Cached(string key, int size, System.Action<Painter> draw)
    {
        if (cache.TryGetValue(key, out Sprite s) && s != null)
            return s;
        return cache[key] = Paint(size, 0, draw);
    }

    public static Sprite Close => Cached("close", 128, p =>
    {
        p.Segment(36, 36, 92, 92, 10f);
        p.Segment(36, 92, 92, 36, 10f);
    });

    public static Sprite Search => Cached("search", 128, p =>
    {
        p.Ring(54, 74, 28f, 11f);
        p.Segment(76, 52, 106, 22, 14f);
    });

    /// <summary>도감 (손에 드는 기계)</summary>
    public static Sprite Dex => Cached("dex", 128, p =>
    {
        p.Box(64, 64, 32f, 52f, 16f);
        p.Erase(q => q.CircleD(64, 88, 14f));
        p.Circle(64, 88, 7f);
        for (int i = 0; i < 3; i++)
            p.Erase(q => q.CircleD(50, 54 - i * 14, 4.5f));
        p.Erase(q => q.BoxD(74, 40, 8f, 16f, 3f, 0f));
    });

    /// <summary>경희몬 (귀가 쫑긋한 동그란 몬스터)</summary>
    public static Sprite Monster => Cached("monster", 128, p =>
    {
        p.Polygon(Color.white, new Vector2(30, 66), new Vector2(26, 120), new Vector2(60, 82));
        p.Polygon(Color.white, new Vector2(98, 66), new Vector2(68, 82), new Vector2(102, 120));
        p.Circle(64, 54, 40f);
        p.Erase(q => q.CircleD(50, 60, 6f));
        p.Erase(q => q.CircleD(78, 60, 6f));
        p.Erase(q => q.BoxD(64, 40, 10f, 3.5f, 3.5f, 0f));
    });

    public static Sprite Footprints => Cached("foot", 128, p =>
    {
        p.Box(42, 58, 15f, 26f, 15f, -10f);
        p.Box(86, 76, 15f, 26f, 15f, 10f);
        p.Erase(q => q.SegmentD(20, 46, 64, 38, 5f));
        p.Erase(q => q.SegmentD(64, 64, 108, 72, 5f));
    });

    public static Sprite Pin => Cached("pin", 128, p =>
    {
        p.Circle(64, 80, 34f);
        p.Polygon(Color.white, new Vector2(36, 64), new Vector2(92, 64), new Vector2(64, 10));
        p.Erase(q => q.CircleD(64, 80, 14f));
    });

    /// <summary>역대 파트너 (두 얼굴)</summary>
    public static Sprite Partner => Cached("partner", 128, p =>
    {
        p.Circle(46, 66, 30f);
        p.Erase(q => q.CircleD(84, 62, 36f));
        p.Circle(84, 62, 30f);
        p.Erase(q => q.CircleD(36, 70, 4.5f));
        p.Erase(q => q.CircleD(50, 70, 4.5f));
        p.Erase(q => q.CircleD(76, 66, 4.5f));
        p.Erase(q => q.CircleD(92, 66, 4.5f));
        p.Erase(q => q.SegmentD(76, 50, 92, 50, 5f));
    });

    public static Sprite Scrapbook => Cached("scrap", 128, p =>
    {
        p.Box(64, 64, 42f, 50f, 10f);
        p.Box(64, 70, 30f, 32f, 4f, erase: true);
        p.Polygon(Color.white, new Vector2(38, 42), new Vector2(60, 72), new Vector2(76, 42));
        p.Polygon(Color.white, new Vector2(62, 42), new Vector2(78, 62), new Vector2(92, 42));
        p.Circle(82, 88, 6f);
    });

    /// <summary>모험노트 (펼친 책)</summary>
    public static Sprite Notebook => Cached("note", 128, p =>
    {
        p.Polygon(Color.white, new Vector2(14, 98), new Vector2(60, 88), new Vector2(60, 24), new Vector2(14, 34));
        p.Polygon(Color.white, new Vector2(68, 88), new Vector2(114, 98), new Vector2(114, 34), new Vector2(68, 24));
    });

    /// <summary>갈아입는다 (옷걸이)</summary>
    public static Sprite Hanger => Cached("hanger", 128, p =>
    {
        p.Segment(64, 82, 16, 40, 9f);
        p.Segment(64, 82, 112, 40, 9f);
        p.Segment(16, 40, 112, 40, 9f);
        p.Ring(64, 98, 11f, 8f);
        p.Erase(q => q.BoxD(54, 94, 8f, 6f, 0f, 0f));
    });

    public static Sprite Sort => Cached("sort", 128, p =>
    {
        p.Segment(28, 92, 100, 92, 11f);
        p.Segment(28, 64, 82, 64, 11f);
        p.Segment(28, 36, 62, 36, 11f);
    });

    /// <summary>도구 아이콘 (색이 들어 있음)</summary>
    public static Sprite ItemIcon(string id)
    {
        switch (id)
        {
            case "khball":
                return Cached("i_ball", 128, p =>
                {
                    p.Circle(64, 64, 52f, new Color(0.15f, 0.18f, 0.3f));
                    p.Fill(q => Mathf.Max(q.CircleD(64, 64, 48f), 64 - q.y), new Color(0.72f, 0.12f, 0.15f));
                    p.Fill(q => Mathf.Max(q.CircleD(64, 64, 48f), q.y - 64), new Color(0.97f, 0.97f, 0.97f));
                    p.Fill(q => q.BoxD(64, 64, 50f, 4f, 0f, 0f), new Color(0.15f, 0.18f, 0.3f));
                    p.Circle(64, 64, 15f, new Color(0.15f, 0.18f, 0.3f));
                    p.Circle(64, 64, 9f, new Color(0.85f, 0.72f, 0.45f));
                });
            case "potion":
                return Cached("i_potion", 128, p =>
                {
                    p.Fill(q => q.BoxD(64, 46, 34f, 38f, 14f, 0f), new Color(0.35f, 0.75f, 0.95f));
                    p.Fill(q => q.BoxD(64, 90, 14f, 12f, 3f, 0f), new Color(0.35f, 0.75f, 0.95f));
                    p.Fill(q => q.BoxD(64, 108, 18f, 8f, 4f, 0f), new Color(0.6f, 0.62f, 0.68f));
                    p.Fill(q => q.BoxD(64, 46, 22f, 6f, 2f, 0f), Color.white);
                    p.Fill(q => q.BoxD(64, 46, 6f, 22f, 2f, 0f), Color.white);
                });
            case "berry":
                return Cached("i_berry", 128, p =>
                {
                    p.Fill(q => q.BoxD(50, 104, 18f, 8f, 8f, 30f), new Color(0.3f, 0.7f, 0.3f));
                    p.Fill(q => q.BoxD(78, 104, 18f, 8f, 8f, -30f), new Color(0.3f, 0.7f, 0.3f));
                    p.Circle(64, 56, 44f, new Color(0.98f, 0.78f, 0.2f));
                    p.Circle(50, 70, 10f, new Color(1f, 0.92f, 0.6f));
                });
            default:
                return Cached("i_incense", 128, p =>
                {
                    p.Fill(q => q.BoxD(64, 40, 40f, 30f, 14f, 0f), new Color(0.55f, 0.35f, 0.8f));
                    p.Fill(q => q.BoxD(64, 76, 30f, 8f, 4f, 0f), new Color(0.85f, 0.72f, 0.45f));
                    p.Fill(q => Mathf.Abs(q.CircleD(52, 100, 12f)) - 3f, new Color(0.75f, 0.6f, 0.95f, 0.8f));
                    p.Fill(q => Mathf.Abs(q.CircleD(76, 108, 10f)) - 3f, new Color(0.75f, 0.6f, 0.95f, 0.6f));
                });
        }
    }

    // ---------- 배경 ----------

    /// <summary>왼쪽 위 → 오른쪽 아래 대각선 그라데이션 (RawImage 용)</summary>
    public static Texture2D Gradient(Color topLeft, Color bottomRight)
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HudGradient" };
        var px = new Color[n * n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float t = Mathf.Clamp01((i / (float)(n - 1) + (1f - j / (float)(n - 1))) * 0.5f);
                px[j * n + i] = Color.Lerp(topLeft, bottomRight, Mathf.SmoothStep(0f, 1f, t));
            }
        tex.SetPixels(px);
        tex.Apply(false, true);
        return tex;
    }

    /// <summary>가운데에서 뻗어 나가는 빛줄기 (흰색, 가장자리로 사라짐)</summary>
    public static Sprite Rays => Cached("rays", 256, p =>
    {
        float c = 128f;
        for (int j = 0; j < 256; j++)
            for (int i = 0; i < 256; i++)
            {
                Vector2 d = new Vector2(i + 0.5f - c, j + 0.5f - c);
                float r = d.magnitude / c;
                float a = Mathf.Atan2(d.y, d.x);
                float ray = Mathf.SmoothStep(0.55f, 0.75f, Mathf.Cos(a * 9f) * 0.5f + 0.5f);
                p.px[j * 256 + i] = new Color(1f, 1f, 1f, ray * Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f));
            }
    });

    static Vector2[] Hex(float cx, float cy, float r)
    {
        var pts = new Vector2[6];
        for (int i = 0; i < 6; i++)
        {
            float a = (90f + i * 60f) * Mathf.Deg2Rad;
            pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
        }
        return pts;
    }

    // ---------- 그리기 ----------

    public delegate float Sdf(Painter p);

    public class Painter
    {
        public readonly int size;
        public readonly Color[] px;
        public float x, y; // 지금 칠하는 픽셀 중심

        public Painter(int size)
        {
            this.size = size;
            px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(1f, 1f, 1f, 0f);
        }

        /// <summary>거리 함수 sdf 안쪽을 color 로 덮어 칠한다.</summary>
        public void Fill(Sdf sdf, Color color)
        {
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    x = i + 0.5f;
                    y = j + 0.5f;
                    float a = Mathf.Clamp01(0.5f - sdf(this)) * color.a;
                    if (a <= 0f)
                        continue;
                    Color d = px[j * size + i];
                    float outA = a + d.a * (1f - a);
                    Color rgb = (color * a + d * d.a * (1f - a)) / Mathf.Max(outA, 1e-5f);
                    rgb.a = outA;
                    px[j * size + i] = rgb;
                }
        }

        /// <summary>거리 함수 sdf 안쪽을 투명하게 지운다.</summary>
        public void Erase(Sdf sdf)
        {
            for (int j = 0; j < size; j++)
                for (int i = 0; i < size; i++)
                {
                    x = i + 0.5f;
                    y = j + 0.5f;
                    float a = Mathf.Clamp01(0.5f - sdf(this));
                    px[j * size + i].a *= 1f - a;
                }
        }

        public float CircleD(float cx, float cy, float r) => new Vector2(x - cx, y - cy).magnitude - r;

        public float SegmentD(float ax, float ay, float bx, float by, float w)
        {
            Vector2 p = new Vector2(x - ax, y - ay), ab = new Vector2(bx - ax, by - ay);
            float t = Mathf.Clamp01(Vector2.Dot(p, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
            return (p - ab * t).magnitude - w * 0.5f;
        }

        public float BoxD(float cx, float cy, float hw, float hh, float r, float angle)
        {
            float px0 = x - cx, py0 = y - cy;
            if (angle != 0f)
            {
                float c = Mathf.Cos(-angle * Mathf.Deg2Rad), s = Mathf.Sin(-angle * Mathf.Deg2Rad);
                float rx = px0 * c - py0 * s;
                py0 = px0 * s + py0 * c;
                px0 = rx;
            }
            float qx = Mathf.Abs(px0) - (hw - r), qy = Mathf.Abs(py0) - (hh - r);
            return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        /// <summary>볼록 다각형 (점 순서는 시계/반시계 아무거나)</summary>
        public float PolygonD(Vector2[] pts)
        {
            float area = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                area += a.x * b.y - b.x * a.y;
            }
            float sign = area >= 0f ? 1f : -1f;
            float d = float.MinValue;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % pts.Length];
                Vector2 e = (b - a).normalized;
                Vector2 n = new Vector2(e.y, -e.x) * sign; // 바깥쪽 법선
                d = Mathf.Max(d, Vector2.Dot(new Vector2(x, y) - a, n));
            }
            return d;
        }

        public void Circle(float cx, float cy, float r) => Fill(p => p.CircleD(cx, cy, r), Color.white);
        public void Circle(float cx, float cy, float r, Color c) => Fill(p => p.CircleD(cx, cy, r), c);
        public void Ring(float cx, float cy, float r, float w) => Fill(p => Mathf.Abs(p.CircleD(cx, cy, r)) - w * 0.5f, Color.white);
        public void Segment(float ax, float ay, float bx, float by, float w) => Fill(p => p.SegmentD(ax, ay, bx, by, w), Color.white);
        public void Polygon(Color c, params Vector2[] pts) => Fill(p => p.PolygonD(pts), c);

        public void Box(float cx, float cy, float hw, float hh, float r, float angle = 0f, bool erase = false)
        {
            if (erase) Erase(p => p.BoxD(cx, cy, hw, hh, r, angle));
            else Fill(p => p.BoxD(cx, cy, hw, hh, r, angle), Color.white);
        }

        public Sprite ToSprite(int border)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HudIcon" };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
        }
    }

    static Sprite Paint(int size, int border, System.Action<Painter> draw)
    {
        var p = new Painter(size);
        draw(p);
        return p.ToSprite(border);
    }

    static Sprite PaintGlowRing(int size, float radius, float width, float glow)
    {
        var p = new Painter(size);
        float c = size * 0.5f;
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float d = Mathf.Abs(new Vector2(i + 0.5f - c, j + 0.5f - c).magnitude - radius) - width * 0.5f;
                float core = Mathf.Clamp01(0.5f - d);
                float halo = Mathf.Exp(-Mathf.Max(d, 0f) / glow * 2.5f) * 0.55f;
                p.px[j * size + i] = new Color(1f, 1f, 1f, Mathf.Max(core, halo));
            }
        return p.ToSprite(0);
    }

    static Sprite PaintSoftDisk(int size)
    {
        var p = new Painter(size);
        float c = size * 0.5f;
        for (int j = 0; j < size; j++)
            for (int i = 0; i < size; i++)
            {
                float t = new Vector2(i + 0.5f - c, j + 0.5f - c).magnitude / c;
                float a = Mathf.Clamp01(1f - t);
                p.px[j * size + i] = new Color(1f, 1f, 1f, a * a);
            }
        return p.ToSprite(0);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        circle = pill = rounded = glowRing = softDisk = needle = bag = shirt = gear = bell = hexagon = chevron = null;
        cache.Clear();
    }
}
