using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 걷는 중 지도 위 코스 안내. 지금 갈 구간 (내 위치 → 다음 장소) 만 진하게 그리고 그 위로 방향 화살표가 흐른다.
/// 그 뒤 구간은 가늘고 옅게, 지나온 길은 지운다. 갈 때 · 올 때 길이 겹쳐도 따라갈 선은 하나뿐이다.
/// </summary>
public class CourseGuide : MonoBehaviour
{
    const float AheadWidth = 4.2f, RestWidth = 2.1f;
    const float ArrowSpacing = 13f, ArrowSpeed = 7f, ArrowSize = 3.6f;   // m, m/s, m
    const float AheadLift = 1.7f, RestLift = 1.5f, ArrowLift = 1.9f;

    static Mesh arrowMesh;
    static Material arrowMaterial;

    MapRouteLine ahead, rest;
    readonly List<Transform> arrows = new List<Transform>();
    readonly List<Vector3> aheadPoints = new List<Vector3>();
    readonly List<float> aheadDist = new List<float>();   // 앞 구간 점마다 시작에서부터 거리
    List<Vector3> path;
    List<int> legEnds;
    Color color;
    bool visible = true;

    public static CourseGuide Create(Transform parent)
    {
        var go = new GameObject("CourseGuide");
        go.transform.SetParent(parent, false);
        var g = go.AddComponent<CourseGuide>();
        g.rest = MapRouteLine.Create("Rest", go.transform);
        g.ahead = MapRouteLine.Create("Ahead", go.transform);
        return g;
    }

    /// <summary>새 코스. legEnds[i] = path 에서 i 번째 장소에 닿는 점의 번호</summary>
    public void Show(List<Vector3> path, List<int> legEnds, Color color)
    {
        this.path = path;
        this.legEnds = legEnds;
        this.color = color;
    }

    /// <summary>다음 장소 (next 번째) 와 내 위치로 선을 다시 그린다. next 가 -1 이면 다 돌았음</summary>
    public void Refresh(int next, Vector3 player)
    {
        aheadPoints.Clear();
        if (path == null || legEnds == null || next < 0 || next >= legEnds.Count)
        {
            ahead.Set(aheadPoints, color, AheadWidth, AheadLift);
            rest.Set(aheadPoints, color, RestWidth, RestLift);
            Apply();
            return;
        }
        int from = next == 0 ? 0 : legEnds[next - 1], to = legEnds[next];

        // 이번 구간에서 내 위치와 가장 가까운 곳부터 (지나온 길은 지운다)
        int bestSeg = from;
        Vector3 bestPoint = path[from];
        float bestD = float.MaxValue;
        for (int i = from; i < to; i++)
        {
            Vector3 p = Closest(path[i], path[i + 1], player);
            float d = Flat(p, player);
            if (d < bestD) { bestD = d; bestSeg = i; bestPoint = p; }
        }
        aheadPoints.Add(bestPoint);
        for (int i = bestSeg + 1; i <= to; i++)
            aheadPoints.Add(path[i]);
        ahead.Set(aheadPoints, color, AheadWidth, AheadLift);

        var later = path.GetRange(to, path.Count - to);
        Color faint = Color.Lerp(color, Color.white, 0.35f);
        faint.a = 0.55f;
        rest.Set(later, faint, RestWidth, RestLift);

        aheadDist.Clear();
        float sum = 0f;
        for (int i = 0; i < aheadPoints.Count; i++)
        {
            if (i > 0) sum += Flat(aheadPoints[i - 1], aheadPoints[i]);
            aheadDist.Add(sum);
        }
        Apply();
    }

    public void SetVisible(bool on)
    {
        if (visible == on)
            return;
        visible = on;
        Apply();
    }

    void Apply()
    {
        ahead.SetVisible(visible);
        rest.SetVisible(visible);
        if (!visible)
            foreach (Transform a in arrows) a.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!visible)
            return;
        float length = aheadDist.Count > 0 ? aheadDist[aheadDist.Count - 1] : 0f;
        int count = length > ArrowSize ? Mathf.CeilToInt(length / ArrowSpacing) : 0;
        while (arrows.Count < count)
            arrows.Add(NewArrow());
        float shift = Time.time * ArrowSpeed % ArrowSpacing;
        int seg = 0;
        for (int k = 0; k < arrows.Count; k++)
        {
            float d = k * ArrowSpacing + shift;
            bool on = k < count && d < length - ArrowSize * 0.5f;
            arrows[k].gameObject.SetActive(on);
            if (!on) continue;
            while (seg < aheadDist.Count - 2 && aheadDist[seg + 1] < d) seg++;
            Vector3 a = aheadPoints[seg], b = aheadPoints[seg + 1];
            float span = aheadDist[seg + 1] - aheadDist[seg];
            Vector3 p = span > 0.001f ? Vector3.Lerp(a, b, (d - aheadDist[seg]) / span) : a;
            p.y = KeyArtTerrain.HeightAt(p.x, p.z) + ArrowLift;
            Vector3 dir = b - a;
            dir.y = 0f;
            arrows[k].SetPositionAndRotation(p, Quaternion.LookRotation(dir.sqrMagnitude > 0.0001f ? dir : Vector3.forward, Vector3.up));
        }
    }

    Transform NewArrow()
    {
        if (arrowMesh == null)
        {
            arrowMesh = ChevronMesh();
            arrowMaterial = new Material(Shader.Find("Sprites/Default")) { name = "CourseArrow", color = Color.white };
        }
        var go = new GameObject("Arrow", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.transform.localScale = Vector3.one * ArrowSize;
        go.GetComponent<MeshFilter>().sharedMesh = arrowMesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = arrowMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    /// <summary>땅에 눕힌 '>' 모양 (앞 = +z, 크기 1)</summary>
    static Mesh ChevronMesh()
    {
        const float w = 0.5f, t = 0.32f;   // 반폭, 두께
        var v = new[]
        {
            new Vector3(-w, 0f, -0.35f), new Vector3(0f, 0f, 0.35f), new Vector3(w, 0f, -0.35f),
            new Vector3(w, 0f, -0.35f - t), new Vector3(0f, 0f, 0.35f - t), new Vector3(-w, 0f, -0.35f - t),
        };
        var m = new Mesh { name = "Chevron", vertices = v };
        m.triangles = new[] { 0, 1, 4, 0, 4, 5, 1, 2, 3, 1, 3, 4 };
        m.RecalculateBounds();
        return m;
    }

    static Vector3 Closest(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector2 ab = new Vector2(b.x - a.x, b.z - a.z), ap = new Vector2(p.x - a.x, p.z - a.z);
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Lerp(a, b, t);
    }

    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
}
