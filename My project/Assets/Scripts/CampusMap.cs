using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// GPS 위경도를 캠퍼스 지도(Map Quad) 위의 월드 좌표로 변환해 Player를 이동시킨다.
/// Map은 원점에 놓인 Quad(Rotation 90,0,0)이며 1 유닛 = 1 m 기준으로
/// 동쪽 = +X, 북쪽 = +Z 이다.
/// </summary>
public class CampusMap : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Renderer mapRenderer;

    [Header("Mapbox")]
    [Tooltip("Mapbox 액세스 토큰 (pk.로 시작). 비워두면 Resources/MapboxToken.txt(깃 제외)를 사용")]
    public string mapboxAccessToken = "";
    [Tooltip("Mapbox 스타일 (username/style_id)")]
    public string mapboxStyle = "mapbox/streets-v12";
    [Tooltip("고해상도(@2x) 이미지 요청")]
    public bool highDpi = true;
    [Tooltip("플레이 시작 시 지도 이미지를 새로 받아온다. 끄면 에셋으로 저장된 지도를 그대로 사용")]
    public bool downloadOnStart = false;

    [Header("Campus Bounds (degrees)")]
    public double minLongitude = 127.0720;
    public double maxLongitude = 127.0880;
    public double minLatitude = 37.2370;
    public double maxLatitude = 37.2510;

    [Header("Map Size (world units, = Map Quad scale)")]
    public float mapWidth = 1418f;
    public float mapHeight = 1548f;

    [Header("Player")]
    public float playerHeight = 10f;
    [Tooltip("위치 보간 속도. 0이면 즉시 이동")]
    public float smoothSpeed = 5f;

    [Header("Location Service")]
    public float desiredAccuracyInMeters = 5f;
    public float updateDistanceInMeters = 1f;

    [Header("Editor Test")]
    [Tooltip("에디터에서 GPS 대신 사용할 테스트 좌표")]
    public double testLongitude = 127.0800;
    public double testLatitude = 37.2440;

    public bool IsLocationReady { get; private set; }
    public double CurrentLongitude { get; private set; }
    public double CurrentLatitude { get; private set; }

    Vector3 targetPosition;

    IEnumerator Start()
    {
        if (player != null)
            targetPosition = player.position;

        bool hasMap = mapRenderer != null && mapRenderer.sharedMaterial != null && mapRenderer.sharedMaterial.mainTexture != null;
        if (downloadOnStart || !hasMap)
            StartCoroutine(LoadMapTexture());

#if UNITY_EDITOR
        SetLocation(testLongitude, testLatitude);
        IsLocationReady = true;
        yield break;
#else
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            float waitPermission = 10f;
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && waitPermission > 0f)
            {
                waitPermission -= Time.deltaTime;
                yield return null;
            }
        }
#endif
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[CampusMap] 위치 서비스가 꺼져 있습니다.");
            yield break;
        }

        Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);

        float wait = 20f;
        while (Input.location.status == LocationServiceStatus.Initializing && wait > 0f)
        {
            wait -= Time.deltaTime;
            yield return null;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Debug.LogWarning($"[CampusMap] 위치 서비스를 시작하지 못했습니다: {Input.location.status}");
            yield break;
        }

        IsLocationReady = true;
#endif
    }

    void Update()
    {
#if UNITY_EDITOR
        // 인스펙터에서 테스트 좌표를 바꾸면 바로 반영
        SetLocation(testLongitude, testLatitude);
#else
        if (IsLocationReady && Input.location.status == LocationServiceStatus.Running)
        {
            LocationInfo info = Input.location.lastData;
            SetLocation(info.longitude, info.latitude);
        }
#endif

        if (player == null)
            return;

        player.position = smoothSpeed > 0f
            ? Vector3.Lerp(player.position, targetPosition, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime))
            : targetPosition;
    }

    void OnDisable()
    {
#if !UNITY_EDITOR
        if (Input.location.status == LocationServiceStatus.Running)
            Input.location.Stop();
#endif
    }

    IEnumerator LoadMapTexture()
    {
        if (mapRenderer == null)
            yield break;

        if (string.IsNullOrWhiteSpace(MapboxToken))
        {
            Debug.LogWarning("[CampusMap] Mapbox 액세스 토큰이 비어 있습니다. GameManager 인스펙터에 입력하세요.");
            yield break;
        }

        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(BuildMapboxUrl()))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CampusMap] 지도 이미지를 받지 못했습니다: {req.responseCode} {req.error}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(req);
            tex.wrapMode = TextureWrapMode.Clamp;

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Material mat = unlit != null ? new Material(unlit) : new Material(mapRenderer.sharedMaterial);
            mat.mainTexture = tex;
            mapRenderer.material = mat;
        }
    }

    /// <summary>인스펙터 값이 있으면 그것을, 없으면 Resources/MapboxToken.txt를 사용</summary>
    string MapboxToken
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(mapboxAccessToken))
                return mapboxAccessToken.Trim();
            TextAsset file = Resources.Load<TextAsset>("MapboxToken");
            return file != null ? file.text.Trim() : "";
        }
    }

    string BuildMapboxUrl()
    {
        // Static Images API 최대 크기는 1280px. 지도 비율에 맞춰 요청한다.
        const int maxSize = 1280;
        float aspect = mapWidth / mapHeight;
        int width = aspect >= 1f ? maxSize : Mathf.RoundToInt(maxSize * aspect);
        int height = aspect >= 1f ? Mathf.RoundToInt(maxSize / aspect) : maxSize;

        CultureInfo ci = CultureInfo.InvariantCulture;
        string bbox = string.Format(ci, "[{0},{1},{2},{3}]", minLongitude, minLatitude, maxLongitude, maxLatitude);
        return $"https://api.mapbox.com/styles/v1/{mapboxStyle}/static/{bbox}/{width}x{height}{(highDpi ? "@2x" : "")}" +
               $"?padding=0&access_token={UnityWebRequest.EscapeURL(MapboxToken)}";
    }

