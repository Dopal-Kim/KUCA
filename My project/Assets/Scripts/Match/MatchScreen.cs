using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 실시간 산책 매칭 화면 (B-V3). 지도 오른쪽 위 산책메이트 버튼으로 연다.
/// 흐름: 조건 (15/30/60분 · 동성만/상관없음) → 대기 (남은 시간 · 취소) → 상대 제안 (닉네임 · 공통 취향, 수락/거절)
///      → 상대 수락 기다림 → 만남 스팟 → 인상착의 → 스몰토크 → 산책 시작 → 산책 중 (끝내기).
/// 어느 단계든 신고 · 차단 · 취소를 할 수 있고, 만나기로 한 뒤 10분 안에 시작하지 않으면 노쇼로 취소된다 (서버 판정).
/// 화면을 닫아도 매칭은 계속되고 (상태를 계속 물어봄), 상대가 나타나면 화면이 다시 열린다.
/// 서버 호출은 IMatchService: 서버에 API 가 없으면 기기 안 가상 상대 (HttpMatchService → MockMatchService).
/// </summary>
public class MatchScreen : MonoBehaviour
{
    static readonly Color Ink = KucaTheme.Ink;
    static readonly Color InkQuiet = KucaTheme.InkMuted;
    static readonly Color Accent = KucaTheme.Primary;
    static readonly Color Warm = new Color(0.96f, 0.52f, 0.16f);
    static readonly Color ChipOff = KucaTheme.PrimarySoft;
    static readonly Color Danger = KucaTheme.Crimson;
    static readonly Color Paper = KucaTheme.Surface;

    static readonly Dictionary<string, string> TasteNames = new Dictionary<string, string>
    {
        ["photo"] = "사진 명소", ["quiet"] = "조용한 곳", ["food"] = "맛집·카페", ["unvisited"] = "안 가 본 곳",
        ["partner"] = "파트너와 걷기", ["exercise"] = "운동",
    };

    IMatchService service;
    string serviceUrl;
    MatchState state = new MatchState();
    bool busy;
    float pollAt;
    int minutes = 30;
    bool sameGender;
    int topicIndex;
    string shownTopic;

    GameObject root, conditionPage, waitPage, proposePage, acceptWaitPage, meetPage, lookPage, talkPage, walkPage, endPage, reportPanel;
    Text waitTime, proposeName, proposeTastes, proposeTime, meetName, meetInfo, meetNoShow, lookMine, lookPartner,
        talkTopic, walkTitle, endTitle, endMessage, serviceLabel;
    RawImage waitPartner, meetCreature;
    InputField lookInput;
    Image[] minuteChips, genderChips, reasonChips;
    Image blockChip;
    Button reportButton;
    string reportReason = "rude";
    bool reportBlock = true;

    static readonly (string id, string label)[] Reasons =
        { ("rude", "불쾌한 말 · 행동"), ("noshow", "나오지 않음"), ("personal", "개인정보 요구"), ("other", "기타") };

    public static void Open()
    {
        MatchScreen s = FindAnyObjectByType<MatchScreen>();
        if (s == null)
            s = new GameObject("MatchScreen").AddComponent<MatchScreen>();
        s.Show(true);
    }

    void Awake()
    {
        EnsureService();
        Build();
    }

    void EnsureService()
    {
        var board = FindAnyObjectByType<MemoBoard>();
        string url = board != null ? board.ServerUrl : "";
        if (service != null && url == serviceUrl)
            return;
        serviceUrl = url;
        var mock = new MockMatchService(NearestMeetSpot);
        service = url.Length == 0 ? mock : new HttpMatchService(url, mock);
    }

    void Show(bool open)
    {
        root.SetActive(open);
        UIInputBlocker.SetModal(this, open);
        if (!open)
            return;
        EnsureService();
        root.transform.SetAsLastSibling();
        if (!state.Active)
            ResetCondition();
        Render();
        pollAt = 0f; // 바로 한 번 물어본다
    }

