using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 컨셉아트(Art/Concept/01_Buildings, 02_Landscapes)를 따라 캠퍼스 건물마다 특징을 살린다.
/// - 외벽 재질: 신고전주의(크림 석조, 좁고 긴 창), 현대(흰 벽, 가로 띠 창), 유리, 붉은 벽돌
/// - 덧붙이는 형태: 열주 현관(포르티코), 시계탑, 천문대 돔, 노천극장 계단석, 정문 열주, 박공지붕, 오벨리스크
/// 건물 발자국의 최소 사각형으로 방향을 잡고, 정면은 지정한 목표(사색의 광장, 연못 등)를 향한다.
/// KeyArtMapSetup.ApplyKeyArt 에서 부른다.
/// </summary>
public static class KeyArtLandmarks
{
    const string Dir = "Assets/Art/KeyArt";
    const string MeshAsset = Dir + "/KeyArtLandmarks.asset";
    public const string RootName = "KeyArtLandmarks";

    // 사색의 광장(OSM place=square) 중심, 노천극장 옆 연못 중심 (월드 m)
    static readonly Vector2 PlazaCenter = new Vector2(94f, -352f);
    static readonly Vector2 TheaterPond = new Vector2(327f, -594f);

    enum Wall { Default, Classical, Modern, Glass, Brick }

    static readonly Dictionary<string, Wall> WallStyles = new Dictionary<string, Wall>
    {
        { "way-474085534", Wall.Classical },  // 중앙도서관
        { "way-474085536", Wall.Classical },  // 예술디자인대학
        { "way-474521123", Wall.Classical },  // 예술디자인대학 도예관
        { "way-455726113", Wall.Classical },  // 체육대학관
        { "way-585696506", Wall.Classical },  // 선승관
        { "way-474521125", Wall.Classical },  // 천문대
        { "way-455725718", Wall.Modern },     // 공학관
        { "way-455728922", Wall.Modern },     // 공학실험동
        { "way-474085540", Wall.Modern },     // 전자정보대학
        { "way-474085537", Wall.Glass },      // 멀티미디어교육관
        { "way-474085538", Wall.Glass },      // 글로벌관
        { "relation-8269760", Wall.Brick },   // 우정원
    };

    static readonly Color Stone = new Color(0.93f, 0.90f, 0.82f);
    static readonly Color Column = new Color(0.97f, 0.95f, 0.89f);
    static readonly Color Step = new Color(0.86f, 0.84f, 0.78f);
    static readonly Color Roof = new Color(0.70f, 0.72f, 0.75f);
    static readonly Color DomeWhite = new Color(0.95f, 0.96f, 0.98f);
    static readonly Color Dark = new Color(0.25f, 0.30f, 0.38f);

