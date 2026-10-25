using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 캐릭터 꾸미기 화면 (거울). 위쪽은 캐릭터만 찍는 미리보기 (드래그로 돌려 보기), 아래쪽은 탭 세 개.
/// - 얼굴: 눈 · 눈썹 · 입 · 코 · 볼 모양 고르기 + 눈 크기 · 간격 · 높이 슬라이더 (얼굴 가까이 확대)
/// - 색: 피부 8 · 머리 8 · 눈 6 색 칸
/// - 옷: 성별 · 머리 · 상의 · 하의 · 신발 · 가방
/// 얼굴 · 색 · 슬라이더는 캐릭터를 다시 만들지 않고 바로 입혀 (CharacterLook) 움직이는 대로 미리보기에 보인다.
/// 저장된 캐릭터가 없으면(첫 실행) 자동으로 열리고, 이후에는 HUD 왼쪽 위 프로필을 눌러 연다.
/// </summary>
public class CharacterCustomizer : MonoBehaviour
{
    public ModularCharacter character;

    [Header("Preview")]
    [Tooltip("미리보기 카메라 거리 (캐릭터 로컬 단위, 키 약 1.7) — 옷 탭")]
    public float previewDistance = 4.5f;
    [Tooltip("미리보기 카메라가 바라보는 높이 (캐릭터 로컬 단위) — 옷 탭")]
    public float previewLookHeight = 0.9f;
    [Tooltip("얼굴 · 색 탭에서 얼굴 가까이 갈 때의 거리와 높이")]
    public float faceDistance = 2.1f;
    public float faceLookHeight = 1.3f;
    public float previewFov = 30f;
    public float previewRotateSpeed = 0.4f;

    public static bool IsOpen { get; private set; }

    enum Tab { Face, Color, Clothes }
    static readonly string[] TabNames = { "얼굴", "색", "옷" };

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
    float zoom;   // 0 = 전신, 1 = 얼굴
    Tab tab;
    Text genderValue;
    readonly Dictionary<CharacterSlot, Text> slotValues = new Dictionary<CharacterSlot, Text>();
    readonly Dictionary<CharacterLook.Part, Text> faceValues = new Dictionary<CharacterLook.Part, Text>();
    readonly Slider[] eyeSliders = new Slider[3];
    readonly GameObject[] pages = new GameObject[3];
    readonly Image[] tabButtons = new Image[3];
    readonly List<(Image ring, System.Func<bool> selected)> swatchRings = new List<(Image, System.Func<bool>)>();
    readonly Dictionary<string, Text> swatchNames = new Dictionary<string, Text>();
    GameObject faceRows;
    Text faceNote;
    Font font;

    CharacterPartsDatabase Db => character != null ? character.database : null;

