using UnityEngine;

/// <summary>
/// 인스타그램 스토리에 붙일 사진 스티커 PNG 를 만든다. 메모판 콜라주처럼
/// 흰 종이 테두리(가장자리가 오톨도톨) + 위쪽 마스킹 테이프 + 살짝 기울기 + 부드러운 그림자, 바깥은 투명.
/// 기울기·테이프 위치는 seed(메모 id)로 정해서 같은 사진은 늘 같은 모양이다.
/// </summary>
public static class StorySticker
{
    const int MaxPhotoSide = 720;

    public static byte[] Make(Texture source, Rect uv, int seed)
    {
        Color32[] photo = ReadCrop(source, uv, out int pw, out int ph);
        var rng = new System.Random(seed);
        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        float border = Mathf.Round(Mathf.Max(pw, ph) * 0.055f);
        float hx = pw * 0.5f + border, hy = ph * 0.5f + border;
        float angle = Range(3f, 6f) * (rng.Next(2) == 0 ? -1f : 1f) * Mathf.Deg2Rad;
        float tapeW = hx * 2f * 0.36f, tapeH = Mathf.Max(28f, hx * 2f * 0.1f);
        var tapeCenter = new Vector2(Range(-0.12f, 0.12f) * hx, hy - tapeH * 0.12f);
        float tapeAngle = Range(-5f, 5f) * Mathf.Deg2Rad;
        var shadowOffset = new Vector2(10f, -16f);
        const float blur = 20f, margin = 48f;

        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        float ex = Mathf.Abs(cos) * hx + Mathf.Abs(sin) * (hy + tapeH);
        float ey = Mathf.Abs(sin) * hx + Mathf.Abs(cos) * (hy + tapeH);
        int w = Mathf.CeilToInt(ex * 2f + margin * 2f), h = Mathf.CeilToInt(ey * 2f + margin * 2f);
        var outPixels = new Color32[w * h];
        float tc = Mathf.Cos(-tapeAngle), ts = Mathf.Sin(-tapeAngle);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x - w * 0.5f + 0.5f, y - h * 0.5f + 0.5f);
                Vector2 local = Rotate(p, cos, -sin);
                Vector2 shadowLocal = Rotate(p - shadowOffset, cos, -sin);

                // 그림자 → 종이 → 테이프 순서로 겹친다 (straight alpha)
                float sa = 0.3f * Smooth(blur, -blur * 0.4f, PaperSdf(shadowLocal, hx, hy));
                Color c = new Color(0f, 0f, 0f, sa);

                float pa = Mathf.Clamp01(0.5f - PaperSdf(local, hx, hy));
                if (pa > 0f)
                {
                    Color paper = new Color(0.99f, 0.99f, 0.98f);
                    float inside = Mathf.Clamp01(0.5f - BoxSdf(local, pw * 0.5f, ph * 0.5f, 3f));
                    if (inside > 0f)
                        paper = Color.Lerp(paper, Sample(photo, pw, ph, local.x + pw * 0.5f - 0.5f, local.y + ph * 0.5f - 0.5f), inside);
                    c = Over(paper, pa, c);
                }

                Vector2 t = local - tapeCenter;
                t = new Vector2(t.x * tc - t.y * ts, t.x * ts + t.y * tc);
                float jag = 3f * Mathf.Abs(Mathf.Sin(t.y * 0.45f)); // 손으로 뜯은 테이프 끝
                float td = Mathf.Max(Mathf.Abs(t.x) - (tapeW * 0.5f - jag), Mathf.Abs(t.y) - tapeH * 0.5f);
                float ta = Mathf.Clamp01(0.5f - td) * 0.8f;
                if (ta > 0f)
                {
                    bool check = ((Mathf.FloorToInt(t.x / 12f) + Mathf.FloorToInt(t.y / 12f)) & 1) == 0;
                    Color tape = check ? new Color(0.92f, 0.84f, 0.80f) : new Color(0.98f, 0.95f, 0.91f);
                    c = Over(tape, ta, c);
                }
                outPixels[y * w + x] = c;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(outPixels);
        tex.Apply(false, false);
        byte[] png = tex.EncodeToPNG();
        Object.Destroy(tex);
        return png;
    }

    /// <summary>uv 부분을 읽을 수 있는 픽셀로 (긴 변 MaxPhotoSide 이하)</summary>
    static Color32[] ReadCrop(Texture source, Rect uv, out int pw, out int ph)
    {
        float cw = source.width * uv.width, ch = source.height * uv.height;
        float s = Mathf.Min(1f, MaxPhotoSide / Mathf.Max(cw, ch));
        pw = Mathf.Max(8, Mathf.RoundToInt(cw * s));
        ph = Mathf.Max(8, Mathf.RoundToInt(ch * s));
        RenderTexture rt = RenderTexture.GetTemporary(pw, ph, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt, uv.size, uv.position);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var read = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, pw, ph), 0, 0);
        read.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = read.GetPixels32();
        Object.Destroy(read);
        return px;
    }

    static Vector2 Rotate(Vector2 p, float cos, float sin) => new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);

    static float BoxSdf(Vector2 p, float hx, float hy, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x) - hx + r, Mathf.Abs(p.y) - hy + r);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    /// <summary>둥근 사각형에 둘레를 따라 불규칙한 오톨도톨함을 더한 종이 모양</summary>
    static float PaperSdf(Vector2 p, float hx, float hy)
    {
        float a = Mathf.Atan2(p.y / hy, p.x / hx);
        float n = Mathf.Sin(a * 61f) * 1.1f + Mathf.Sin(a * 137f + 1.3f) * 0.8f + Mathf.Sin((p.x + p.y) * 0.21f) * 0.7f;
        return BoxSdf(p, hx, hy, 8f) + n;
    }

    static float Smooth(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    static Color Over(Color src, float a, Color dst)
    {
        float outA = a + dst.a * (1f - a);
        if (outA <= 1e-5f)
            return new Color(0f, 0f, 0f, 0f);
        Color c = (src * a + dst * dst.a * (1f - a)) / outA;
        c.a = outA;
        return c;
    }

    static Color Sample(Color32[] px, int w, int h, float x, float y)
    {
        x = Mathf.Clamp(x, 0f, w - 1f);
        y = Mathf.Clamp(y, 0f, h - 1f);
        int x0 = (int)x, y0 = (int)y, x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
        float fx = x - x0, fy = y - y0;
        Color a = Color.Lerp(px[y0 * w + x0], px[y0 * w + x1], fx);
        Color b = Color.Lerp(px[y1 * w + x0], px[y1 * w + x1], fx);
        Color c = Color.Lerp(a, b, fy);
        c.a = 1f;
        return c;
    }
}
