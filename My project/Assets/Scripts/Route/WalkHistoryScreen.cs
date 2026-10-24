using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 산책 기록 (메뉴 › 산책 기록). 끝난 산책을 최근 것부터 보여 주고, 누르면 걸은 길을 지도에서 다시 본다.
/// 기록은 기기 안 (WalkHistory) 에 있다.
/// </summary>
public class WalkHistoryScreen : MonoBehaviour
{
    static readonly Color Ink = KucaTheme.Ink;
    static readonly Color InkQuiet = KucaTheme.InkMuted;
    static readonly Color Page = KucaTheme.Surface;
    static readonly Color Dim = KucaTheme.Scrim;
    static readonly Color Green = KucaTheme.Primary;

    GameObject root, detail;
    RectTransform list;
    Text summary, detailTitle, detailStats;
    RawImage detailMap;
    RectTransform detailOverlay;
    RenderTexture detailTexture;

    public static void Show()
    {
        WalkHistoryScreen s = FindAnyObjectByType<WalkHistoryScreen>();
        if (s == null)
            s = new GameObject("WalkHistoryScreen").AddComponent<WalkHistoryScreen>();
        s.Open();
    }

    void Awake() => Build();

    void Open()
    {
        TopMap.Clear(list);
        List<WalkHistory.Entry> all = WalkHistory.All;
        // 이번 주 (월요일부터)
        System.DateTime now = System.DateTime.Now;
        System.DateTime monday = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        int weekCount = 0, weekMeters = 0;
        foreach (WalkHistory.Entry e in all)
            if (e.EndedAt >= monday) { weekCount++; weekMeters += e.meters; }
        summary.text = all.Count == 0 ? "" : $"이번 주 {weekCount}번  ·  {RouteMath.Distance(weekMeters)} 걸었어요";
        if (all.Count == 0)
            UIKit.Size(UIKit.Text(list, "아직 기록이 없어요.\n코스를 걷거나 자유 산책을 끝내면 여기에 남아요.", 32, TextAnchor.MiddleCenter, InkQuiet), 260f);
        foreach (WalkHistory.Entry e in all)
            Row(e);
        detail.SetActive(false);
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        UIInputBlocker.SetModal(this, true);
    }

