using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 산책 기록 (메뉴 › 산책 기록). 끝난 산책을 최근 것부터 보여 주고, 누르면 걸은 길을 지도에서 다시 본다.
/// 기록은 기기 안 (WalkHistory) 에 있다. v4: 스트라바 활동 목록 — 카드마다 날짜 · 코스 이름 · 큰 숫자 줄 (docs/디자인_v4.md).
/// </summary>
public class WalkHistoryScreen : MonoBehaviour
{
    GameObject root, detail;
    RectTransform list;
    Text summary, detailTitle, detailStats;
    RawImage detailMap;
    RectTransform detailOverlay;
    RenderTexture detailTexture;
    RectTransform detailFrame;
    Button detailShare;
    WalkHistory.Entry detailEntry;
    bool sharing;

    IEnumerator UnlockAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        sharing = false;
    }

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
        summary.text = all.Count == 0 ? "아직 걸은 기록이 없어요" : $"이번 주 {weekCount}번  ·  {RouteMath.Distance(weekMeters)} 걸었어요   " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(WalkCompleteScreen.LineColor(WalkSession.Course, 3, 1))}>■</color> 코스  " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(FreeWalk.TrackColor)}>■</color> 자유  " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(WalkCompleteScreen.LineColor(WalkSession.Mate, 0, 0))}>■</color> 산책메이트";
        if (all.Count == 0)
            UIKit.EmptyState(list, "첫 산책을 시작해 보세요", "코스를 걷거나 자유 산책을 끝내면\n여기에 기록이 남아요.", "산책 코스 고르기",
                () => { Close(); RoutePlanner.Open(); });
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

    /// <summary>스트라바 활동 카드: 날짜 · 굵은 코스 이름 · 큰 숫자 줄. 왼쪽 색 띠 = 산책 종류 (지도 선 색과 같음)</summary>
    void Row(WalkHistory.Entry e)
    {
        RectTransform card = UIKit.CardBox(list, "Walk", KucaTheme.Space1);
        var b = card.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        WalkHistory.Entry entry = e;
        b.onClick.AddListener(() => OpenDetail(entry));
        card.gameObject.AddComponent<PressScale>();

        Image bar = UIKit.Image(card, "Bar", WalkCompleteScreen.LineColor(e.kind, e.stopsTotal, 0));
        bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        bar.sprite = HudIcons.Pill;
        bar.type = Image.Type.Sliced;
        RectTransform br = bar.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 0.5f);
        br.offsetMin = new Vector2(18f, 48f);
        br.offsetMax = new Vector2(30f, -48f);

        string when = When(e.EndedAt) + (e.completed ? "" : "  ·  중간에 끝냄");   // 완료가 기본: 다른 것만 표시
        UIKit.Size(UIKit.Text(card, when, KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkMuted), 48f);
        Text name = UIKit.Bold(UIKit.Text(card, e.title, KucaTheme.Heading, TextAnchor.MiddleLeft, KucaTheme.Ink));
        UIKit.Size(name, 72f);
        if (e.stopsTotal > 0)
            UIKit.StatRow(card, (RouteMath.Distance(e.meters), "거리"), ($"{e.minutes}분", "시간"), ($"{e.stopsDone}/{e.stopsTotal}", "들른 곳"));
        else
            UIKit.StatRow(card, (RouteMath.Distance(e.meters), "거리"), ($"{e.minutes}분", "시간"));
    }

    void ShareToKakao()
    {
        WalkHistory.Entry e = detailEntry;
        if (sharing || e == null)
            return;
        sharing = true;
        FindAnyObjectByType<GameHUD>()?.Toast("공유 준비 중…");
        StartCoroutine(UnlockAfter(10f));   // 중간에 예외가 나도 버튼이 묶여 있지 않게
        StartCoroutine(WalkShare.Share(detailFrame, WalkShare.KindOf(e.kind, e.stopsTotal, 0), e.meters, e.minutes, e.visitedBuildingIds, error =>
        {
            sharing = false;
            var hud = FindAnyObjectByType<GameHUD>();
            if (hud != null && error != null) hud.Toast(error);
        }));
    }

    void OpenDetail(WalkHistory.Entry e)
    {
        detailEntry = e;
        detailTitle.text = e.title;
        detailStats.text = $"{When(e.EndedAt)}\n{Stats(e)}" + (e.completed ? "  ·  완료" : "  ·  중간에 끝냄");
        detail.SetActive(true);
        detail.transform.SetAsLastSibling();
        Canvas.ForceUpdateCanvases();
        List<Vector3> track = e.Track;
        TopMap.Clear(detailOverlay);
        detailShare.gameObject.SetActive(track.Count >= 2);
        if (track.Count >= 2)
            WalkCompleteScreen.DrawTrack(detailMap, ref detailTexture, detailOverlay, track, WalkCompleteScreen.LineColor(e.kind, e.stopsTotal, 0));
        else
        {
            detailMap.texture = null;
            Text t = UIKit.Text(detailOverlay, "걸은 길 기록이 없어요", KucaTheme.Body, TextAnchor.MiddleCenter, Color.white);
            UIKit.Stretch(t.rectTransform);
        }
    }

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "WalkHistoryCanvas", 50);
        UIKit.Page page = UIKit.CreatePage(canvas.transform, "WalkHistory", "산책 기록", Close, " ");
        root = page.root;
        summary = page.subtitle;
        list = page.content;

        // 기록 하나 자세히 (지도): 어둡게 덮고 흰 카드
        detail = UIKit.Image(canvas.transform, "Detail", KucaTheme.Scrim, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)detail.transform);
        detail.AddComponent<Button>().onClick.AddListener(() => detail.SetActive(false));
        BackButton.Attach(detail, () => detail.SetActive(false));
        Image card = UIKit.Image(detail.transform, "Card", KucaTheme.Card, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.34f;
        card.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // 카드 안을 눌러도 닫히지 않게
        RectTransform cr = card.rectTransform;
        cr.anchorMin = new Vector2(0f, 0.5f);
        cr.anchorMax = new Vector2(1f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.offsetMin = new Vector2(KucaTheme.Space4, -720f);
        cr.offsetMax = new Vector2(-KucaTheme.Space4, 720f);
        detailTitle = UIKit.Bold(UIKit.Text(cr, "", KucaTheme.Heading, TextAnchor.MiddleLeft, KucaTheme.Ink));
        Box(detailTitle.rectTransform, 56f, 48f, 170f, 80f);
        var x = (RectTransform)UIKit.IconButton(cr, HudIcons.Close, () => detail.SetActive(false), KucaTheme.Ink, Vector2.zero).transform;
        x.anchorMin = x.anchorMax = x.pivot = Vector2.one;   // 카드 오른쪽 위 × (팝업 카드 규칙)
        x.anchoredPosition = new Vector2(-24f, -24f);
        detailStats = UIKit.Text(cr, "", KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
        Box(detailStats.rectTransform, 56f, 140f, 56f, 110f);
        Image frame = TopMap.Frame(cr, "Map", out detailMap, out detailOverlay);
        Box(frame.rectTransform, 40f, 270f, 40f, 990f);
        detailFrame = frame.rectTransform;
        detailShare = WalkCompleteScreen.KakaoButton(cr, () => ShareToKakao());
        Box((RectTransform)detailShare.transform, 40f, 1290f, 40f, 104f);   // 지도와 30 띄움 (16 은 붙어 보였음, REVIEW_v40)
        detail.SetActive(false);
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