    void Start()
    {
        EnsureEventSystem();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();

        CharacterAppearance saved = CharacterAppearance.Load();
        if (saved != null && saved.version < CharacterAppearance.CurrentVersion)
        {
            // 기본 캐릭터가 경희 모험가로 바뀌기 전에 저장한 모습은 성별만 두고 새 기본 옷으로
            saved = DefaultAppearance(saved.gender);
            saved.Save();
        }
        if (saved != null)
        {
            character.Build(saved);
            SetOpen(false);
        }
        else
        {
            character.Build(DefaultAppearance(AuthService.Current?.gender == "female" ? 1 : 0));
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
        // 얼굴 · 색 탭에서는 얼굴 가까이, 옷 탭에서는 전신 (부드럽게 옮겨 간다)
        zoom = Mathf.MoveTowards(zoom, tab == Tab.Clothes ? 0f : 1f, Time.unscaledDeltaTime * 3f);
        float k = Mathf.SmoothStep(0f, 1f, zoom);
        float distance = Mathf.Lerp(previewDistance, faceDistance, k);
        float height = Mathf.Lerp(previewLookHeight, faceLookHeight, k);
        // 캐릭터 정면 기준으로 previewYaw 만큼 돈 위치에서 바라본다.
        Transform c = character.transform;
        Vector3 look = c.TransformPoint(0f, height, 0f);
        Vector3 local = Quaternion.Euler(0f, previewYaw, 0f) * new Vector3(0f, 0f, distance);
        Vector3 pos = c.TransformPoint(local + Vector3.up * (height + 0.15f * (1f - k)));
        previewCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        previewLight.transform.rotation = previewCam.transform.rotation * Quaternion.Euler(20f, -25f, 0f);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        UIInputBlocker.SetModal(this, open);
        screen.SetActive(open);
        previewCam.enabled = open;
        previewLight.enabled = open;
        if (open)
        {
            editing = character.Appearance != null ? character.Appearance.Clone() : RandomAppearance();
            previewYaw = 0f;
            SetTab(Tab.Face);
            zoom = 1f;
            Refresh();
        }
    }

    void Confirm()
    {
        character.Build(editing);   // 새 Appearance → HUD 초상화도 한 번 다시 만든다
        character.Appearance.Save();
        SetOpen(false);
    }

    void Apply()
    {
        character.Build(editing);
        editing = character.Appearance.Clone();
        Refresh();
    }

    /// <summary>얼굴 · 색 · 슬라이더: 다시 만들지 않고 바로 입힌다</summary>
    void ApplyLook()
    {
        character.ApplyLook(editing);
        Refresh();
    }

    void CycleFace(CharacterLook.Part part, int dir)
    {
        int n = CharacterLook.Shapes[(int)part].Length;
        CharacterLook.Set(editing, part, ((CharacterLook.Get(editing, part) + dir) % n + n) % n);
        ApplyLook();
    }

    void SetTab(Tab t)
    {
        tab = t;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].SetActive(i == (int)t);
            KucaTheme.PaintToggle(tabButtons[i], tabButtons[i].GetComponentInChildren<Text>(), i == (int)t);
        }
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

    /// <summary>기본 캐릭터: 경희 모험가 한 벌 (없으면 첫 선택지)</summary>
    CharacterAppearance DefaultAppearance(int gender)
    {
        var a = new CharacterAppearance { gender = Mathf.Clamp(gender, 0, Db.genders.Count - 1) };
        CharacterPartsDatabase.GenderSet set = Db.genders[a.gender];
        foreach (var (slot, _) in SlotRows)
        {
            List<CharacterPartsDatabase.PartOption> options = set.Options(slot);
            if (options.Count == 0)
                continue;
            var match = options.Find(o => o.id == CharacterAppearance.DefaultOutfit);
            a.Set(slot, slot == CharacterSlot.Accessory ? options[0].id : (match ?? options[0]).id);
        }
        return a;
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
        // 얼굴 · 색도 섞는다 (피부는 자연색 6 안에서, 슬라이더는 기본)
        a.skin = Random.Range(0, 6);
        a.hairColor = Random.Range(0, CharacterLook.Hairs.Length);
        a.eyeColor = Random.Range(0, CharacterLook.EyeColors.Length);
        foreach (CharacterLook.Part part in System.Enum.GetValues(typeof(CharacterLook.Part)))
            CharacterLook.Set(a, part, Random.Range(0, CharacterLook.Shapes[(int)part].Length));
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
        foreach (var pair in faceValues)
        {
            string[] shapes = CharacterLook.Shapes[(int)pair.Key];
            pair.Value.text = shapes[Mathf.Clamp(CharacterLook.Get(editing, pair.Key), 0, shapes.Length - 1)];
        }
        eyeSliders[0].SetValueWithoutNotify(editing.eyeSize);
        eyeSliders[1].SetValueWithoutNotify(editing.eyeSpacing);
        eyeSliders[2].SetValueWithoutNotify(editing.eyeHeight);
        foreach (var (ring, selected) in swatchRings)
            ring.enabled = selected();
        swatchNames["피부"].text = "피부  ·  " + CharacterLook.Skins[Mathf.Clamp(editing.skin, 0, CharacterLook.Skins.Length - 1)].name;
        swatchNames["머리"].text = "머리 · 눈썹  ·  " + CharacterLook.Hairs[Mathf.Clamp(editing.hairColor, 0, CharacterLook.Hairs.Length - 1)].name;
        swatchNames["눈"].text = "눈  ·  " + CharacterLook.EyeColors[Mathf.Clamp(editing.eyeColor, 0, CharacterLook.EyeColors.Length - 1)].name;
        bool face = character.HasFaceParts;
        faceRows.SetActive(face);
        faceNote.gameObject.SetActive(!face);
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

        // 꾸미기 화면 전체: 산뜻한 연두빛 바탕 + 위쪽 미리보기 + 아래쪽 흰 카드
        screen = new GameObject("Screen", typeof(RectTransform), typeof(Image));
        BackButton.Attach(screen, () => SetOpen(false));
        screen.transform.SetParent(canvasGo.transform, false);
        screen.GetComponent<Image>().color = KucaTheme.PrimarySoft;
        var sr = (RectTransform)screen.transform;
        sr.anchorMin = Vector2.zero;
        sr.anchorMax = Vector2.one;
        sr.offsetMin = sr.offsetMax = Vector2.zero;
        UIInputBlocker.Register(sr);

        BuildPreview(screen.transform);

        // 아래쪽 패널
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(screen.transform, false);
        var panelImage = panel.GetComponent<Image>();
        panelImage.color = KucaTheme.Card;
        panelImage.sprite = HudIcons.Rounded;
        panelImage.type = Image.Type.Sliced;
        var pr = (RectTransform)panel.transform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, -40f);   // 아래 모서리는 화면 밖으로 (위만 둥글게)
        pr.sizeDelta = new Vector2(0f, PanelHeight + 40f);

