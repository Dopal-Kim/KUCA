using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 지도를 컨셉아트 키아트 스타일로 바꾸거나 원래(Mapbox) 스타일로 되돌린다.
/// 메뉴: KUCA/Map Style/Apply Key Art, KUCA/Map Style/Apply Mapbox
/// 입력 (tools/keyart/build_art.py 가 만듦, Assets/Art/KeyArt):
///   CampusGround.jpg, GrassDetail.png, KeyArtGeometry.bytes, KeyArtManifest.json
///   KeyArtLook.json — 조명·재질·카메라 값 (웹 미리보기와 같이 씀)
/// </summary>
public static class KeyArtMapSetup
{
    const string Dir = "Assets/Art/KeyArt";
    const string GroundTex = Dir + "/CampusGround.jpg";
    const string DetailTex = Dir + "/GrassDetail.png";
    const string GeometryFile = Dir + "/KeyArtGeometry.bytes";
    const string ManifestFile = Dir + "/KeyArtManifest.json";
    const string LookFile = Dir + "/KeyArtLook.json";
    const string GroundMat = Dir + "/KeyArtGround.mat";
    const string OuterMat = Dir + "/KeyArtOuterGround.mat";
    const string BuildingMat = Dir + "/KeyArtBuilding.mat";
    const string VertexColorMat = Dir + "/KeyArtTree.mat";
    const string SkyMat = Dir + "/KeyArtSky.mat";
    const string GeometryRoot = "KeyArtGeometry";
    const string LookRoot = "KeyArtLook";
    static readonly string[] OldRoots = { "KeyArtTrees", "KeyArtLandmarks" };   // 이전 버전이 만든 오브젝트

    const string MapboxMapMat = "Assets/Materials/CampusMap.mat";
    const string MapboxBuildingMat = "Assets/Materials/CampusBuilding.mat";

    // ---------- JSON ----------

    [System.Serializable] class Sun { public float[] color; public float intensity, pitch, yaw; }
    [System.Serializable] class Ambient { public float[] sky, ground; }
    [System.Serializable] class Fog { public float[] color; public float start, end; }
    [System.Serializable] class Detail { public float tile, strength; }
    [System.Serializable] class Cam { public float fov, distance, minDistance, maxDistance, pitch; }
    [System.Serializable]
    class Style
    {
        public float[] wall, trim, window, roof;
        public float floorHeight, spacing, windowWidth, windowHeight, pilasterEvery, brick;
    }
    [System.Serializable] class Styles { public Style Default, Classical, Modern, Glass, Brick; }
    [System.Serializable]
    class Look
    {
        public Sun sun; public Ambient ambient; public float shadowStrength; public Fog fog;
        public float[] outerGrass; public float buildingHeightScale = 1f; public Detail grassDetail; public Cam camera; public Styles styles;
    }
    [System.Serializable] class StyleRow { public string id, style; }
    [System.Serializable] class Manifest { public StyleRow[] styles; public string[] hidden; }

    static Color Srgb(float[] c) => new Color(c[0], c[1], c[2]);   // Unity Color 는 감마(sRGB) 값

    // ---------- 키아트 ----------