    /// <summary>건물 외벽 재질을 스타일별로 만들어 입히고, 덧붙이는 형태를 만든다.</summary>
    public static void Build(Material baseBuilding, Material vertexColor)
    {
        Clear();
        var buildings = new Dictionary<string, List<GameObject>>();
        foreach (CampusBuildingInfo info in GameObject.Find("Buildings").GetComponentsInChildren<CampusBuildingInfo>())
        {
            if (!buildings.TryGetValue(info.buildingId, out var list)) buildings[info.buildingId] = list = new List<GameObject>();
            list.Add(info.gameObject);
        }

        // 외벽 재질
        var mats = new Dictionary<Wall, Material>
        {
            { Wall.Classical, Variant(baseBuilding, "Classical", new Color(0.95f, 0.91f, 0.82f), new Color(0.36f, 0.46f, 0.56f), 3.6f, 3.4f, 0.32f, 0.62f) },
            { Wall.Modern, Variant(baseBuilding, "Modern", new Color(0.96f, 0.96f, 0.95f), new Color(0.42f, 0.62f, 0.80f), 3.4f, 3.0f, 0.97f, 0.42f) },
            { Wall.Glass, Variant(baseBuilding, "Glass", new Color(0.88f, 0.90f, 0.93f), new Color(0.38f, 0.58f, 0.76f), 3.4f, 2.4f, 0.9f, 0.86f) },
            { Wall.Brick, Variant(baseBuilding, "Brick", new Color(0.72f, 0.38f, 0.27f), new Color(0.88f, 0.86f, 0.80f), 3.2f, 2.8f, 0.5f, 0.5f) },
        };
        foreach (var kv in WallStyles)
            if (buildings.TryGetValue(kv.Key, out var gos))
                foreach (GameObject go in gos)
                {
                    var r = go.GetComponent<Renderer>();
                    Undo.RecordObject(r, "Wall style");
                    r.sharedMaterial = mats[kv.Value];
                }

        // 덧붙이는 형태
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Landmarks");
        AssetDatabase.DeleteAsset(MeshAsset);
        var container = ScriptableObject.CreateInstance<CampusBuildingMeshes>();
        AssetDatabase.CreateAsset(container, MeshAsset);

        void Make(string id, string name, System.Action<LowPolyMeshBuilder, Footprint> build, bool hideBuilding = false)
        {
            if (!buildings.TryGetValue(id, out var gos))
            {
                Debug.LogWarning($"[KeyArtLandmarks] 건물을 찾지 못했습니다: {id}");
                return;
            }
            var fp = Footprint.From(gos[0]);
            var mb = new LowPolyMeshBuilder();
            build(mb, fp);
            AddMesh(root, container, mb, vertexColor, name);
            if (hideBuilding)
                foreach (GameObject go in gos)
                {
                    var r = go.GetComponent<Renderer>();
                    Undo.RecordObject(r, "Hide building");
                    r.enabled = false;   // 충돌체와 건물 정보는 남겨 둔다 (경희스팟, 탭)
                }
        }

        Make("way-474085534", "중앙도서관_포르티코", (mb, fp) => Portico(mb, fp.Face(PlazaCenter), fp.Height, true, 8));
        Make("way-474085536", "예술디자인대학_포르티코", (mb, fp) => Portico(mb, fp.Face(PlazaCenter), fp.Height, true, 6));
        Make("way-455726113", "체육대학관_열주", (mb, fp) => Portico(mb, fp.Face(PlazaCenter), fp.Height, false, 8));
        Make("way-585696506", "선승관_시계탑", (mb, fp) =>
        {
            Face f = fp.Face(PlazaCenter);
            Portico(mb, f, fp.Height, false, 6);
            ClockTower(mb, f, fp.Height);
        });
        Make("way-474521125", "천문대_돔", (mb, fp) => Observatory(mb, fp));
        Make("way-474521123", "도예관_박공지붕", (mb, fp) => PitchedRoof(mb, fp));
        Make("way-474531805", "평화노천극장", (mb, fp) => Amphitheater(mb, fp, fp.Face(TheaterPond)), hideBuilding: true);
        Make("way-473963422", "정문_네오르네상스문", (mb, fp) => Gate(mb, fp), hideBuilding: true);

        // 사색의 광장 오벨리스크 2기: 광장 중심에서 중앙도서관 쪽으로, 축 양옆에
        var plaza = new LowPolyMeshBuilder();
        Vector2 toLibrary = (new Vector2(-40f, -352f) - PlazaCenter).normalized;
        Vector2 side = new Vector2(-toLibrary.y, toLibrary.x);
        foreach (int s in new[] { -1, 1 })
        {
            Vector2 p = PlazaCenter + toLibrary * 30f + side * 13f * s;
            Obelisk(plaza, new Vector3(p.x, 0f, p.y));
        }
        AddMesh(root, container, plaza, vertexColor, "사색의광장_오벨리스크");

        AssetDatabase.SaveAssets();
    }

    public static void Clear()
    {
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var b = GameObject.Find("Buildings");
        if (b != null)
            foreach (Renderer r in b.GetComponentsInChildren<Renderer>(true))
                if (!r.enabled)
                {
                    Undo.RecordObject(r, "Show building");
                    r.enabled = true;
                }
    }

    static void AddMesh(GameObject root, ScriptableObject container, LowPolyMeshBuilder mb, Material mat, string name)
    {
        Mesh mesh = mb.ToMesh(name);
        AssetDatabase.AddObjectToAsset(mesh, container);
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.isStatic = true;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.On;
    }

    static Material Variant(Material baseMat, string name, Color wall, Color window, float floorH, float spacing, float winW, float winH)
    {
        string path = $"{Dir}/KeyArtBuilding_{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(baseMat);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_WallColor", wall);
        mat.SetColor("_WindowColor", window);
        mat.SetFloat("_FloorHeight", floorH);
        mat.SetFloat("_WindowSpacing", spacing);
        mat.SetFloat("_WindowWidth", winW);
        mat.SetFloat("_WindowHeight", winH);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ---------- 발자국 ----------

    /// <summary>건물 한 면: 면 가운데 점, 바깥 방향, 면을 따라가는 방향, 면 길이</summary>
    struct Face
    {
        public Vector3 center, normal, tangent;
        public float length, depth;
    }