        var layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 32, 100);
        layout.spacing = KucaTheme.Space2;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 탭
        var tabs = new GameObject("Tabs", typeof(RectTransform));
        tabs.transform.SetParent(panel.transform, false);
        SetHeight(tabs, KucaTheme.TouchMin);
        var th = tabs.AddComponent<HorizontalLayoutGroup>();
        th.spacing = KucaTheme.Space2;
        th.childControlWidth = th.childControlHeight = true;
        th.childForceExpandWidth = th.childForceExpandHeight = true;
        for (int i = 0; i < TabNames.Length; i++)
        {
            Tab t = (Tab)i;
            GameObject b = CreateButton(tabs.transform, TabNames[i], () => SetTab(t), KucaTheme.PrimarySoft, KucaTheme.Body);
            Pill(b.GetComponent<Image>());
            tabButtons[i] = b.GetComponent<Image>();
        }

        var pageArea = new GameObject("Pages", typeof(RectTransform));
        pageArea.transform.SetParent(panel.transform, false);
        var pageLayout = pageArea.AddComponent<LayoutElement>();
        pageLayout.flexibleHeight = 1f;
        pages[(int)Tab.Face] = BuildFacePage(pageArea.transform);
        pages[(int)Tab.Color] = BuildColorPage(pageArea.transform);
        pages[(int)Tab.Clothes] = BuildClothesPage(pageArea.transform);

        var bottom = new GameObject("Buttons", typeof(RectTransform));
        bottom.transform.SetParent(panel.transform, false);
        SetHeight(bottom, 110f);
        var h = bottom.AddComponent<HorizontalLayoutGroup>();
        h.spacing = KucaTheme.Space3;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;
        GameObject random = CreateButton(bottom.transform, "랜덤", Randomize, KucaTheme.PrimarySoft, KucaTheme.Heading);
        random.GetComponentInChildren<Text>().color = KucaTheme.PrimaryDeep;
        Pill(random.GetComponent<Image>());
        Pill(CreateButton(bottom.transform, "완료", Confirm, KucaTheme.Primary, KucaTheme.Heading).GetComponent<Image>());
    }

    const float PanelHeight = 1080f;

    GameObject Page(Transform parent, string name)
    {
        var page = new GameObject(name, typeof(RectTransform));
        page.transform.SetParent(parent, false);
        var rt = (RectTransform)page.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var v = page.AddComponent<VerticalLayoutGroup>();
        v.spacing = KucaTheme.Space1;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return page;
    }

    GameObject BuildFacePage(Transform parent)
    {
        GameObject page = Page(parent, "FacePage");
        faceRows = new GameObject("FaceRows", typeof(RectTransform));
        faceRows.transform.SetParent(page.transform, false);
        var v = faceRows.AddComponent<VerticalLayoutGroup>();
        v.spacing = KucaTheme.Space1;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        foreach (CharacterLook.Part part in System.Enum.GetValues(typeof(CharacterLook.Part)))
        {
            CharacterLook.Part p = part;
            faceValues[part] = CreateRow(faceRows.transform, CharacterLook.PartNames[(int)part], () => CycleFace(p, -1), () => CycleFace(p, 1), 84f);
        }
        string[] labels = { "눈 크기", "눈 간격", "눈 높이" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            eyeSliders[i] = CreateSlider(faceRows.transform, labels[i], value =>
            {
                if (index == 0) editing.eyeSize = value;
                else if (index == 1) editing.eyeSpacing = value;
                else editing.eyeHeight = value;
                character.ApplyLook(editing);
            });
        }
        faceNote = CreateText(page.transform, "얼굴 모양은 \"경희 모험가\" 머리에서 고를 수 있어요.\n옷 탭에서 머리를 바꿔 보세요.", KucaTheme.Body, TextAnchor.MiddleCenter);
        faceNote.color = KucaTheme.InkMuted;
        SetHeight(faceNote.gameObject, 200f);
        return page;
    }

    GameObject BuildColorPage(Transform parent)
    {
        GameObject page = Page(parent, "ColorPage");
        SwatchRow(page.transform, "피부", CharacterLook.Skins, i => editing.skin = i, () => editing.skin);
        SwatchRow(page.transform, "머리", CharacterLook.Hairs, i => editing.hairColor = i, () => editing.hairColor);
        SwatchRow(page.transform, "눈", CharacterLook.EyeColors, i => editing.eyeColor = i, () => editing.eyeColor);
        return page;
    }

    GameObject BuildClothesPage(Transform parent)
    {
        GameObject page = Page(parent, "ClothesPage");
        genderValue = CreateRow(page.transform, "성별", () => CycleGender(-1), () => CycleGender(1), 96f);
        foreach (var (slot, label) in SlotRows)
        {
            CharacterSlot s = slot;
            slotValues[slot] = CreateRow(page.transform, label, () => Cycle(s, -1), () => Cycle(s, 1), 96f);
        }
        return page;
    }

    /// <summary>이름 줄 + 동그란 색 칸 한 줄. 고른 칸에는 진한 테두리</summary>
    void SwatchRow(Transform parent, string key, CharacterLook.Swatch[] swatches, System.Action<int> set, System.Func<int> get)
    {
        Text title = CreateText(parent, key, KucaTheme.Label, TextAnchor.LowerLeft);
        title.color = KucaTheme.InkMuted;
        SetHeight(title.gameObject, 52f);
        swatchNames[key] = title;

        var row = new GameObject(key + "Swatches", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetHeight(row, 104f);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = KucaTheme.Space2;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        for (int i = 0; i < swatches.Length; i++)
        {
            int index = i;
            var cell = new GameObject(swatches[i].name, typeof(RectTransform), typeof(Image), typeof(Button));
            cell.transform.SetParent(row.transform, false);
            ((RectTransform)cell.transform).sizeDelta = new Vector2(KucaTheme.TouchMin, KucaTheme.TouchMin);
            var ring = cell.GetComponent<Image>();
            ring.sprite = HudIcons.Circle;
            ring.color = KucaTheme.Ink;
            var dot = new GameObject("Color", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(cell.transform, false);
            var dotRect = (RectTransform)dot.transform;
            dotRect.anchorMin = Vector2.zero;
            dotRect.anchorMax = Vector2.one;
            dotRect.offsetMin = new Vector2(9f, 9f);
            dotRect.offsetMax = new Vector2(-9f, -9f);
            var dotImage = dot.GetComponent<Image>();
            dotImage.sprite = HudIcons.Circle;
            dotImage.color = swatches[i].color;
            // 색 점이 터치를 받는다: 고르지 않은 칸은 테두리(ring)를 꺼 두므로, 테두리만 받으면 고른 칸 말고는 눌리지 않는다
            dotImage.raycastTarget = true;
            var button = cell.GetComponent<Button>();
            button.targetGraphic = dotImage;
            button.onClick.AddListener(() => { set(index); ApplyLook(); });
            swatchRings.Add((ring, () => get() == index));
        }
    }

    /// <summary>-1 ~ 1 슬라이더 (가운데 눈금 = 기본). 움직이는 동안 바로 캐릭터에 입힌다</summary>
    Slider CreateSlider(Transform parent, string label, UnityEngine.Events.UnityAction<float> onChange)
    {
        var row = new GameObject(label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetHeight(row, 84f);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = KucaTheme.Space3;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        Text l = CreateText(row.transform, label, KucaTheme.Body, TextAnchor.MiddleLeft);
        l.color = KucaTheme.Ink;
        SetWidth(l.gameObject, 180f, 0f);

        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(row.transform, false);
        SetWidth(go, 0f, 1f);
        Image track = Child(go.transform, "Track", KucaTheme.Line, HudIcons.Pill);
        Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 14f));
        track.type = Image.Type.Sliced;
        Image tick = Child(go.transform, "Default", KucaTheme.InkSoft, null);
        Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4f, 34f));
        var area = new GameObject("HandleArea", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        Place((RectTransform)area.transform, Vector2.zero, Vector2.one, new Vector2(-56f, 0f));
        Image handle = Child(area.transform, "Handle", KucaTheme.Accent, HudIcons.Circle);
        handle.rectTransform.sizeDelta = new Vector2(56f, -28f);

        var slider = go.GetComponent<Slider>();
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = -1f;
        slider.maxValue = 1f;
        slider.SetValueWithoutNotify(0f);
        slider.onValueChanged.AddListener(onChange);
        return slider;
    }

    static Image Child(Transform parent, string name, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }

    static void Pill(Image image)
    {
        image.sprite = HudIcons.Pill;
        image.type = Image.Type.Sliced;
    }

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
        previewCam.nearClipPlane = 0.3f;
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

        // 캐릭터 뒤 햇살 같은 흰 원 (바탕이 비어 보이지 않게)
        Image halo = Child(area.transform, "Halo", new Color(1f, 1f, 1f, 0.7f), HudIcons.Circle);
        Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, 620f));

        var img = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        img.transform.SetParent(area.transform, false);
        var raw = img.GetComponent<RawImage>();
        raw.texture = previewTexture;
        raw.raycastTarget = false;
        var fitter = img.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;

        Text hint = CreateText(area.transform, "좌우로 드래그해서 돌려 보기", KucaTheme.Label, TextAnchor.LowerCenter);
        hint.color = KucaTheme.InkMuted;
        var hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f);
        hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.sizeDelta = new Vector2(0f, 60f);
        hr.anchoredPosition = new Vector2(0f, 10f);
    }

    Text CreateRow(Transform parent, string label, UnityEngine.Events.UnityAction prev, UnityEngine.Events.UnityAction next, float height)
    {
        var row = new GameObject(label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        SetHeight(row, height);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = KucaTheme.Space2;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;

        Text l = CreateText(row.transform, label, KucaTheme.Body, TextAnchor.MiddleLeft);
        l.color = KucaTheme.Ink;
        SetWidth(l.gameObject, 180f, 0f);
        SetWidth(Arrow(row.transform, "<", prev), 110f, 0f);
        Text value = CreateText(row.transform, "", KucaTheme.Body, TextAnchor.MiddleCenter);
        value.color = KucaTheme.Ink;
        value.fontStyle = FontStyle.Bold;
        SetWidth(value.gameObject, 0f, 1f);
        SetWidth(Arrow(row.transform, ">", next), 110f, 0f);
        return value;
    }

    GameObject Arrow(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject b = CreateButton(parent, label, onClick, KucaTheme.PrimarySoft, KucaTheme.Heading);
        b.GetComponentInChildren<Text>().color = KucaTheme.PrimaryDeep;
        Pill(b.GetComponent<Image>());
        return b;
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
        // 높이를 정한 칸은 늘어나지 않게 (안에 가로 줄 배치가 있으면 기본으로 남는 공간을 나눠 가져
        // 탭 · 랜덤 · 완료 버튼이 내용 칸만큼 커지던 것)
        le.flexibleHeight = 0f;
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
