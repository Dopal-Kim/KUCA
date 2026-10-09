using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// 경희몬 잡기 화면 (포켓몬 GO 의 AR 화면처럼).
/// - AR 모드 (ARKit/ARCore 기기): AR Foundation 으로 바닥을 찾아 그 위에 3D 경희몬을 세운다. 화면을 누르면 그 자리로 옮긴다.
/// - 대체 모드 (AR 을 못 쓸 때, 에디터): 후면 카메라 영상 위에 경희몬을 겹치고 자이로(없으면 끌기)로 둘러본다.
/// 경희몬이 화면 안에 있을 때 셔터를 누르면 사진을 찍고, 그 사진과 함께 경희몬을 얻는다 (사진은 스크랩북에 저장).
/// </summary>
public class CatchCameraScreen : MonoBehaviour
{
    [Tooltip("에디터에서도 컴퓨터 웹캠을 켤지 (끄면 대신 배경 그림을 보여 준다)")]
    public bool useWebcamInEditor = false;
    [Tooltip("에디터에서도 AR 을 시도할지 (XR Simulation 을 쓸 때만 켠다)")]
    public bool useARInEditor = false;
    [Tooltip("AR 모드에서 바닥에 선 경희몬 크기 (m)")]
    public float arMonsterSize = 0.35f;
    [Tooltip("AR 에서 이 시간(초) 안에 바닥을 못 찾으면 앞쪽 허공에 세운다")]
    public float arPlaneTimeout = 10f;
    [Tooltip("경희몬까지 거리 (m)")]
    public float monsterDistance = 3f;
    [Tooltip("경희몬 크기 (m)")]
    public float monsterSize = 0.8f;
    public float cameraFov = 60f;
    [Tooltip("저장할 사진의 긴 변 최대 픽셀")]
    public int maxPhotoSize = 1600;
    [Range(30, 100)] public int jpgQuality = 85;

    static readonly Vector3 StagePos = new Vector3(0f, -30000f, 0f);
    static readonly Color Gold = new Color(0.96f, 0.79f, 0.45f);
    static readonly Color Navy = new Color(0.10f, 0.22f, 0.52f);

    public static bool IsOpen { get; private set; }

    Canvas canvas;
    GameObject root;
    RawImage camView;
    AspectRatioFitter camFitter;
    RawImage fallbackBg;
    RawImage arView;
    CanvasGroup controls;
    Text title, hint, note;
    Image shutterCore;
    Image flash;
    float flashAlpha;
    GameObject resultCard;
    RawImage resultPhoto;
    AspectRatioFitter resultFitter;
    Text resultTitle, resultSub;
    Button albumButton;

    WebCamTexture webcam;
    Camera arCam;
    Light arLight;
    RenderTexture arTexture;
    GameObject stage;
    Transform monster;
    Transform shadow;
    ArCatchRig ar;
    bool arMode;
    Camera hiddenMainCamera;
    float placeDeadline;
    Vector3 monsterBase;
    bool monsterPlaced;
    /// <summary>피벗에서 발바닥까지 높이</summary>
    float monsterHalf;
    float openTime;
    float phase;

    Collectible target;
    string speciesId;
    string displayName;
    CollectibleType type;
    CollectController owner;
    float yaw, pitch;
    bool busy;
    float hintHoldUntil;
    Texture2D lastPhoto;
    string lastPhotoPath;

    /// <summary>target 을 잡는 카메라 화면을 연다.</summary>
    public void Open(Collectible target, CollectController owner)
    {
        if (IsOpen || target == null || !CreatureLibrary.TryGetMesh(target.SpeciesId, out _))
            return;
        if (root == null)
            Build();
        this.target = target;
        this.owner = owner;
        type = target.Type;
        speciesId = target.SpeciesId;
        displayName = target.DisplayName;
        IsOpen = true;
        busy = false;
        UIInputBlocker.SetModal(this, true);
        root.SetActive(true);
        resultCard.SetActive(false);
        controls.alpha = 1f;
        controls.blocksRaycasts = true;
        title.text = $"야생의 {displayName}{Josa(displayName, "이", "가")} 나타났다!";
        hint.text = "";
        note.text = "";
        yaw = pitch = 0f;
        openTime = Time.unscaledTime;
        monsterPlaced = false;

        bool tryAR = Application.isEditor ? useARInEditor : true;
        if (tryAR)
            StartCoroutine(OpenAR());
        else
            StartFallback();
    }

