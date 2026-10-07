using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 지도를 컨셉아트 키아트 스타일로 바꾸거나 원래(Mapbox) 스타일로 되돌린다.
/// 메뉴: KUCA/Map Style/Apply Key Art, KUCA/Map Style/Apply Mapbox
/// 입력: Assets/Art/KeyArt/CampusGround.jpg, CampusTrees.json (tools/keyart/render_ground.py 로 생성)
/// </summary>
public static class KeyArtMapSetup
{
    const string Dir = "Assets/Art/KeyArt";
    const string GroundTex = Dir + "/CampusGround.jpg";
    const string TreesJson = Dir + "/CampusTrees.json";
    const string GroundMat = Dir + "/KeyArtGround.mat";
    const string OuterMat = Dir + "/KeyArtOuterGround.mat";
    const string BuildingMat = Dir + "/KeyArtBuilding.mat";
    const string TreeMat = Dir + "/KeyArtTree.mat";
    const string SkyMat = Dir + "/KeyArtSky.mat";
    const string TreeMeshes = Dir + "/KeyArtTrees.asset";

    const string MapboxMapMat = "Assets/Materials/CampusMap.mat";
    const string MapboxBuildingMat = "Assets/Materials/CampusBuilding.mat";

    static readonly Color GrassColor = new Color(0.51f, 0.62f, 0.23f); // 잔디 타일 평균색
    static readonly Color FogColor = new Color(0.80f, 0.89f, 0.97f);