    void Hide()
    {
        Show(false);
        if (state.Active)
            Toast("매칭은 계속 진행돼요. 산책메이트 버튼으로 다시 열 수 있어요");
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    // ---------- 상태 ----------

    void Update()
    {
        if (!state.Active || busy || Time.unscaledTime < pollAt)
            return;
        bool fast = state.status == MatchState.Waiting || state.status == MatchState.Accepted || state.status == MatchState.Matched;
        pollAt = Time.unscaledTime + (fast ? 2f : 5f);
        Call(service.Status(Apply));
    }

    void Call(System.Collections.IEnumerator work)
    {
        busy = true;
        StartCoroutine(Run(work));
    }

    System.Collections.IEnumerator Run(System.Collections.IEnumerator work)
    {
        yield return work;
        busy = false;
    }

    void Apply(MatchState s, string error)
    {
        if (error != null)
        {
            Toast(error);
            return;
        }
        if (s == null)
            return;
        string before = state.status;
        state = s;
        if (before != s.status)
        {
            // 산책 기록 (B-V4): 시작하면 걸은 거리를 재기 시작하고, 끝나면 완료 화면으로
            if (s.status == MatchState.Walking)
                WalkSession.BeginMate(s.matchId, s.partner?.nickname, s.walkId);
            else if (s.status == MatchState.Ended && WalkSession.Active && WalkSession.Kind == WalkSession.Mate)
            {
                WalkCompleteScreen.Show(WalkSession.Finish(null));
                Again();
                Show(false);
                return;
            }
            else if (!s.Active && WalkSession.Active && WalkSession.Kind == WalkSession.Mate)
                WalkSession.Abandon();
            // 화면을 닫아 둔 사이 상대가 나타나거나 매칭이 끝나면 알려 주고 다시 연다
            if (!root.activeSelf && (s.status == MatchState.Proposed || s.status == MatchState.Matched
                || s.status == MatchState.Cancelled || s.status == MatchState.Expired))
            {
                Toast(s.status == MatchState.Proposed ? "산책메이트를 찾았어요!" : s.status == MatchState.Matched ? "상대도 수락했어요!" : s.message);
                Show(true);
            }
            if (s.status == MatchState.Matched)
                ShowPage(meetPage);
        }
        Render();
    }

    /// <summary>지금 상태에 맞는 쪽을 보여 준다. matched 는 만남 → 인상착의 → 스몰토크를 사용자가 넘긴다.</summary>
    void Render()
    {
        if (serviceLabel != null)
            serviceLabel.text = service.Description == "가상 상대" ? "시연용 가상 상대와 연결돼요" : "";
        switch (state.status)
        {
            case MatchState.Waiting:
                ShowPage(waitPage);
                if (waitPartner.texture == null)
                {
                    // 기다리는 동안 내 파트너 경희몬이 통통 뛴다
                    var collect = FindAnyObjectByType<CollectController>();
                    GameProgress.Caught partner = collect != null && collect.Progress != null ? collect.Progress.Partner : null;
                    try { waitPartner.texture = partner != null ? MonsterThumbnails.Get(partner.speciesId) : null; }
                    catch (System.Exception e) { Debug.LogWarning("[Match] 파트너 그림 실패: " + e.Message); }
                    waitPartner.enabled = waitPartner.texture != null;
                }
                waitTime.text = $"남은 시간 {state.waitSecondsLeft / 60}:{state.waitSecondsLeft % 60:00}";
                break;
            case MatchState.Proposed:
                ShowPage(proposePage);
                proposeName.text = state.partner?.nickname ?? "산책메이트";
                proposeTastes.text = CommonTastes();
                proposeTime.text = $"{state.minutes}분 산책  ·  {state.decideSecondsLeft}초 안에 골라 주세요";
                break;
            case MatchState.Accepted:
                ShowPage(acceptWaitPage);
                break;
            case MatchState.Matched:
                if (!meetPage.activeSelf && !lookPage.activeSelf && !talkPage.activeSelf)
                    ShowPage(meetPage);
                FillMeet();
                FillLook();
                if (talkPage.activeSelf && shownTopic == null)
                    ShowTopic();
                break;
            case MatchState.Walking:
                ShowPage(walkPage);
                walkTitle.text = $"{state.partner?.nickname ?? "산책메이트"}님과 걷는 중";
                break;
            case MatchState.Ended:
                ShowPage(endPage);
                endTitle.text = "산책을 마쳤어요";
                endMessage.text = $"{state.partner?.nickname ?? "산책메이트"}님과 함께 걸었어요";
                break;
            case MatchState.Cancelled:
            case MatchState.Expired:
                ShowPage(endPage);
                endTitle.text = state.status == MatchState.Expired ? "매칭되지 않았어요" : "매칭이 끝났어요";
                endMessage.text = string.IsNullOrEmpty(state.message) ? "다시 찾아볼까요?" : state.message;
                break;
            default:
                ShowPage(conditionPage);
                RefreshChips();
                break;
        }
        reportButton.gameObject.SetActive(state.partner != null && state.Active && state.status != MatchState.Waiting);
    }

    void ShowPage(GameObject page)
    {
        foreach (GameObject p in new[] { conditionPage, waitPage, proposePage, acceptWaitPage, meetPage, lookPage, talkPage, walkPage, endPage })
            p.SetActive(p == page);
    }

    string CommonTastes()
    {
        var names = new List<string>();
        if (state.partner?.commonTastes != null)
            foreach (string t in state.partner.commonTastes)
                names.Add(TasteNames.TryGetValue(t, out string n) ? n : t);
        string talk = state.partner?.talkStyle switch { "chatty" => "수다 떨며", "quiet" => "조용히 각자", "some" => "가끔 대화", _ => null };
        string tastes = names.Count > 0 ? "같이 좋아하는 것  " + string.Join(" · ", names) : "";
        return talk != null ? $"{tastes}\n걸을 때 분위기  {talk}" : tastes;
    }

    // ---------- 조건 ----------

    void ResetCondition()
    {
        WalkPreferences p = WalkPreferences.Load();
        minutes = p.walkMinutes >= 60 ? 60 : 30;
        sameGender = p.matchGender == "same";
    }

    void RefreshChips()
    {
        SetChip(minuteChips[0], minutes == 15);
        SetChip(minuteChips[1], minutes == 30);
        SetChip(minuteChips[2], minutes == 60);
        SetChip(genderChips[0], sameGender);
        SetChip(genderChips[1], !sameGender);
    }

    static void SetChip(Image chip, bool on)
    {
        chip.color = on ? Accent : ChipOff;
        chip.GetComponentInChildren<Text>().color = on ? Color.white : Ink;
    }

    void Join()
    {
        if (busy) return;
        if (AuthService.HasServer && !AuthService.HasToken)
        {
            Toast("로그인하거나 체험 모드로 시작하면 쓸 수 있어요");
            return;
        }
        var req = new MatchQueueRequest
        {
            minutes = minutes,
            sameGender = sameGender,
            buildingId = NearestSpot()?.buildingId,
            tastes = new List<string>(WalkPreferences.Load().tastes),
        };
        shownTopic = null;
        Call(service.Join(req, Apply));
    }

    // ---------- 제안 · 만남 ----------

    void Respond(bool accept) { if (!busy) Call(service.Respond(state.matchId, accept, Apply)); }

    void FillMeet()
    {
        MeetSpot spot = state.spot;
        meetName.text = spot?.name ?? "만남 스팟";
        Texture tex = null;
        if (spot != null && !string.IsNullOrEmpty(spot.species))
        {
            try { tex = MonsterThumbnails.Get(spot.species); }
            catch (System.Exception e) { Debug.LogWarning("[Match] 경희몬 그림 실패: " + e.Message); }
        }
        meetCreature.texture = tex;
        meetCreature.enabled = tex != null;
        float d = DistanceTo(spot);
        string where = d >= 0f ? $"지금 위치에서 {RouteMath.Distance(d)}  ·  " : "";
        string creature = spot != null && !string.IsNullOrEmpty(spot.species) ? $"{CreatureLibrary.NameOf(spot.species)} 앞에서 만나요" : "스팟 앞에서 만나요";
        meetInfo.text = $"{where}{creature}";
        meetNoShow.text = $"{state.noShowSecondsLeft / 60}분 {state.noShowSecondsLeft % 60:00}초 안에 만나지 않으면 자동으로 취소돼요";
    }

    void FillLook()
    {
        lookMine.text = string.IsNullOrEmpty(state.myLook) ? "아직 쓰지 않았어요" : $"나: {state.myLook}";
        lookPartner.text = string.IsNullOrEmpty(state.partnerLook) ? "상대가 아직 쓰지 않았어요" : state.partnerLook;
    }

    void SendLook()
    {
        string look = lookInput.text.Trim();
        if (look.Length is < 2 or > 40) { Toast("2~40자로 써 주세요 (예: 검은 후드, 흰 운동화)"); return; }
        if (!busy) Call(service.SetLook(state.matchId, look, Apply));
    }

    void ShowTopic()
    {
        if (state.smalltalk != null && topicIndex < state.smalltalk.Count)
            shownTopic = state.smalltalk[topicIndex];
        talkTopic.text = shownTopic ?? "무엇이든 편하게 이야기해요";
    }

    void NextTopic()
    {
        topicIndex++;
        if (state.smalltalk != null && topicIndex < state.smalltalk.Count)
        {
            ShowTopic();
            return;
        }
        if (busy) return;
        busy = true;
        StartCoroutine(Run(service.NextTopic(state.matchId, (topic, error) =>
        {
            if (error != null) { Toast(error); return; }
            shownTopic = topic;
            talkTopic.text = topic;
        })));
    }

    void StartWalk()
    {
        if (busy) return;
        Call(service.StartWalk(state.matchId, (s, e) =>
        {
            Apply(s, e);
            // 서버 매칭: 나만 도착했으면 상대를 기다린다고 알려 준다
            if (s != null && s.status == MatchState.Matched && !string.IsNullOrEmpty(s.message))
                Toast(s.message);
        }));
    }
    void EndWalk() { if (!busy) Call(service.End(state.matchId, Apply)); }

    void CancelMatch()
    {
        if (busy) return;
        if (state.status == MatchState.Waiting)
            Call(service.Leave((s, e) => { Apply(s, e); ResetCondition(); Render(); }));
        else
            Call(service.Cancel(state.matchId, "user", Apply));
    }

    void Again()
    {
        state = new MatchState();
        topicIndex = 0;
        shownTopic = null;
        ResetCondition();
        Render();
    }

    // ---------- 신고 · 차단 ----------

    void OpenReport()
    {
        reportReason = "rude";
        reportBlock = true;
        RefreshReport();
        reportPanel.SetActive(true);
        reportPanel.transform.SetAsLastSibling();
    }

    void RefreshReport()
    {
        for (int i = 0; i < Reasons.Length; i++)
            SetChip(reasonChips[i], Reasons[i].id == reportReason);
        SetChip(blockChip, reportBlock);
    }

    void SendReport()
    {
        if (busy) return;
        reportPanel.SetActive(false);
        Call(service.Report(state.matchId, reportReason, "", reportBlock, Apply));
    }

    // ---------- 위치 ----------

    /// <summary>지금 위치에서 가장 가까운 경희스팟 (대기열에 보내는 건물 ID)</summary>
    MeetSpot NearestSpot() => Nearest(false);

    /// <summary>가장 가까운 만남 스팟과 그 앞의 경희몬 (A-V3 MeetSpotCatalog, 가상 상대가 쓴다)</summary>
    MeetSpot NearestMeetSpot() => Nearest(true);

    MeetSpot Nearest(bool meetSpotsOnly)
    {
        var spots = FindAnyObjectByType<KyungHeeSpots>();
        Vector3 me = PlayerPosition;
        KyungHeeSpot best = null;
        float bestD = float.MaxValue;
        if (spots != null)
            foreach (KyungHeeSpot s in spots.Spots)
            {
                if (s == null || s.Building == null) continue;
                if (meetSpotsOnly && !MeetSpotCatalog.IsMeetSpot(s.Building.buildingId)) continue;
                float d = (s.transform.position - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
        if (best == null)
            return meetSpotsOnly ? Nearest(false) : null;
        return new MeetSpot
        {
            buildingId = best.Building.buildingId,
            name = best.Building.DisplayName,
            species = MeetSpotCatalog.SpeciesAt(best.Building.buildingId),
        };
    }

    float DistanceTo(MeetSpot spot)
    {
        if (spot == null || string.IsNullOrEmpty(spot.buildingId))
            return -1f;
        var spots = FindAnyObjectByType<KyungHeeSpots>();
        if (spots == null)
            return -1f;
        foreach (KyungHeeSpot s in spots.Spots)
            if (s != null && s.Building != null && s.Building.buildingId == spot.buildingId)
            {
                Vector3 a = s.transform.position, b = PlayerPosition;
                return new Vector2(a.x - b.x, a.z - b.z).magnitude;
            }
        return -1f;
    }

    static Vector3 PlayerPosition
    {
        get
        {
            var collect = FindAnyObjectByType<CollectController>();
            return collect != null && collect.campusMap != null && collect.campusMap.player != null
                ? collect.campusMap.player.position : Vector3.zero;
        }
    }

    static void Toast(string message)
    {
        var hud = FindAnyObjectByType<GameHUD>();
        if (hud != null && !string.IsNullOrEmpty(message)) hud.Toast(message);
    }

    // ---------- 화면 만들기 ----------

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "MatchCanvas", 50);
        root = UIKit.Image(canvas.transform, "Match", Paper, raycast: true).gameObject;
        BackButton.Attach(root, Hide);
        UIKit.Stretch((RectTransform)root.transform);
        Rect sa = Screen.safeArea;
        RectTransform safe = UIKit.Rect(root.transform, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        // 위: 닫기 · 제목 · 신고
        Button close = IconButton(safe, HudIcons.Close, Hide);
        Place((RectTransform)close.transform, new Vector2(0f, 1f), new Vector2(30f, -30f));
        Text title = UIKit.Text(safe, "산책메이트", 44, TextAnchor.MiddleCenter, Ink);
        title.fontStyle = FontStyle.Bold;
        Box(title.rectTransform, 150f, 40f, 150f, 70f);
        reportButton = UIKit.Button(safe, "신고 · 차단", OpenReport, new Color(0f, 0f, 0f, 0f), 30);
        reportButton.GetComponentInChildren<Text>().color = Danger;
        Place((RectTransform)reportButton.transform, new Vector2(1f, 1f), new Vector2(-30f, -40f));
        ((RectTransform)reportButton.transform).sizeDelta = new Vector2(200f, 70f);

        RectTransform body = UIKit.Rect(safe, "Body");
        UIKit.StretchBetween(body, 0f, 140f);

        BuildCondition(body);
        BuildWait(body);
        BuildPropose(body);
        BuildAcceptWait(body);
        BuildMeet(body);
        BuildLook(body);
        BuildTalk(body);
        BuildWalk(body);
        BuildEnd(body);
        BuildReport(root.transform);
        root.SetActive(false);
    }

    GameObject Page(RectTransform body, string name)
    {
        RectTransform p = UIKit.Rect(body, name);
        UIKit.Stretch(p);
        p.gameObject.SetActive(false);
        return p.gameObject;
    }

    void BuildCondition(RectTransform body)
    {
        conditionPage = Page(body, "Condition");
        Transform p = conditionPage.transform;
        Heading(p, "같이 걸을 사람을 찾아요", 20f);
        Note(p, "공강 시간에 근처에서 함께 걸을 경희대 학생을 이어 드려요.", 110f);
        Label(p, "얼마나 걸을까요?", 220f);
        minuteChips = new[]
        {
            ChipAt(p, "15분", () => { minutes = 15; RefreshChips(); }, 60f, 300f, 196f),
            ChipAt(p, "30분", () => { minutes = 30; RefreshChips(); }, 272f, 300f, 196f),
            ChipAt(p, "1시간", () => { minutes = 60; RefreshChips(); }, 484f, 300f, 196f),
        };
        Label(p, "같이 걸을 상대는요?", 440f);
        genderChips = new[]
        {
            ChipAt(p, "동성만", () => { sameGender = true; RefreshChips(); }, 60f, 520f, 300f),
            ChipAt(p, "상관없어요", () => { sameGender = false; RefreshChips(); }, 380f, 520f, 300f),
        };
        Note(p, "위치는 건물 단위로만 쓰고, 매칭이 끝나면 지워요.\n성별은 상대에게 보이지 않아요.", 660f);
        serviceLabel = Note(p, "", 740f);
        PrimaryAtBottom(p, "산책메이트 찾기", Accent, Join);
    }

    void BuildWait(RectTransform body)
    {
        waitPage = Page(body, "Wait");
        Transform p = waitPage.transform;
        waitPartner = Raw(p, 360f, 320f);
        waitPartner.gameObject.AddComponent<Bob>();
        Heading(p, "산책메이트를 찾는 중…", 560f, TextAnchor.MiddleCenter);
        waitTime = Note(p, "", 650f, TextAnchor.MiddleCenter);
        Note(p, "화면을 닫아도 계속 찾아요. 찾으면 알려 드릴게요.", 710f, TextAnchor.MiddleCenter);
        SecondaryAtBottom(p, "그만 찾기", CancelMatch);
    }

    void BuildPropose(RectTransform body)
    {
        proposePage = Page(body, "Propose");
        Transform p = proposePage.transform;
        Note(p, "이 사람과 같이 걸을까요?", 40f, TextAnchor.MiddleCenter);
        Image avatar = UIKit.Image(p, "Avatar", new Color(0.85f, 0.90f, 1f));
        avatar.sprite = HudIcons.Circle;
        Place(avatar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f));
        avatar.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        avatar.rectTransform.sizeDelta = new Vector2(220f, 220f);
        Image person = UIKit.Image(avatar.transform, "Icon", Accent);
        person.sprite = HudIcons.Person;
        person.rectTransform.sizeDelta = new Vector2(120f, 120f);
        proposeName = Heading(p, "", 400f, TextAnchor.MiddleCenter);
        proposeTastes = Note(p, "", 490f, TextAnchor.MiddleCenter);
        proposeTastes.rectTransform.sizeDelta = new Vector2(proposeTastes.rectTransform.sizeDelta.x, 100f);
        proposeTime = Note(p, "", 610f, TextAnchor.MiddleCenter);
        Button yes = Pill(p, "같이 걸을래요", Accent, Color.white, () => Respond(true));
        BottomBox((RectTransform)yes.transform, 200f);
        SecondaryAtBottom(p, "다음에 할게요", () => Respond(false));
    }

    void BuildAcceptWait(RectTransform body)
    {
        acceptWaitPage = Page(body, "AcceptWait");
        Transform p = acceptWaitPage.transform;
        Heading(p, "상대의 수락을 기다리는 중…", 400f, TextAnchor.MiddleCenter);
        Note(p, "상대도 수락하면 만날 곳을 알려 드려요.", 490f, TextAnchor.MiddleCenter);
        SecondaryAtBottom(p, "매칭 취소", CancelMatch);
    }

    void BuildMeet(RectTransform body)
    {
        meetPage = Page(body, "Meet");
        Transform p = meetPage.transform;
        Note(p, "여기서 만나요", 30f, TextAnchor.MiddleCenter);
        meetCreature = Raw(p, 300f, 340f);
        meetCreature.gameObject.AddComponent<Bob>();
        meetName = Heading(p, "", 490f, TextAnchor.MiddleCenter);
        meetInfo = Note(p, "", 580f, TextAnchor.MiddleCenter);
        meetNoShow = Note(p, "", 640f, TextAnchor.MiddleCenter);
        meetNoShow.color = Warm;
        Button next = Pill(p, "다음: 인상착의 알려 주기", Accent, Color.white, () => { ShowPage(lookPage); Render(); });
        BottomBox((RectTransform)next.transform, 200f);
        SecondaryAtBottom(p, "매칭 취소", CancelMatch);
    }

    void BuildLook(RectTransform body)
    {
        lookPage = Page(body, "Look");
        Transform p = lookPage.transform;
        Heading(p, "서로 알아볼 수 있게", 20f);
        Note(p, "오늘 옷차림을 짧게 알려 주세요. 매칭 상대에게만 보이고, 산책이 끝나면 지워요.", 110f);
        lookInput = UIKit.Input(p, "예: 검은 후드, 흰 운동화", 36, false, 40);
        Image ib = lookInput.GetComponent<Image>();
        ib.color = ChipOff;
        ib.sprite = HudIcons.Rounded;
        ib.type = Image.Type.Sliced;
        ib.pixelsPerUnitMultiplier = 0.8f;
        lookInput.textComponent.color = Ink;
        ((Text)lookInput.placeholder).color = InkQuiet;
        Box((RectTransform)lookInput.transform, 60f, 220f, 280f, 110f);
        Button send = Pill(p, "보내기", Ink, Color.white, SendLook);
        RectTransform sr = (RectTransform)send.transform;
        sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(1f, 1f);
        sr.anchoredPosition = new Vector2(-60f, -220f);
        sr.sizeDelta = new Vector2(200f, 110f);
        lookMine = Note(p, "", 360f);
        Label(p, "상대의 인상착의", 450f);
        lookPartner = Heading(p, "", 520f);
        lookPartner.fontSize = 40;
        Button next = Pill(p, "다음: 대화 주제 보기", Accent, Color.white, () => { ShowPage(talkPage); ShowTopic(); });
        BottomBox((RectTransform)next.transform, 200f);
        SecondaryAtBottom(p, "매칭 취소", CancelMatch);
    }

    void BuildTalk(RectTransform body)
    {
        talkPage = Page(body, "Talk");
        Transform p = talkPage.transform;
        Note(p, "어색하면 이 이야기로 시작해 보세요", 30f, TextAnchor.MiddleCenter);
        Image card = UIKit.Image(p, "Card", new Color(0.93f, 0.95f, 1f));
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        Box(card.rectTransform, 60f, 120f, 60f, 420f);
        card.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.08f);
        talkTopic = UIKit.Text(card.transform, "", 50, TextAnchor.MiddleCenter, Ink);
        talkTopic.fontStyle = FontStyle.Bold;
        UIKit.Stretch(talkTopic.rectTransform, 60f, 40f);
        Button more = Pill(p, "다음 주제", ChipOff, Ink, NextTopic);
        Box((RectTransform)more.transform, 220f, 580f, 220f, 100f);
        Button start = Pill(p, "만났어요! 산책 시작", Warm, Color.white, StartWalk);
        BottomBox((RectTransform)start.transform, 200f);
        SecondaryAtBottom(p, "매칭 취소", CancelMatch);
    }