    [MenuItem("KUCA/Map Style/Apply Key Art")]
    public static void ApplyKeyArt()
    {
        var look = JsonUtility.FromJson<Look>(System.IO.File.ReadAllText(LookFile));
        var manifest = JsonUtility.FromJson<Manifest>(System.IO.File.ReadAllText(ManifestFile));

        // 텍스처
        var gImp = (TextureImporter)AssetImporter.GetAtPath(GroundTex);
        gImp.maxTextureSize = 4096;
        gImp.anisoLevel = 8;
        gImp.wrapMode = TextureWrapMode.Clamp;
        gImp.npotScale = TextureImporterNPOTScale.None;
        gImp.sRGBTexture = true;
        gImp.SaveAndReimport();
        var dImp = (TextureImporter)AssetImporter.GetAtPath(DetailTex);
        dImp.sRGBTexture = false;
        dImp.wrapMode = TextureWrapMode.Repeat;
        dImp.anisoLevel = 4;
        dImp.SaveAndReimport();

        // 지면
        Material ground = LoadOrCreate(GroundMat, "KUCA/StylizedGround");
        ground.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundTex));
        ground.SetColor("_BaseColor", Color.white);
        ground.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DetailTex));
        ground.SetFloat("_DetailTile", look.grassDetail.tile);
        ground.SetFloat("_DetailStrength", look.grassDetail.strength);
        EditorUtility.SetDirty(ground);

        Material outer = LoadOrCreate(OuterMat, "KUCA/StylizedGround");
        outer.SetTexture("_BaseMap", null);
        outer.SetColor("_BaseColor", Srgb(look.outerGrass));
        outer.SetTexture("_DetailMap", AssetDatabase.LoadAssetAtPath<Texture2D>(DetailTex));
        outer.SetFloat("_DetailTile", look.grassDetail.tile);
        outer.SetFloat("_DetailStrength", look.grassDetail.strength);
        EditorUtility.SetDirty(outer);

        // 건물 외벽: 스타일별 재질 (층 높이도 건물 높이 배율만큼 키워 층수는 그대로)
        float hs = look.buildingHeightScale > 0f ? look.buildingHeightScale : 1f;
        var styleMats = new Dictionary<string, Material>
        {
            { "Default", StyleMaterial(BuildingMat, look.styles.Default, hs) },
            { "Classical", StyleMaterial(Dir + "/KeyArtBuilding_Classical.mat", look.styles.Classical, hs) },
            { "Modern", StyleMaterial(Dir + "/KeyArtBuilding_Modern.mat", look.styles.Modern, hs) },
            { "Glass", StyleMaterial(Dir + "/KeyArtBuilding_Glass.mat", look.styles.Glass, hs) },
            { "Brick", StyleMaterial(Dir + "/KeyArtBuilding_Brick.mat", look.styles.Brick, hs) },
        };
        Material vertexColor = LoadOrCreate(VertexColorMat, "KUCA/VertexColorLit");
        vertexColor.SetColor("_Tint", Color.white);
        EditorUtility.SetDirty(vertexColor);

        Material sky = LoadOrCreate(SkyMat, "Skybox/Procedural");
        sky.SetColor("_SkyTint", new Color(0.48f, 0.68f, 0.96f));
        sky.SetColor("_GroundColor", Srgb(look.outerGrass));
        sky.SetFloat("_AtmosphereThickness", 0.7f);
        sky.SetFloat("_Exposure", 1.3f);
        sky.SetFloat("_SunSize", 0.03f);
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssets();

        var map = GameObject.Find("Map");
        Undo.RecordObject(map.GetComponent<Renderer>(), "Key Art");
        map.GetComponent<Renderer>().sharedMaterial = ground;
        map.GetComponent<Renderer>().receiveShadows = true;
        // 실행할 때 Mapbox 지도를 다시 받아 덮어쓰지 않게 한다.
        var campusMap = Object.FindAnyObjectByType<CampusMap>();
        if (campusMap != null)
        {
            Undo.RecordObject(campusMap, "Key Art");
            campusMap.downloadOnStart = false;
        }

        ApplyBuildingStyles(styleMats, manifest, hs);
        BuildGeometry(vertexColor, styleMats);
        SetOuterGround(outer, true);
        SetLighting(look, sky, true);
        SetCamera(look.camera);
        SetRenderQuality();
        SetPostProcessing(true);

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
        var mapboxBuilding = AssetDatabase.LoadAssetAtPath<Material>(MapboxBuildingMat);
        var buildings = GameObject.Find("Buildings");
        Undo.RecordObject(buildings.transform, "Mapbox");
        buildings.transform.localScale = Vector3.one;
        foreach (Renderer r in buildings.GetComponentsInChildren<Renderer>(true))
        {
            Undo.RecordObject(r, "Mapbox");
            r.sharedMaterial = mapboxBuilding;
            r.enabled = true;
        }
        DestroyIfExists(GeometryRoot);
        DestroyIfExists(LookRoot);
        foreach (string n in OldRoots) DestroyIfExists(n);
        SetOuterGround(null, false);
        SetLighting(null, null, false);
        SetPostProcessing(false);
        EditorSceneManager.MarkSceneDirty(map.scene);
        EditorSceneManager.SaveScene(map.scene);
    }

    static Material StyleMaterial(string path, Style st, float heightScale)
    {
        Material mat = LoadOrCreate(path, "KUCA/StylizedBuilding");
        mat.SetColor("_WallColor", Srgb(st.wall));
        mat.SetColor("_TrimColor", Srgb(st.trim));
        mat.SetColor("_WindowColor", Srgb(st.window));
        mat.SetColor("_RoofColor", Srgb(st.roof));
        mat.SetFloat("_FloorHeight", st.floorHeight * heightScale);
        mat.SetFloat("_WindowSpacing", st.spacing);
        mat.SetFloat("_WindowWidth", st.windowWidth);
        mat.SetFloat("_WindowHeight", st.windowHeight);
        mat.SetFloat("_PilasterEvery", st.pilasterEvery);
        mat.SetFloat("_Brick", st.brick);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>건물마다 외벽 재질, 랜드마크로 대신하는 상자 건물은 숨김 (충돌체·정보는 남김)</summary>
    static void ApplyBuildingStyles(Dictionary<string, Material> mats, Manifest manifest, float heightScale)
    {
        var styleOf = new Dictionary<string, string>();
        foreach (StyleRow row in manifest.styles) styleOf[row.id] = row.style;
        var hidden = new HashSet<string>(manifest.hidden);

        var root = GameObject.Find("Buildings");
        // 미니어처 비율: 건물 높이만 키운다 (build_art.py 도 같은 배율로 난간·지붕 디테일을 올림)
        Undo.RecordObject(root.transform, "Building height");
        root.transform.localScale = new Vector3(1f, heightScale, 1f);
        foreach (CampusBuildingInfo info in root.GetComponentsInChildren<CampusBuildingInfo>(true))
        {
            var r = info.GetComponent<Renderer>();
            if (r == null) continue;
            Undo.RecordObject(r, "Building style");
            r.sharedMaterial = mats[styleOf.TryGetValue(info.buildingId, out string s) && mats.ContainsKey(s) ? s : "Default"];
            r.enabled = !hidden.Contains(info.buildingId);
            r.shadowCastingMode = ShadowCastingMode.On;
            r.receiveShadows = true;
        }
        var gen = root.GetComponent<CampusBuildings>();
        if (gen != null)
        {
            Undo.RecordObject(gen, "Building material");
            gen.buildingMaterial = mats["Default"];
        }
    }

    static void BuildGeometry(Material mat, Dictionary<string, Material> styleMats)
    {
        foreach (string n in OldRoots) DestroyIfExists(n);
        var go = GameObject.Find(GeometryRoot);
        if (go == null)
        {
            go = new GameObject(GeometryRoot);
            Undo.RegisterCreatedObjectUndo(go, "Key Art geometry");
        }
        var geo = go.GetComponent<KeyArtGeometry>();
        if (geo == null) geo = Undo.AddComponent<KeyArtGeometry>(go);
        Undo.RecordObject(geo, "Key Art geometry");
        geo.geometry = AssetDatabase.LoadAssetAtPath<TextAsset>(GeometryFile);
        geo.material = mat;
        geo.shellStyles = new List<string>(styleMats.Keys).ToArray();
        geo.shellMaterials = new List<Material>(styleMats.Values).ToArray();
        geo.castShadows = ShadowCastingMode.On;
        geo.Build();
        Debug.Log($"[KeyArtMapSetup] 키아트 지오메트리 정점 {geo.VertexCount:N0}개");
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
        r.receiveShadows = true;
    }

    static void SetLighting(Look look, Material sky, bool on)
    {
        Camera cam = Camera.main;
        Undo.RecordObject(cam, "Sky");
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.farClipPlane = 6000f;
        RenderSettings.skybox = on ? sky : null;
        RenderSettings.fog = on;
        if (on)
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Srgb(look.fog.color);
            RenderSettings.fogStartDistance = look.fog.start;
            RenderSettings.fogEndDistance = look.fog.end;
            // 키아트 셰이더 밖(캐릭터 등 URP Lit)도 비슷한 환경광을 받게 (리니어 배율 → 감마 색)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color skyAmb = new Color(look.ambient.sky[0], look.ambient.sky[1], look.ambient.sky[2]);
            Color groundAmb = new Color(look.ambient.ground[0], look.ambient.ground[1], look.ambient.ground[2]);
            RenderSettings.ambientSkyColor = skyAmb.gamma;
            RenderSettings.ambientEquatorColor = Color.Lerp(skyAmb, groundAmb, 0.5f).gamma;
            RenderSettings.ambientGroundColor = groundAmb.gamma;
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
        }

        // 셰이더 전역 값 (KUCAStylizedLighting.hlsl)
        var lookGo = GameObject.Find(LookRoot);
        if (on)
        {
            if (lookGo == null)
            {
                lookGo = new GameObject(LookRoot);
                Undo.RegisterCreatedObjectUndo(lookGo, "Key Art look");
            }
            var kl = lookGo.GetComponent<KeyArtLook>();
            if (kl == null) kl = Undo.AddComponent<KeyArtLook>(lookGo);
            Undo.RecordObject(kl, "Key Art look");
            kl.skyAmbient = new Vector3(look.ambient.sky[0], look.ambient.sky[1], look.ambient.sky[2]);
            kl.groundAmbient = new Vector3(look.ambient.ground[0], look.ambient.ground[1], look.ambient.ground[2]);
            kl.shadowStrength = look.shadowStrength;
            kl.Apply();
        }

        var sun = Object.FindAnyObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional)
        {
            Undo.RecordObject(sun, "Sun");
            Undo.RecordObject(sun.transform, "Sun");
            sun.color = on ? Srgb(look.sun.color) : Color.white;
            sun.intensity = on ? look.sun.intensity : 1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 1f;   // 그림자 세기는 셰이더(_KucaShadowStrength)가 정한다
            sun.transform.rotation = on ? Quaternion.Euler(look.sun.pitch, look.sun.yaw, 0f) : Quaternion.Euler(50f, 330f, 0f);
        }
    }

    /// <summary>디오라마 카메라: 좁은 화각으로 멀리서 (원근 왜곡이 적은 미니어처 느낌)</summary>
    static void SetCamera(Cam c)
    {
        Camera cam = Camera.main;
        Undo.RecordObject(cam, "Camera");
        cam.fieldOfView = c.fov;
        cam.nearClipPlane = 5f;
        var follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
        {
            Undo.RecordObject(follow, "Camera");
            follow.distance = c.distance;
            follow.minDistance = c.minDistance;
            follow.maxDistance = c.maxDistance;
            follow.pitch = c.pitch;
        }
        var data = cam.GetComponent<UniversalAdditionalCameraData>();
        if (data != null)
        {
            Undo.RecordObject(data, "Camera");
            data.renderShadows = true;
            data.renderPostProcessing = true;
        }
    }

    /// <summary>
    /// URP 품질: 멀리서 보는 카메라에도 그림자가 보이게 그림자 거리·캐스케이드를 늘리고, MSAA 4x, 렌더 스케일 1.
    /// (이전 설정은 그림자 거리 50 m 라 250 m 밖 카메라에서 그림자가 하나도 안 보였다)
    /// </summary>
    static void SetRenderQuality()
    {
        var assets = new HashSet<RenderPipelineAsset>();
        if (GraphicsSettings.defaultRenderPipeline != null) assets.Add(GraphicsSettings.defaultRenderPipeline);
        for (int i = 0; i < QualitySettings.count; i++)
        {
            var a = QualitySettings.GetRenderPipelineAssetAt(i);
            if (a != null) assets.Add(a);
        }
        foreach (RenderPipelineAsset a in assets)
        {
            var so = new SerializedObject(a);
            bool mobile = a.name.Contains("Mobile");
            SetFloat(so, "m_ShadowDistance", 1100f);
            SetInt(so, "m_ShadowCascadeCount", 2);
            SetFloat(so, "m_Cascade2Split", 0.35f);
            SetInt(so, "m_MainLightShadowmapResolution", mobile ? 2048 : 4096);
            SetBool(so, "m_MainLightShadowsSupported", true);
            SetBool(so, "m_SoftShadowsSupported", true);
            SetInt(so, "m_MSAA", 4);
            SetFloat(so, "m_RenderScale", 1f);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(a);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>키아트 색을 그대로: 톤매핑 끄고 비네팅 약하게</summary>
    static void SetPostProcessing(bool on)
    {
        var volume = Object.FindAnyObjectByType<Volume>();
        VolumeProfile profile = volume != null ? volume.sharedProfile : null;
        if (profile == null) return;
        Undo.RecordObject(profile, "Post processing");
        if (profile.TryGet(out Tonemapping tm))
        {
            tm.active = true;
            tm.mode.Override(on ? TonemappingMode.None : TonemappingMode.Neutral);
        }
        if (profile.TryGet(out Vignette vig))
            vig.intensity.Override(on ? 0.12f : 0.2f);
        if (profile.TryGet(out Bloom bloom))
            bloom.intensity.Override(on ? 0.15f : 0.25f);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }

    static void SetFloat(SerializedObject so, string name, float v) { var p = so.FindProperty(name); if (p != null) p.floatValue = v; }
    static void SetInt(SerializedObject so, string name, int v) { var p = so.FindProperty(name); if (p != null) p.intValue = v; }
    static void SetBool(SerializedObject so, string name, bool v) { var p = so.FindProperty(name); if (p != null) p.boolValue = v; }

    static void DestroyIfExists(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
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