    [MenuItem("KUCA/Map Style/Apply Key Art")]
    public static void ApplyKeyArt()
    {
        // 지면
        var tImp = (TextureImporter)AssetImporter.GetAtPath(GroundTex);
        tImp.maxTextureSize = 4096;
        tImp.anisoLevel = 4;
        tImp.wrapMode = TextureWrapMode.Clamp;
        tImp.npotScale = TextureImporterNPOTScale.None;
        tImp.SaveAndReimport();
        Material ground = LoadOrCreate(GroundMat, "Universal Render Pipeline/Lit");
        ground.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTex));
        ground.SetColor("_BaseColor", Color.white);
        ground.SetFloat("_Smoothness", 0f);
        ground.SetFloat("_SpecularHighlights", 0f);
        ground.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        ground.SetFloat("_EnvironmentReflections", 0f);
        ground.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(ground);

        Material outer = LoadOrCreate(OuterMat, "Universal Render Pipeline/Lit");
        outer.SetColor("_BaseColor", GrassColor);
        outer.SetFloat("_Smoothness", 0f);
        EditorUtility.SetDirty(outer);

        Material building = LoadOrCreate(BuildingMat, "KUCA/StylizedBuilding");
        Material tree = LoadOrCreate(TreeMat, "KUCA/VertexColorLit");

        Material sky = LoadOrCreate(SkyMat, "Skybox/Procedural");
        sky.SetColor("_SkyTint", new Color(0.42f, 0.62f, 0.95f));
        sky.SetColor("_GroundColor", GrassColor);
        sky.SetFloat("_AtmosphereThickness", 0.75f);
        sky.SetFloat("_Exposure", 1.35f);
        sky.SetFloat("_SunSize", 0.03f);
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssets();

        var map = GameObject.Find("Map");
        Undo.RecordObject(map.GetComponent<Renderer>(), "Key Art");
        map.GetComponent<Renderer>().sharedMaterial = ground;
        // 실행할 때 Mapbox 지도를 다시 받아 덮어쓰지 않게 한다.
        var campusMap = Object.FindAnyObjectByType<CampusMap>();
        if (campusMap != null)
        {
            Undo.RecordObject(campusMap, "Key Art");
            campusMap.downloadOnStart = false;
        }

        SetBuildingMaterial(building);
        KeyArtLandmarks.Build(building, tree);   // 건물마다 외벽 재질과 특징 형태
        BuildTrees(tree);
        SetOuterGround(outer, true);
        SetSky(sky, true);

        EditorSceneManager.MarkSceneDirty(map.scene);
        EditorSceneManager.SaveScene(map.scene);
        Debug.Log("[KeyArtMapSetup] 키아트 스타일을 적용했습니다.");
    }

    [MenuItem("KUCA/Map Style/Apply Mapbox")]
    public static void ApplyMapbox()
    {
        var map = GameObject.Find("Map");
        Undo.RecordObject(map.GetComponent<Renderer>(), "Mapbox");
        map.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MapboxMapMat);
        KeyArtLandmarks.Clear();
        SetBuildingMaterial(AssetDatabase.LoadAssetAtPath<Material>(MapboxBuildingMat));
        var trees = GameObject.Find("KeyArtTrees");
        if (trees != null) Undo.DestroyObjectImmediate(trees);
        SetOuterGround(null, false);
        SetSky(null, false);
        EditorSceneManager.MarkSceneDirty(map.scene);
        EditorSceneManager.SaveScene(map.scene);
    }

    static void SetBuildingMaterial(Material mat)
    {
        var root = GameObject.Find("Buildings");
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
        {
            Undo.RecordObject(r, "Building material");
            r.sharedMaterial = mat;
        }
        var gen = root.GetComponent<CampusBuildings>();
        if (gen != null)
        {
            Undo.RecordObject(gen, "Building material");
            gen.buildingMaterial = mat;
        }
    }

    /// <summary>지도 바깥을 잔디로 채워 지평선까지 이어지게 한다.</summary>
    static void SetOuterGround(Material mat, bool on)
    {
        var go = GameObject.Find("OuterGround");
        if (!on)
        {
            if (go != null) Undo.DestroyObjectImmediate(go);
            return;
        }
        if (go == null)
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "OuterGround";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Undo.RegisterCreatedObjectUndo(go, "Outer ground");
        }
        go.transform.SetPositionAndRotation(new Vector3(0f, -0.3f, 0f), Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(9000f, 9000f, 1f);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
    }

    static void SetSky(Material sky, bool on)
    {
        Camera cam = Camera.main;
        Undo.RecordObject(cam, "Sky");
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.farClipPlane = 4000f;
        RenderSettings.skybox = on ? sky : null;
        RenderSettings.fog = on;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogColor;
        RenderSettings.fogStartDistance = 500f;
        RenderSettings.fogEndDistance = 1700f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = on ? new Color(0.78f, 0.86f, 0.98f) : new Color(0.21f, 0.23f, 0.26f);
        RenderSettings.ambientEquatorColor = on ? new Color(0.72f, 0.78f, 0.70f) : new Color(0.11f, 0.12f, 0.13f);
        RenderSettings.ambientGroundColor = on ? new Color(0.45f, 0.55f, 0.32f) : new Color(0.05f, 0.04f, 0.04f);
        if (!on)
            RenderSettings.ambientMode = AmbientMode.Skybox;

        var sun = Object.FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            Undo.RecordObject(sun, "Sun");
            Undo.RecordObject(sun.transform, "Sun");
            sun.color = on ? new Color(1f, 0.97f, 0.9f) : Color.white;
            sun.intensity = on ? 1.15f : 1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(52f, 330f, 0f);
        }
    }

    // ---------- 나무 ----------

    [System.Serializable] class TreeFile { public List<TreeRow> trees; }
    [System.Serializable] class TreeRow { public float x, z, s; public string kind; public int rot; }

    static void BuildTrees(Material mat)
    {
        string json = System.IO.File.ReadAllText(TreesJson);
        // [x, z, kind, s, rot] 배열을 JsonUtility 가 읽을 수 있게 바꾼다
        var rows = ParseTrees(json);

        var old = GameObject.Find("KeyArtTrees");
        if (old != null) Undo.DestroyObjectImmediate(old);
        AssetDatabase.DeleteAsset(TreeMeshes);
        var container = ScriptableObject.CreateInstance<CampusBuildingMeshes>();
        AssetDatabase.CreateAsset(container, TreeMeshes);

        var root = new GameObject("KeyArtTrees");
        Undo.RegisterCreatedObjectUndo(root, "Trees");
        root.isStatic = true;

        // 지도를 6x6 칸으로 나눠 칸마다 메시 하나 (보이는 칸만 그리도록)
        const int Grid = 6;
        var chunks = new Dictionary<int, LowPolyMeshBuilder>();
        foreach (TreeRow t in rows)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt((t.x / 1418f + 0.5f) * Grid), 0, Grid - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt((t.z / 1548f + 0.5f) * Grid), 0, Grid - 1);
            int key = cz * Grid + cx;
            if (!chunks.TryGetValue(key, out LowPolyMeshBuilder mb)) chunks[key] = mb = new LowPolyMeshBuilder();
            AddTree(mb, t);
        }

        foreach (var kv in chunks)
        {
            Mesh mesh = kv.Value.ToMesh($"Trees_{kv.Key}");
            AssetDatabase.AddObjectToAsset(mesh, container);
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root.transform, false);
            go.isStatic = true;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.On;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[KeyArtMapSetup] 나무 {rows.Count}그루, 메시 {chunks.Count}개");
    }

    static List<TreeRow> ParseTrees(string json)
    {
        var list = new List<TreeRow>();
        int start = json.IndexOf('[', json.IndexOf("trees")) + 1;
        string body = json.Substring(start, json.LastIndexOf(']') - start);
        foreach (string item in body.Split(new[] { "], [", "],[" }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string[] p = item.Trim('[', ']', ' ').Split(',');
            if (p.Length < 5) continue;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            list.Add(new TreeRow
            {
                x = float.Parse(p[0], inv),
                z = float.Parse(p[1], inv),
                kind = p[2].Trim().Trim('"'),
                s = float.Parse(p[3], inv),
                rot = int.Parse(p[4].Trim()),
            });
        }
        return list;
    }

    static readonly Color Trunk = new Color(0.47f, 0.34f, 0.24f);
    static readonly Color[] Evergreen = { new Color(0.24f, 0.52f, 0.20f), new Color(0.30f, 0.58f, 0.22f), new Color(0.20f, 0.46f, 0.19f) };
    static readonly Color[] Leafy = { new Color(0.40f, 0.66f, 0.24f), new Color(0.46f, 0.70f, 0.28f), new Color(0.35f, 0.60f, 0.22f) };
    static readonly Color[] Blossom = { new Color(0.98f, 0.74f, 0.82f), new Color(0.96f, 0.66f, 0.77f), new Color(1.00f, 0.82f, 0.88f) };

    static void AddTree(LowPolyMeshBuilder mb, TreeRow t)
    {
        var rnd = new System.Random(t.rot * 7919 + (int)(t.x * 13) + (int)(t.z * 31));
        Color Pick(Color[] c) => c[rnd.Next(c.Length)];
        Matrix4x4 m = Matrix4x4.TRS(new Vector3(t.x, 0f, t.z), Quaternion.Euler(0f, t.rot, 0f), Vector3.one * t.s);

        switch (t.kind)
        {
            case "cone":
                mb.Prism(m, Vector3.zero, 0.35f, 1.6f, 6, Trunk);
                mb.Cone(m, new Vector3(0f, 1.2f, 0f), 3.2f, 5.2f, 8, Pick(Evergreen));
                mb.Cone(m, new Vector3(0f, 4.0f, 0f), 2.4f, 4.8f, 8, Pick(Evergreen));
                break;
            case "round":
                mb.Prism(m, Vector3.zero, 0.4f, 2.4f, 6, Trunk);
                mb.Ico(m, new Vector3(0f, 4.6f, 0f), new Vector3(3.0f, 2.7f, 3.0f), Pick(Leafy));
                break;
            default: // cherry
                mb.Prism(m, Vector3.zero, 0.38f, 2.2f, 6, Trunk);
                mb.Ico(m, new Vector3(0f, 4.3f, 0f), new Vector3(2.6f, 2.1f, 2.6f), Pick(Blossom));
                mb.Ico(m, new Vector3(1.4f, 3.7f, 0.6f), new Vector3(1.8f, 1.5f, 1.8f), Pick(Blossom));
                mb.Ico(m, new Vector3(-1.2f, 3.8f, -0.8f), new Vector3(1.7f, 1.4f, 1.7f), Pick(Blossom));
                break;
        }
    }

    static Material LoadOrCreate(string path, string shaderName)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (mat.shader != shader)
            mat.shader = shader;
        return mat;
    }
}