    void BuildWalk(RectTransform body)
    {
        walkPage = Page(body, "Walk");
        Transform p = walkPage.transform;
        Image ic = UIKit.Image(p, "Icon", Accent);
        ic.sprite = HudIcons.Footprints;
        Place(ic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f));
        ic.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ic.rectTransform.sizeDelta = new Vector2(200f, 200f);
        walkTitle = Heading(p, "", 460f, TextAnchor.MiddleCenter);
        Note(p, "즐겁게 걸어요! 불편한 일이 생기면 오른쪽 위 신고 · 차단을 눌러 주세요.", 550f, TextAnchor.MiddleCenter);
        Button end = Pill(p, "산책 끝내기", Accent, Color.white, EndWalk);
        BottomBox((RectTransform)end.transform, 200f);
        SecondaryAtBottom(p, "지도로 돌아가기", Hide);
    }

    void BuildEnd(RectTransform body)
    {
        endPage = Page(body, "End");
        Transform p = endPage.transform;
        endTitle = Heading(p, "", 400f, TextAnchor.MiddleCenter);
        endMessage = Note(p, "", 490f, TextAnchor.MiddleCenter);
        Button again = Pill(p, "다시 찾기", Accent, Color.white, Again);
        BottomBox((RectTransform)again.transform, 200f);
        SecondaryAtBottom(p, "닫기", () => { Again(); Show(false); });
    }

    void BuildReport(Transform parent)
    {
        Image dim = UIKit.Image(parent, "Report", new Color(0f, 0f, 0f, 0.45f), raycast: true);
        UIKit.Stretch(dim.rectTransform);
        reportPanel = dim.gameObject;
        Image card = UIKit.Image(dim.transform, "Card", Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        card.rectTransform.sizeDelta = new Vector2(900f, 1000f);
        Transform c = card.transform;
        Heading(c, "신고하기", 50f);
        Note(c, "무슨 일이 있었나요? 신고하면 이번 매칭은 끝나요.", 140f);
        reasonChips = new Image[Reasons.Length];
        for (int i = 0; i < Reasons.Length; i++)
        {
            string id = Reasons[i].id;
            reasonChips[i] = ChipAt(c, Reasons[i].label, () => { reportReason = id; RefreshReport(); }, 60f + (i % 2) * 400f, 230f + (i / 2) * 120f, 380f);
        }
        blockChip = ChipAt(c, "이 사람과 다시 매칭하지 않기 (차단)", () => { reportBlock = !reportBlock; RefreshReport(); }, 60f, 500f, 780f);
        Button send = Pill(c, "신고하기", Danger, Color.white, SendReport);
        BottomBox((RectTransform)send.transform, 170f);
        Button cancel = Pill(c, "닫기", ChipOff, Ink, () => reportPanel.SetActive(false));
        BottomBox((RectTransform)cancel.transform, 40f);
        reportPanel.SetActive(false);
    }

    // ---------- 작은 도우미 ----------

    static Text Heading(Transform parent, string text, float top, TextAnchor anchor = TextAnchor.MiddleLeft)
    {
        Text t = UIKit.Text(parent, text, 50, anchor, Ink);
        t.fontStyle = FontStyle.Bold;
        Box(t.rectTransform, 60f, top, 60f, 80f);
        return t;
    }

    static Text Label(Transform parent, string text, float top)
    {
        Text t = UIKit.Text(parent, text, 36, TextAnchor.MiddleLeft, Ink);
        t.fontStyle = FontStyle.Bold;
        Box(t.rectTransform, 60f, top, 60f, 60f);
        return t;
    }

    static Text Note(Transform parent, string text, float top, TextAnchor anchor = TextAnchor.UpperLeft)
    {
        Text t = UIKit.Text(parent, text, 30, anchor, InkQuiet);
        Box(t.rectTransform, 60f, top, 60f, 70f);
        return t;
    }

    static RawImage Raw(Transform parent, float centerY, float size)
    {
        var raw = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        raw.transform.SetParent(parent, false);
        raw.raycastTarget = false;
        Place(raw.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -centerY));
        raw.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        raw.rectTransform.sizeDelta = new Vector2(size, size);
        return raw;
    }

    static Image ChipAt(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float left, float top, float width)
    {
        Button b = UIKit.Button(parent, label, onClick, ChipOff, 32);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 120f / 96f;
        b.GetComponentInChildren<Text>().color = Ink;
        b.gameObject.AddComponent<PressScale>();
        RectTransform rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(left, -top);
        rt.sizeDelta = new Vector2(width, 96f);
        return img;
    }

    static Button Pill(Transform parent, string label, Color face, Color ink, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, face, 40);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 120f / 120f;
        Text t = b.GetComponentInChildren<Text>();
        t.color = ink;
        t.fontStyle = FontStyle.Bold;
        b.gameObject.AddComponent<PressScale>();
        return b;
    }

    static void PrimaryAtBottom(Transform parent, string label, Color face, UnityEngine.Events.UnityAction onClick)
    {
        Button b = Pill(parent, label, face, Color.white, onClick);
        BottomBox((RectTransform)b.transform, 60f);
    }

    static void SecondaryAtBottom(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, new Color(0f, 0f, 0f, 0f), 34);
        b.GetComponentInChildren<Text>().color = InkQuiet;
        BottomBox((RectTransform)b.transform, 60f);
    }

    static Button IconButton(Transform parent, Sprite icon, UnityEngine.Events.UnityAction onClick)
    {
        Image hit = UIKit.Image(parent, "Close", new Color(0f, 0f, 0f, 0f), raycast: true);
        hit.rectTransform.sizeDelta = new Vector2(90f, 90f);
        Button b = hit.gameObject.AddComponent<Button>();
        b.onClick.AddListener(onClick);
        Image ic = UIKit.Image(hit.transform, "Icon", Ink);
        ic.sprite = icon;
        ic.rectTransform.sizeDelta = new Vector2(42f, 42f);
        return b;
    }

    /// <summary>아래에서 bottom 만큼 떨어진 높이 120 의 버튼 줄 (좌우 60)</summary>
    static void BottomBox(RectTransform rt, float bottom)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(60f, bottom);
        rt.offsetMax = new Vector2(-60f, bottom + 120f);
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

    /// <summary>그림이 위아래로 살짝 통통 뛴다 (기다리는 중)</summary>
    class Bob : MonoBehaviour
    {
        Vector2 start;
        RectTransform rt;
        void Awake() { rt = (RectTransform)transform; start = rt.anchoredPosition; }
        void Update() => rt.anchoredPosition = start + new Vector2(0f, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) * 18f);
    }
}