    /// <summary>카메라 시점 (AR 이면 AR 카메라)</summary>
    Camera ViewCam => arMode && ar != null ? ar.Camera : arCam;

    void StartFallback()
    {
        arMode = false;
        SetArLook(false);
        BuildStage(withCamera: true);
        EnableGyro();
        StartCoroutine(StartCamera());
    }

    IEnumerator OpenAR()
    {
        arMode = true;
        SetArLook(true);
        note.text = "AR 준비 중…";
        // 지도 카메라는 잠시 끄고 AR 카메라가 화면 전체를 그린다.
        hiddenMainCamera = Camera.main;
        if (hiddenMainCamera != null)
            hiddenMainCamera.enabled = false;

        ar = new ArCatchRig();
        yield return ar.Start(StagePos, MonsterThumbnails.StageLayer);
        if (!ar.Ready)
        {
            string why = ar.FailReason;
            ar.Stop();
            ar = null;
            RestoreMainCamera();
            StartFallback();
            note.text = $"{why} · 일반 카메라로 찍어요";
            yield break;
        }
        BuildStage(withCamera: false);
        note.text = "주변 바닥을 천천히 비춰 주세요";
        placeDeadline = Time.unscaledTime + arPlaneTimeout;
    }

    /// <summary>AR 이면 화면을 투명하게 비워 AR 카메라 영상이 보이게 한다.</summary>
    void SetArLook(bool on)
    {
        root.GetComponent<Image>().color = on ? new Color(0f, 0f, 0f, 0f) : Color.black;
        fallbackBg.enabled = !on;
        camView.enabled = false;
        arView.enabled = !on;
    }

    void RestoreMainCamera()
    {
        if (hiddenMainCamera != null)
            hiddenMainCamera.enabled = true;
        hiddenMainCamera = null;
    }

    public void Close()
    {
        StopAllCoroutines();
        if (webcam != null)
        {
            webcam.Stop();
            Destroy(webcam);
            webcam = null;
        }
        if (stage != null)
            Destroy(stage);
        stage = null;
        if (ar != null)
        {
            ar.Stop();
            ar = null;
        }
        arMode = false;
        RestoreMainCamera();
        if (arTexture != null)
        {
            arTexture.Release();
            Destroy(arTexture);
            arTexture = null;
        }
        if (lastPhoto != null)
            Destroy(lastPhoto);
        lastPhoto = null;
        if (root != null)
            root.SetActive(false);
        target = null;
        IsOpen = false;
        UIInputBlocker.SetModal(this, false);
    }

    void OnDisable()
    {
        if (IsOpen)
            Close();
    }

    // ---------- 화면 ----------

    void Build()
    {
        canvas = UIKit.CreateCanvas(transform, "CatchCanvas", 25);
        root = UIKit.Image(canvas.transform, "CatchScreen", Color.black, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)root.transform);

