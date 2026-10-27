using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 캐릭터 꾸미기 화면 (디자인 v4, 참조: 동물의 숲 꾸미기 · 제페토 아바타 편집).
/// 위 절반은 밝은 무대 (캐릭터 + 둥근 바닥 그림자, 드래그로 돌려 보기), 아래는 흰 시트:
/// 세그먼트 탭(얼굴 · 색 · 옷) → 큰 선택 칩 · 원 견본 격자 → 아래 고정 "랜덤" · "완료".
/// - 얼굴: 눈 · 눈썹 · 입 · 코 · 볼 모양 칩 + 눈 크기 · 간격 · 높이 슬라이더 (얼굴 가까이 확대)
/// - 색: 피부 8 · 머리 8 · 눈 6 원 견본
/// - 옷: 성별 · 머리 · 상의 · 하의 · 신발 · 가방 칩 (성별이 바뀌면 그 성별의 옷으로 다시 만든다)
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
    public float faceDistance = 3.4f;     // 2.6 은 캡처에서 모자 · 어깨가 잘렸음: 머리 + 어깨까지
    public float faceLookHeight = 1.5f;
    public float previewFov = 30f;
    public float previewRotateSpeed = 0.4f;

    public static bool IsOpen { get; private set; }

    enum Tab { Face, Color, Clothes }
    static readonly string[] TabNames = { "얼굴", "색", "옷" };


    CharacterAppearance editing;
    GameObject screen;
    GameObject panel;
    Camera previewCam;
    Light previewLight;
    RenderTexture previewTexture;
    float previewYaw = 180f;
    float zoom;   // 0 = 전신, 1 = 얼굴
    Tab tab;
    readonly Slider[] eyeSliders = new Slider[3];
    readonly GameObject[] pages = new GameObject[3];
    readonly Image[] tabButtons = new Image[3];
    readonly List<(Image ring, System.Func<bool> selected)> swatchRings = new List<(Image, System.Func<bool>)>();
    readonly Dictionary<string, Text> swatchNames = new Dictionary<string, Text>();
    /// <summary>선택 칩: 고른 것은 초록 바탕 + 흰 글자 (Refresh 가 칠한다). 옷 칩은 성별이 바뀌면 다시 만든다</summary>
    readonly List<(Image bg, Text label, System.Func<bool> selected)> faceChips = new List<(Image, Text, System.Func<bool>)>();
    readonly List<(Image bg, Text label, System.Func<bool> selected)> clothesChips = new List<(Image, Text, System.Func<bool>)>();
    RectTransform clothesContent;
    int clothesGender = -1;
    GameObject faceRows;
    Text lead;
    Text faceNote;

    CharacterPartsDatabase Db => character != null ? character.database : null;

    void Start()
    {
        EnsureEventSystem();
        BuildUI();

        CharacterAppearance saved = CharacterAppearance.Load();
        if (saved != null && saved.version < CharacterAppearance.CurrentVersion)
        {
            // 기본 캐릭터가 경희 모험가로 바뀌기 전에 저장한 모습은 성별만 두고 새 기본 옷으로
            saved = DefaultAppearance(saved.gender);
            saved.Save();
        }
        if (saved != null && Normalize(saved))
            saved.Save();   // 예전 부위 섞기 · 색 8개 시절 저장을 지금 선택지 안으로
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
        // 옷 = 전신, 색 = 반신 (피부 · 머리 · 옷 색이 함께 보이게), 얼굴 = 얼굴
        zoom = Mathf.MoveTowards(zoom, tab == Tab.Clothes ? 0f : tab == Tab.Color ? 0.5f : 1f, Time.unscaledDeltaTime * 3f);
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

    void SetTab(Tab t)
    {
        tab = t;
        lead.enabled = t != Tab.Clothes;   // 옷 탭은 전신이라 모자가 안내 문구를 덮었음 (REVIEW_v16)
        for (int i = 0; i < pages.Length; i++)
        {
            bool on = i == (int)t;
            pages[i].SetActive(on);
            // 세그먼트: 고른 칸은 흰 알약 + 진한 글자, 나머지는 회색 바탕에 흐린 글자 (iOS 세그먼트 컨트롤)
            tabButtons[i].color = on ? KucaTheme.Card : new Color(1f, 1f, 1f, 0f);
            tabButtons[i].GetComponentInChildren<Text>().color = on ? KucaTheme.Ink : KucaTheme.InkMuted;
        }
    }

    void SetGender(int gender)
    {
        if (editing.gender == gender)
            return;
        // 성별이 바뀌어도 같은 스타일 · 가방 · 얼굴 · 색을 이어 입는다
        CharacterAppearance a = DefaultAppearance(gender);
        WearStyle(a, Mathf.Max(0, StyleOf(editing)));
        if (Has(a.gender, CharacterSlot.Accessory, editing.accessory))
            a.accessory = editing.accessory;
        a.skin = editing.skin; a.hairColor = editing.hairColor; a.eyeColor = editing.eyeColor;
        a.eyes = editing.eyes; a.brows = editing.brows; a.mouth = editing.mouth; a.nose = editing.nose; a.cheeks = editing.cheeks;
        a.eyeSize = editing.eyeSize; a.eyeSpacing = editing.eyeSpacing; a.eyeHeight = editing.eyeHeight;
        editing = a;
        Apply();
    }

    void SetStyle(int style)
    {
        WearStyle(editing, style);
        Apply();
    }

    void SetPart(CharacterSlot slot, string id)
    {
        editing.Set(slot, id);
        Apply();
    }

    bool Has(int gender, CharacterSlot slot, string id) => Db.genders[gender].Options(slot).Exists(o => o.id == id);

    /// <summary>스타일 i 를 입힌다: 머리는 경희 모험가, 상의 · 하의 · 신발은 그 성별의 스타일 부품 (없으면 경희 모험가)</summary>
    void WearStyle(CharacterAppearance a, int i)
    {
        var st = CharacterLook.Styles[Mathf.Clamp(i, 0, CharacterLook.Styles.Length - 1)];
        bool women = Db.genders[a.gender].id == "women";
        string top = women ? st.womenTop : st.menTop, bottom = women ? st.womenBottom : st.menBottom;
        string fallback = CharacterAppearance.DefaultOutfit;
        a.head = fallback;
        a.body = Has(a.gender, CharacterSlot.Body, top) ? top : fallback;
        a.legs = Has(a.gender, CharacterSlot.Legs, bottom) ? bottom : fallback;
        a.feet = Has(a.gender, CharacterSlot.Feet, bottom) ? bottom : fallback;
    }

    /// <summary>지금 입은 옷이 몇 번 스타일인지 (어느 스타일과도 다르면 -1)</summary>
    int StyleOf(CharacterAppearance a)
    {
        var t = new CharacterAppearance { gender = a.gender };
        for (int i = 0; i < CharacterLook.Styles.Length; i++)
        {
            WearStyle(t, i);
            if (t.head == a.head && t.body == a.body && t.legs == a.legs && t.feet == a.feet)
                return i;
        }
        return -1;
    }

    /// <summary>선택지 밖의 저장(스타일에 없는 부위 조합 · 없어진 색 번호)을 기본으로 되돌린다. 바뀌었으면 true</summary>
    bool Normalize(CharacterAppearance a)
    {
        bool changed = false;
        if (a.gender < 0 || a.gender >= Db.genders.Count) { a.gender = 0; changed = true; }
        if (StyleOf(a) < 0) { WearStyle(a, 0); changed = true; }
        var bags = Db.genders[a.gender].Options(CharacterSlot.Accessory);
        if (bags.Count > 0 && !Has(a.gender, CharacterSlot.Accessory, a.accessory)) { a.accessory = bags[0].id; changed = true; }
        if (a.skin >= CharacterLook.Skins.Length) { a.skin = 0; changed = true; }
        if (a.hairColor >= CharacterLook.Hairs.Length) { a.hairColor = 0; changed = true; }
        if (a.eyeColor >= CharacterLook.EyeColors.Length) { a.eyeColor = 0; changed = true; }
        return changed;
    }

    /// <summary>기본 캐릭터: 경희 모험가 스타일, 가방 첫 선택지</summary>
    CharacterAppearance DefaultAppearance(int gender)
    {
        var a = new CharacterAppearance { gender = Mathf.Clamp(gender, 0, Db.genders.Count - 1) };
        WearStyle(a, 0);
        var bags = Db.genders[a.gender].Options(CharacterSlot.Accessory);
        if (bags.Count > 0)
            a.accessory = bags[0].id;
        return a;
    }

    CharacterAppearance RandomAppearance()
    {
        CharacterAppearance a = DefaultAppearance(Random.Range(0, Db.genders.Count));
        WearStyle(a, Random.Range(0, CharacterLook.Styles.Length));
        return a;
    }

    void Randomize()
    {
        CharacterAppearance a = DefaultAppearance(editing.gender);
        WearStyle(a, Random.Range(0, CharacterLook.Styles.Length));
        var bags = Db.genders[a.gender].Options(CharacterSlot.Accessory);
        if (bags.Count > 0)
            a.accessory = bags[Random.Range(0, bags.Count)].id;
        // 얼굴 · 색도 섞는다 (슬라이더는 기본)
        a.skin = Random.Range(0, CharacterLook.Skins.Length);
        a.hairColor = Random.Range(0, CharacterLook.Hairs.Length);
        a.eyeColor = Random.Range(0, CharacterLook.EyeColors.Length);
        foreach (CharacterLook.Part part in System.Enum.GetValues(typeof(CharacterLook.Part)))
            CharacterLook.Set(a, part, Random.Range(0, CharacterLook.Shapes[(int)part].Length));
        editing = a;
        Apply();
    }

    void Refresh()
    {
        if (clothesGender != editing.gender)
            BuildClothesChips();
        PaintChips(faceChips);
        PaintChips(clothesChips);
        eyeSliders[0].SetValueWithoutNotify(editing.eyeSize);
        eyeSliders[1].SetValueWithoutNotify(editing.eyeSpacing);
        eyeSliders[2].SetValueWithoutNotify(editing.eyeHeight);
        foreach (var (ring, selected) in swatchRings)
            ring.color = selected() ? KucaTheme.Primary : new Color(1f, 1f, 1f, 0f);
        swatchNames["피부"].text = "피부  ·  " + CharacterLook.Skins[Mathf.Clamp(editing.skin, 0, CharacterLook.Skins.Length - 1)].name;
        swatchNames["머리"].text = "머리 · 눈썹  ·  " + CharacterLook.Hairs[Mathf.Clamp(editing.hairColor, 0, CharacterLook.Hairs.Length - 1)].name;
        swatchNames["눈"].text = "눈  ·  " + CharacterLook.EyeColors[Mathf.Clamp(editing.eyeColor, 0, CharacterLook.EyeColors.Length - 1)].name;
        bool face = character.HasFaceParts;
        faceRows.SetActive(face);
        faceNote.gameObject.SetActive(!face);
    }

    static void PaintChips(List<(Image bg, Text label, System.Func<bool> selected)> chips)
    {
        foreach (var (bg, label, selected) in chips)
        {
            bool on = selected();
            bg.color = on ? KucaTheme.Primary : KucaTheme.Fill;
            label.color = on ? Color.white : KucaTheme.Ink;
        }
    }

    // ---------- UI 생성 ----------

    const float PanelHeight = 1300f;   // 1120 은 항목이 3줄 남짓만 보였음: 시트를 키우고 위 무대를 줄임
    const float ChipHeight = 104f;

    void BuildUI()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "CustomizerCanvas", 20);

        // 화면 전체: 밝은 무대(회색 바탕) + 아래 흰 시트
        Image bg = UIKit.Image(canvas.transform, "Screen", KucaTheme.Surface, raycast: true);
        UIKit.Stretch(bg.rectTransform);
        screen = bg.gameObject;
        BackButton.Attach(screen, () => SetOpen(false));
        UIInputBlocker.Register(bg.rectTransform);

        Rect sa = Screen.safeArea;
        RectTransform safe = UIKit.Rect(bg.transform, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        BuildPreview(bg.rectTransform);
        safe.SetAsLastSibling();   // 제목 · 안내가 미리보기 흰 원 위에 그려지게 (원에 덮여 "나를 닮은 ㄷ" 로 잘렸음, REVIEW_v15)

        // 위: ‹ 뒤로 (저장 안 하고 닫기) + 제목
        UIKit.IconButton(safe, HudIcons.Chevron, () => SetOpen(false), KucaTheme.Ink, new Vector2(KucaTheme.Gutter - 30f, -12f), flip: true);
        Text title = UIKit.Bold(UIKit.Text(safe, "꾸미기", KucaTheme.Title, TextAnchor.MiddleLeft, KucaTheme.Ink));
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = Vector2.one;
        tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(KucaTheme.Gutter + 110f, -144f);
        tr.offsetMax = new Vector2(-KucaTheme.Gutter, -12f);
        // 첫 실행에 무엇을 하는 화면인지 한 줄 (REVIEW_v14: "꾸미기" 제목뿐이었음)
        lead = UIKit.Text(safe, "나를 닮은 탐험가를 만들어요", KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        RectTransform lr = lead.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f);
        lr.anchorMax = Vector2.one;
        lr.pivot = new Vector2(0.5f, 1f);
        lr.offsetMin = new Vector2(KucaTheme.Gutter + 110f, -208f);
        lr.offsetMax = new Vector2(-KucaTheme.Gutter, -144f);

        // 아래 흰 시트 (위 모서리만 둥글게: 아래는 화면 밖으로)
        Image sheet = UIKit.Image(bg.transform, "Panel", KucaTheme.Card, raycast: true);
        sheet.sprite = HudIcons.Rounded;
        sheet.type = Image.Type.Sliced;
        sheet.pixelsPerUnitMultiplier = 0.25f;
        panel = sheet.gameObject;
        RectTransform pr = sheet.rectTransform;
        pr.anchorMin = Vector2.zero;
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.anchoredPosition = new Vector2(0f, -60f);
        pr.sizeDelta = new Vector2(0f, PanelHeight + 60f);
        UIKit.Vertical(sheet, KucaTheme.Space4, new RectOffset(48, 48, 40, 60 + 100));   // 아래 100: 홈 표시줄

        // 세그먼트 탭 (회색 알약 안에 흰 알약이 고른 칸)
        Image seg = UIKit.Image(sheet.transform, "Tabs", KucaTheme.Fill);
        seg.sprite = HudIcons.Pill;
        seg.type = Image.Type.Sliced;
        seg.pixelsPerUnitMultiplier = 1.2f;
        UIKit.Size(seg, 116f);
        var th = UIKit.Horizontal(seg, 0f);
        th.padding = new RectOffset(8, 8, 8, 8);
        for (int i = 0; i < TabNames.Length; i++)
        {
            Tab t = (Tab)i;
            Image cell = UIKit.Image(seg.transform, TabNames[i], new Color(1f, 1f, 1f, 0f), raycast: true);
            cell.sprite = HudIcons.Pill;
            cell.type = Image.Type.Sliced;
            cell.pixelsPerUnitMultiplier = 1.4f;
            var b = cell.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => SetTab(t));
            Text label = UIKit.Bold(UIKit.Text(cell.transform, TabNames[i], KucaTheme.Label, TextAnchor.MiddleCenter, KucaTheme.InkMuted));
            UIKit.Stretch(label.rectTransform);
            tabButtons[i] = cell;
        }

        RectTransform pageArea = UIKit.Rect(sheet.transform, "Pages");
        pageArea.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;
        pages[(int)Tab.Face] = BuildFacePage(pageArea);
        pages[(int)Tab.Color] = BuildColorPage(pageArea);
        pages[(int)Tab.Clothes] = BuildClothesPage(pageArea);

        // 아래 고정: 랜덤(보조) + 완료(주요)
        RectTransform bottom = UIKit.Rect(sheet.transform, "Buttons");
        UIKit.Size(bottom, KucaTheme.ButtonHeight);
        var h = UIKit.Horizontal(bottom, KucaTheme.Space2);
        h.childForceExpandWidth = false;
        UIKit.Size(UIKit.SecondaryButton(bottom, "랜덤", Randomize), -1f, -1f, 1f);
        UIKit.Size(UIKit.PrimaryButton(bottom, "완료", Confirm), -1f, -1f, 2f);
    }

    /// <summary>탭 한 장: 세로로 스크롤되는 내용 (반환: 페이지, out content 에 쌓는다)</summary>
    static GameObject Page(RectTransform parent, string name, out RectTransform content)
    {
        RectTransform page = UIKit.Rect(parent, name);
        UIKit.Stretch(page);
        content = UIKit.ScrollList(page, KucaTheme.Space2, new RectOffset(0, 0, 0, (int)KucaTheme.Space4));
        return page.gameObject;
    }

    static Text Label(Transform parent, string text)
    {
        Text t = UIKit.Bold(UIKit.Text(parent, text, KucaTheme.Label, TextAnchor.LowerLeft, KucaTheme.Ink));
        UIKit.Size(t, 64f);
        return t;
    }

    /// <summary>칩 격자 (열 수 고정, 줄은 개수만큼). 칩 너비 = (시트 안 너비 984 - 간격) / 열</summary>
    static RectTransform ChipGrid(Transform parent, int columns)
    {
        RectTransform grid = UIKit.Rect(parent, "Chips");
        var g = grid.gameObject.AddComponent<GridLayoutGroup>();
        float inner = 1080f - 96f;
        g.cellSize = new Vector2((inner - (columns - 1) * KucaTheme.Space2) / columns, ChipHeight);
        g.spacing = new Vector2(KucaTheme.Space2, KucaTheme.Space2);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = columns;
        FitGridColumns.Attach(g);   // 1080 기준 칸 너비라 세로로 긴 폰에서 마지막 칸이 잘렸음 ("또렷" · "후드티")
        return grid;
    }

    static (Image, Text) ChipCell(Transform grid, string label, UnityEngine.Events.UnityAction onClick)
    {
        Image bg = UIKit.Image(grid, label, KucaTheme.Fill, raycast: true);
        bg.sprite = HudIcons.Rounded;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 0.5f;
        var b = bg.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        bg.gameObject.AddComponent<PressScale>();
        Text t = UIKit.Bold(UIKit.Text(bg.transform, label, KucaTheme.Label, TextAnchor.MiddleCenter, KucaTheme.Ink));
        UIKit.Stretch(t.rectTransform, 12f, 0f);
        return (bg, t);
    }

    GameObject BuildFacePage(RectTransform parent)
    {
        GameObject page = Page(parent, "FacePage", out RectTransform content);
        faceRows = UIKit.Rect(content, "FaceRows").gameObject;
        UIKit.Vertical(faceRows.GetComponent<RectTransform>(), KucaTheme.Space2);
        Transform rows = faceRows.transform;
        foreach (CharacterLook.Part part in System.Enum.GetValues(typeof(CharacterLook.Part)))
        {
            CharacterLook.Part p = part;
            Label(rows, CharacterLook.PartNames[(int)part]);
            RectTransform grid = ChipGrid(rows, 4);
            string[] shapes = CharacterLook.Shapes[(int)part];
            for (int i = 0; i < shapes.Length; i++)
            {
                int index = i;
                var (bg, label) = ChipCell(grid, shapes[i], () => { CharacterLook.Set(editing, p, index); ApplyLook(); });
                faceChips.Add((bg, label, () => CharacterLook.Get(editing, p) == index));
            }
        }
        string[] labels = { "눈 크기", "눈 간격", "눈 높이" };
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            eyeSliders[i] = CreateSlider(rows, labels[i], value =>
            {
                if (index == 0) editing.eyeSize = value;
                else if (index == 1) editing.eyeSpacing = value;
                else editing.eyeHeight = value;
                character.ApplyLook(editing);
            });
        }
        faceNote = UIKit.Paragraph(content, "얼굴 모양은 \"경희 모험가\" 머리에서 고를 수 있어요.\n옷 탭에서 머리를 바꿔 보세요.");
        return page;
    }

    GameObject BuildColorPage(RectTransform parent)
    {
        GameObject page = Page(parent, "ColorPage", out RectTransform content);
        SwatchRow(content, "피부", CharacterLook.Skins, i => editing.skin = i, () => editing.skin);
        SwatchRow(content, "머리", CharacterLook.Hairs, i => editing.hairColor = i, () => editing.hairColor);
        SwatchRow(content, "눈", CharacterLook.EyeColors, i => editing.eyeColor = i, () => editing.eyeColor);
        return page;
    }

    GameObject BuildClothesPage(RectTransform parent)
    {
        GameObject page = Page(parent, "ClothesPage", out clothesContent);
        return page;
    }

    /// <summary>옷 탭 칩: 성별 + 칸마다 그 성별의 선택지 (성별이 바뀌면 다시 만든다)</summary>
    void BuildClothesChips()
    {
        clothesGender = editing.gender;
        clothesChips.Clear();
        for (int i = clothesContent.childCount - 1; i >= 0; i--)
            Destroy(clothesContent.GetChild(i).gameObject);

        // 썸네일 대신: 이름만 있는 칩을 눌러 보면 위 캐릭터가 바로 바뀐다는 것을 먼저 알린다 (REVIEW_v14 · v15)
        Text hint = UIKit.Text(clothesContent, "누르면 위 캐릭터에 바로 입혀 봐요. 마음에 들면 완료", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        UIKit.Size(hint, 56f);
        Label(clothesContent, "성별");
        RectTransform genders = ChipGrid(clothesContent, 3);
        for (int g = 0; g < Db.genders.Count; g++)
        {
            int gi = g;
            var (bg, label) = ChipCell(genders, Db.genders[g].displayName, () => SetGender(gi));
            clothesChips.Add((bg, label, () => editing.gender == gi));
        }
        // 스타일 5 (상의 · 하의 · 신발 한 벌씩) + 가방
        Label(clothesContent, "스타일");
        RectTransform styles = ChipGrid(clothesContent, 3);
        for (int i = 0; i < CharacterLook.Styles.Length; i++)
        {
            int si = i;
            var (bg, label) = ChipCell(styles, CharacterLook.Styles[i].name, () => SetStyle(si));
            clothesChips.Add((bg, label, () => StyleOf(editing) == si));
        }
        List<CharacterPartsDatabase.PartOption> bags = Db.genders[editing.gender].Options(CharacterSlot.Accessory);
        if (bags.Count > 1)
        {
            Label(clothesContent, "가방");
            RectTransform grid = ChipGrid(clothesContent, 3);
            foreach (var o in bags)
            {
                string id = o.id;
                var (bg, label) = ChipCell(grid, o.displayName, () => SetPart(CharacterSlot.Accessory, id));
                clothesChips.Add((bg, label, () => editing.accessory == id));
            }
        }
    }

    /// <summary>이름 줄 + 큰 원 견본 격자. 고른 칸에는 초록 테</summary>
    void SwatchRow(Transform parent, string key, CharacterLook.Swatch[] swatches, System.Action<int> set, System.Func<int> get)
    {
        swatchNames[key] = Label(parent, key);
        RectTransform row = UIKit.Rect(parent, key + "Swatches");
        var g = row.gameObject.AddComponent<GridLayoutGroup>();
        const float cell = 108f;   // 8칸 × 108 + 간격 7 × 16 = 976 ≤ 시트 안 너비 984
        g.cellSize = new Vector2(cell, cell);
        g.spacing = new Vector2(KucaTheme.Space2, KucaTheme.Space2);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 8;
        for (int i = 0; i < swatches.Length; i++)
        {
            int index = i;
            Image ring = UIKit.Image(row, swatches[i].name, new Color(1f, 1f, 1f, 0f), raycast: true);
            ring.sprite = HudIcons.Circle;
            Image dot = UIKit.Image(ring.transform, "Color", swatches[i].color, raycast: true);
            dot.sprite = HudIcons.Circle;
            UIKit.Stretch(dot.rectTransform, 10f, 10f);
            var button = ring.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { set(index); ApplyLook(); });
            ring.gameObject.AddComponent<PressScale>();
            swatchRings.Add((ring, () => get() == index));
        }
    }

    /// <summary>-1 ~ 1 슬라이더 (가운데 눈금 = 기본). 움직이는 동안 바로 캐릭터에 입힌다</summary>
    Slider CreateSlider(Transform parent, string label, UnityEngine.Events.UnityAction<float> onChange)
    {
        RectTransform row = UIKit.Rect(parent, label);
        UIKit.Size(row, KucaTheme.TouchMin);
        var h = UIKit.Horizontal(row, KucaTheme.Space3);
        h.childForceExpandWidth = false;
        Text l = UIKit.Bold(UIKit.Text(row, label, KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.Ink));
        UIKit.Size(l, -1f, 200f, 0f);

        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(row, false);
        UIKit.Size(go.transform as RectTransform, -1f, -1f, 1f);
        Image track = Child(go.transform, "Track", KucaTheme.Fill, HudIcons.Pill);
        Place(track.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 24f));
        track.type = Image.Type.Sliced;
        track.raycastTarget = true;   // 트랙을 눌러도 그 자리로
        Image tick = Child(go.transform, "Default", KucaTheme.InkSoft, null);
        Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4f, 44f));
        var area = new GameObject("HandleArea", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        Place((RectTransform)area.transform, Vector2.zero, Vector2.one, new Vector2(-72f, 0f));
        Image handle = Child(area.transform, "Handle", KucaTheme.Primary, HudIcons.Circle);
        handle.raycastTarget = true;
        handle.rectTransform.sizeDelta = new Vector2(72f, -60f);

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

    void BuildPreview(RectTransform parent)
    {
        previewTexture = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { name = "CharacterPreview", antiAliasing = 2 };

        var camGo = new GameObject("CharacterPreviewCamera", typeof(Camera), typeof(NeutralAmbient));
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
        previewLight.intensity = 1.4f;   // 미리보기에서 얼굴이 어둡게 보이지 않게
        previewLight.shadows = LightShadows.None;
        previewLight.cullingMask = 1 << character.gameObject.layer;
        previewLight.enabled = false;

        // 무대: 시트 위 영역 전체 (드래그로 돌려 보기)
        var area = new GameObject("PreviewArea", typeof(RectTransform), typeof(Image), typeof(PreviewDragArea));
        area.transform.SetParent(parent, false);
        area.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 드래그 입력만 받는 투명 영역
        area.GetComponent<PreviewDragArea>().onDragX = dx => previewYaw += dx * previewRotateSpeed;
        var ar = (RectTransform)area.transform;
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = new Vector2(0f, PanelHeight - 40f);
        ar.offsetMax = new Vector2(0f, -150f);   // 위 제목 줄 아래

        // 뒤 흰 원 (무대 조명) + 발밑 둥근 그림자 (떠 보이지 않게)
        Image halo = Child(area.transform, "Halo", KucaTheme.Card, HudIcons.Circle);
        Place(halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(640f, 640f));
        Image floor = Child(area.transform, "Floor", new Color(0f, 0f, 0f, 0.16f), HudIcons.SoftDisk);
        Place(floor.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(460f, 90f));
        floor.rectTransform.anchoredPosition = new Vector2(0f, 120f);

        var img = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        img.transform.SetParent(area.transform, false);
        var raw = img.GetComponent<RawImage>();
        raw.texture = previewTexture;
        raw.raycastTarget = false;
        var fitter = img.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;

        // 안내: 시트 바로 위 흰 칩 (캐릭터 몸 위에 겹쳐도 읽힌다)
        Image chip = Child(area.transform, "HintChip", KucaTheme.Card, HudIcons.Pill);
        chip.type = Image.Type.Sliced;
        Place(chip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(560f, 80f));
        chip.rectTransform.pivot = new Vector2(0.5f, 0f);
        chip.rectTransform.anchoredPosition = new Vector2(0f, 24f);
        Text hint = UIKit.Text(chip.transform, "좌우로 드래그해서 돌려 보기", KucaTheme.Caption, TextAnchor.MiddleCenter, KucaTheme.InkMuted);
        UIKit.Stretch(hint.rectTransform);
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
