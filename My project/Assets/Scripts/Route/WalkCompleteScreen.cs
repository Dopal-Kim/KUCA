using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 산책 완료 화면 (B-V4). 추천 코스를 다 돌았거나 (또는 80% 이상), 산책메이트와 "산책 끝내기" 를 누르면 뜬다.
/// - 걸은 거리 · 시간 · 칼로리, "파트너와 사진 찍기" (A-V4 AR 사진 화면을 여는 진입점: PartnerPhoto)
/// - 리뷰: 산책메이트 별점 (함께 걸었을 때) · 코스 별점 · 한 줄 → 서버
/// - 보상: 서버가 정한 경험치 · 티켓 · 파트너 진화 경험치를 받아 더하고, 레벨 막대를 채운다
/// 서버 호출은 IWalkService: 서버에 API 가 없으면 기기 계산 (HttpWalkService → LocalWalkService).
/// </summary>
public class WalkCompleteScreen : MonoBehaviour
{
    static readonly Color Ink = new Color(0.11f, 0.11f, 0.12f);
    static readonly Color InkQuiet = new Color(0.11f, 0.11f, 0.12f, 0.55f);
    static readonly Color Accent = new Color(0.16f, 0.42f, 0.95f);
    static readonly Color Gold = new Color(0.98f, 0.72f, 0.12f);
    static readonly Color Field = new Color(0.94f, 0.95f, 0.96f);
    static readonly Color Green = new Color(0.24f, 0.68f, 0.38f);

    /// <summary>
    /// A-V4: 파트너와 AR 사진 화면을 여는 함수. CollectController 가 CatchCameraScreen.OpenPartnerPhoto 를 넣어 준다.
    /// 비어 있으면 (사진 화면이 없는 씬 등) 안내만.
    /// </summary>
    public static System.Action PartnerPhoto;

    IWalkService service;
    string serviceUrl;
    WalkCompleteRequest walk;
    WalkCompleteResult result;
    bool reviewSent, busy;
    int partnerStars, courseStars;

    GameObject root, partnerRow, reviewCard, rewardCard;
    Text title, sub, statMeters, statMinutes, statKcal, reason, rewardText, levelText, reviewNote;
    RectTransform levelFill;
    Image[] partnerStarImages, courseStarImages;
    InputField comment;
    Button sendButton;

    /// <summary>산책이 끝났을 때 (RoutePlanner · MatchScreen 이 부른다)</summary>
    public static void Show(WalkCompleteRequest finished)
    {
        WalkCompleteScreen s = FindAnyObjectByType<WalkCompleteScreen>();
        if (s == null)
            s = new GameObject("WalkCompleteScreen").AddComponent<WalkCompleteScreen>();
        s.Open(finished);
    }

    void Awake() => Build();

    void EnsureService()
    {
        var board = FindAnyObjectByType<MemoBoard>();
        string url = board != null ? board.ServerUrl : "";
        if (service != null && url == serviceUrl)
            return;
        serviceUrl = url;
        var local = new LocalWalkService();
        service = url.Length == 0 ? local : new HttpWalkService(url, local);
    }

    static GameProgress Progress
    {
        get
        {
            var collect = FindAnyObjectByType<CollectController>();
            return collect != null ? collect.Progress : null;
        }
    }

    void Open(WalkCompleteRequest finished)
    {
        EnsureService();
        walk = finished;
        result = null;
        reviewSent = false;
        partnerStars = courseStars = 0;
        comment.text = "";
        bool mate = walk.kind == WalkSession.Mate;
        title.text = "산책을 마쳤어요";
        sub.text = WalkSession.Title ?? "";
        statMeters.text = RouteMath.Distance(walk.meters);
        statMinutes.text = $"{walk.minutes}분";
        statKcal.text = $"{walk.kcal}kcal";
        reason.text = "확인하는 중…";
        partnerRow.SetActive(mate);
        rewardText.text = "";
        levelText.text = "";
        RefreshStars();
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);