        // 카메라가 없을 때 대신 보여 줄 하늘·땅 배경
        fallbackBg = new GameObject("Fallback", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        fallbackBg.transform.SetParent(root.transform, false);
        fallbackBg.texture = HudIcons.Gradient(new Color(0.62f, 0.82f, 1f), new Color(0.42f, 0.62f, 0.38f));
        fallbackBg.raycastTarget = false;
        UIKit.Stretch(fallbackBg.rectTransform);

        // 카메라 영상 (화면을 꽉 채움)
        RectTransform camArea = UIKit.Rect(root.transform, "CameraArea");
        UIKit.Stretch(camArea);
        camArea.gameObject.AddComponent<RectMask2D>();
        var cv = new GameObject("Camera", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        cv.transform.SetParent(camArea, false);
        camView = cv.GetComponent<RawImage>();
        camView.raycastTarget = false;
        camFitter = cv.GetComponent<AspectRatioFitter>();
        camFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        camView.enabled = false;

        // 3D 경희몬 (투명 배경 렌더 텍스처)
        arView = new GameObject("Monster", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        arView.transform.SetParent(root.transform, false);
        arView.raycastTarget = false;
        UIKit.Stretch(arView.rectTransform);

        // 대체 모드: 끌어서 둘러보기 (자이로가 있어도 좌우 미세 조정용) / AR 모드: 누른 바닥으로 경희몬 옮기기
        Image dragArea = UIKit.Image(root.transform, "Drag", new Color(0f, 0f, 0f, 0f), raycast: true);
        UIKit.Stretch(dragArea.rectTransform);
        var drag = dragArea.gameObject.AddComponent<CatchDragArea>();
        drag.onDrag = d =>
        {
            if (arMode)
                return;
            yaw -= d.x * 0.15f;
            pitch = Mathf.Clamp(pitch + d.y * 0.15f, -60f, 60f);
        };
        drag.onTap = screenPos =>
        {
            if (arMode && ar != null && !busy && ar.Raycast(screenPos, out Pose p))
                PlaceAt(p.position);
        };

        // 버튼·글자 (사진에는 안 찍힘)
        RectTransform ui = UIKit.Rect(root.transform, "Controls");
        UIKit.Stretch(ui);
        controls = ui.gameObject.AddComponent<CanvasGroup>();
        Rect sa = Screen.safeArea;
        RectTransform safe = UIKit.Rect(ui, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        Image titleBg = UIKit.Image(safe, "Title", new Color(0.05f, 0.08f, 0.18f, 0.6f));
        titleBg.sprite = HudIcons.Pill;
        titleBg.type = Image.Type.Sliced;
        titleBg.pixelsPerUnitMultiplier = 120f / 100f;
        RectTransform tr = titleBg.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -40f);
        tr.sizeDelta = new Vector2(820f, 100f);
        title = UIKit.Text(titleBg.transform, "", 38, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        UIKit.Stretch(title.rectTransform, 24f, 0f);

        note = UIKit.Text(safe, "", 28, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
        RectTransform nr = note.rectTransform;
        nr.anchorMin = new Vector2(0f, 1f);
        nr.anchorMax = new Vector2(1f, 1f);
        nr.pivot = new Vector2(0.5f, 1f);
        nr.offsetMin = new Vector2(160f, -210f);
        nr.offsetMax = new Vector2(-160f, -150f);

        Button close = RoundIconButton(safe, "Close", HudIcons.Close, 110f, Close);
        Place((RectTransform)close.transform, new Vector2(0f, 1f), new Vector2(40f, -170f));

        hint = UIKit.Text(safe, "", 36, TextAnchor.MiddleCenter);
        hint.fontStyle = FontStyle.Bold;
        var hintShadow = hint.gameObject.AddComponent<Outline>();
        hintShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        hintShadow.effectDistance = new Vector2(2f, -2f);
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.offsetMin = new Vector2(40f, 290f);
        hr.offsetMax = new Vector2(-40f, 370f);

        // 셔터
        Button shutter = UIKit.Image(safe, "Shutter", new Color(0f, 0f, 0f, 0f), raycast: true).gameObject.AddComponent<Button>();
        shutter.transition = Selectable.Transition.None;
        shutter.onClick.AddListener(Shoot);
        shutter.gameObject.AddComponent<PressScale>();
        RectTransform sr = (RectTransform)shutter.transform;
        Place(sr, new Vector2(0.5f, 0f), new Vector2(0f, 80f));
        sr.sizeDelta = new Vector2(190f, 190f);
        Image ring = Circle(sr, "Ring", Color.white, 190f);
        Place(ring.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        Image gap = Circle(ring.transform, "Gap", new Color(0f, 0f, 0f, 0.35f), 166f);
        Place(gap.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        shutterCore = Circle(gap.transform, "Core", Color.white, 148f);
        Place(shutterCore.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);

        // 셔터 번쩍임
        flash = UIKit.Image(root.transform, "Flash", new Color(1f, 1f, 1f, 0f));
        UIKit.Stretch(flash.rectTransform);

        BuildResultCard();
        root.SetActive(false);
    }

    void BuildResultCard()
    {
        Image dim = UIKit.Image(root.transform, "Result", new Color(0.03f, 0.05f, 0.12f, 0.7f), raycast: true);
        UIKit.Stretch(dim.rectTransform);
        resultCard = dim.gameObject;

        Image card = UIKit.Image(dim.transform, "Card", new Color(0.98f, 0.99f, 1f));
        card.sprite = HudIcons.Pill;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 1.4f;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        card.rectTransform.sizeDelta = new Vector2(900f, 1400f);

        RectTransform frame = UIKit.Rect(card.transform, "PhotoFrame");
        frame.anchorMin = new Vector2(0f, 1f);
        frame.anchorMax = new Vector2(1f, 1f);
        frame.pivot = new Vector2(0.5f, 1f);
        frame.offsetMin = new Vector2(50f, -900f);
        frame.offsetMax = new Vector2(-50f, -50f);
        frame.gameObject.AddComponent<RectMask2D>();
        var photo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        photo.transform.SetParent(frame, false);
        resultPhoto = photo.GetComponent<RawImage>();
        resultPhoto.raycastTarget = false;
        resultFitter = photo.GetComponent<AspectRatioFitter>();
        resultFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        resultTitle = UIKit.Text(card.transform, "", 50, TextAnchor.MiddleCenter, Navy);
        resultTitle.fontStyle = FontStyle.Bold;
        TopBox(resultTitle.rectTransform, 930f, 80f);
        resultSub = UIKit.Text(card.transform, "", 32, TextAnchor.MiddleCenter, new Color(0.4f, 0.45f, 0.5f));
        TopBox(resultSub.rectTransform, 1010f, 110f);

        albumButton = PillButton(card.transform, "앨범에도 저장", new Color(0.88f, 0.92f, 0.96f), Navy, SaveToAlbum);
        Place((RectTransform)albumButton.transform, new Vector2(0.5f, 0f), new Vector2(-190f, 50f));
        ((RectTransform)albumButton.transform).sizeDelta = new Vector2(360f, 116f);
        Button ok = PillButton(card.transform, "확인", Navy, Color.white, Close);
        Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(200f, 50f));
        ((RectTransform)ok.transform).sizeDelta = new Vector2(320f, 116f);
    }

    // ---------- 3D 경희몬 무대 ----------

    /// <summary>
    /// 경희몬과 조명을 만든다. withCamera 면 대체 모드용 카메라(투명 렌더 텍스처)도 만든다.
    /// AR 모드에서는 AR 카메라가 같은 레이어를 화면에 바로 그린다.
    /// </summary>
    void BuildStage(bool withCamera)
    {
        int layer = MonsterThumbnails.StageLayer;
        stage = new GameObject("CatchStage");
        stage.transform.position = StagePos;
        if (withCamera)
            BuildFallbackCamera(layer);
        BuildLightAndMonster(layer);
    }

    void BuildFallbackCamera(int layer)
    {
        var camGo = new GameObject("CatchCamera", typeof(Camera));
        camGo.transform.SetParent(stage.transform, false);
        arCam = camGo.GetComponent<Camera>();
        arCam.clearFlags = CameraClearFlags.SolidColor;
        arCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        arCam.cullingMask = 1 << layer;
        arCam.fieldOfView = cameraFov;
        arCam.nearClipPlane = 0.05f;
        arCam.farClipPlane = 100f;
        int h = Mathf.Min(Screen.height, 2400);
        int w = Mathf.Max(1, Mathf.RoundToInt(h * (float)Screen.width / Mathf.Max(1, Screen.height)));
        arTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "CatchView", antiAliasing = 4 };
        arCam.targetTexture = arTexture;
        arView.texture = arTexture;
    }

    void BuildLightAndMonster(int layer)
    {
        var lightGo = new GameObject("CatchLight", typeof(Light));
        lightGo.transform.SetParent(stage.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        arLight = lightGo.GetComponent<Light>();
        arLight.type = LightType.Directional;
        arLight.intensity = 1.3f;
        arLight.shadows = LightShadows.None;
        arLight.cullingMask = 1 << layer;

        float size = arMode ? arMonsterSize : monsterSize;
        // 지도의 수집 대상은 늘 동물 피규어다 (모델이 없는 대상은 지도에 나오지 않는다).
        CreatureLibrary.TryGetMesh(speciesId, out Mesh body);
        monster = BuildCreature(body, size * 1.4f, layer);
        monster.gameObject.SetActive(false);

        // 바닥 그림자 (AR 에서 땅에 서 있는 느낌)
        var sh = new GameObject("Shadow", typeof(SpriteRenderer));
        sh.layer = layer;
        sh.transform.SetParent(stage.transform, false);
        var sr = sh.GetComponent<SpriteRenderer>();
        sr.sprite = HudIcons.SoftDisk;
        sr.color = new Color(0f, 0f, 0f, 0.45f);
        float w = size * 1.3f / (HudIcons.SoftDisk.rect.width / HudIcons.SoftDisk.pixelsPerUnit);
        sh.transform.localScale = new Vector3(w, w, 1f);
        shadow = sh.transform;
        shadow.gameObject.SetActive(false);
    }

    /// <summary>동물 피규어를 높이 height 로 세운다. 피벗은 피규어 가운데 (도형과 같게).</summary>
    Transform BuildCreature(Mesh body, float height, int layer)
    {
        var pivot = new GameObject("Monster_" + speciesId).transform;
        pivot.SetParent(stage.transform, false);
        Bounds b = body.bounds;
        float k = height / Mathf.Max(b.size.y, 1e-4f);
        var go = new GameObject("Figure", typeof(MeshFilter), typeof(MeshRenderer));
        go.layer = layer;
        go.transform.SetParent(pivot, false);
        go.transform.localScale = Vector3.one * k;
        go.transform.localPosition = -b.center * k;
        go.GetComponent<MeshFilter>().sharedMesh = body;
        go.GetComponent<MeshRenderer>().sharedMaterial = CreatureLibrary.Material;
        if (CreatureLibrary.TryGetGlass(speciesId, out Mesh glass) && CreatureLibrary.GlassMaterial != null)
        {
            var g = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
            g.layer = layer;
            g.transform.SetParent(go.transform, false);
            g.GetComponent<MeshFilter>().sharedMesh = glass;
            g.GetComponent<MeshRenderer>().sharedMaterial = CreatureLibrary.GlassMaterial;
        }
        monsterHalf = height * 0.5f;
        return pivot;
    }

    /// <summary>AR: 바닥 위 point 에 경희몬을 세운다 (다시 부르면 옮긴다).</summary>
    void PlaceAt(Vector3 point)
    {
        monsterBase = point;
        monster.gameObject.SetActive(true);
        shadow.gameObject.SetActive(true);
        shadow.SetPositionAndRotation(point + Vector3.up * 0.002f, Quaternion.Euler(90f, 0f, 0f));
        if (!monsterPlaced)
            note.text = "화면을 누르면 그 바닥으로 옮길 수 있어요";
        monsterPlaced = true;
        phase = 0f;
    }

    /// <summary>지금 카메라가 보는 수평 방향 앞쪽에 경희몬을 둔다.</summary>
    void PlaceMonster()
    {
        Vector3 fwd = arCam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f)
            fwd = Vector3.forward;
        fwd.Normalize();
        monsterBase = arCam.transform.position + fwd * monsterDistance + Vector3.down * 0.35f;
        monster.gameObject.SetActive(true);
        monsterPlaced = true;
    }

    // ---------- 자이로 ----------

    bool gyroOn;

    void EnableGyro()
    {
        gyroOn = false;
#if ENABLE_INPUT_SYSTEM
        if (AttitudeSensor.current != null)
        {
            InputSystem.EnableDevice(AttitudeSensor.current);
            gyroOn = true;
        }
#else
        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = true;
            gyroOn = true;
        }
#endif
    }

    bool TryGyro(out Quaternion rot)
    {
        rot = Quaternion.identity;
        if (!gyroOn)
            return false;
#if ENABLE_INPUT_SYSTEM
        if (AttitudeSensor.current == null || !AttitudeSensor.current.enabled)
            return false;
        Quaternion q = AttitudeSensor.current.attitude.ReadValue();
#else
        Quaternion q = Input.gyro.attitude;
#endif
        if (q == Quaternion.identity || (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f))
            return false;
        // 기기 좌표(오른손) → Unity 좌표(왼손), 폰을 세워 든 자세가 정면이 되도록
        rot = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
        return true;
    }

    // ---------- 카메라 영상 ----------

    IEnumerator StartCamera()
    {
        camView.enabled = false;
        fallbackBg.enabled = true;
        if (Application.isEditor && !useWebcamInEditor)
        {
            note.text = "에디터에서는 카메라 대신 배경을 보여 줘요 · 끌어서 둘러보기";
            yield break;
        }
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            float wait = 10f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && wait > 0f) { wait -= Time.unscaledDeltaTime; yield return null; }
        }
#elif UNITY_IOS
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#endif
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam) || WebCamTexture.devices.Length == 0)
        {
            note.text = "카메라를 쓸 수 없어요. 설정에서 KUCA의 카메라 권한을 켜 주세요";
            yield break;
        }
        string device = WebCamTexture.devices[0].name;
        foreach (WebCamDevice d in WebCamTexture.devices)
            if (!d.isFrontFacing) { device = d.name; break; }
        webcam = new WebCamTexture(device, 1920, 1080, 30);
        webcam.Play();
        float timeout = 5f;
        while (webcam.width <= 16 && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        camView.texture = webcam;
        camView.enabled = true;
        fallbackBg.enabled = false;
    }

