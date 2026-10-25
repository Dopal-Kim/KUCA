using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>
/// 코드로 uGUI 화면을 만들 때 쓰는 공통 도구. 기준 해상도 1080x1920(세로) 단위로 크기를 쓴다.
/// </summary>
public static class UIKit
{
    // 어두운 화면(카메라 촬영 등)용 옛 기본색. 밝은 화면은 KucaTheme 을 쓴다.
    public static readonly Color ButtonGray = new Color(0.25f, 0.27f, 0.33f, 1f);
    public static readonly Color Accent = KucaTheme.Primary;
    public static readonly Color Quiet = new Color(1f, 1f, 1f, 0.6f);

    static Font font, boldFont;
    /// <summary>본문 글꼴: Pretendard Medium (Resources/KUCA/Fonts, OFL). 없으면 내장 글꼴</summary>
    public static Font Font => font != null ? font : font = Resources.Load<Font>("KUCA/Fonts/Pretendard-Medium") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    /// <summary>굵은 글꼴: Pretendard Bold. 가짜 굵게(FontStyle.Bold) 대신 이걸 쓴다</summary>
    public static Font BoldFont => boldFont != null ? boldFont : boldFont = Resources.Load<Font>("KUCA/Fonts/Pretendard-Bold");

    /// <summary>글자를 Pretendard Bold 로 (굵은 글꼴이 없으면 FontStyle.Bold)</summary>
    public static Text Bold(Text t)
    {
        if (BoldFont != null) { t.font = BoldFont; t.fontStyle = FontStyle.Normal; }
        else t.fontStyle = FontStyle.Bold;
        return t;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => font = boldFont = null;

    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        EnsureEventSystem();
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || Object.FindFirstObjectByType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
    }

