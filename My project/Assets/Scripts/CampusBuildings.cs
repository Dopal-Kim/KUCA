using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// OpenStreetMap 건물 외곽선을 높이만큼 돌출시켜 지도 위에 3D 건물을 만든다.
/// 인스펙터 컴포넌트 메뉴(⋮)의 "Download And Generate Buildings"로 실행한다.
/// 생성된 메시는 에셋으로 저장되므로 런타임에는 추가 요청이 없다.
/// </summary>
public class CampusBuildings : MonoBehaviour
{
    [Header("References")]
    public CampusMap campusMap;
    public Material buildingMaterial;
    [Tooltip("Overpass API로 받은 OSM 건물 데이터 (자동 저장)")]
    public TextAsset osmData;

    [Header("Height")]
    [Tooltip("층당 높이 (m)")]
    public float levelHeight = 3.3f;
    [Tooltip("height / building:levels 정보가 없는 건물의 기본 높이 (m)")]
    public float defaultHeight = 10f;
    public float apartmentsHeight = 45f;
    public float dormitoryHeight = 24f;
    public float collegeHeight = 18f;
    public float residentialHeight = 15f;
    public float commercialHeight = 12f;
    public float houseHeight = 7f;

    [Header("Options")]
    [Tooltip("캠퍼스 범위 밖에 중심이 있는 건물은 만들지 않는다")]
    public bool skipOutsideBounds = true;
    public bool addColliders = true;

    #region OSM JSON (JsonUtility 호환)

    [Serializable] class OsmResponse { public OsmElement[] elements; }

    [Serializable]
    class OsmElement
    {
        public string type;
        public long id;
        public OsmTags tags;
        public OsmPoint[] geometry;
        public OsmMember[] members;
    }

    [Serializable] class OsmMember { public string type; public string role; public OsmPoint[] geometry; }
    [Serializable] class OsmPoint { public double lat; public double lon; }

    [Serializable]
    class OsmTags
    {
        public string building;
        public string height;
        public string building_levels;
        public string min_height;
        public string building_min_level;
        public string name;
    }

    #endregion

    public const string OverpassUrl = "https://overpass-api.de/api/interpreter";

