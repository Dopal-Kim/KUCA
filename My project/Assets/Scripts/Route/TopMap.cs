using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 위에서 내려다본 캠퍼스 지도 한 장 + 그 위에 그리는 선 · 표시 (코스 고르기 · 장소 팝업 · 산책 돌아보기 · 산책 기록).
/// 카메라는 하나를 돌려 쓰고, 찍을 때만 켠다.
/// </summary>
public static class TopMap
{
    static Camera miniCam;

    /// <summary>
    /// points 가 모두 들어오게 지도를 위에서 한 장 찍어 target 에 띄운다 (방향은 지금 화면과 같게). 찍은 카메라를 돌려준다
    /// (선 · 표시를 같은 자리에 그리려고). 카메라는 찍고 나면 꺼 둔다.
    /// </summary>
    public static Camera Render(RawImage target, ref RenderTexture texture, List<Vector3> points)
    {
        Camera main = Camera.main;
        if (main == null || points.Count == 0)
            return null;
        Vector2 size = ((RectTransform)target.transform).rect.size;
        float aspect = size.x / Mathf.Max(1f, size.y);
        Quaternion rot = Quaternion.Euler(90f, main.transform.eulerAngles.y, 0f);
        Vector3 right = rot * Vector3.right, up = rot * Vector3.up;
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (Vector3 p in points)
        {
            float x = Vector3.Dot(p, right), y = Vector3.Dot(p, up);
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        Vector3 center = right * ((minX + maxX) * 0.5f) + up * ((minY + maxY) * 0.5f);
        center.y = 0f;
        float half = Mathf.Max((maxY - minY) * 0.5f, (maxX - minX) * 0.5f / aspect) * 1.18f + 40f;

        if (miniCam == null)
        {
            miniCam = new GameObject("TopMapCamera").AddComponent<Camera>();
            Object.DontDestroyOnLoad(miniCam.gameObject);
            miniCam.enabled = false;
            miniCam.orthographic = true;
            miniCam.clearFlags = CameraClearFlags.SolidColor;
            miniCam.backgroundColor = new Color(0.55f, 0.72f, 0.42f);
        }
        int w = 1024, h = Mathf.Max(64, Mathf.RoundToInt(w / aspect));
        if (texture == null || texture.width != w || texture.height != h)
        {
            if (texture != null) texture.Release();
            texture = new RenderTexture(w, h, 24) { name = "RouteMap", antiAliasing = 2 };
        }
        miniCam.cullingMask = main.cullingMask;
        miniCam.orthographicSize = half;
        miniCam.aspect = aspect;
        miniCam.transform.SetPositionAndRotation(center + Vector3.up * 800f, rot);
        miniCam.nearClipPlane = 1f;
        miniCam.farClipPlane = 2000f;
        miniCam.targetTexture = texture;
        miniCam.Render();
        miniCam.targetTexture = null;
        target.texture = texture;
        return miniCam;
    }

    public static Vector2 ToOverlay(Camera cam, Vector2 size, Vector3 world)
    {
        Vector3 vp = cam.WorldToViewportPoint(world);
        return new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y);
    }

    const float ArrowGap = 120f;   // 미니맵 방향 화살표 간격 (px)