    public static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static Image Image(Transform parent, string name, Color color, bool raycast = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    public static Text Text(Transform parent, string text, int size, TextAnchor anchor, Color? color = null)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Font;
        t.lineSpacing = 1.1f;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color ?? Color.white;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    /// <summary>둥근 사각 버튼 (모든 화면 공통 모양, docs/디자인_v4.md). 새 화면은 PrimaryButton · SecondaryButton 을 쓴다</summary>
    public static Button Button(Transform parent, string label, UnityAction onClick, Color color, int fontSize = 44)
    {
        Image img = Image(parent, label, color, raycast: true);
        img.sprite = HudIcons.Rounded;   // v4: 알약 대신 둥근 사각 (모서리 약 40)
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.4f;
        var button = img.gameObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        Text t = Bold(Text(img.transform, label, fontSize, TextAnchor.MiddleCenter));
        Stretch(t.rectTransform);
        return button;
    }

    public static void SetLabel(Button button, string label) => button.GetComponentInChildren<Text>().text = label;

    /// <summary>한 줄 또는 여러 줄 입력칸 (밝은 화면 기본: 옅은 초록 둥근 칸 + 진한 글자)</summary>
    public static InputField Input(Transform parent, string placeholder, int fontSize, bool multiLine, int characterLimit)
    {
        Image bg = Image(parent, "Input", KucaTheme.PrimarySoft, raycast: true);
        bg.sprite = HudIcons.Rounded;
        bg.type = UnityEngine.UI.Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 0.8f;
        var field = bg.gameObject.AddComponent<InputField>();

        Text text = Text(bg.transform, "", fontSize, multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, KucaTheme.Ink);
        text.supportRichText = false;
        Stretch(text.rectTransform, 24f, 18f);
        Text hint = Text(bg.transform, placeholder, fontSize, multiLine ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft, KucaTheme.InkSoft);
        Stretch(hint.rectTransform, 24f, 18f);

        field.textComponent = text;
        field.placeholder = hint;
        field.lineType = multiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
        field.characterLimit = characterLimit;
        return field;
    }

    // ================= v4 공통 부품 (docs/디자인_v4.md) =================

    /// <summary>v4 전체 화면 하나: 회색 바탕 · 위 뒤로(‹) · 큰 왼쪽 정렬 제목 · 스크롤 내용 · (선택) 아래 고정 버튼 칸</summary>
    public class Page
    {
        public GameObject root;
        public Text title, subtitle;
        public RectTransform header, content, bottom;
        public Button back;
    }

    /// <summary>
    /// v4 화면 틀. content 에 카드 · 목록을 쌓는다 (세로 레이아웃, 좌우 Gutter). bottomButtons &gt; 0 이면 아래 고정 칸(bottom)에
    /// 주요 버튼을 넣을 자리가 생기고 내용은 그 위에서 끝난다. onBack 은 ‹ 버튼과 안드로이드 뒤로 가기 둘 다.
    /// </summary>
    static Texture2D fadeUp;
    /// <summary>아래는 불투명 · 위로 갈수록 투명한 세로 띠 (색은 RawImage.color 로)</summary>
    static Texture2D FadeUp
    {
        get
        {
            if (fadeUp != null)
                return fadeUp;
            fadeUp = new Texture2D(1, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "FadeUp" };
            for (int y = 0; y < 16; y++)
                fadeUp.SetPixel(0, y, new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, y / 15f)));
            fadeUp.Apply();
            return fadeUp;
        }
    }

    public static Page CreatePage(Transform parent, string name, string title, UnityAction onBack, string subtitle = null, int bottomButtons = 0, bool scroll = true)
    {
        var page = new Page();
        Image bg = Image(parent, name, KucaTheme.Surface, raycast: true);
        Stretch(bg.rectTransform);
        page.root = bg.gameObject;
        if (onBack != null)
            BackButton.Attach(page.root, () => onBack());

        Rect sa = Screen.safeArea;
        RectTransform safe = Rect(bg.transform, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        // 위: 뒤로 + 큰 제목 + 부제
        float headH = 150f + (string.IsNullOrEmpty(title) ? 0f : 100f) + (string.IsNullOrEmpty(subtitle) ? 0f : 70f);
        RectTransform head = Rect(safe, "Header");
        head.anchorMin = new Vector2(0f, 1f);
        head.anchorMax = Vector2.one;
        head.pivot = new Vector2(0.5f, 1f);
        head.offsetMin = new Vector2(0f, -headH);
        head.offsetMax = Vector2.zero;
        page.header = head;
        if (onBack != null)
            page.back = IconButton(head, HudIcons.Chevron, onBack, KucaTheme.Ink, new Vector2(KucaTheme.Gutter - 30f, -12f), flip: true);
        if (!string.IsNullOrEmpty(title))
        {
            page.title = Bold(Text(head, title, KucaTheme.Title, TextAnchor.MiddleLeft, KucaTheme.Ink));
            Box(page.title.rectTransform, KucaTheme.Gutter, 150f, 100f);
        }
        if (!string.IsNullOrEmpty(subtitle))
        {
            page.subtitle = Text(head, subtitle, KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
            Box(page.subtitle.rectTransform, KucaTheme.Gutter, 250f, 64f);
        }

        // 아래 고정 버튼 칸 (버튼 수만큼 세로로)
        float bottomH = 0f;
        if (bottomButtons > 0)
        {
            bottomH = bottomButtons * KucaTheme.ButtonHeight + (bottomButtons - 1) * KucaTheme.Space2 + 2 * KucaTheme.Space4;
            page.bottom = Rect(safe, "Bottom");
            page.bottom.anchorMin = Vector2.zero;
            page.bottom.anchorMax = new Vector2(1f, 0f);
            page.bottom.pivot = new Vector2(0.5f, 0f);
            page.bottom.offsetMin = Vector2.zero;
            page.bottom.offsetMax = new Vector2(0f, bottomH);
            Vertical(page.bottom, KucaTheme.Space2, new RectOffset((int)KucaTheme.Gutter, (int)KucaTheme.Gutter, (int)KucaTheme.Space4, (int)KucaTheme.Space4));
        }

        RectTransform area = Rect(safe, "Content");
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(0f, bottomH);
        area.offsetMax = new Vector2(0f, -headH);
        var pad = new RectOffset((int)KucaTheme.Gutter, (int)KucaTheme.Gutter, (int)KucaTheme.Space3, (int)KucaTheme.Space8);
        if (scroll)
        {
            page.content = ScrollList(area, KucaTheme.Space4, pad);
            // 내용이 제목 밑으로 올라가면 제목 아래 구분선 (iOS 큰 제목 방식): 잘린 카드 윗부분이 제목에 붙어 보이지 않게
            Image divider = Image(head, "Divider", KucaTheme.InkSoft);   // Line 은 회색 바탕 위에서 거의 안 보였음 (UI_REVIEW_v8)
            RectTransform dr = divider.rectTransform;
            dr.anchorMin = Vector2.zero;
            dr.anchorMax = new Vector2(1f, 0f);
            dr.pivot = new Vector2(0.5f, 0f);
            dr.offsetMin = Vector2.zero;
            dr.offsetMax = new Vector2(0f, 2f);
            divider.enabled = false;
            // 아래로 더 있으면 아래 끝에 구분선 + 위로 옅어지는 그림자: 확인 버튼 위에서 잘린 카드가 끝처럼 보였음 (REVIEW_v14).
            // 바탕색으로 흐리게 하던 v15 는 흰 카드 → #F2F4F6 라 눈에 안 띄었음
            RawImage shade = new GameObject("MoreBelow", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            shade.transform.SetParent(safe, false);
            shade.texture = FadeUp;
            shade.color = new Color(KucaTheme.Ink.r, KucaTheme.Ink.g, KucaTheme.Ink.b, 0.22f);
            shade.raycastTarget = false;
            RectTransform sr = shade.rectTransform;
            sr.anchorMin = Vector2.zero;
            sr.anchorMax = new Vector2(1f, 0f);
            sr.pivot = new Vector2(0.5f, 0f);
            sr.offsetMin = new Vector2(0f, bottomH);
            sr.offsetMax = new Vector2(0f, bottomH + 48f);
            Image line = Image(shade.transform, "Line", KucaTheme.InkSoft);
            RectTransform lr = line.rectTransform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = new Vector2(1f, 0f);
            lr.pivot = new Vector2(0.5f, 0f);
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = new Vector2(0f, 2f);
            shade.gameObject.SetActive(false);
            area.GetComponent<ScrollRect>().onValueChanged.AddListener(v =>
            {
                bool longer = page.content.rect.height > area.rect.height + 1f;
                divider.enabled = longer && v.y < 0.995f;
                shade.gameObject.SetActive(longer && v.y > 0.005f);
            });
        }
        else
        {
            page.content = area;
            Vertical(area, KucaTheme.Space4, pad);
        }
        page.root.SetActive(false);
        return page;
    }

    /// <summary>아이콘만 있는 둥근 터치 영역 (‹ 뒤로 · × 닫기 등). flip: 좌우 뒤집기 (› 를 ‹ 로)</summary>
    public static Button IconButton(Transform parent, Sprite icon, UnityAction onClick, Color color, Vector2 topLeft, bool flip = false)
    {
        Image hit = Image(parent, "IconButton", new Color(1f, 1f, 1f, 0f), raycast: true);
        RectTransform r = hit.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = topLeft;
        r.sizeDelta = new Vector2(KucaTheme.TouchMin, KucaTheme.TouchMin);
        var b = hit.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        Image ic = Image(hit.transform, "Icon", color);
        ic.sprite = icon;
        RectTransform ir = ic.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.sizeDelta = new Vector2(60f, 60f);
        if (flip) ir.localScale = new Vector3(-1f, 1f, 1f);
        return b;
    }

    /// <summary>부모 위에서 top 만큼 내려와 높이 height 로 가로를 채운다 (좌우 pad)</summary>
    static void Box(RectTransform rt, float pad, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(pad, -top - height);
        rt.offsetMax = new Vector2(-pad, -top);
    }

    /// <summary>v4 주요 버튼: 꽉 찬 너비 · 168 높이 · 둥근 사각 · 초록 + 흰 굵은 글자. 화면당 하나</summary>
    public static Button PrimaryButton(Transform parent, string label, UnityAction onClick)
        => BigButton(parent, label, onClick, KucaTheme.Primary, Color.white);

    /// <summary>v4 보조 버튼: 옅은 초록 바탕 + 진한 초록 글자 (회색 바탕 · 흰 카드 어디서나 버튼으로 보이게)</summary>
    public static Button SecondaryButton(Transform parent, string label, UnityAction onClick)
        => BigButton(parent, label, onClick, KucaTheme.PrimarySoft, KucaTheme.PrimaryDeep);

    public static Button BigButton(Transform parent, string label, UnityAction onClick, Color bg, Color fg)
    {
        Image img = Image(parent, label, bg, raycast: true);
        img.sprite = HudIcons.Rounded;
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.4f;   // 모서리 반지름 약 40
        var b = img.gameObject.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        var c = b.colors;
        c.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        c.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        b.colors = c;
        Text t = Bold(Text(img.transform, label, KucaTheme.Heading - 4, TextAnchor.MiddleCenter, fg));
        Stretch(t.rectTransform, 24f, 0f);
        Size(img, KucaTheme.ButtonHeight);
        return b;
    }

    /// <summary>v4 흰 카드: 둥근 사각 + 안쪽 여백 48 의 세로 레이아웃. 반환값에 내용을 넣는다</summary>
    public static RectTransform CardBox(Transform parent, string name = "Card", float spacing = 24f)
    {
        Image card = Image(parent, name, KucaTheme.Card);
        card.sprite = HudIcons.Rounded;
        card.type = UnityEngine.UI.Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.34f;   // 모서리 반지름 약 48
        Vertical(card, spacing, new RectOffset(48, 48, 44, 48));
        return card.rectTransform;
    }

    /// <summary>카드 · 구역 제목 (굵게, Heading)</summary>
    public static Text SectionTitle(Transform parent, string text)
    {
        Text t = Bold(Text(parent, text, KucaTheme.Heading, TextAnchor.MiddleLeft, KucaTheme.Ink));
        Size(t, 72f);
        return t;
    }

    /// <summary>본문 한 덩어리 (여러 줄 가능, 높이는 글 길이에 맞춤)</summary>
    public static Text Paragraph(Transform parent, string text, int size = KucaTheme.Body, Color? color = null)
    {
        Text t = Text(parent, text, size, TextAnchor.UpperLeft, color ?? KucaTheme.InkMuted);
        t.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return t;
    }

    /// <summary>큰 숫자 줄: (값, 이름) 을 가로로 같은 너비로. 예) 1.9 km 걸은 거리 · 32 분 시간 (애플 피트니스 요약)</summary>
    public static RectTransform StatRow(Transform parent, params (string value, string label)[] stats)
    {
        RectTransform row = Rect(parent, "Stats");
        Size(row, 190f);
        Horizontal(row, KucaTheme.Space2);
        foreach (var (value, label) in stats)
        {
            RectTransform cell = Rect(row, label);
            Text v = Bold(Text(cell, value, KucaTheme.Display - 20, TextAnchor.LowerLeft, KucaTheme.Ink));
            v.horizontalOverflow = HorizontalWrapMode.Overflow;
            v.rectTransform.anchorMin = new Vector2(0f, 0.38f);
            v.rectTransform.anchorMax = Vector2.one;
            v.rectTransform.offsetMin = v.rectTransform.offsetMax = Vector2.zero;
            Text l = Text(cell, label, KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
            l.rectTransform.anchorMin = Vector2.zero;
            l.rectTransform.anchorMax = new Vector2(1f, 0.36f);
            l.rectTransform.offsetMin = l.rectTransform.offsetMax = Vector2.zero;
        }
        return row;
    }

    /// <summary>선택 칩 (둥근 알약). on = 초록 바탕 + 흰 글자, off = 흰 바탕 + 회색 테 + 진한 글자</summary>
    public static Button Chip(Transform parent, string label, bool on, UnityAction onClick)
    {
        Image img = Image(parent, label, on ? KucaTheme.Primary : KucaTheme.Card, raycast: true);
        img.sprite = HudIcons.Pill;
        img.type = UnityEngine.UI.Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.4f;
        if (!on)
        {
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = KucaTheme.Line;
            o.effectDistance = new Vector2(2f, -2f);
        }
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        Text t = Text(img.transform, label, KucaTheme.Label, TextAnchor.MiddleCenter, on ? Color.white : KucaTheme.Ink);
        if (on) Bold(t);
        Stretch(t.rectTransform, 32f, 0f);
        var fit = img.gameObject.AddComponent<LayoutElement>();
        fit.preferredHeight = fit.minHeight = 104f;
        fit.preferredWidth = t.preferredWidth + 80f;
        return b;
    }

    /// <summary>빈 상태: 큰 제목 + 설명 + (선택) 할 일 버튼. "아무것도 없어요" 로 끝내지 않고 다음 행동을 준다</summary>
    public static RectTransform EmptyState(Transform parent, string title, string body, string actionLabel = null, UnityAction onAction = null)
    {
        RectTransform card = CardBox(parent, "Empty", 20f);
        ((VerticalLayoutGroup)card.GetComponent<VerticalLayoutGroup>()).padding = new RectOffset(48, 48, 72, 56);
        Text h = Bold(Text(card, title, KucaTheme.Heading, TextAnchor.MiddleCenter, KucaTheme.Ink));
        Size(h, 80f);
        Text b = Text(card, body, KucaTheme.Body, TextAnchor.UpperCenter, KucaTheme.InkMuted);
        b.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        if (!string.IsNullOrEmpty(actionLabel) && onAction != null)
        {
            RectTransform gap = Rect(card, "Gap");
            Size(gap, 16f);
            PrimaryButton(card, actionLabel, onAction);
        }
        return card;
    }

    /// <summary>설정형 목록 묶음: 회색 소제목 + 흰 둥근 카드. 반환값(카드)에 ListRow · 내용을 넣는다 (iOS 그룹 목록 · Material 목록)</summary>
    public static RectTransform ListGroup(Transform parent, string title)
    {
        if (!string.IsNullOrEmpty(title))
        {
            RectTransform head = Rect(parent, "Header");
            Size(head, 72f);
            Text h = Text(head, title, KucaTheme.Label, TextAnchor.LowerLeft, KucaTheme.InkMuted);
            h.fontStyle = FontStyle.Bold;
            Stretch(h.rectTransform, KucaTheme.Space3, 10f);
        }
        Image card = Image(parent, "Group", KucaTheme.Card);
        card.sprite = HudIcons.Rounded;
        card.type = UnityEngine.UI.Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.6f;
        Vertical(card, 0f, new RectOffset(0, 0, 4, 4));
        return card.rectTransform;
    }

    /// <summary>
    /// 목록 한 줄: 왼쪽 이름 + 오른쪽 화살표, 위에 옅은 구분선. danger 면 크림슨 글자 · 화살표 없음 (탈퇴 등).
    /// chevron = false: 다른 화면으로 가지 않고 바로 실행하는 줄 (화살표가 있으면 화면이 넘어가는 것처럼 보였음, REVIEW_v14)
    /// </summary>
    public static Button ListRow(Transform group, string label, UnityAction onClick, bool danger = false, bool chevron = true)
    {
        bool first = group.childCount == 0;
        Image row = Image(group, label, new Color(1f, 1f, 1f, 0f), raycast: true);
        Size(row, KucaTheme.TouchMin + 16f);
        var b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        Text t = Text(row.transform, label, KucaTheme.Body, TextAnchor.MiddleLeft, danger ? KucaTheme.Crimson : KucaTheme.Ink);
        Stretch(t.rectTransform, 40f, 0f);
        t.rectTransform.offsetMax = new Vector2(-100f, 0f);
        if (!danger && chevron)
        {
            Image chev = Image(row.transform, "Chevron", KucaTheme.InkSoft);
            chev.sprite = HudIcons.Chevron;
            RectTransform cr = chev.rectTransform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 0.5f);
            cr.anchoredPosition = new Vector2(-36f, 0f);
            cr.sizeDelta = new Vector2(40f, 40f);
        }
        if (!first)
        {
            Image line = Image(row.transform, "Line", KucaTheme.Line);
            RectTransform lr = line.rectTransform;
            lr.anchorMin = new Vector2(0f, 1f);
            lr.anchorMax = Vector2.one;
            lr.pivot = new Vector2(0.5f, 1f);
            lr.offsetMin = new Vector2(40f, -2f);
            lr.offsetMax = Vector2.zero;
        }
        return b;
    }

    public static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padX, padY);
        rt.offsetMax = new Vector2(-padX, -padY);
    }

    /// <summary>부모 기준으로 위/아래 여백을 둔 채 가로로 꽉 채운다.</summary>
    public static void StretchBetween(RectTransform rt, float bottom, float top)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0f, bottom);
        rt.offsetMax = new Vector2(0f, -top);
    }

    public static LayoutElement Size(Component c, float height = -1f, float width = -1f, float flexibleWidth = -1f)
    {
        if (!c.TryGetComponent(out LayoutElement le))
            le = c.gameObject.AddComponent<LayoutElement>();
        // 높이를 정했으면 늘어나지 않게: 안에 레이아웃 그룹(childForceExpand)이 있으면 flexibleHeight 1 을 보고해 남는 높이를 나눠 가짐
        if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; le.flexibleHeight = 0f; }
        if (width >= 0f) { le.preferredWidth = width; le.minWidth = width; }
        if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
        return le;
    }

    public static VerticalLayoutGroup Vertical(Component c, float spacing, RectOffset padding = null)
    {
        var v = c.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = padding ?? new RectOffset(0, 0, 0, 0);
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return v;
    }

    public static HorizontalLayoutGroup Horizontal(Component c, float spacing)
    {
        var h = c.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = true;
        return h;
    }

    /// <summary>세로로 스크롤되는 목록. 반환값(content)에 항목을 넣는다.</summary>
    public static RectTransform ScrollList(RectTransform area, float spacing, RectOffset padding)
    {
        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;

        RectTransform viewport = Rect(area, "Viewport");
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        // 빈 곳을 잡고도 스크롤되도록 투명 이미지로 입력을 받는다.
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        RectTransform content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        Vertical(content, spacing, padding);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    /// <summary>사진을 잘라서(가득 채워) 보여 주는 칸. 반환값에 texture 를 넣는다.</summary>
    public static RawImage PhotoBox(Transform parent, float height, out AspectRatioFitter fitter)
    {
        Image frame = Image(parent, "Photo", new Color(0f, 0f, 0f, 0.3f));
        Size(frame, height);
        frame.gameObject.AddComponent<RectMask2D>();
        var go = new GameObject("Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(frame.transform, false);
        var raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        fitter = go.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 4f / 3f;
        return raw;
    }

    public static void ShowPhoto(RawImage raw, AspectRatioFitter fitter, Texture tex)
    {
        raw.texture = tex;
        if (tex != null && tex.height > 0)
            fitter.aspectRatio = (float)tex.width / tex.height;
    }
}
