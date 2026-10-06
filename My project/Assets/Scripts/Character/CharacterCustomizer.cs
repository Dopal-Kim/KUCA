using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 캐릭터 꾸미기 화면. 성별과 부위별 선택지를 좌우 버튼으로 바꾸면 Player 캐릭터에 바로 반영된다.
/// 위쪽에는 캐릭터 레이어만 찍는 미리보기 카메라 화면을 보여 주고, 드래그로 돌려 볼 수 있다.
/// 저장된 캐릭터가 없으면(첫 실행) 자동으로 열리고, 이후에는 왼쪽 아래 '캐릭터' 버튼으로 연다.
/// </summary>
public class CharacterCustomizer : MonoBehaviour
{
    public ModularCharacter character;

    [Header("Preview")]
    [Tooltip("미리보기 카메라 거리 (캐릭터 로컬 단위, 키 약 1.7)")]
    public float previewDistance = 4.5f;
    [Tooltip("미리보기 카메라가 바라보는 높이 (캐릭터 로컬 단위)")]
    public float previewLookHeight = 0.9f;
    public float previewFov = 30f;
    public float previewRotateSpeed = 0.4f;

    public static bool IsOpen { get; private set; }

    static readonly (CharacterSlot slot, string label)[] SlotRows =
    {
        (CharacterSlot.Head, "머리"),
        (CharacterSlot.Body, "상의"),
        (CharacterSlot.Legs, "하의"),
        (CharacterSlot.Feet, "신발"),
        (CharacterSlot.Accessory, "가방"),
    };

    CharacterAppearance editing;
    GameObject screen;
    GameObject panel;
    Camera previewCam;
    Light previewLight;
    RenderTexture previewTexture;
    float previewYaw = 180f;
    GameObject openButton;
    Text genderValue;
    readonly Dictionary<CharacterSlot, Text> slotValues = new Dictionary<CharacterSlot, Text>();
    Font font;

    CharacterPartsDatabase Db => character != null ? character.database : null;

    void Start()
    {
        EnsureEventSystem();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();

        CharacterAppearance saved = CharacterAppearance.Load();
        if (saved != null)
        {
            character.Build(saved);
            SetOpen(false);
        }
        else
        {
            character.Build(RandomAppearance());
            SetOpen(true);
        }
    }

    void OnDestroy()
    {
        IsOpen = false;
        UIInputBlocker.SetModal(this, false);
        if (previewTexture != null)
            previewTexture.Release();
    }

    void LateUpdate()
    {
        if (!IsOpen || previewCam == null || character == null)
            return;
        // 캐릭터 정면 기준으로 previewYaw 만큼 돈 위치에서 가슴 높이를 바라본다.
        Transform c = character.transform;
        Vector3 look = c.TransformPoint(0f, previewLookHeight, 0f);
        Vector3 local = Quaternion.Euler(0f, previewYaw, 0f) * new Vector3(0f, 0f, previewDistance);
        Vector3 pos = c.TransformPoint(local + Vector3.up * (previewLookHeight + 0.15f));
        previewCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        previewLight.transform.rotation = previewCam.transform.rotation * Quaternion.Euler(20f, -25f, 0f);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        UIInputBlocker.SetModal(this, open);
        screen.SetActive(open);
        openButton.SetActive(!open);
        previewCam.enabled = open;
        previewLight.enabled = open;
        if (open)
        {
            editing = character.Appearance != null ? character.Appearance.Clone() : RandomAppearance();
            previewYaw = 0f;
            Refresh();
        }
    }

    void Confirm()
    {
        character.Appearance.Save();
        SetOpen(false);
    }

    void Apply()
    {
        character.Build(editing);
        editing = character.Appearance.Clone();
        Refresh();
    }

    void CycleGender(int dir)
    {
        int n = Db.genders.Count;
        editing.gender = ((editing.gender + dir) % n + n) % n;
        Apply();
    }