    // ---------- 매 프레임 ----------

    void Update()
    {
        if (!IsOpen)
            return;
        if (arMode)
        {
            UpdateAR();
            UpdateAimAndFlash();
            return;
        }
        if (arCam == null)
            return;

        if (webcam != null && webcam.isPlaying && webcam.width > 16)
            PhotoCaptureScreen.ShowCameraPreview(camView, camFitter, webcam, fill: true);

        Quaternion look = Quaternion.Euler(-pitch, yaw, 0f);
        if (TryGyro(out Quaternion g))
            look = Quaternion.Euler(0f, yaw, 0f) * g;
        arCam.transform.localRotation = look;

        // 자이로 값이 들어올 시간을 조금 준 뒤 앞쪽에 세운다.
        if (!monsterPlaced && Time.unscaledTime - openTime > 0.25f)
            PlaceMonster();

        if (monsterPlaced)
        {
            phase += Time.deltaTime;
            Vector3 right = Vector3.Cross(Vector3.up, (monsterBase - arCam.transform.position).normalized);
            monster.position = monsterBase + right * Mathf.Sin(phase * 0.7f) * 0.45f + Vector3.up * Mathf.Sin(phase * 2.2f) * 0.08f;
            monster.rotation = Quaternion.Euler(15f, phase * 50f, 0f);
        }
        UpdateAimAndFlash();
    }