        GameProgress p = Progress;
        int score = p != null ? p.score : 0;
        busy = true;
        StartCoroutine(service.Complete(walk, score, (r, error) =>
        {
            busy = false;
            if (r == null)
            {
                reason.text = error ?? "완료를 확인하지 못했어요";
                return;
            }
            result = r;
            title.text = r.completed ? (mate ? "함께 산책 완료!" : "산책 완료!") : "산책을 마쳤어요";
            reason.text = r.reason;
            reason.color = r.completed ? Green : InkQuiet;
            ApplyReward(r.xp, r.tickets, r.growth);
            StartCoroutine(FillLevel(score, r));
        }));
    }

    /// <summary>받은 보상을 기기 저장에 더한다 (값은 서버가 정함)</summary>
    void ApplyReward(int xp, int tickets, int growth, string label = "경험치")
    {
        GameProgress p = Progress;
        if (p == null)
            return;
        p.score += Mathf.Max(0, xp);
        p.tickets += Mathf.Max(0, tickets);
        bool evolved = false;
        int stage = 0;
        string partnerName = null;
        if (growth > 0 && p.Partner != null && !string.IsNullOrEmpty(p.Partner.speciesId))
        {
            int before = p.StageOf(p.Partner.speciesId);
            evolved = p.AddGrowth(p.Partner.speciesId, growth, out stage);
            if (evolved)
                EvolutionScreen.Queue(p.Partner.speciesId, before, stage); // 완료 화면을 닫으면 뜬다
            partnerName = CreatureLibrary.NameOf(p.Partner.speciesId);
        }
        p.Save();
        var hud = FindAnyObjectByType<GameHUD>();
        var collect = FindAnyObjectByType<CollectController>();
        if (hud != null && collect != null)
            hud.ShowProgress(p, collect.spawner != null ? collect.spawner.types : null);

        var parts = new System.Collections.Generic.List<string>();
        if (xp > 0) parts.Add($"{label} +{xp}");
        if (tickets > 0) parts.Add($"티켓 +{tickets}");
        if (growth > 0 && partnerName != null) parts.Add($"{partnerName} 진화 경험치 +{growth}");
        if (evolved && p.Partner != null)
        {
            parts.Add(KhuMenus.EvolvedMessage(p.Partner.speciesId, stage));
            FindAnyObjectByType<KhuMenus>()?.RefreshPartner();
        }
        if (parts.Count > 0)
            rewardText.text = (rewardText.text.Length > 0 ? rewardText.text + "\n" : "") + string.Join("   ", parts);
        else if (rewardText.text.Length == 0)
            rewardText.text = "이번 산책은 보상이 없어요";
    }

    /// <summary>레벨 막대가 보상 전 값에서 보상 뒤 값으로 차오른다</summary>
    IEnumerator FillLevel(int before, WalkCompleteResult r)
    {
        GameHUD.LevelOf(before, out int lv0, out int in0, out int need0);
        float from = need0 > 0 ? (float)in0 / need0 : 0f;
        float to = r.xpNeeded > 0 ? (float)r.xpInLevel / r.xpNeeded : 0f;
        if (r.level > lv0) from = 0f; // 레벨이 올랐으면 새 레벨의 처음부터
        levelText.text = (r.level > lv0 ? $"레벨 {r.level} 달성!  " : $"레벨 {r.level}  ") + $"{r.xpInLevel}/{r.xpNeeded} XP";
        for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime)
        {
            levelFill.anchorMax = new Vector2(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.8f)), 1f);
            yield return null;
        }
        levelFill.anchorMax = new Vector2(to, 1f);
    }

    // ---------- 리뷰 ----------

    void SetStars(bool partner, int n)
    {
        if (reviewSent) return;
        if (partner) partnerStars = n; else courseStars = n;
        RefreshStars();
    }

    void RefreshStars()
    {
        for (int i = 0; i < 5; i++)
        {
            partnerStarImages[i].color = i < partnerStars ? Gold : new Color(0f, 0f, 0f, 0.12f);
            courseStarImages[i].color = i < courseStars ? Gold : new Color(0f, 0f, 0f, 0.12f);
        }
        sendButton.interactable = !reviewSent;
        UIKit.SetLabel(sendButton, reviewSent ? "리뷰를 보냈어요" : $"리뷰 보내기  (+{LocalWalkService.ReviewXp} XP)");
    }

    void SendReview()
    {
        if (busy || reviewSent || result == null) return;
        bool mate = walk.kind == WalkSession.Mate;
        if (courseStars == 0 || (mate && partnerStars == 0))
        {
            reviewNote.text = mate ? "산책메이트와 코스 별점을 골라 주세요" : "코스 별점을 골라 주세요";
            return;
        }
        var req = new WalkReviewRequest { partnerStars = mate ? partnerStars : 0, courseStars = courseStars, comment = comment.text.Trim() };
        busy = true;
        StartCoroutine(service.Review(result.walkId, req, (r, error) =>
        {
            busy = false;
            if (r == null) { reviewNote.text = error ?? "리뷰를 보내지 못했어요"; return; }
            reviewSent = true;
            reviewNote.text = mate ? "고마워요! 산책메이트 별점은 상대에게 보이지 않아요" : "고마워요! 다음 추천에 반영할게요";
            ApplyReward(r.xp, 0, 0, "리뷰 경험치");
            GameProgress p = Progress;
            if (p != null)
            {
                GameHUD.LevelOf(p.score, out int lv, out int inLv, out int need);
                levelText.text = $"레벨 {lv}  {inLv}/{need} XP";
                levelFill.anchorMax = new Vector2(need > 0 ? (float)inLv / need : 0f, 1f);
            }
            RefreshStars();
        }));
    }

    void TakePhoto()
    {
        if (PartnerPhoto != null)
        {
            PartnerPhoto();
            return;
        }
        var hud = FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.Toast("지금은 파트너와 사진을 찍을 수 없어요");
    }

    void Close()
    {
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    // ---------- 화면 만들기 ----------

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "WalkCompleteCanvas", 52);
        root = UIKit.Image(canvas.transform, "WalkComplete", new Color(0.98f, 0.98f, 1f), raycast: true).gameObject;
        UIKit.Stretch((RectTransform)root.transform);
        Rect sa = Screen.safeArea;
        RectTransform safe = UIKit.Rect(root.transform, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        RectTransform area = UIKit.Rect(safe, "Area");
        UIKit.StretchBetween(area, 180f, 0f);
        RectTransform list = UIKit.ScrollList(area, 26f, new RectOffset(60, 60, 60, 60));

        title = Line(list, "", 60, Ink, 90f, true);
        title.alignment = TextAnchor.MiddleCenter;
        sub = Line(list, "", 32, InkQuiet, 50f);
        sub.alignment = TextAnchor.MiddleCenter;

        // 걸은 거리 · 시간 · 칼로리
        RectTransform stats = UIKit.Rect(list, "Stats");
        UIKit.Size(stats, 190f);
        var h = UIKit.Horizontal(stats, 20f);
        h.childForceExpandWidth = true;
        statMeters = Stat(stats, "걸은 거리");
        statMinutes = Stat(stats, "시간");
        statKcal = Stat(stats, "칼로리");
        Line(list, RouteMath.HealthNote, 24, InkQuiet, 36f).alignment = TextAnchor.MiddleCenter;
        reason = Line(list, "", 32, InkQuiet, 50f, true);
        reason.alignment = TextAnchor.MiddleCenter;

        Button photo = Pill(list, "파트너와 사진 찍기", Ink, Color.white, TakePhoto);
        UIKit.Size(photo, 116f);

        // 보상
        rewardCard = Card(list, "Reward", out RectTransform rc);
        Line(rc, "이번 산책 보상", 34, Ink, 50f, true);
        rewardText = Line(rc, "", 32, Ink, 90f);
        levelText = Line(rc, "", 28, InkQuiet, 40f);
        Image track = UIKit.Image(rc, "Track", new Color(0f, 0f, 0f, 0.08f));
        track.sprite = HudIcons.Pill;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 6f;
        UIKit.Size(track, 24f);
        Image fill = UIKit.Image(track.transform, "Fill", Gold);
        fill.sprite = HudIcons.Pill;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 6f;
        levelFill = fill.rectTransform;
        levelFill.anchorMin = Vector2.zero;
        levelFill.anchorMax = new Vector2(0f, 1f);
        levelFill.offsetMin = levelFill.offsetMax = Vector2.zero;

        // 리뷰
        reviewCard = Card(list, "Review", out RectTransform vc);
        Line(vc, "산책은 어땠어요?", 34, Ink, 50f, true);
        partnerRow = UIKit.Rect(vc, "PartnerRow").gameObject;
        UIKit.Size(partnerRow.GetComponent<RectTransform>(), 120f);
        UIKit.Vertical(partnerRow.GetComponent<RectTransform>(), 6f);
        Line(partnerRow.transform, "산책메이트  (상대에게 보이지 않아요)", 28, InkQuiet, 40f);
        partnerStarImages = Stars(partnerRow.transform, n => SetStars(true, n));
        Line(vc, "코스", 28, InkQuiet, 40f);
        courseStarImages = Stars(vc, n => SetStars(false, n));
        comment = UIKit.Input(vc, "한 줄 남기기 (선택, 60자)", 32, false, 60);
        Image cb = comment.GetComponent<Image>();
        cb.color = Field;
        cb.sprite = HudIcons.Rounded;
        cb.type = Image.Type.Sliced;
        cb.pixelsPerUnitMultiplier = 0.8f;
        comment.textComponent.color = Ink;
        ((Text)comment.placeholder).color = InkQuiet;
        UIKit.Size(comment, 100f);
        sendButton = Pill(vc, "리뷰 보내기", Accent, Color.white, SendReview);
        UIKit.Size(sendButton, 104f);
        reviewNote = Line(vc, "", 26, InkQuiet, 40f);

        Button close = Pill(safe, "확인", Ink, Color.white, Close);
        RectTransform cr = (RectTransform)close.transform;
        cr.anchorMin = new Vector2(0f, 0f);
        cr.anchorMax = new Vector2(1f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.offsetMin = new Vector2(60f, 50f);
        cr.offsetMax = new Vector2(-60f, 166f);

        root.SetActive(false);
    }

    static Text Line(Transform parent, string text, int size, Color color, float height, bool bold = false)
    {
        Text t = UIKit.Text(parent, text, size, TextAnchor.MiddleLeft, color);
        if (bold) t.fontStyle = FontStyle.Bold;
        UIKit.Size(t, height);
        return t;
    }

    static Text Stat(Transform parent, string label)
    {
        Image box = UIKit.Image(parent, label, Color.white);
        box.sprite = HudIcons.Rounded;
        box.type = Image.Type.Sliced;
        box.pixelsPerUnitMultiplier = 0.6f;
        Text v = UIKit.Text(box.transform, "", 46, TextAnchor.MiddleCenter, Ink);
        v.fontStyle = FontStyle.Bold;
        v.rectTransform.anchorMin = new Vector2(0f, 0.38f);
        v.rectTransform.anchorMax = Vector2.one;
        v.rectTransform.offsetMin = v.rectTransform.offsetMax = Vector2.zero;
        Text l = UIKit.Text(box.transform, label, 26, TextAnchor.MiddleCenter, InkQuiet);
        l.rectTransform.anchorMin = Vector2.zero;
        l.rectTransform.anchorMax = new Vector2(1f, 0.4f);
        l.rectTransform.offsetMin = l.rectTransform.offsetMax = Vector2.zero;
        return v;
    }

    static GameObject Card(Transform parent, string name, out RectTransform content)
    {
        Image card = UIKit.Image(parent, name, Color.white);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        UIKit.Vertical(card, 14f, new RectOffset(40, 40, 34, 36));
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        content = card.rectTransform;
        return card.gameObject;
    }

    static Image[] Stars(Transform parent, System.Action<int> onPick)
    {
        RectTransform row = UIKit.Rect(parent, "Stars");
        UIKit.Size(row, 76f);
        var h = UIKit.Horizontal(row, 12f);
        h.childForceExpandWidth = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        var images = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            int n = i + 1;
            Image star = UIKit.Image(row, "Star" + n, new Color(0f, 0f, 0f, 0.12f), raycast: true);
            star.sprite = HudIcons.Star;
            UIKit.Size(star, 76f, 76f);
            var b = star.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onPick(n));
            star.gameObject.AddComponent<PressScale>();
            images[i] = star;
        }
        return images;
    }

    static Button Pill(Transform parent, string label, Color face, Color ink, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, face, 38);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 120f / 110f;
        Text t = b.GetComponentInChildren<Text>();
        t.color = ink;
        t.fontStyle = FontStyle.Bold;
        b.gameObject.AddComponent<PressScale>();
        return b;
    }
}
