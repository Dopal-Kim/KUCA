using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 산책 완료 화면 (B-V4). 추천 코스를 다 돌았거나 (또는 80% 이상), 산책메이트와 "산책 끝내기" 를 누르면 뜬다.
/// - 걸은 거리 · 시간 · 칼로리, "파트너와 사진 찍기" (A-V4 AR 사진 화면을 여는 진입점: PartnerPhoto)
/// - 리뷰: 산책메이트 별점 (함께 걸었을 때) · 코스 별점 · 한 줄 → 서버
/// - 보상: 서버가 정한 경험치 · 티켓 · 파트너 진화 경험치를 받아 더하고, 레벨 막대를 채운다
/// 서버 호출은 IWalkService: 서버에 API 가 없으면 기기 계산 (HttpWalkService → LocalWalkService).
/// v4 (docs/디자인_v4.md): 애플 피트니스 운동 요약 + 듀오링고 레슨 완료 — 큰 숫자 3개 · 걸은 길 카드 · 보상 카드 · 리뷰 카드,
/// 아래 꽉 찬 "확인" 하나.
/// </summary>
public class WalkCompleteScreen : MonoBehaviour
{
    static readonly Color Ink = KucaTheme.Ink;
    static readonly Color InkQuiet = KucaTheme.InkMuted;
    static readonly Color Accent = KucaTheme.Primary;
    static readonly Color Gold = KucaTheme.Gold;
    static readonly Color Green = KucaTheme.Primary;

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
    static readonly Color StarOff = Color.Lerp(KucaTheme.Line, KucaTheme.InkSoft, 0.35f);   // 빈 별: 흰 카드 위에서 보이는 회색