    public string BuildOverpassQuery()
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        string bbox = string.Format(ci, "{0},{1},{2},{3}",
            campusMap.minLatitude, campusMap.minLongitude, campusMap.maxLatitude, campusMap.maxLongitude);
        return $"[out:json][timeout:60];(way[\"building\"]({bbox});relation[\"building\"]({bbox}););out geom;";
    }

    /// <summary>OSM JSON을 파싱해 건물별 (이름, 메시) 목록을 만든다.</summary>
    public List<(string name, string id, Mesh mesh)> BuildMeshes(string json)
    {
        // JsonUtility는 ':'가 들어간 키를 매핑할 수 없으므로 미리 바꾼다.
        json = json.Replace("\"building:levels\"", "\"building_levels\"")
                   .Replace("\"building:min_level\"", "\"building_min_level\"");
        OsmResponse resp = JsonUtility.FromJson<OsmResponse>(json);

        var result = new List<(string, string, Mesh)>();
        if (resp?.elements == null)
            return result;

        foreach (OsmElement e in resp.elements)
        {
            var outlines = new List<OsmPoint[]>();
            if (e.type == "way" && e.geometry != null)
                outlines.Add(e.geometry);
            else if (e.type == "relation" && e.members != null)
                foreach (OsmMember m in e.members)
                    if (m.role == "outer" && m.geometry != null)
                        outlines.Add(m.geometry);

            GetHeights(e.tags, out float minH, out float maxH);

            foreach (OsmPoint[] outline in outlines)
            {
                List<Vector2> poly = ToPolygon(outline, out double cLon, out double cLat);
                if (poly == null)
                    continue;
                if (skipOutsideBounds && !campusMap.IsInsideCampus(cLon, cLat))
                    continue;

                Mesh mesh = Extrude(poly, minH, maxH);
                if (mesh == null)
                    continue;

                string label = !string.IsNullOrEmpty(e.tags?.name) ? e.tags.name : $"{e.type}_{e.id}";
                mesh.name = $"Building_{e.id}";
                result.Add((label, $"{e.type}-{e.id}", mesh));
            }
        }
        return result;
    }

    void GetHeights(OsmTags t, out float minH, out float maxH)
    {
        minH = 0f;
        maxH = 0f;
        if (t != null)
        {
            if (TryParseMeters(t.height, out float h)) maxH = h;
            else if (TryParseMeters(t.building_levels, out float lv)) maxH = lv * levelHeight;

            if (TryParseMeters(t.min_height, out float mh)) minH = mh;
            else if (TryParseMeters(t.building_min_level, out float ml)) minH = ml * levelHeight;
        }

        if (maxH <= 0f)
        {
            switch (t?.building)
            {
                case "apartments": maxH = apartmentsHeight; break;
                case "dormitory": maxH = dormitoryHeight; break;
                case "college":
                case "university":
                case "school": maxH = collegeHeight; break;
                case "residential": maxH = residentialHeight; break;
                case "commercial":
                case "retail":
                case "office": maxH = commercialHeight; break;
                case "house":
                case "detached": maxH = houseHeight; break;
                default: maxH = defaultHeight; break;
            }
        }
        if (minH >= maxH)
            minH = 0f;
    }

    static bool TryParseMeters(string s, out float value)
    {
        value = 0f;
        if (string.IsNullOrWhiteSpace(s))
            return false;
        s = s.Trim().Replace("m", "").Trim();
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0f;
    }

    List<Vector2> ToPolygon(OsmPoint[] pts, out double cLon, out double cLat)
    {
        cLon = cLat = 0;
        var poly = new List<Vector2>(pts.Length);
        foreach (OsmPoint p in pts)
        {
            Vector3 w = campusMap.GeoToMapPlane(p.lon, p.lat);
            var v = new Vector2(w.x, w.z);
            if (poly.Count == 0 || (poly[poly.Count - 1] - v).sqrMagnitude > 1e-6f)
                poly.Add(v);
            cLon += p.lon;
            cLat += p.lat;
        }
        cLon /= pts.Length;
        cLat /= pts.Length;

        if (poly.Count > 1 && (poly[0] - poly[poly.Count - 1]).sqrMagnitude < 1e-6f)
            poly.RemoveAt(poly.Count - 1);
        if (poly.Count < 3)
            return null;

        // 위에서 봤을 때 시계 방향(Unity 앞면)으로 통일
        if (SignedArea(poly) > 0f)
            poly.Reverse();
        return poly;
    }

    static float SignedArea(List<Vector2> p)
    {
        float a = 0f;
        for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
            a += p[j].x * p[i].y - p[i].x * p[j].y;
        return a * 0.5f;
    }

    static Mesh Extrude(List<Vector2> poly, float minH, float maxH)
    {
        List<int> roofTris = Triangulate(poly);
        if (roofTris == null)
            return null;

        var verts = new List<Vector3>();
        var tris = new List<int>();

        // 지붕
        foreach (Vector2 p in poly)
            verts.Add(new Vector3(p.x, maxH, p.y));
        tris.AddRange(roofTris);

        // 벽 (면마다 정점을 따로 두어 각진 음영)
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % n];
            int s = verts.Count;
            verts.Add(new Vector3(a.x, minH, a.y));
            verts.Add(new Vector3(b.x, minH, b.y));
            verts.Add(new Vector3(b.x, maxH, b.y));
            verts.Add(new Vector3(a.x, maxH, a.y));
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
            tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
        }

        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>시계 방향 단순 다각형의 이어 클리핑 삼각분할</summary>
    static List<int> Triangulate(List<Vector2> poly)
    {
        int n = poly.Count;
        var idx = new List<int>(n);
        for (int i = 0; i < n; i++) idx.Add(i);

        var tris = new List<int>((n - 2) * 3);
        int guard = n * n;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i + idx.Count - 1) % idx.Count];
                int ib = idx[i];
                int ic = idx[(i + 1) % idx.Count];
                Vector2 a = poly[ia], b = poly[ib], c = poly[ic];

                if (Cross(b - a, c - b) >= 0f) // 시계 방향 기준 볼록하지 않음
                    continue;

                bool contains = false;
                foreach (int k in idx)
                {
                    if (k == ia || k == ib || k == ic) continue;
                    if (PointInTriangle(poly[k], a, b, c)) { contains = true; break; }
                }
                if (contains) continue;

                tris.Add(ia); tris.Add(ib); tris.Add(ic);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped)
                return null; // 자기 교차 등 잘못된 외곽선
        }
        if (idx.Count == 3)
        {
            tris.Add(idx[0]); tris.Add(idx[1]); tris.Add(idx[2]);
        }
        return tris;
    }

    static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