    void UpdateAR()
    {
        if (ar == null || ar.Camera == null || monster == null)
            return;
        Transform cam = ar.Camera.transform;
        if (!monsterPlaced)
        {
            // 화면 가운데가 가리키는 바닥에 세운다. 오래 못 찾으면 앞쪽 발밑 높이에 세운다.
            if (ar.Raycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f), out Pose p))
                PlaceAt(p.position);
            else if (Time.unscaledTime > placeDeadline)
            {
                Vector3 fwd = cam.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                PlaceAt(cam.position + fwd * 2f + Vector3.down * 1f);
                note.text = "바닥을 찾지 못해 앞쪽에 세웠어요 · 화면을 누르면 옮겨요";
            }
            return;
        }

        // 바닥 위에서 통통 뛰며 카메라 쪽을 바라본다.
        phase += Time.deltaTime;
        float hop = Mathf.Abs(Mathf.Sin(phase * 3f)) * arMonsterSize * 0.25f;
        monster.position = monsterBase + Vector3.up * (monsterHalf + hop);
        Vector3 toCam = cam.position - monsterBase;
        toCam.y = 0f;
        float face = toCam.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(toCam).eulerAngles.y : 0f;
        monster.rotation = Quaternion.Euler(0f, face + Mathf.Sin(phase * 1.3f) * 20f, 0f);
        // 높이 뛸수록 그림자가 옅어진다.
        float lift = hop / (arMonsterSize * 0.25f + 1e-4f);
        shadow.GetComponent<SpriteRenderer>().color = new Color(0f, 0f, 0f, 0.45f * (1f - 0.35f * lift));
        ar.SetPlanesVisible(false);
    }

    void UpdateAimAndFlash()
    {
        bool inFrame = InFrame(out Vector3 vp);
        shutterCore.color = Color.Lerp(shutterCore.color, inFrame ? Gold : Color.white, Time.unscaledDeltaTime * 8f);
        if (!busy && monsterPlaced && Time.unscaledTime > hintHoldUntil)
            hint.text = inFrame ? "지금이에요! 셔터를 눌러 함께 찍어 보세요" : DirectionHint(vp);

        if (flashAlpha > 0f)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 2.5f);
            flash.color = new Color(1f, 1f, 1f, flashAlpha);
        }
    }

    /// <summary>경희몬이 화면 안쪽(가장자리 8% 제외)에 보이는지</summary>
    bool InFrame(out Vector3 vp)
    {
        vp = Vector3.zero;
        if (!monsterPlaced)
            return false;
        Camera c = ViewCam;
        if (c == null)
            return false;
        vp = c.WorldToViewportPoint(monster.position);
        return vp.z > 0f && vp.x > 0.08f && vp.x < 0.92f && vp.y > 0.12f && vp.y < 0.9f;
    }

    static string DirectionHint(Vector3 vp)
    {
        if (vp.z <= 0f)
            return "경희몬이 뒤쪽에 있어요. 몸을 돌려 찾아보세요";
        if (vp.x <= 0.08f) return "← 왼쪽에 있어요";
        if (vp.x >= 0.92f) return "오른쪽에 있어요 →";
        return vp.y <= 0.12f ? "↓ 아래쪽에 있어요" : "↑ 위쪽에 있어요";
    }

    // ---------- 찍기 ----------

    void Shoot()
    {
        if (busy)
            return;
        if (!InFrame(out _))
        {
            hint.text = "경희몬이 화면에 들어오게 찍어야 잡을 수 있어요!";
            hintHoldUntil = Time.unscaledTime + 1.5f;
            return;
        }
        StartCoroutine(Capture());
    }

    IEnumerator Capture()
    {
        busy = true;
        controls.alpha = 0f;
        controls.blocksRaycasts = false;
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        controls.alpha = 1f;
        flashAlpha = 1f;
        flash.color = Color.white;

        shot = PhotoCaptureScreen.Downscale(shot, maxPhotoSize);
        string file = $"{DateTime.Now:yyyyMMdd_HHmmss}_{type.id}.jpg";
        string path = Path.Combine(GameProgress.ScrapbookDir, file);
        try
        {
            Directory.CreateDirectory(GameProgress.ScrapbookDir);
            File.WriteAllBytes(path, shot.EncodeToJPG(jpgQuality));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CatchCamera] 사진을 저장하지 못했습니다: {e.Message}");
            file = "";
        }

        GameProgress.Caught caught = owner != null ? owner.CompleteCatch(type, target, file, speciesId) : null;
        target = null;
        lastPhoto = shot;
        lastPhotoPath = string.IsNullOrEmpty(file) ? null : path;

        yield return new WaitForSecondsRealtime(0.35f);
        ShowResult(caught);
    }

    void ShowResult(GameProgress.Caught caught)
    {
        monster.gameObject.SetActive(false);
        resultPhoto.texture = lastPhoto;
        resultFitter.aspectRatio = lastPhoto != null && lastPhoto.height > 0 ? (float)lastPhoto.width / lastPhoto.height : 0.5625f;
        resultTitle.text = $"{displayName}{Josa(displayName, "을", "를")} 잡았다!";
        resultSub.text = caught != null
            ? $"쿠옹력 {caught.cp}  ·  +{type.points} XP\n사진은 프로필 › 스크랩북에 저장됐어요"
            : "사진을 저장했어요";
        albumButton.interactable = lastPhotoPath != null;
        UIKit.SetLabel(albumButton, "앨범에도 저장");
        resultCard.SetActive(true);
        resultCard.transform.SetAsLastSibling();
        controls.blocksRaycasts = false;
    }

    void SaveToAlbum()
    {
        if (lastPhotoPath == null)
            return;
        NativeGallery.SaveImageToGallery(lastPhotoPath, "KUCA", Path.GetFileName(lastPhotoPath), (success, _) =>
        {
            UIKit.SetLabel(albumButton, success ? "앨범에 저장됨" : "저장하지 못했어요");
            albumButton.interactable = !success;
        });
    }

    // ---------- 도구 ----------

    /// <summary>받침이 있으면 withFinal(이/을), 없으면 withoutFinal(가/를)</summary>
    public static string Josa(string word, string withFinal, string withoutFinal)
    {
        if (string.IsNullOrEmpty(word))
            return withFinal;
        char c = word[word.Length - 1];
        bool hasFinal = c >= 0xAC00 && c <= 0xD7A3 && (c - 0xAC00) % 28 != 0;
        return hasFinal ? withFinal : withoutFinal;
    }

    static Button RoundIconButton(Transform parent, string name, Sprite icon, float size, UnityEngine.Events.UnityAction onClick)
    {
        Image bg = UIKit.Image(parent, name, new Color(0.05f, 0.08f, 0.18f, 0.55f), raycast: true);
        bg.sprite = HudIcons.Circle;
        bg.rectTransform.sizeDelta = new Vector2(size, size);
        var b = bg.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        bg.gameObject.AddComponent<PressScale>();
        Image ic = UIKit.Image(bg.transform, "Icon", Color.white);
        ic.sprite = icon;
        UIKit.Stretch(ic.rectTransform, size * 0.25f, size * 0.25f);
        return b;
    }

    static Button PillButton(Transform parent, string label, Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, bg, 40);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.1f;
        b.targetGraphic = img;
        Text t = b.GetComponentInChildren<Text>();
        t.color = fg;
        t.fontStyle = FontStyle.Bold;
        return b;
    }

    static Image Circle(Transform parent, string name, Color color, float size)
    {
        Image img = UIKit.Image(parent, name, color);
        img.sprite = HudIcons.Circle;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    static void TopBox(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(30f, -top - height);
        rt.offsetMax = new Vector2(-30f, -top);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsOpen = false;
}

/// <summary>화면을 끈 만큼(픽셀)과 누른 자리를 알려 준다.</summary>
public class CatchDragArea : MonoBehaviour, IDragHandler, IPointerClickHandler
{
    public Action<Vector2> onDrag;
    /// <summary>끌지 않고 눌렀을 때 (화면 좌표)</summary>
    public Action<Vector2> onTap;
    public void OnDrag(PointerEventData e) => onDrag?.Invoke(e.delta);
    public void OnPointerClick(PointerEventData e)
    {
        if (!e.dragging)
            onTap?.Invoke(e.position);
    }
}