    /// <summary>건물 바닥의 최소 넓이 사각형 (방향 있는 상자)</summary>
    class Footprint
    {
        public Vector2 center, axisU, axisV;
        public float halfU, halfV, Height;

        public static Footprint From(GameObject building)
        {
            Mesh mesh = building.GetComponent<MeshFilter>().sharedMesh;
            var pts = new List<Vector2>();
            foreach (Vector3 p in mesh.vertices)
            {
                Vector3 w = building.transform.TransformPoint(p);
                pts.Add(new Vector2(w.x, w.z));
            }
            List<Vector2> hull = Hull(pts);
            var fp = new Footprint { Height = building.GetComponent<Renderer>().bounds.max.y };
            float best = float.MaxValue;
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 e = (hull[(i + 1) % hull.Count] - hull[i]).normalized;
                if (e.sqrMagnitude < 1e-6f) continue;
                Vector2 n = new Vector2(-e.y, e.x);
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (Vector2 p in hull)
                {
                    float u = Vector2.Dot(p, e), v = Vector2.Dot(p, n);
                    minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u);
                    minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
                }
                float area = (maxU - minU) * (maxV - minV);
                if (area < best)
                {
                    best = area;
                    fp.axisU = e; fp.axisV = n;
                    fp.halfU = (maxU - minU) / 2; fp.halfV = (maxV - minV) / 2;
                    fp.center = e * (minU + maxU) / 2 + n * (minV + maxV) / 2;
                }
            }
            return fp;
        }

        /// <summary>네 면 중 target 쪽을 바라보는 면</summary>
        public Face Face(Vector2 target)
        {
            Vector2 dir = (target - center).normalized;
            float du = Vector2.Dot(dir, axisU), dv = Vector2.Dot(dir, axisV);
            Vector2 n, t; float half, other;
            if (Mathf.Abs(du) > Mathf.Abs(dv)) { n = axisU * Mathf.Sign(du); t = axisV; half = halfU; other = halfV; }
            else { n = axisV * Mathf.Sign(dv); t = axisU; half = halfV; other = halfU; }
            Vector2 c = center + n * half;
            return new Face
            {
                center = new Vector3(c.x, 0f, c.y),
                normal = new Vector3(n.x, 0f, n.y),
                tangent = new Vector3(t.x, 0f, t.y),
                length = other * 2f,
                depth = half * 2f,
            };
        }

        public Matrix4x4 Frame(Vector3 origin, Vector3 forward) =>
            Matrix4x4.TRS(origin, Quaternion.LookRotation(forward, Vector3.up), Vector3.one);

        static List<Vector2> Hull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            foreach (Vector2 p in pts) { while (h.Count >= 2 && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1); h.Add(p); }
            int lower = h.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--) { Vector2 p = pts[i]; while (h.Count >= lower && Cross(h[h.Count - 2], h[h.Count - 1], p) <= 0) h.RemoveAt(h.Count - 1); h.Add(p); }
            h.RemoveAt(h.Count - 1);
            return h;
        }
    }

    // ---------- 형태 ----------

    /// <summary>정면 열주 현관: 기단, 기둥, 엔태블러처, (페디먼트), 앞 계단</summary>
    static void Portico(LowPolyMeshBuilder mb, Face f, float height, bool pediment, int maxColumns)
    {
        // 로컬: X = 면을 따라, Y = 위, Z = 바깥쪽
        Matrix4x4 m = Matrix4x4.TRS(f.center, Quaternion.LookRotation(f.normal, Vector3.up), Vector3.one);
        float width = Mathf.Min(f.length * 0.5f, 44f);
        int cols = Mathf.Clamp(Mathf.RoundToInt(width / 5f) + 1, 4, maxColumns);
        float depth = 7f, baseH = 1.4f;
        float colTop = Mathf.Clamp(height - 2.2f, 7f, 16f);

        mb.Box(m, new Vector3(0f, baseH / 2f, depth / 2f - 0.5f), new Vector3(width + 2f, baseH, depth + 1f), Step);
        for (int i = 0; i < cols; i++)
        {
            float x = Mathf.Lerp(-width / 2f + 1.4f, width / 2f - 1.4f, cols == 1 ? 0.5f : (float)i / (cols - 1));
            mb.Prism(m, new Vector3(x, baseH, depth - 1.6f), 0.85f, colTop - baseH, 8, Column);
            mb.Box(m, new Vector3(x, baseH + 0.25f, depth - 1.6f), new Vector3(2.2f, 0.5f, 2.2f), Stone);
        }
        mb.Box(m, new Vector3(0f, colTop + 1.1f, depth / 2f - 0.5f), new Vector3(width + 1.2f, 2.2f, depth + 1f), Stone);
        if (pediment)
            mb.Gable(m, new Vector3(0f, colTop + 2.2f, depth / 2f - 0.5f), width + 1.2f, depth + 1f, 4.6f, Stone, ridgeAlongZ: true);

        // 앞 계단 (바깥으로 갈수록 낮아짐)
        for (int i = 0; i < 4; i++)
        {
            float h = baseH * (4 - i) / 5f;
            mb.Box(m, new Vector3(0f, h / 2f, depth + 0.45f + 0.9f * i), new Vector3(width + 4f + i * 1.5f, h, 0.9f), Step);
        }
    }

    /// <summary>시계탑: 정면 한쪽 끝에 높은 사각 탑, 네 면에 시계, 사각뿔 지붕</summary>
    static void ClockTower(LowPolyMeshBuilder mb, Face f, float height)
    {
        Vector3 pos = f.center + f.tangent * (f.length / 2f - 6f) - f.normal * 4f;
        Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.LookRotation(f.normal, Vector3.up), Vector3.one);
        float towerH = height + 18f, w = 8f;
        mb.Box(m, new Vector3(0f, towerH / 2f, 0f), new Vector3(w, towerH, w), Stone);
        mb.Box(m, new Vector3(0f, towerH + 0.4f, 0f), new Vector3(w + 1.2f, 0.8f, w + 1.2f), Step);
        mb.Cone(m, new Vector3(0f, towerH + 0.8f, 0f), (w + 1.2f) * 0.72f, 6f, 4, Roof, 45f);
        float clockY = towerH - 4f;
        for (int k = 0; k < 4; k++)
        {
            Matrix4x4 side = m * Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 90f * k, 0f), Vector3.one);
            mb.Disc(side, new Vector3(0f, clockY, w / 2f + 0.15f), 2.6f, 0.15f, 16, new Color(0.98f, 0.97f, 0.93f));
            mb.Box(side, new Vector3(0f, clockY + 0.8f, w / 2f + 0.3f), new Vector3(0.25f, 1.7f, 0.1f), Dark);
            mb.Box(side, new Vector3(0.55f, clockY, w / 2f + 0.3f), new Vector3(1.2f, 0.22f, 0.1f), Dark);
        }
    }

    /// <summary>천문대: 지붕 위 원통 받침과 흰 반구 돔, 관측 창</summary>
    static void Observatory(LowPolyMeshBuilder mb, Footprint fp)
    {
        float r = Mathf.Clamp(Mathf.Min(fp.halfU, fp.halfV) * 0.6f, 4f, 9f);
        Vector2 c = fp.center;
        var baseC = new Vector3(c.x, fp.Height, c.y);
        Matrix4x4 m = Matrix4x4.identity;
        mb.Prism(m, baseC, r + 0.6f, 0.8f, 16, Step, caps: true);
        mb.Prism(m, baseC + Vector3.up * 0.8f, r, 3f, 16, Stone);
        mb.Hemisphere(m, baseC + Vector3.up * 3.8f, r, 16, 5, DomeWhite);
        // 관측 창: 돔 앞쪽(남쪽, 기본 카메라 쪽)에 세로 띠
        Matrix4x4 slit = Matrix4x4.TRS(baseC + Vector3.up * 3.8f, Quaternion.Euler(-35f, 180f, 0f), Vector3.one);
        mb.Box(slit, new Vector3(0f, r * 0.55f, r * 0.82f), new Vector3(1.4f, r * 0.9f, 0.6f), Dark);
    }

    /// <summary>도예관: 건물 위에 긴 방향으로 박공지붕</summary>
    static void PitchedRoof(LowPolyMeshBuilder mb, Footprint fp)
    {
        bool longU = fp.halfU >= fp.halfV;
        Vector2 along = longU ? fp.axisU : fp.axisV;
        Matrix4x4 m = Matrix4x4.TRS(new Vector3(fp.center.x, fp.Height, fp.center.y),
            Quaternion.LookRotation(new Vector3(along.x, 0f, along.y), Vector3.up), Vector3.one);
        float len = (longU ? fp.halfU : fp.halfV) * 2f + 1.2f, wid = (longU ? fp.halfV : fp.halfU) * 2f + 1.2f;
        mb.Gable(m, Vector3.zero, wid, len, Mathf.Clamp(wid * 0.28f, 3f, 6f), Roof, ridgeAlongZ: true);
    }

    /// <summary>노천극장: 연못 쪽에 무대와 배경 벽, 반대쪽으로 반원 계단석</summary>
    static void Amphitheater(LowPolyMeshBuilder mb, Footprint fp, Face toPond)
    {
        // 무대 중심: 연못 쪽 면에서 조금 안쪽
        Vector3 stage = toPond.center - toPond.normal * Mathf.Min(14f, toPond.depth * 0.3f);
        Matrix4x4 m = Matrix4x4.TRS(stage, Quaternion.LookRotation(toPond.normal, Vector3.up), Vector3.one);
        // 계단석: 로컬 -Z(연못 반대쪽)를 중심으로 ±80도
        float maxR = Mathf.Min(fp.halfU, fp.halfV) * 1.6f;
        int tiers = Mathf.Clamp(Mathf.FloorToInt((maxR - 12f) / 3f), 4, 9);
        float center = -Mathf.PI / 2f, spread = 80f * Mathf.Deg2Rad;
        for (int i = 0; i < tiers; i++)
        {
            float r0 = 12f + i * 3f;
            Color col = i % 2 == 0 ? Stone : Step;
            mb.ArcTier(m, Vector3.zero, r0, r0 + 3f, center - spread, center + spread, 0.6f * (i + 1), 20, col);
        }
        // 무대와 배경 벽, 양옆 기둥
        mb.Box(m, new Vector3(0f, 0.6f, 2f), new Vector3(18f, 1.2f, 10f), Step);
        mb.Box(m, new Vector3(0f, 3.8f, 7.6f), new Vector3(22f, 7.6f, 1.4f), Stone);
        mb.Box(m, new Vector3(0f, 7.9f, 7.6f), new Vector3(23.5f, 0.8f, 2.2f), Step);
        foreach (int s in new[] { -1, 1 })
            mb.Prism(m, new Vector3(s * 10.2f, 0f, 6.2f), 0.7f, 7.2f, 8, Column);
    }

    /// <summary>정문: 길을 가로지르는 석조 열주 문 (새천년기념탑)</summary>
    static void Gate(LowPolyMeshBuilder mb, Footprint fp)
    {
        bool longU = fp.halfU >= fp.halfV;
        Vector2 along = longU ? fp.axisU : fp.axisV;
        float len = (longU ? fp.halfU : fp.halfV) * 2f, thick = Mathf.Max((longU ? fp.halfV : fp.halfU) * 2f, 5f);
        Matrix4x4 m = Matrix4x4.TRS(new Vector3(fp.center.x, 0f, fp.center.y),
            Quaternion.LookRotation(new Vector3(along.x, 0f, along.y), Vector3.up) * Quaternion.Euler(0f, 90f, 0f), Vector3.one);
        // 로컬 X = 문의 긴 방향
        const int Pillars = 6;
        float pillarH = 13f;
        for (int i = 0; i < Pillars; i++)
        {
            float x = Mathf.Lerp(-len / 2f + 1.5f, len / 2f - 1.5f, (float)i / (Pillars - 1));
            mb.Box(m, new Vector3(x, 0.6f, 0f), new Vector3(3.6f, 1.2f, thick + 0.8f), Step);
            mb.Prism(m, new Vector3(x, 1.2f, -thick / 4f), 0.9f, pillarH - 1.2f, 8, Column);
            mb.Prism(m, new Vector3(x, 1.2f, thick / 4f), 0.9f, pillarH - 1.2f, 8, Column);
        }
        mb.Box(m, new Vector3(0f, pillarH + 1.3f, 0f), new Vector3(len + 1.5f, 2.6f, thick + 1f), Stone);
        mb.Box(m, new Vector3(0f, pillarH + 3.9f, 0f), new Vector3(len * 0.42f, 2.6f, thick), Stone);
        mb.Box(m, new Vector3(0f, pillarH + 5.4f, 0f), new Vector3(len * 0.46f, 0.5f, thick + 0.6f), Step);
    }

    /// <summary>오벨리스크: 받침, 위로 좁아지는 몸통, 꼭대기 사각뿔</summary>
    static void Obelisk(LowPolyMeshBuilder mb, Vector3 pos)
    {
        Matrix4x4 m = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
        mb.Box(m, new Vector3(0f, 1.1f, 0f), new Vector3(7f, 2.2f, 7f), Step);
        mb.Box(m, new Vector3(0f, 2.8f, 0f), new Vector3(5.2f, 1.2f, 5.2f), Stone);
        mb.Frustum4(m, new Vector3(0f, 3.4f, 0f), 2.1f, 1.3f, 24f, Stone);
        mb.Cone(m, new Vector3(0f, 27.4f, 0f), 1.3f * 1.414f, 3.6f, 4, Column, 45f);
    }
}
