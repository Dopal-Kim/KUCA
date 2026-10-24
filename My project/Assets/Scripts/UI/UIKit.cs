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

    static Font font;
    public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

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
        t.fontSize = size;
        t.alignment = anchor;
        t.color = color ?? Color.white;
        t.text = text;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    /// <summary>둥근 알약 모양 버튼 (모든 화면 공통 모양, docs/인터페이스_기준.md)</summary>
    public static Button Button(Transform parent, string label, UnityAction onClick, Color color, int fontSize = 44)
    {
        Image img = Image(parent, label, color, raycast: true);
        img.sprite = HudIcons.Pill;
        img.type = UnityEngine.UI.Image.Type.Sliced;
        var button = img.gameObject.AddComponent<Button>();
        button.onClick.AddListener(onClick);
        Text t = Text(img.transform, label, fontSize, TextAnchor.MiddleCenter);
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

    /// <summary>목록 한 줄: 왼쪽 이름 + 오른쪽 화살표, 위에 옅은 구분선. danger 면 크림슨 글자 · 화살표 없음 (탈퇴 등)</summary>
    public static Button ListRow(Transform group, string label, UnityAction onClick, bool danger = false)
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
        if (!danger)
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
        if (height >= 0f) { le.preferredHeight = height; le.minHeight = height; }
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