    void Cycle(CharacterSlot slot, int dir)
    {
        List<CharacterPartsDatabase.PartOption> options = Db.genders[editing.gender].Options(slot);
        if (options.Count == 0)
            return;
        int i = Mathf.Max(0, options.FindIndex(o => o.id == editing.Get(slot)));
        i = ((i + dir) % options.Count + options.Count) % options.Count;
        editing.Set(slot, options[i].id);
        Apply();
    }

    CharacterAppearance RandomAppearance()
    {
        var a = new CharacterAppearance { gender = Random.Range(0, Db.genders.Count) };
        CharacterPartsDatabase.GenderSet set = Db.genders[a.gender];
        // 머리·상의·하의·신발은 같은 의상 세트에서 시작해 어울리게 만든다.
        string outfit = set.bodies[Random.Range(0, set.bodies.Count)].id;
        foreach (var (slot, _) in SlotRows)
        {
            List<CharacterPartsDatabase.PartOption> options = set.Options(slot);
            if (options.Count == 0)
                continue;
            var match = options.Find(o => o.id == outfit);
            a.Set(slot, slot == CharacterSlot.Accessory ? options[0].id : (match ?? options[0]).id);
        }
        return a;
    }

    void Randomize()
    {
        CharacterAppearance a = new CharacterAppearance { gender = editing.gender };
        CharacterPartsDatabase.GenderSet set = Db.genders[a.gender];
        foreach (var (slot, _) in SlotRows)
        {
            List<CharacterPartsDatabase.PartOption> options = set.Options(slot);
            if (options.Count > 0)
                a.Set(slot, options[Random.Range(0, options.Count)].id);
        }
        editing = a;
        Apply();
    }

    void Refresh()
    {
        CharacterPartsDatabase.GenderSet set = Db.genders[editing.gender];
        genderValue.text = set.displayName;
        foreach (var (slot, _) in SlotRows)
        {
            var o = set.Options(slot).Find(x => x.id == editing.Get(slot));
            slotValues[slot].text = o != null ? o.displayName : "-";
        }
    }

    // ---------- UI 생성 ----------