    void Close()
    {
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    void OnDestroy()
    {
        if (detailTexture != null) detailTexture.Release();
    }

    static string When(System.DateTime t)
    {
        string[] days = { "일", "월", "화", "수", "목", "금", "토" };
        string ampm = t.Hour < 12 ? "오전" : "오후";
        int h = t.Hour % 12 == 0 ? 12 : t.Hour % 12;
        return $"{t.Month}월 {t.Day}일 ({days[(int)t.DayOfWeek]}) {ampm} {h}:{t.Minute:00}";
    }

    static string Stats(WalkHistory.Entry e)
    {
        string s = $"{RouteMath.Distance(e.meters)}  ·  {e.minutes}분";
        if (e.stopsTotal > 0) s += $"  ·  {e.stopsDone}/{e.stopsTotal}곳";
        return s;
    }

    void Row(WalkHistory.Entry e)
    {
        Image row = UIKit.Image(list, "Walk", Color.white, raycast: true);
        row.sprite = HudIcons.Rounded;
        row.type = Image.Type.Sliced;
        row.pixelsPerUnitMultiplier = 0.6f;
        UIKit.Size(row, 190f);
        var b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        WalkHistory.Entry entry = e;
        b.onClick.AddListener(() => OpenDetail(entry));
        row.gameObject.AddComponent<PressScale>();

        Color c = WalkCompleteScreen.LineColor(e.kind, e.stopsTotal, 0);
        Image bar = UIKit.Image(row.transform, "Bar", c);
        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.sizeDelta = new Vector2(14f, -40f);
        bar.rectTransform.anchoredPosition = new Vector2(20f, 0f);

        Text when = UIKit.Text(row.transform, When(e.EndedAt), 26, TextAnchor.UpperLeft, InkQuiet);
        Box(when.rectTransform, 60f, 24f, 200f, 40f);
        Text name = UIKit.Text(row.transform, e.title, 36, TextAnchor.UpperLeft, Ink);
        name.fontStyle = FontStyle.Bold;
        Box(name.rectTransform, 60f, 66f, 200f, 52f);
        Text stats = UIKit.Text(row.transform, Stats(e), 30, TextAnchor.UpperLeft, Ink);
        Box(stats.rectTransform, 60f, 124f, 200f, 44f);
        Text done = UIKit.Text(row.transform, e.completed ? "완료" : "", 28, TextAnchor.MiddleRight, Green);
        done.fontStyle = FontStyle.Bold;
        Box(done.rectTransform, 0f, 0f, 44f, 190f);
        done.rectTransform.anchorMin = new Vector2(0.7f, 1f);
    }

    void OpenDetail(WalkHistory.Entry e)
    {
        detailTitle.text = e.title;
        detailStats.text = $"{When(e.EndedAt)}\n{Stats(e)}" + (e.completed ? "  ·  <color=#2E9E5C>완료</color>" : "");
        detail.SetActive(true);
        detail.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        List<Vector3> track = e.Track;
        TopMap.Clear(detailOverlay);
        if (track.Count >= 2)
            WalkCompleteScreen.DrawTrack(detailMap, ref detailTexture, detailOverlay, track, WalkCompleteScreen.LineColor(e.kind, e.stopsTotal, 0));
        else
        {
            detailMap.texture = null;
            Text t = UIKit.Text(detailOverlay, "걸은 길 기록이 없어요", 32, TextAnchor.MiddleCenter, Color.white);
            UIKit.Stretch(t.rectTransform);
        }
    }

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "WalkHistoryCanvas", 50);
        root = UIKit.Image(canvas.transform, "WalkHistory", Page, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)root.transform);
        Rect sa = Screen.safeArea;
        RectTransform safe = UIKit.Rect(root.transform, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        Text title = UIKit.Text(safe, "산책 기록", 52, TextAnchor.MiddleLeft, Ink);
        title.fontStyle = FontStyle.Bold;
        Box(title.rectTransform, 60f, 40f, 160f, 80f);
        CloseButton(safe, Close);
        summary = UIKit.Text(safe, "", 30, TextAnchor.MiddleLeft, InkQuiet);
        Box(summary.rectTransform, 60f, 124f, 60f, 50f);

        RectTransform area = UIKit.Rect(safe, "List");
        UIKit.StretchBetween(area, 0f, 190f);
        list = UIKit.ScrollList(area, 20f, new RectOffset(48, 48, 10, 60));

        // 기록 하나 자세히 (지도)
        detail = UIKit.Image(canvas.transform, "Detail", Dim, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)detail.transform);
        detail.AddComponent<Button>().onClick.AddListener(() => detail.SetActive(false));
        Image card = UIKit.Image(detail.transform, "Card", Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // 카드 안을 눌러도 닫히지 않게
        RectTransform cr = card.rectTransform;
        cr.anchorMin = new Vector2(0.04f, 0.5f);
        cr.anchorMax = new Vector2(0.96f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(0f, 1300f);
        detailTitle = UIKit.Text(cr, "", 42, TextAnchor.MiddleLeft, Ink);
        detailTitle.fontStyle = FontStyle.Bold;
        Box(detailTitle.rectTransform, 50f, 40f, 140f, 64f);
        CloseButton(cr, () => detail.SetActive(false));
        detailStats = UIKit.Text(cr, "", 30, TextAnchor.UpperLeft, InkQuiet);
        detailStats.lineSpacing = 1.15f;
        Box(detailStats.rectTransform, 50f, 110f, 50f, 90f);
        Image frame = TopMap.Frame(cr, "Map", out detailMap, out detailOverlay);
        Box(frame.rectTransform, 40f, 220f, 40f, 1040f);
        detail.SetActive(false);
        root.SetActive(false);
    }

    static void CloseButton(RectTransform parent, UnityEngine.Events.UnityAction onClick)
    {
        Image close = UIKit.Image(parent, "Close", new Color(0f, 0f, 0f, 0f), raycast: true);
        close.rectTransform.anchorMin = close.rectTransform.anchorMax = close.rectTransform.pivot = new Vector2(1f, 1f);
        close.rectTransform.anchoredPosition = new Vector2(-30f, -36f);
        close.rectTransform.sizeDelta = new Vector2(96f, 96f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
        Image ci = UIKit.Image(close.transform, "Icon", Ink);
        ci.sprite = HudIcons.Close;
        ci.rectTransform.anchorMin = ci.rectTransform.anchorMax = ci.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ci.rectTransform.sizeDelta = new Vector2(44f, 44f);
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