#if UNITY_EDITOR
    const string DataPath = "Assets/Data/CampusBuildings.json";
    const string MeshAssetPath = "Assets/Meshes/CampusBuildings.asset";
    const string MaterialPath = "Assets/Materials/CampusBuilding.mat";

    [ContextMenu("Download And Generate Buildings")]
    public void DownloadAndGenerate()
    {
        if (DownloadOsmData())
            GenerateBuildings();
    }

    [ContextMenu("Download OSM Data")]
    public bool DownloadOsmData()
    {
        if (campusMap == null)
        {
            Debug.LogError("[CampusBuildings] Campus Map을 연결하세요.");
            return false;
        }

        var form = new WWWForm();
        form.AddField("data", BuildOverpassQuery());
        using (var req = UnityEngine.Networking.UnityWebRequest.Post(OverpassUrl, form))
        {
            req.timeout = 90;
            var op = req.SendWebRequest();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!op.isDone && sw.Elapsed.TotalSeconds < 95)
                System.Threading.Thread.Sleep(50);

            if (!op.isDone || req.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CampusBuildings] OSM 데이터를 받지 못했습니다: {req.responseCode} {req.error}");
                return false;
            }

            System.IO.Directory.CreateDirectory("Assets/Data");
            System.IO.File.WriteAllText(DataPath, req.downloadHandler.text);
        }

        UnityEditor.AssetDatabase.ImportAsset(DataPath, UnityEditor.ImportAssetOptions.ForceUpdate);
        osmData = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
        UnityEditor.EditorUtility.SetDirty(this);
        return true;
    }

    [ContextMenu("Generate Buildings")]
    public void GenerateBuildings()
    {
        if (campusMap == null || osmData == null)
        {
            Debug.LogError("[CampusBuildings] Campus Map과 OSM Data를 연결하세요.");
            return;
        }

        if (buildingMaterial == null)
        {
            buildingMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (buildingMaterial == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                buildingMaterial = new Material(lit != null ? lit : Shader.Find("Standard"));
                buildingMaterial.color = new Color(0.92f, 0.92f, 0.95f);
                System.IO.Directory.CreateDirectory("Assets/Materials");
                UnityEditor.AssetDatabase.CreateAsset(buildingMaterial, MaterialPath);
            }
        }

        // 기존 건물 제거
        for (int i = transform.childCount - 1; i >= 0; i--)
            UnityEditor.Undo.DestroyObjectImmediate(transform.GetChild(i).gameObject);

        // 메시 에셋 새로 만들기
        System.IO.Directory.CreateDirectory("Assets/Meshes");
        UnityEditor.AssetDatabase.DeleteAsset(MeshAssetPath);
        var container = ScriptableObject.CreateInstance<CampusBuildingMeshes>();
        UnityEditor.AssetDatabase.CreateAsset(container, MeshAssetPath);

        List<(string name, string id, Mesh mesh)> meshes = BuildMeshes(osmData.text);
        foreach (var (label, id, mesh) in meshes)
        {
            UnityEditor.AssetDatabase.AddObjectToAsset(mesh, container);

            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = buildingMaterial;
            if (addColliders)
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            var info = go.AddComponent<CampusBuildingInfo>();
            info.buildingId = id;
            info.buildingName = label.StartsWith(id.Replace('-', '_')) ? "" : label;
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Generate Buildings");
        }

        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        Debug.Log($"[CampusBuildings] 건물 {meshes.Count}개 생성");
    }
#endif
}