    /// <summary>길을 따라 선을 긋는다. arrows 면 선 위에 진행 방향 화살표를 일정 간격으로 얹는다</summary>
    public static void Polyline(RectTransform overlay, Camera cam, Vector2 size, List<Vector3> path, float width, Color color, bool arrows = false)
    {
        if (path.Count < 2)
            return;
        var marks = new List<(Vector2 pos, float angle)>();
        float walked = ArrowGap * 0.5f;
        Vector2 prev = ToOverlay(cam, size, path[0]);
        for (int i = 1; i < path.Count; i++)
        {
            Vector2 p = ToOverlay(cam, size, path[i]);
            if ((p - prev).sqrMagnitude < 9f && i < path.Count - 1)
                continue; // 너무 짧은 조각은 건너뛴다 (선 개수 줄이기)
            Line(overlay, prev, p, width, color);
            if (arrows)
            {
                // 이 조각 위에 놓일 화살표 자리
                float len = (p - prev).magnitude, angle = Mathf.Atan2(p.y - prev.y, p.x - prev.x) * Mathf.Rad2Deg;
                for (float d = walked; d < len; d += ArrowGap)
                    marks.Add((Vector2.Lerp(prev, p, d / len), angle));
                walked = Mathf.Repeat(walked - len, ArrowGap);
            }
            Image joint = UIKit.Image(overlay, "Joint", color);   // 꺾이는 곳을 둥글게
            joint.sprite = HudIcons.Circle;
            joint.rectTransform.anchorMin = joint.rectTransform.anchorMax = joint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            joint.rectTransform.sizeDelta = new Vector2(width, width);
            joint.rectTransform.anchoredPosition = p;
            prev = p;
        }
        foreach ((Vector2 pos, float angle) in marks)   // 선을 다 그은 뒤 위에
        {
            Image a = UIKit.Image(overlay, "Arrow", Color.white);
            a.sprite = ChevronSprite;
            a.rectTransform.anchorMin = a.rectTransform.anchorMax = a.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            a.rectTransform.sizeDelta = new Vector2(width * 2.1f, width * 2.1f);
            a.rectTransform.anchoredPosition = pos;
            a.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    static Sprite chevron;

    /// <summary>오른쪽을 가리키는 흰 '>' (미니맵 화살표)</summary>
    public static Sprite ChevronSprite
    {
        get
        {
            if (chevron != null)
                return chevron;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Chevron", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n, v = Mathf.Abs((y + 0.5f) / n - 0.5f) * 2f;   // v: 가운데 0 → 위아래 1
                    float edge = 0.78f - v * 0.42f;                                        // '>' 의 바깥 변
                    float d = Mathf.Min(edge - u, u - (edge - 0.3f));                      // 두께 0.3 안쪽이면 양수
                    float alpha = v < 0.9f ? Mathf.Clamp01(d * n * 0.5f + 0.5f) : 0f;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            chevron = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return chevron;
        }
    }

    public static void Marker(RectTransform overlay, Camera cam, Vector2 size, Vector3 world, Color color, string label, float diameter)
    {
        Image c = Circle(overlay, color, diameter, label);
        c.rectTransform.anchoredPosition = ToOverlay(cam, size, world);
        if (label.Length > 1)
            c.GetComponentInChildren<Text>().fontSize = Mathf.RoundToInt(diameter * 0.32f);
        Outline o = c.gameObject.AddComponent<Outline>();
        o.effectColor = Color.white;
        o.effectDistance = new Vector2(3f, -3f);
    }

    static void Line(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
    {
        Image seg = UIKit.Image(parent, "Line", color);
        RectTransform r = seg.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        Vector2 d = b - a;
        r.anchoredPosition = (a + b) * 0.5f;
        r.sizeDelta = new Vector2(d.magnitude, width);
        r.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    /// <summary>overlay 의 자식을 모두 지운다</summary>
    public static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.Destroy(parent.GetChild(i).gameObject);
    }

    static Image Circle(Transform parent, Color color, float size, string label)
    {
        Image c = UIKit.Image(parent, "Badge", color);
        c.sprite = HudIcons.Circle;
        c.rectTransform.anchorMin = c.rectTransform.anchorMax = c.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        c.rectTransform.sizeDelta = new Vector2(size, size);
        Text t = UIKit.Text(c.transform, label, Mathf.RoundToInt(size * 0.48f), TextAnchor.MiddleCenter, Color.white);
        t.fontStyle = FontStyle.Bold;
        UIKit.Stretch(t.rectTransform);
        return c;
    }

    /// <summary>지도 칸 (둥근 틀 + 지도 그림 + 선을 그릴 칸). 지도는 Render 로, 선은 overlay 에</summary>
    public static Image Frame(Transform parent, string name, out RawImage map, out RectTransform overlay)
    {
        Image frame = UIKit.Image(parent, name, new Color(0.55f, 0.72f, 0.42f));
        frame.sprite = HudIcons.Rounded;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1f;
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var mm = new GameObject("Texture", typeof(RectTransform), typeof(RawImage));
        mm.transform.SetParent(frame.transform, false);
        map = mm.GetComponent<RawImage>();
        map.raycastTarget = false;
        UIKit.Stretch(map.rectTransform);
        overlay = UIKit.Rect(frame.transform, "Lines");
        UIKit.Stretch(overlay);
        return frame;
    }
}