    GameObject root, partnerRow, reviewCard, rewardCard, recap, shareRow;
    RawImage recapMap;
    RectTransform recapOverlay;
    RenderTexture recapTexture;
    WalkHistory.Entry saved;
    Text title, sub, statMeters, statMinutes, statStops, reason, rewardText, levelText, reviewNote;
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
        string url = ServerUrl;
        if (service != null && url == serviceUrl)
            return;
        serviceUrl = url;
        service = CreateService();
    }

    static string ServerUrl
    {
        get
        {
            var board = FindAnyObjectByType<MemoBoard>();
            return board != null ? board.ServerUrl : "";
        }
    }

    /// <summary>산책 서버 (메모 서버 주소가 없으면 기기 계산). RoutePlanner 가 산책 시작을 알릴 때도 쓴다</summary>
    public static IWalkService CreateService()
    {
        string url = ServerUrl;
        var local = new LocalWalkService();
        return url.Length == 0 ? local : new HttpWalkService(url, local);
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
        statStops.text = mate ? "함께" : walk.stopsTotal <= 0 ? "자유" : $"{walk.stopsDone}/{walk.stopsTotal}곳";
        reason.text = "확인하는 중…";
        partnerRow.SetActive(false);   // 리뷰는 코스만 평가 (10/24 결정, 상대 별점 없음)
        rewardText.text = "";
        levelText.text = "";
        RefreshStars();
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);
        // 걸은 길 돌아보기 + 산책 기록에 남기기
        bool hasTrack = walk.track != null && walk.track.Count >= 2;
        recap.SetActive(hasTrack);
        shareRow.SetActive(hasTrack);
        if (hasTrack)
            StartCoroutine(DrawRecap(walk.track, LineColor(walk)));
        saved = walk.meters > 0 || hasTrack ? WalkHistory.Add(walk, System.DateTime.Now) : null;

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
            if (saved != null) WalkHistory.SetCompleted(saved.id, r.completed);
            title.text = r.completed ? (mate ? "함께 산책 완료!" : "산책 완료!") : "산책을 마쳤어요";
            reason.text = r.completed ? r.reason : AlmostThere(walk);
            reason.color = r.completed ? Green : Ink;
            ApplyReward(r.xp, r.tickets, r.growth);
            StartCoroutine(FillLevel(score, r));
        }));
    }

    /// <summary>카카오톡 노란 버튼 (말풍선 + 글자)</summary>
    public static Button KakaoButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.BigButton(parent, "카카오톡으로 자랑하기", onClick, KucaTheme.KakaoYellow, KucaTheme.KakaoInk);
        Image icon = UIKit.Image(b.transform, "Icon", KucaTheme.KakaoInk);
        icon.sprite = HudIcons.Bubble;
        icon.raycastTarget = false;
        icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = icon.rectTransform.pivot = new Vector2(0f, 0.5f);
        icon.rectTransform.anchoredPosition = new Vector2(44f, 0f);
        icon.rectTransform.sizeDelta = new Vector2(48f, 48f);
        return b;
    }

    bool sharing;

    IEnumerator UnlockAfter(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        sharing = false;
    }

    void ShareToKakao(RectTransform frame)
    {
        if (sharing || walk == null)
            return;
        sharing = true;
        FindAnyObjectByType<GameHUD>()?.Toast("공유 준비 중…");
        StartCoroutine(UnlockAfter(10f));   // 중간에 예외가 나도 버튼이 묶여 있지 않게
        StartCoroutine(WalkShare.Share(frame, WalkShare.KindOf(walk.kind, walk.stopsTotal, walk.plannedMeters),
            walk.meters, walk.minutes, walk.visitedBuildingIds, error =>
            {
                sharing = false;
                var hud = FindAnyObjectByType<GameHUD>();
                if (hud != null && error != null) hud.Toast(error);
            }));
    }

    /// <summary>완료를 못 했을 때: 얼마나 더 했으면 됐는지 (다음에 다시 걷게)</summary>
    static string AlmostThere(WalkCompleteRequest w)
    {
        if (w.kind == WalkSession.Mate || w.stopsTotal <= 0 && w.plannedMeters <= 0)
        {
            int left = Mathf.Max(0, LocalWalkService.MateMeters - w.meters);
            return $"{left}m만 더 걸었으면 완료였어요";
        }
        int stops = Mathf.Max(0, w.stopsTotal - w.stopsDone);
        int meters = Mathf.Max(0, Mathf.RoundToInt(w.plannedMeters * LocalWalkService.CourseRatio) - w.meters);
        return stops > 0 && meters > 0 ? $"{stops}곳 더 들르거나 {RouteMath.Distance(meters)} 더 걸었으면 완료였어요"
             : stops > 0 ? $"{stops}곳 더 들렀으면 완료였어요"
             : $"{RouteMath.Distance(meters)} 더 걸었으면 완료였어요";
    }

    /// <summary>산책 종류별 길 색 (자유 산책 · 코스 · 산책메이트)</summary>
    public static Color LineColor(WalkCompleteRequest w) => LineColor(w.kind, w.stopsTotal, w.plannedMeters);

    public static Color LineColor(string kind, int stopsTotal, int plannedMeters) =>
        kind == WalkSession.Mate ? MateColor : stopsTotal <= 0 && plannedMeters <= 0 ? FreeWalk.TrackColor : Accent;

    /// <summary>산책메이트와 걸은 길 (코스 초록 · 자유 청록과 구분되게 주황, 계절과 상관없이 고정)</summary>
    static readonly Color MateColor = new Color(0.93f, 0.50f, 0.13f);

    /// <summary>위에서 본 지도에 걸은 길을 그린다 (흰 테두리 + 색 선 + 방향 화살표, 출발 · 끝 표시)</summary>
    public static void DrawTrack(RawImage map, ref RenderTexture texture, RectTransform overlay, List<Vector3> track, Color color)
    {
        TopMap.Clear(overlay);
        Camera cam = TopMap.Render(map, ref texture, track);
        if (cam == null)
            return;
        Vector2 size = overlay.rect.size;
        TopMap.Polyline(overlay, cam, size, track, 19f, new Color(1f, 1f, 1f, 0.95f));
        TopMap.Polyline(overlay, cam, size, track, 12f, color, true);
        TopMap.Marker(overlay, cam, size, track[0], Ink, "출발", 74f);
        TopMap.Marker(overlay, cam, size, track[track.Count - 1], color, "끝", 64f);
    }

    IEnumerator DrawRecap(List<Vector3> track, Color color)
    {
        yield return null;   // 화면 배치가 끝난 뒤 크기를 알 수 있다
        Canvas.ForceUpdateCanvases();
        DrawTrack(recapMap, ref recapTexture, recapOverlay, track, color);
    }

    void OnDestroy()
    {
        if (recapTexture != null) recapTexture.Release();
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

        if (evolved && p.Partner != null)
        {
            parts.Add(KhuMenus.EvolvedMessage(p.Partner.speciesId, stage));
            FindAnyObjectByType<KhuMenus>()?.RefreshPartner();
        }
        // 파트너 진화 경험치는 내 경험치와 다른 것이라 따로 한 줄 (같은 줄이라 헷갈렸음, REVIEW_v22_day)
        if (growth > 0 && partnerName != null) parts.Add($"\n파트너 {partnerName} 성장 경험치 +{growth}");
        if (parts.Count > 0)
            rewardText.text = (rewardText.text.Length > 0 ? rewardText.text + "\n" : "") + string.Join("   ", parts).Replace("   \n", "\n");
        else if (rewardText.text.Length == 0)
        {
            // 완료 보상은 없어도 걸은 거리는 티켓 · 경험치에 이미 쌓였다는 것을 보여 준다 (실망만 주지 않게)
            int toTicket = Mathf.Max(0, Mathf.RoundToInt(GachaRules.TicketMeters - p.ticketMeters));
            string next = toTicket > 0 ? $"다음 티켓까지 {RouteMath.Distance(toTicket)}" : "오늘 받을 티켓은 다 받았어요";
            rewardText.text = $"완료 보상은 다음 기회에! 걸은 거리는 그대로 쌓였어요\n<size=28>{next}</size>";
        }
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
            partnerStarImages[i].color = i < partnerStars ? Gold : StarOff;
            courseStarImages[i].color = i < courseStars ? Gold : StarOff;
        }
        sendButton.interactable = !reviewSent;
        UIKit.SetLabel(sendButton, reviewSent ? "리뷰를 보냈어요" : $"리뷰 보내기  (+{LocalWalkService.ReviewXp} XP)");
    }

    void SendReview()
    {
        if (busy || reviewSent || result == null) return;
        if (courseStars == 0)
        {
            reviewNote.text = "코스 별점을 골라 주세요";
            return;
        }
        var req = new WalkReviewRequest { courseRating = courseStars, text = comment.text.Trim() };
        busy = true;
        StartCoroutine(service.Review(result.walkId, req, (r, error) =>
        {
            busy = false;
            if (r == null) { reviewNote.text = error ?? "리뷰를 보내지 못했어요"; return; }
            reviewSent = true;
            reviewNote.text = "고마워요! 다음 추천에 반영할게요";
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
        UIKit.Page page = UIKit.CreatePage(canvas.transform, "WalkComplete", "산책 완료!", Close, " ", bottomButtons: 1);
        root = page.root;
        title = page.title;
        sub = page.subtitle;
        RectTransform list = page.content;

        // 요약: 큰 숫자 3개 (애플 피트니스) + 완료 이유
        RectTransform summary = UIKit.CardBox(list, "Summary", KucaTheme.Space2);
        RectTransform stats = UIKit.StatRow(summary, ("", "걸은 거리"), ("", "시간"), ("", "들른 곳"));
        statMeters = stats.GetChild(0).GetComponentInChildren<Text>();
        statMinutes = stats.GetChild(1).GetComponentInChildren<Text>();
        statStops = stats.GetChild(2).GetComponentInChildren<Text>();
        reason = UIKit.Bold(UIKit.Text(summary, "", KucaTheme.Body, TextAnchor.MiddleLeft, InkQuiet));
        UIKit.Size(reason, 64f);
        UIKit.Paragraph(summary, RouteMath.HealthNote, KucaTheme.Caption, KucaTheme.InkSoft);

        // 걸은 길 돌아보기
        RectTransform recapCard = UIKit.CardBox(list, "RecapCard", KucaTheme.Space2);
        UIKit.SectionTitle(recapCard, "걸은 길");
        Image frame = TopMap.Frame(recapCard, "Recap", out recapMap, out recapOverlay);
        UIKit.Size(frame, 640f);
        Button kakao = KakaoButton(recapCard, () => ShareToKakao(frame.rectTransform));
        UIKit.Size(kakao, 104f);
        shareRow = kakao.gameObject;
        recap = recapCard.gameObject;

        // 보상 (듀오링고: 받은 것을 크게 + 레벨 막대)
        RectTransform rc = UIKit.CardBox(list, "Reward", KucaTheme.Space2);
        rewardCard = rc.gameObject;
        UIKit.SectionTitle(rc, "이번 산책 보상");
        rewardText = UIKit.Paragraph(rc, "", KucaTheme.Body, Ink);
        levelText = UIKit.Text(rc, "", KucaTheme.Label, TextAnchor.MiddleLeft, InkQuiet);
        UIKit.Size(levelText, 56f);
        Image track = UIKit.Image(rc, "Track", KucaTheme.Fill);
        track.sprite = HudIcons.Pill;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 4f;
        UIKit.Size(track, 32f);
        Image fill = UIKit.Image(track.transform, "Fill", Gold);
        fill.sprite = HudIcons.Pill;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 4f;
        levelFill = fill.rectTransform;
        levelFill.anchorMin = Vector2.zero;
        levelFill.anchorMax = new Vector2(0f, 1f);
        levelFill.offsetMin = levelFill.offsetMax = Vector2.zero;

        UIKit.SecondaryButton(list, "파트너와 사진 찍기", TakePhoto);

        // 리뷰: 별점 + 한 줄 + 보내기를 한 카드에
        RectTransform vc = UIKit.CardBox(list, "Review", KucaTheme.Space2);
        reviewCard = vc.gameObject;
        UIKit.SectionTitle(vc, "산책은 어땠어요?");
        partnerRow = UIKit.Rect(vc, "PartnerRow").gameObject;
        UIKit.Vertical(partnerRow.GetComponent<RectTransform>(), KucaTheme.Space1);
        Label(partnerRow.transform, "산책메이트  (상대에게 보이지 않아요)");
        partnerStarImages = Stars(partnerRow.transform, n => SetStars(true, n));
        Label(vc, "코스");
        courseStarImages = Stars(vc, n => SetStars(false, n));
        comment = UIKit.Input(vc, $"한 줄 남기기 (선택, {WalkReviewRequest.MaxText}자)", KucaTheme.Body, false, WalkReviewRequest.MaxText);
        comment.GetComponent<Image>().color = KucaTheme.Fill;
        UIKit.Size(comment, KucaTheme.TouchMin);
        sendButton = UIKit.SecondaryButton(vc, "리뷰 보내기", SendReview);   // 강조 버튼은 아래 "확인" 하나
        reviewNote = UIKit.Text(vc, "", KucaTheme.Label, TextAnchor.MiddleLeft, InkQuiet);
        UIKit.Size(reviewNote, 56f);

        UIKit.PrimaryButton(page.bottom, "확인", Close);
    }

    static void Label(Transform parent, string text) =>
        UIKit.Size(UIKit.Text(parent, text, KucaTheme.Label, TextAnchor.MiddleLeft, InkQuiet), 56f);

    static Image[] Stars(Transform parent, System.Action<int> onPick)
    {
        RectTransform row = UIKit.Rect(parent, "Stars");
        UIKit.Size(row, KucaTheme.TouchMin);
        var h = UIKit.Horizontal(row, KucaTheme.Space2);
        h.childForceExpandWidth = false;
        h.childAlignment = TextAnchor.MiddleLeft;
        var images = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            int n = i + 1;
            Image star = UIKit.Image(row, "Star" + n, StarOff, raycast: true);
            star.sprite = HudIcons.Star;
            UIKit.Size(star, 112f, 112f);
            var b = star.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onPick(n));
            star.gameObject.AddComponent<PressScale>();
            images[i] = star;
        }
        return images;
    }
}