#if UNITY_EDITOR
    const string MapTexturePath = "Assets/Textures/CampusMap.png";
    const string MapMaterialPath = "Assets/Materials/CampusMap.mat";

    /// <summary>
    /// 지도 이미지를 받아 PNG 에셋과 머티리얼로 저장하고 Map에 적용한다.
    /// 인스펙터의 CampusMap 컴포넌트 메뉴(⋮)에서 실행.
    /// </summary>
    [ContextMenu("Download Map To Asset")]
    public void DownloadMapToAsset()
    {
        if (mapRenderer == null || string.IsNullOrWhiteSpace(MapboxToken))
        {
            Debug.LogError("[CampusMap] Map Renderer와 Mapbox 액세스 토큰을 먼저 설정하세요.");
            return;
        }

        byte[] png;
        using (UnityWebRequest req = UnityWebRequest.Get(BuildMapboxUrl()))
        {
            req.timeout = 30;
            var op = req.SendWebRequest();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!op.isDone && sw.Elapsed.TotalSeconds < 35)
                System.Threading.Thread.Sleep(50);
            if (!op.isDone)
            {
                req.Abort();
                Debug.LogError("[CampusMap] 지도 이미지 요청 시간이 초과되었습니다.");
                return;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[CampusMap] 지도 이미지를 받지 못했습니다: {req.responseCode} {req.error}");
                return;
            }
            png = req.downloadHandler.data;
        }

        System.IO.Directory.CreateDirectory("Assets/Textures");
        System.IO.Directory.CreateDirectory("Assets/Materials");
        System.IO.File.WriteAllBytes(MapTexturePath, png);
        UnityEditor.AssetDatabase.ImportAsset(MapTexturePath, UnityEditor.ImportAssetOptions.ForceUpdate);

        var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(MapTexturePath);
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.npotScale = UnityEditor.TextureImporterNPOTScale.None;
        importer.maxTextureSize = 4096;
        importer.SaveAndReimport();

        var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(MapTexturePath);
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(MapMaterialPath);
        if (mat == null)
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(unlit != null ? unlit : Shader.Find("Unlit/Texture"));
            UnityEditor.AssetDatabase.CreateAsset(mat, MapMaterialPath);
        }
        mat.mainTexture = tex;
        UnityEditor.EditorUtility.SetDirty(mat);
        UnityEditor.AssetDatabase.SaveAssets();

        UnityEditor.Undo.RecordObject(mapRenderer, "Assign Campus Map Material");
        mapRenderer.sharedMaterial = mat;
        UnityEditor.EditorUtility.SetDirty(mapRenderer);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mapRenderer.gameObject.scene);

        Debug.Log($"[CampusMap] 지도 저장 완료: {MapTexturePath} ({tex.width}x{tex.height})");
    }
#endif

    void SetLocation(double longitude, double latitude)
    {
        CurrentLongitude = longitude;
        CurrentLatitude = latitude;
        targetPosition = GeoToWorld(longitude, latitude);
    }

    /// <summary>위경도 → 지도 위 월드 좌표 (캠퍼스 범위 밖이면 가장자리로 고정)</summary>
    public Vector3 GeoToWorld(double longitude, double latitude)
    {
        double u = (longitude - minLongitude) / (maxLongitude - minLongitude);
        double v = (latitude - minLatitude) / (maxLatitude - minLatitude);
        u = System.Math.Clamp(u, 0.0, 1.0);
        v = System.Math.Clamp(v, 0.0, 1.0);

        float x = (float)((u - 0.5) * mapWidth);
        float z = (float)((v - 0.5) * mapHeight);
        return new Vector3(x, playerHeight, z);
    }

    /// <summary>위경도 → 지도 평면(y = 0) 위 좌표. 범위 밖도 그대로 계산한다.</summary>
    public Vector3 GeoToMapPlane(double longitude, double latitude)
    {
        double u = (longitude - minLongitude) / (maxLongitude - minLongitude);
        double v = (latitude - minLatitude) / (maxLatitude - minLatitude);
        return new Vector3((float)((u - 0.5) * mapWidth), 0f, (float)((v - 0.5) * mapHeight));
    }

    /// <summary>월드 좌표 → 위경도 (x = 경도, y = 위도)</summary>
    public Vector2d WorldToGeo(Vector3 world)
    {
        double u = world.x / mapWidth + 0.5;
        double v = world.z / mapHeight + 0.5;
        return new Vector2d(
            minLongitude + u * (maxLongitude - minLongitude),
            minLatitude + v * (maxLatitude - minLatitude));
    }

    public bool IsInsideCampus(double longitude, double latitude)
    {
        return longitude >= minLongitude && longitude <= maxLongitude
            && latitude >= minLatitude && latitude <= maxLatitude;
    }

    public struct Vector2d
    {
        public double x;
        public double y;
        public Vector2d(double x, double y) { this.x = x; this.y = y; }
        public override string ToString() => $"({x:F6}, {y:F6})";
    }
}
