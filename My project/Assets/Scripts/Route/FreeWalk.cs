using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코스 없이 자유 산책: 시작부터 끝까지 걸은 길을 기록하고 (WalkTrack), 지도에 지나온 길을 그리고,
/// 끝내면 완료 화면에서 걸은 길을 돌아본다. 서버에는 추천 ID 없이 산책 시작을 알린다 (500 m 이상이면 완료).
/// </summary>
public class FreeWalk : MonoBehaviour
{
    static readonly Color Ink = KucaTheme.Ink;
    static readonly Color ChipOff = KucaTheme.Fill;
    const float PanelHeight = 280f;
    /// <summary>자유 산책 선 색 (코스 색 파랑 · 보라 · 분홍과 다르게)</summary>
    public static readonly Color TrackColor = new Color(0.05f, 0.62f, 0.66f);

    static FreeWalk instance;
    int session = -1;
    bool showPath = true;

    GameObject panel;
    Text title, stats;
    Image toggle;
    Text toggleText;
    MapRouteLine line;

    /// <summary>자유 산책 중인지</summary>
    public static bool Active => instance != null && instance.session >= 0 && WalkSession.Active && WalkSession.Id == instance.session;

    public static void Begin()
    {
        if (instance == null)
            instance = new GameObject("FreeWalk").AddComponent<FreeWalk>();
        instance.BeginWalk();
    }

    void Awake() => Build();

    void BeginWalk()
    {
        if (WalkSession.Active)
        {
            Toast("이미 산책 중이에요. 지금 산책을 끝내고 시작해 주세요");
            return;
        }
        WalkSession.BeginFree();
        session = WalkSession.Id;
        showPath = true;
        // 서버에 산책 시작 (추천 ID 없음 = 자유 산책). 실패하면 완료는 기기에서 판정
        StartCoroutine(WalkCompleteScreen.CreateService().Begin("", 30, WalkSession.WalkIdSetter()));
        Refresh();
        Toast("자유 산책을 시작해요. 걸은 길이 기록돼요");
    }

    void End()
    {
        if (!Active)
            return;
        session = -1;
        if (line != null) line.SetVisible(false);
        panel.SetActive(false);
        WalkCompleteScreen.Show(WalkSession.Finish(null));
    }

    void Update()
    {
        bool on = Active;
        if (!on && session >= 0)
            session = -1;   // 다른 산책(산책메이트 등)이 시작돼 이 산책은 끝남
        bool show = on && !UIInputBlocker.IsModalOpen;
        if (panel.activeSelf != show)
            panel.SetActive(show);
        if (show)
            VirtualWalk.PlacePanel((RectTransform)panel.transform, PanelHeight);
        if (line != null)
            line.SetVisible(on && showPath);
        if (on && Time.frameCount % 20 == 0)
            Refresh();
    }

    void Refresh()
    {
        if (!Active)
            return;
        int seconds = Mathf.RoundToInt(WalkSession.Seconds);
        stats.text = $"{RouteMath.Distance(WalkSession.WalkedMeters)}  ·  {seconds / 60}분 {seconds % 60:00}초";
        int left = LocalWalkService.MateMeters - Mathf.RoundToInt(WalkSession.WalkedMeters);
        title.text = left > 0
            ? $"자유 산책 중  <color=#8B95A1>완료까지 {left}m</color>"
            : "자유 산책 중  <color=#2B7A4B>완료!</color>";
        toggle.color = showPath ? TrackColor : ChipOff;
        toggleText.color = showPath ? Color.white : Ink;
        toggleText.text = showPath ? "경로 켜짐" : "경로 꺼짐";
        var points = WalkTrack.Current;
        if (points.Count >= 2)
        {
            if (line == null)
                line = MapRouteLine.Create("FreeWalkLine", transform);
            line.Set(points, TrackColor, 3.6f, 1.6f);
        }
    }

    static void Toast(string message)
    {
        var hud = FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.Toast(message);
    }

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "FreeWalkCanvas", 20);
        RectTransform root = (RectTransform)canvas.transform;

        // 산책 카드 (애플 지도 · 스트라바 기록 중 카드): 상태 한 줄 · 큰 숫자 · 오른쪽 경로 칩 + 끝내기
        Image card = UIKit.Image(root, "FreeWalkPanel", KucaTheme.Card, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.34f;
        panel = card.gameObject;
        RectTransform pr = card.rectTransform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(-2f * KucaTheme.Space4, PanelHeight);
        UIInputBlocker.Register(pr);

        Image dot = UIKit.Image(pr, "Dot", TrackColor);
        dot.sprite = HudIcons.Footprints;
        Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(48f, -44f));
        dot.rectTransform.sizeDelta = new Vector2(56f, 56f);
        title = UIKit.Bold(UIKit.Text(pr, "", KucaTheme.Label, TextAnchor.MiddleLeft, Ink));
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        Box(title.rectTransform, 120f, 40f, 300f, 64f);
        stats = UIKit.Bold(UIKit.Text(pr, "", KucaTheme.Title, TextAnchor.MiddleLeft, Ink));
        stats.horizontalOverflow = HorizontalWrapMode.Overflow;
        Box(stats.rectTransform, 48f, 128f, 300f, 100f);

        Button t = Pill(pr, "경로 켜짐", TrackColor, Color.white, () => { showPath = !showPath; Refresh(); });
        toggle = t.GetComponent<Image>();
        toggleText = t.GetComponentInChildren<Text>();
        Place((RectTransform)t.transform, new Vector2(1f, 1f), new Vector2(-40f, -40f));
        ((RectTransform)t.transform).sizeDelta = new Vector2(240f, 88f);
        Button end = UIKit.Button(pr, "끝내기", End, KucaTheme.Primary, KucaTheme.Label);
        Place((RectTransform)end.transform, new Vector2(1f, 0f), new Vector2(-40f, 40f));
        ((RectTransform)end.transform).sizeDelta = new Vector2(240f, 112f);
        panel.SetActive(false);
    }

    static Button Pill(Transform parent, string label, Color color, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, color, KucaTheme.Caption);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.4f;
        b.GetComponentInChildren<Text>().color = textColor;
        b.gameObject.AddComponent<PressScale>();
        return b;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    static void Box(RectTransform rt, float left, float top, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