    void BuildUI()
    {
        var canvasGo = new GameObject("CustomizerCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // 열기 버튼 (왼쪽 아래)
        openButton = CreateButton(canvasGo.transform, "캐릭터", () => SetOpen(true), new Color(0.15f, 0.45f, 1f, 0.9f), 44);
        var ob = (RectTransform)openButton.transform;
        ob.anchorMin = ob.anchorMax = ob.pivot = new Vector2(0f, 0f);
        ob.anchoredPosition = new Vector2(30f, 330f);
        ob.sizeDelta = new Vector2(240f, 120f);
        UIInputBlocker.Register(ob);

        // 꾸미기 화면 전체: 어두운 배경 + 위쪽 미리보기 + 아래쪽 패널
        screen = new GameObject("Screen", typeof(RectTransform), typeof(Image));
        screen.transform.SetParent(canvasGo.transform, false);
        screen.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.96f);
        var sr = (RectTransform)screen.transform;
        sr.anchorMin = Vector2.zero;
        sr.anchorMax = Vector2.one;
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        UIInputBlocker.Register(sr);

        BuildPreview(screen.transform);

        // 아래쪽 패널
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(screen.transform, false);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.88f);
        var pr = (RectTransform)panel.transform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = Vector2.zero;
        pr.sizeDelta = new Vector2(0f, PanelHeight);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 36, 60);
        layout.spacing = 14f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        Text title = CreateText(panel.transform, "캐릭터 꾸미기", 52, TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;
        SetHeight(title.gameObject, 80f);

        genderValue = CreateRow(panel.transform, "성별", () => CycleGender(-1), () => CycleGender(1));
        foreach (var (slot, label) in SlotRows)
        {
            CharacterSlot s = slot;
            slotValues[slot] = CreateRow(panel.transform, label, () => Cycle(s, -1), () => Cycle(s, 1));
        }

        var bottom = new GameObject("Buttons", typeof(RectTransform));
        bottom.transform.SetParent(panel.transform, false);
        SetHeight(bottom, 120f);
        var h = bottom.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;
        CreateButton(bottom.transform, "랜덤", Randomize, new Color(0.3f, 0.32f, 0.38f), 44);
        CreateButton(bottom.transform, "완료", Confirm, new Color(0.15f, 0.45f, 1f), 44);
    }

    const float PanelHeight = 900f;

    void BuildPreview(Transform parent)
    {
        previewTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { name = "CharacterPreview", antiAliasing = 2 };

        var camGo = new GameObject("CharacterPreviewCamera", typeof(Camera));
        camGo.transform.SetParent(transform, false);
        previewCam = camGo.GetComponent<Camera>();
        previewCam.targetTexture = previewTexture;
        previewCam.clearFlags = CameraClearFlags.SolidColor;
        previewCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        previewCam.cullingMask = 1 << character.gameObject.layer;
        previewCam.fieldOfView = previewFov;
        previewCam.nearClipPlane = 1f;
        previewCam.farClipPlane = 500f;
        previewCam.enabled = false;

        // 미리보기에서 캐릭터 정면을 밝혀 주는 보조 조명 (꾸미기 화면이 열렸을 때만)
        var lightGo = new GameObject("CharacterPreviewLight", typeof(Light));
        lightGo.transform.SetParent(transform, false);
        previewLight = lightGo.GetComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 0.8f;
        previewLight.shadows = LightShadows.None;
        previewLight.cullingMask = 1 << character.gameObject.layer;
        previewLight.enabled = false;

        // 패널 위쪽 영역 (정사각형으로 맞춰 가운데 배치)
        var area = new GameObject("PreviewArea", typeof(RectTransform), typeof(Image), typeof(PreviewDragArea));
        area.transform.SetParent(parent, false);
        area.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 드래그 입력만 받는 투명 영역
        area.GetComponent<PreviewDragArea>().onDragX = dx => previewYaw += dx * previewRotateSpeed;
        var ar = (RectTransform)area.transform;
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(0f, PanelHeight);
        ar.offsetMax = new Vector2(0f, -40f);

        var img = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        img.transform.SetParent(area.transform, false);
        var raw = img.GetComponent<RawImage>();
        raw.texture = previewTexture;
        raw.raycastTarget = false;
        var fitter = img.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;

        Text hint = CreateText(area.transform, "좌우로 드래그해서 돌려 보기", 32, TextAnchor.LowerCenter);
        hint.color = new Color(1f, 1f, 1f, 0.6f);
        var hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(0f, 60f);
        hr.anchoredPosition = new Vector2(0f, 10f);
    }

    Text CreateRow(Transform parent, string label, UnityEngine.Events.UnityAction prev, UnityEngine.Events.UnityAction next)
    {
        var row = new GameObject(label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetHeight(row, 100f);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 16f;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        Text l = CreateText(row.transform, label, 40, TextAnchor.MiddleLeft);
        SetWidth(l.gameObject, 150f, 0f);
        SetWidth(CreateButton(row.transform, "<", prev, new Color(0.25f, 0.27f, 0.33f), 48), 120f, 0f);
        Text value = CreateText(row.transform, "", 40, TextAnchor.MiddleCenter);
        SetWidth(value.gameObject, 0f, 1f);
        SetWidth(CreateButton(row.transform, ">", next, new Color(0.25f, 0.27f, 0.33f), 48), 120f, 0f);
        return value;
    }

    GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, Color color, int fontSize)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        go.GetComponent<Button>().onClick.AddListener(onClick);
        Text t = CreateText(go.transform, label, fontSize, TextAnchor.MiddleCenter);
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    Text CreateText(Transform parent, string text, int size, TextAnchor anchor)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.text = text;
        t.raycastTarget = false;
        return t;
    }

    static void SetHeight(GameObject go, float height)
    {
        if (!go.TryGetComponent(out LayoutElement le))
            le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
        le.minHeight = height;
    }

    static void SetWidth(GameObject go, float width, float flexible)
    {
        if (!go.TryGetComponent(out LayoutElement le))
            le = go.AddComponent<LayoutElement>();
        le.preferredWidth = width;
        le.minWidth = width;
        le.flexibleWidth = flexible;
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }
}
