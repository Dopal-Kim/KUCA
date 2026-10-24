using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지도 홈 HUD (디자인 v4 · docs/디자인_v4.md, 참조: 피크민 블룸 · 포켓몬 GO 지도 홈 + 네이버지도 아래 탭).
/// - 지도는 화면 끝까지. 위 왼쪽: 흰 상태 카드 (얼굴 · 닉네임 · 레벨 · 경험치 막대, 누르면 프로필) + 티켓 칩.
/// - 위 오른쪽: 흰 원형 버튼 세로 줄 (공지 · 사진첩 · 설정).
/// - 아래: 흰 탭 막대 (지도 · 기록 · 경희몬 · 함께 · 나) 와 그 위 가운데 큰 초록 "산책 시작" (산책 중이면 숨기고 산책 카드가 그 자리).
/// 캔버스와 화면은 실행 시 코드로 만든다. 경험치는 GameProgress.score 를 그대로 쓴다.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public float toastDuration = 2.5f;

    [Header("Content")]
    [Tooltip("(v4 에서 안 씀: 가운데 로고 버튼 대신 아래 탭 막대) 씬 직렬화 호환용")]
    public Texture2D logo;
    [Tooltip("공지사항 목록 JSON ({\"notices\":[{id,date,title,body}]})")]
    public TextAsset noticesJson;
    [Tooltip("프로필 얼굴을 찍을 캐릭터. 비우면 씬에서 찾는다.")]
    public ModularCharacter character;
    [Tooltip("프로필을 눌렀을 때 열 캐릭터 꾸미기. 비우면 씬에서 찾는다.")]
    public CharacterCustomizer customizer;
    [Tooltip("닉네임을 정하지 않았을 때 보여 줄 이름")]
    public string defaultNickname = "경희 탐험가";
    [Tooltip("주변 칸에 보여 줄 거리 (m)")]
    public float nearbyRadius = 250f;

    [Header("Portrait")]
    [Tooltip("얼굴을 밝히는 보조광 세기 (밤 장면에서도 얼굴이 보이도록)")]
    public float portraitLightIntensity = 1.5f;
    [Tooltip("얼굴 카메라가 바라보는 높이 (캐릭터 로컬 단위, 키 약 1.7)")]
    public float portraitLookHeight = 1.45f;
    public float portraitDistance = 2.7f;
    public float portraitFov = 22f;

    static readonly Color Shade = new Color(0f, 0f, 0f, 0.14f);   // 지도 위 흰 카드 아래 그림자

    const float Margin = 32f;
    /// <summary>아래 탭 막대 높이 (안전 영역 안)</summary>
    const float TabBarHeight = 196f;
    /// <summary>탭 막대 위로 산책 카드 · 시작 버튼이 놓이는 높이 (VirtualWalk.PlacePanel 이 쓴다)</summary>
    public const float BottomReserve = TabBarHeight + 32f;
    // 아래쪽 줄은 가로가 좁은 폰(iPhone 등, 약 980 단위)에서도 겹치지 않게 작게 잡는다.
    const string NicknameKey = "memo_author"; // MemoBoard 와 같은 키
    const string ReadNoticesKey = "notice_read_ids";

    Canvas canvas;
    RectTransform safe;
    Rect appliedSafeArea;
    RectTransform hud;
    Text toastText;
    Image toastBg;
    float toastTimer;

    // 프로필
    Text levelText, nextText, nicknameText;
    RectTransform xpFill;
    RawImage portraitImage;
    CanvasGroup xpPopup;
    Text xpPopupText;
    float xpPopupTimer;

    // 버튼
    GameObject startButton;
    GameObject noticeBadge;
    Text noticeBadgeText;
    readonly List<RawImage> nearbyIcons = new List<RawImage>();
    float nearbyTimer;

    // 전체 화면
    GameObject noticeScreen, settingsScreen;
    RectTransform noticeList, settingsList;
    InputField nicknameInput;
    Button logoutButton, deleteAccountButton, virtualWalkButton;
    float deleteConfirmUntil; // 회원 탈퇴는 4초 안에 한 번 더 눌러야 한다
    InputField serverInput;
    Text serverStatus;

    Camera portraitCam;
    Light portraitLight;
    RenderTexture portraitTexture;
    ModularCharacter portraitModel;          // 초상화 전용으로 멈춰 선 같은 모습의 캐릭터 (걸어도 얼굴이 흔들리지 않게)
    CharacterAppearance portraitAppearance;

    KhuMenus menus;
    CameraFollow cameraFollow;
    CollectController collect;

    GameProgress progress;
    IList<CollectibleType> types;
    int lastScore = -1;
    NoticeData notices;
    HashSet<string> readNotices;

    [Serializable]
    public class Notice
    {
        public string id;
        public string date;
        public string title;
        public string body;
    }

    [Serializable]
    class NoticeData { public List<Notice> notices = new List<Notice>(); }

    [Serializable]
    class IdList { public List<string> ids = new List<string>(); }

    void Awake()
    {
        canvas = UIKit.CreateCanvas(transform, "HUDCanvas", 10);
        safe = UIKit.Rect(canvas.transform, "SafeArea");
        UIKit.Stretch(safe);
        ApplySafeArea();

        hud = UIKit.Rect(safe, "HUD");
        UIKit.Stretch(hud);

        LoadNotices();
        BuildStatus();
        BuildTopButtons();
        BuildNearby();
        BuildTabBar();
        BuildStartButton();

        // 토스트: 진한 알약 (흰 카드 · 지도 어디 위에서도 읽히게), 시작 버튼 위
        toastBg = UIKit.Image(safe, "Toast", new Color(KucaTheme.Ink.r, KucaTheme.Ink.g, KucaTheme.Ink.b, 0.92f));
        toastBg.sprite = HudIcons.Pill;
        toastBg.type = Image.Type.Sliced;
        toastBg.pixelsPerUnitMultiplier = 1.2f;
        RectTransform tr = toastBg.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0f);
        tr.anchoredPosition = new Vector2(0f, BottomReserve + KucaTheme.ButtonHeight + KucaTheme.Space4);
        tr.sizeDelta = new Vector2(980f, 120f);
        toastText = UIKit.Text(toastBg.transform, "", KucaTheme.Label, TextAnchor.MiddleCenter);
        UIKit.Stretch(toastText.rectTransform, 36f, 10f);
        toastBg.gameObject.SetActive(false);

        UIKit.Page noticePage = UIKit.CreatePage(canvas.transform, "NoticeScreen", "공지사항", CloseNotices);
        noticeScreen = noticePage.root;
        noticeList = noticePage.content;
        UIKit.Page settingsPage = UIKit.CreatePage(canvas.transform, "SettingsScreen", "설정", CloseSettings);
        settingsScreen = settingsPage.root;
        settingsList = settingsPage.content;
        BuildSettings();
        RefreshNotices();

        menus = gameObject.AddComponent<KhuMenus>();
        menus.Build(this, canvas.transform, safe);
    }

    void Start()
    {
        // 로그인 (필수): 카카오/Apple → 경희대 메일 인증 → 닉네임 → 산책 취향 체크리스트
        LoginScreen.Begin();
        if (FindAnyObjectByType<VirtualWalk>() == null)
            new GameObject("VirtualWalk").AddComponent<VirtualWalk>(); // 체험 계정 가상 산책 (심사 설명용)
        if (character == null)
            character = FindAnyObjectByType<ModularCharacter>();
        if (customizer == null)
            customizer = FindAnyObjectByType<CharacterCustomizer>();
        cameraFollow = FindAnyObjectByType<CameraFollow>();
        collect = FindAnyObjectByType<CollectController>();
        BuildPortraitCamera();
    }

    void OnDestroy()
    {
        UIInputBlocker.SetModal(noticeScreen, false);
        UIInputBlocker.SetModal(settingsScreen, false);
        if (portraitTexture != null)
            portraitTexture.Release();
    }

    void Update()
    {
        ApplySafeArea();

        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f)
                toastBg.gameObject.SetActive(false);
        }

        if (xpPopupTimer > 0f)
        {
            xpPopupTimer -= Time.unscaledDeltaTime;
            xpPopup.alpha = Mathf.Clamp01(xpPopupTimer / 0.4f);
            if (xpPopupTimer <= 0f)
                xpPopup.gameObject.SetActive(false);
        }

        // 꾸미기·메모판·도감 같은 전체 화면이 열려 있으면 HUD 를 숨긴다.
        bool visible = !UIInputBlocker.IsModalOpen;
        if (hud.gameObject.activeSelf != visible)
            hud.gameObject.SetActive(visible);
        if (portraitCam != null)
            portraitCam.enabled = portraitLight.enabled = visible;
        if (!visible)
            return;

        nicknameText.text = Nickname;
        // 산책 중에는 시작 버튼 자리에 산책 카드(코스 · 자유 · 산책메이트)가 뜬다
        if (startButton.activeSelf == WalkSession.Active)
            startButton.SetActive(!WalkSession.Active);

        nearbyTimer -= Time.unscaledDeltaTime;
        if (nearbyTimer <= 0f)
        {
            nearbyTimer = 0.5f;
            RefreshNearby();
        }
    }

    void LateUpdate()
    {
        if (portraitCam == null || !portraitCam.enabled || character == null)
            return;
        RefreshPortraitModel();
        if (portraitModel == null)
            return;
        // 초상화용 캐릭터 정면에서 얼굴 높이를 바라본다.
        Transform c = portraitModel.transform;
        Vector3 look = c.TransformPoint(0f, portraitLookHeight, 0f);
        Vector3 pos = c.TransformPoint(0f, portraitLookHeight + 0.1f, portraitDistance);
        portraitCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
        portraitLight.transform.position = c.TransformPoint(0.4f, portraitLookHeight + 0.5f, portraitDistance * 0.8f);
    }

    public void Toast(string message)
    {
        if (toastText == null)
            return;
        toastText.text = message;
        toastBg.gameObject.SetActive(true);
        toastTimer = toastDuration;
    }

    /// <param name="popup">경험치가 늘었을 때 "+N XP" 를 띄울지 (걷기처럼 조금씩 쌓일 때는 false)</param>
    public void ShowProgress(GameProgress progress, IList<CollectibleType> types, bool popup = true)
    {
        if (progress == null || levelText == null)
            return;
        this.progress = progress;
        this.types = types;

        LevelOf(progress.score, out int level, out int xpInLevel, out int xpNeeded);
        levelText.text = $"Lv {level}";
        nextText.text = $"다음 레벨까지 {xpNeeded - xpInLevel} XP";
        xpFill.anchorMax = new Vector2(xpNeeded > 0 ? (float)xpInLevel / xpNeeded : 0f, 1f);

        if (popup && lastScore >= 0 && progress.score > lastScore)
            ShowXpPopup(progress.score - lastScore);
        lastScore = progress.score;

        ShowTickets(progress);
        if (menus != null)
            menus.OnProgressChanged(progress, types);
    }

    // ---------- 걷기 보상: 티켓 게이지 (닉네임 아래, B-V1) ----------

    Text eggText;
    RectTransform eggFill;

    /// <summary>가진 티켓 수와 다음 티켓까지 (1km 마다 1장, 하루 상한)</summary>
    public void ShowTickets(GameProgress progress)
    {
        if (progress == null || eggText == null)
            return;
        bool capped = progress.TicketCapped;
        float m = progress.ticketMeters;
        eggText.text = capped
            ? $"티켓 {progress.tickets}장 · 오늘 다 받음"
            : $"티켓 {progress.tickets}장 · {m:N0}/{GachaRules.TicketMeters:N0}m";
        eggFill.anchorMax = new Vector2(capped ? 1f : Mathf.Clamp01(m / GachaRules.TicketMeters), 1f);
        eggFill.GetComponent<Image>().color = capped ? KucaTheme.Line : KucaTheme.Gold;
    }

    /// <summary>레벨 n 에서 n+1 로 가려면 100 + (n-1)*10 XP 가 필요하다 (서버 WalkRules.LevelStartXp 와 같은 규칙).</summary>
    public static void LevelOf(int score, out int level, out int xpInLevel, out int xpNeeded)
    {
        level = 1;
        xpInLevel = Mathf.Max(0, score);
        xpNeeded = 100;
        while (xpInLevel >= xpNeeded)
        {
            xpInLevel -= xpNeeded;
            level++;
            xpNeeded = 100 + (level - 1) * 10;
        }
    }

    public string Nickname
    {
        get
        {
            string n = PlayerPrefs.GetString(NicknameKey, "").Trim();
            return n.Length > 0 ? n : defaultNickname;
        }
    }

    /// <summary>노치·홈 표시줄을 피하도록 Screen.safeArea 에 맞춘다.</summary>
    void ApplySafeArea()
    {
        Rect area = Screen.safeArea;
        if (area == appliedSafeArea || Screen.width <= 0 || Screen.height <= 0)
            return;
        appliedSafeArea = area;
        safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
    }

    // ---------- 위 왼쪽: 상태 카드 (얼굴 · 닉네임 · 레벨 · 경험치) + 티켓 칩 ----------

    void BuildStatus()
    {
        const float h = 156f, avatar = 124f;
        // 카드 전체가 프로필 버튼
        Button button = InvisibleButton(hud, "Status", () => menus.OpenProfile(), 0f);
        RectTransform root = (RectTransform)button.transform;
        Place(root, new Vector2(0f, 1f), new Vector2(Margin, -Margin));
        root.sizeDelta = new Vector2(660f, h);
        UIInputBlocker.Register(root);
        Image card = Floating(root, "Card", HudIcons.Pill, 1.3f);

        // 얼굴: 옅은 초록 원 안의 3D 초상
        Image mask = Circle(card.transform, "Face", KucaTheme.PrimarySoft, avatar);
        Place(mask.rectTransform, new Vector2(0f, 0.5f), new Vector2((h - avatar) / 2f, 0f));
        mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        portraitImage = new GameObject("Portrait", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        portraitImage.transform.SetParent(mask.transform, false);
        portraitImage.raycastTarget = false;
        UIKit.Stretch(portraitImage.rectTransform);
        portraitImage.enabled = false;

        float left = h - 6f;
        nicknameText = UIKit.Bold(UIKit.Text(card.transform, defaultNickname, KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.Ink));
        nicknameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        SetBox(nicknameText.rectTransform, left, 0.52f, 0.92f, 170f);
        levelText = UIKit.Bold(UIKit.Text(card.transform, "Lv 1", KucaTheme.Label, TextAnchor.MiddleRight, KucaTheme.PrimaryDeep));
        SetBox(levelText.rectTransform, left, 0.52f, 0.92f, 40f);

        Image bar = UIKit.Image(card.transform, "XpBar", KucaTheme.Fill);
        bar.sprite = HudIcons.Pill;
        bar.type = Image.Type.Sliced;
        bar.pixelsPerUnitMultiplier = 8f;
        SetBox(bar.rectTransform, left, 0.40f, 0.48f, 40f);
        Image fill = UIKit.Image(bar.transform, "Fill", KucaTheme.Primary);
        fill.sprite = HudIcons.Pill;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 8f;
        xpFill = fill.rectTransform;
        xpFill.anchorMin = Vector2.zero;
        xpFill.anchorMax = new Vector2(0f, 1f);
        xpFill.offsetMin = xpFill.offsetMax = Vector2.zero;
        nextText = UIKit.Text(card.transform, "", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        nextText.horizontalOverflow = HorizontalWrapMode.Overflow;
        SetBox(nextText.rectTransform, left, 0.06f, 0.36f, 40f);

        // 티켓 칩 (상태 카드 아래): 걸은 만큼 차고, 1km 가 되면 티켓 1장
        RectTransform chip = UIKit.Rect(hud, "TicketChip");
        Place(chip, new Vector2(0f, 1f), new Vector2(Margin, -Margin - h - KucaTheme.Space2));
        chip.sizeDelta = new Vector2(520f, 104f);
        Image chipFace = Floating(chip, "Card", HudIcons.Pill, 2f);
        Image ticket = UIKit.Image(chipFace.transform, "Ticket", KucaTheme.Gold);
        ticket.sprite = HudIcons.Ticket;
        Place(ticket.rectTransform, new Vector2(0f, 0.5f), new Vector2(28f, 0f));
        ticket.rectTransform.sizeDelta = new Vector2(60f, 60f);
        eggText = UIKit.Bold(UIKit.Text(chipFace.transform, "", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.Ink));
        eggText.horizontalOverflow = HorizontalWrapMode.Overflow;
        SetBox(eggText.rectTransform, 104f, 0.34f, 0.94f, 32f);
        Image eggBar = UIKit.Image(chipFace.transform, "Bar", KucaTheme.Fill);
        eggBar.sprite = HudIcons.Pill;
        eggBar.type = Image.Type.Sliced;
        eggBar.pixelsPerUnitMultiplier = 8f;
        SetBox(eggBar.rectTransform, 104f, 0.18f, 0.27f, 40f);
        Image eggBarFill = UIKit.Image(eggBar.transform, "Fill", KucaTheme.Gold);
        eggBarFill.sprite = HudIcons.Pill;
        eggBarFill.type = Image.Type.Sliced;
        eggBarFill.pixelsPerUnitMultiplier = 8f;
        eggFill = eggBarFill.rectTransform;
        eggFill.anchorMin = Vector2.zero;
        eggFill.anchorMax = new Vector2(0f, 1f);
        eggFill.offsetMin = eggFill.offsetMax = Vector2.zero;

        // "+N XP" (티켓 칩 오른쪽에 잠깐)
        Image pill = UIKit.Image(hud, "XpPopup", KucaTheme.Primary);
        pill.sprite = HudIcons.Pill;
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = 1.4f;
        RectTransform pr = pill.rectTransform;
        Place(pr, new Vector2(0f, 1f), new Vector2(Margin + 520f + KucaTheme.Space2, -Margin - h - KucaTheme.Space2 - 8f));
        pr.sizeDelta = new Vector2(220f, 88f);
        xpPopupText = UIKit.Bold(UIKit.Text(pill.transform, "", KucaTheme.Label, TextAnchor.MiddleCenter, Color.white));
        UIKit.Stretch(xpPopupText.rectTransform);
        xpPopup = pill.gameObject.AddComponent<CanvasGroup>();
        xpPopup.blocksRaycasts = false;
        pill.gameObject.SetActive(false);
    }

    // ---------- 위 오른쪽: 공지 · 사진첩 · 설정 ----------

    void BuildTopButtons()
    {
        const float size = 120f, gap = 20f;
        var buttons = new (string name, Sprite icon, UnityEngine.Events.UnityAction action)[]
        {
            ("Notices", HudIcons.Bell, OpenNotices),
            ("Album", HudIcons.Photos, OpenAlbum),
            ("Settings", HudIcons.Gear, OpenSettings),
        };
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform face = RoundButton(hud, buttons[i].name, size, buttons[i].action);
            Place(face, new Vector2(1f, 1f), new Vector2(-Margin, -Margin - i * (size + gap)));
            Image icon = UIKit.Image(face.Find("Face"), "Icon", KucaTheme.Ink);
            icon.sprite = buttons[i].icon;
            icon.rectTransform.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
            if (buttons[i].name == "Notices")
            {
                // 안 읽은 공지 수
                Image badge = Circle(face, "Badge", KucaTheme.Crimson, 48f);
                Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(6f, 6f));
                noticeBadgeText = UIKit.Bold(UIKit.Text(badge.transform, "", KucaTheme.Caption - 6, TextAnchor.MiddleCenter, Color.white));
                UIKit.Stretch(noticeBadgeText.rectTransform);
                noticeBadge = badge.gameObject;
            }
            else if (buttons[i].name == "Album")
            {
                // 내 게시물에 새 좋아요가 오면 빨간 점
                albumBadge = Circle(face, "Badge", KucaTheme.Crimson, 34f);
                Place(albumBadge.rectTransform, new Vector2(1f, 1f), new Vector2(-4f, -4f));
                albumBadge.gameObject.SetActive(false);
            }
        }
    }

    // ---------- 아래: 탭 막대 (네이버지도 · 토스 아래 탭: 아이콘 + 이름표) ----------

    void BuildTabBar()
    {
        Image bar = UIKit.Image(hud, "TabBar", KucaTheme.Card, raycast: true);
        bar.sprite = HudIcons.Rounded;
        bar.type = Image.Type.Sliced;
        bar.pixelsPerUnitMultiplier = 0.34f;
        RectTransform br = bar.rectTransform;
        br.anchorMin = Vector2.zero;
        br.anchorMax = new Vector2(1f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.offsetMin = new Vector2(0f, -400f);   // 아래 둥근 모서리는 홈 표시줄 아래로 숨긴다
        br.offsetMax = new Vector2(0f, TabBarHeight);
        UIInputBlocker.Register(br);
        Image line = UIKit.Image(bar.transform, "Line", KucaTheme.Line);
        line.rectTransform.anchorMin = new Vector2(0f, 1f);
        line.rectTransform.anchorMax = Vector2.one;
        line.rectTransform.offsetMin = new Vector2(48f, -2f);
        line.rectTransform.offsetMax = new Vector2(-48f, 0f);

        var tabs = new (string label, Sprite icon, UnityEngine.Events.UnityAction action)[]
        {
            ("지도", HudIcons.Pin, () => { if (cameraFollow != null) cameraFollow.ResetNorthUp(); }),   // 이미 지도: 북쪽 위로 되돌리기
            ("기록", HudIcons.Footprints, WalkHistoryScreen.Show),
            ("경희몬", HudIcons.Monster, () => menus.OpenMainMenu()),
            ("함께", HudIcons.Partner, MatchScreen.Open),
            ("나", HudIcons.Person, () => menus.OpenProfile()),
        };
        for (int i = 0; i < tabs.Length; i++)
        {
            Button b = InvisibleButton(bar.transform, tabs[i].label, tabs[i].action, 0f);
            RectTransform r = (RectTransform)b.transform;
            r.anchorMin = new Vector2(i / (float)tabs.Length, 1f);
            r.anchorMax = new Vector2((i + 1) / (float)tabs.Length, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(0f, -TabBarHeight);
            r.offsetMax = Vector2.zero;
            Color c = i == 0 ? KucaTheme.Primary : KucaTheme.InkMuted;   // 지금 보는 탭 = 지도
            Image icon = UIKit.Image(r, "Icon", c);
            icon.sprite = tabs[i].icon;
            Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f));
            icon.rectTransform.sizeDelta = new Vector2(76f, 76f);
            Text label = UIKit.Text(r, tabs[i].label, KucaTheme.Caption, TextAnchor.MiddleCenter, c);
            if (i == 0) UIKit.Bold(label);
            SetBox(label.rectTransform, 0f, 0.14f, 0.40f, 0f);
            if (i == 2)
            {
                // 받을 보상 · 뽑을 티켓이 있으면 빨간 점 (KhuMenus → SetMenuBadge)
                menuBadge = Circle(icon.transform, "Badge", KucaTheme.Crimson, 30f);
                Place(menuBadge.rectTransform, new Vector2(1f, 1f), new Vector2(6f, 6f));
                menuBadge.gameObject.SetActive(false);
            }
        }
    }

    // ---------- 아래 가운데: 산책 시작 (피크민 블룸 · 포켓몬 GO 의 가운데 큰 버튼) ----------

    void BuildStartButton()
    {
        Button button = InvisibleButton(hud, "StartWalk", RoutePlanner.Open, 0f);
        RectTransform root = (RectTransform)button.transform;
        Place(root, new Vector2(0.5f, 0f), new Vector2(0f, BottomReserve));
        root.sizeDelta = new Vector2(560f, KucaTheme.ButtonHeight);
        UIInputBlocker.Register(root);
        startButton = root.gameObject;
        Image face = Floating(root, "Face", HudIcons.Pill, 1f);
        face.color = KucaTheme.Primary;
        Image icon = UIKit.Image(face.transform, "Icon", Color.white);
        icon.sprite = HudIcons.Footprints;
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f));
        icon.rectTransform.sizeDelta = new Vector2(68f, 68f);
        Text t = UIKit.Bold(UIKit.Text(face.transform, "산책 시작", KucaTheme.Heading, TextAnchor.MiddleCenter, Color.white));
        UIKit.Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(90f, 0f);
    }

    // ---------- 오른쪽: 주변 (자연 출현이 켜진 경우만) ----------

    void BuildNearby()
    {
        const float cell = 96f, gap = 10f, pad = 16f;
        Vector2 size = new Vector2(cell * 2f + gap + pad * 2f, cell * 3f + gap * 2f + pad * 2f + 56f);
        Button button = InvisibleButton(hud, "Nearby", ShowNearbyToast, 0f);
        RectTransform root = (RectTransform)button.transform;
        nearbyRoot = root.gameObject;
        Place(root, new Vector2(1f, 1f), new Vector2(-Margin, -Margin - 3f * 140f - KucaTheme.Space2));
        root.sizeDelta = size;
        UIInputBlocker.Register(root);

        Image panel = Floating(root, "Panel", HudIcons.Rounded, 0.5f);
        Text tag = UIKit.Bold(UIKit.Text(panel.transform, "주변", KucaTheme.Caption, TextAnchor.MiddleCenter, KucaTheme.Ink));
        tag.rectTransform.anchorMin = new Vector2(0f, 1f);
        tag.rectTransform.anchorMax = Vector2.one;
        tag.rectTransform.offsetMin = new Vector2(0f, -60f);
        tag.rectTransform.offsetMax = new Vector2(0f, -8f);
        for (int i = 0; i < 6; i++)
        {
            int col = i % 2, row = i / 2;
            Image slot = UIKit.Image(panel.transform, "Slot" + i, KucaTheme.Fill);
            slot.sprite = HudIcons.Rounded;
            slot.type = Image.Type.Sliced;
            RectTransform sr = slot.rectTransform;
            sr.anchorMin = sr.anchorMax = sr.pivot = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(pad + col * (cell + gap), -56f - pad - row * (cell + gap));
            sr.sizeDelta = new Vector2(cell, cell);
            // 동물 피규어 썸네일 (칸보다 살짝 크게 그려 피규어가 꽉 차 보이게)
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            icon.transform.SetParent(slot.transform, false);
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
            icon.rectTransform.sizeDelta = new Vector2(cell * 1.05f, cell * 1.05f);
            icon.gameObject.SetActive(false);
            nearbyIcons.Add(icon);
        }
    }

    /// <summary>지도 위에 뜬 흰 면 (아래로 살짝 그림자). 반환값 = 흰 면</summary>
    static Image Floating(RectTransform parent, string name, Sprite sprite, float ppuMultiplier)
    {
        Image shadow = UIKit.Image(parent, name + "Shadow", Shade);
        shadow.sprite = sprite;
        shadow.type = Image.Type.Sliced;
        shadow.pixelsPerUnitMultiplier = ppuMultiplier;
        UIKit.Stretch(shadow.rectTransform);
        shadow.rectTransform.offsetMin = new Vector2(0f, -6f);
        shadow.rectTransform.offsetMax = new Vector2(0f, -6f);
        Image face = UIKit.Image(parent, name, KucaTheme.Card);
        face.sprite = sprite;
        face.type = Image.Type.Sliced;
        face.pixelsPerUnitMultiplier = ppuMultiplier;
        UIKit.Stretch(face.rectTransform);
        return face;
    }

    readonly List<Collectible> nearbyBuffer = new List<Collectible>();

    GameObject nearbyRoot;

    void RefreshNearby()
    {
        // v2: 지도 위 자연 출현이 꺼져 있으면 (뽑기로만 얻음) 주변 칸도 숨긴다
        bool spawning = collect != null && collect.spawner != null && collect.spawner.naturalSpawn;
        if (nearbyRoot != null && nearbyRoot.activeSelf != spawning)
            nearbyRoot.SetActive(spawning);
        nearbyBuffer.Clear();
        if (collect != null && collect.spawner != null && collect.campusMap != null && collect.campusMap.player != null)
        {
            Vector3 p = collect.campusMap.player.position;
            foreach (var c in collect.spawner.Active)
                if (c != null && CollectibleSpawner.HorizontalDistance(c.transform.position, p) <= nearbyRadius)
                    nearbyBuffer.Add(c);
            nearbyBuffer.Sort((a, b) =>
                CollectibleSpawner.HorizontalDistance(a.transform.position, p).CompareTo(CollectibleSpawner.HorizontalDistance(b.transform.position, p)));
        }
        for (int i = 0; i < nearbyIcons.Count; i++)
        {
            RawImage icon = nearbyIcons[i];
            Texture tex = i < nearbyBuffer.Count ? MonsterThumbnails.Get(nearbyBuffer[i].SpeciesId) : null;
            icon.gameObject.SetActive(tex != null);
            icon.texture = tex;
        }
    }

    void ShowNearbyToast()
    {
        if (nearbyBuffer.Count == 0)
        {
            Toast($"{nearbyRadius:F0}m 안에 경희몬이 없어요");
            return;
        }
        Collectible c = nearbyBuffer[0];
        float d = CollectibleSpawner.HorizontalDistance(c.transform.position, collect.campusMap.player.position);
        Toast($"주변 {nearbyBuffer.Count}개 · 가장 가까운 {c.DisplayName} {d:F0}m");
    }

    /// <summary>
    /// 플레이어와 같은 모습의 캐릭터를 지도 아래 멀리 하나 더 두고 대기 자세로 멈춰 초상화로 찍는다.
    /// 꾸미기로 모습이 바뀌면 (Appearance 가 새로 만들어지면) 다시 만든다.
    /// </summary>
    void RefreshPortraitModel()
    {
        CharacterAppearance look = character.Appearance;
        if (look == null || (portraitModel != null && look == portraitAppearance))
            return;
        if (portraitModel != null)
            Destroy(portraitModel.gameObject);
        portraitAppearance = look;
        var go = new GameObject("HUDPortraitModel") { layer = character.gameObject.layer };   // 장면 최상위 (HUD 캔버스 크기에 안 묶이게)
        go.transform.SetPositionAndRotation(new Vector3(0f, -30000f, 0f), Quaternion.identity);
        go.transform.localScale = character.transform.lossyScale;
        portraitModel = go.AddComponent<ModularCharacter>();
        portraitModel.database = character.database;
        portraitModel.headScale = character.headScale;
        portraitModel.legScale = character.legScale;
        portraitModel.armScale = character.armScale;
        portraitModel.freezePose = true;
        portraitModel.Build(look);
    }

    void BuildPortraitCamera()
    {
        if (character == null)
            return;
        portraitTexture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32) { name = "HUDPortrait", antiAliasing = 2 };
        var camGo = new GameObject("HUDPortraitCamera", typeof(Camera), typeof(NeutralAmbient));
        camGo.transform.SetParent(transform, false);
        portraitCam = camGo.GetComponent<Camera>();
        portraitCam.targetTexture = portraitTexture;
        portraitCam.clearFlags = CameraClearFlags.SolidColor;
        portraitCam.backgroundColor = KucaTheme.PrimarySoft;
        portraitCam.cullingMask = 1 << character.gameObject.layer;
        portraitCam.fieldOfView = portraitFov;
        portraitCam.nearClipPlane = 1f;
        portraitCam.farClipPlane = 200f;
        portraitImage.texture = portraitTexture;
        portraitImage.enabled = true;

        // 얼굴 앞의 작은 점광원 (캐릭터 레이어만, 범위는 머리 둘레 정도)
        var lightGo = new GameObject("HUDPortraitLight", typeof(Light));
        lightGo.transform.SetParent(transform, false);
        portraitLight = lightGo.GetComponent<Light>();
        portraitLight.type = LightType.Point;
        portraitLight.color = new Color(1f, 0.96f, 0.9f);
        // 점광원은 거리 제곱으로 약해지므로 캐릭터 크기(스케일)만큼 세기를 키운다.
        float scale = character.transform.lossyScale.y;
        float d = portraitDistance * 0.8f * scale;
        portraitLight.intensity = portraitLightIntensity * d * d;
        portraitLight.range = portraitDistance * 2f * scale;
        portraitLight.shadows = LightShadows.None;
        portraitLight.cullingMask = 1 << character.gameObject.layer;
    }

    void ShowXpPopup(int amount)
    {
        xpPopupText.text = $"+{amount} XP";
        xpPopup.gameObject.SetActive(true);
        xpPopup.alpha = 1f;
        xpPopupTimer = 1.8f;
    }

    Image albumBadge, menuBadge;


    /// <summary>아래 탭 "경희몬" 의 "받을 보상 · 뽑을 티켓 있음" 점</summary>
    public void SetMenuBadge(bool on)
    {
        if (menuBadge == null) return;
        menuBadge.gameObject.SetActive(on);
        menuBadge.transform.SetAsLastSibling();
    }

    /// <summary>사진첩 버튼의 새 반응 점</summary>
    public void SetAlbumBadge(bool on)
    {
        if (albumBadge != null)
        {
            albumBadge.gameObject.SetActive(on);
            albumBadge.transform.SetAsLastSibling();
        }
    }

    /// <summary>내 사진첩 (내가 메모판에 올린 사진들)</summary>
    void OpenAlbum()
    {
        var board = FindAnyObjectByType<MemoBoard>();
        if (board != null) board.OpenAlbum();
    }

    void OpenCustomizer()
    {
        if (customizer != null)
            customizer.SetOpen(true);
    }

    // ---------- 공지사항 ----------

    void LoadNotices()
    {
        notices = new NoticeData();
        if (noticesJson != null)
        {
            try
            {
                notices = JsonUtility.FromJson<NoticeData>(noticesJson.text) ?? new NoticeData();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameHUD] 공지사항을 읽지 못했습니다: {e.Message}");
            }
        }
        var saved = JsonUtility.FromJson<IdList>(PlayerPrefs.GetString(ReadNoticesKey, "{}")) ?? new IdList();
        readNotices = new HashSet<string>(saved.ids);
    }

    void RefreshNotices()
    {
        int unread = 0;
        foreach (var nt in notices.notices)
            if (!readNotices.Contains(nt.id))
                unread++;
        noticeBadge.SetActive(unread > 0);
        noticeBadgeText.text = unread > 9 ? "9+" : unread.ToString();
    }

    void OpenNotices()
    {
        SetScreen(noticeScreen, true, FillNotices);
        foreach (var nt in notices.notices)
            readNotices.Add(nt.id);
        PlayerPrefs.SetString(ReadNoticesKey, JsonUtility.ToJson(new IdList { ids = new List<string>(readNotices) }));
        PlayerPrefs.Save();
        RefreshNotices();
    }

    void CloseNotices() => SetScreen(noticeScreen, false, null);

    void FillNotices()
    {
        Clear(noticeList);
        if (notices.notices.Count == 0)
        {
            UIKit.EmptyState(noticeList, "새 공지가 없어요", "학교 · 앱 소식이 생기면 여기에 올라와요.");
            return;
        }
        foreach (var nt in notices.notices)
        {
            bool isNew = !readNotices.Contains(nt.id);
            RectTransform card = UIKit.CardBox(noticeList, nt.id, 16f);
            UIKit.Text(card, (isNew ? "<color=#A6192E><b>NEW</b></color>  " : "") + nt.date, KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
            UIKit.Bold(UIKit.Paragraph(card, nt.title, KucaTheme.Heading, KucaTheme.Ink));
            UIKit.Paragraph(card, nt.body, KucaTheme.Body, KucaTheme.Ink);
        }
    }

    // ---------- 설정 ----------

    void OpenSettings()
    {
        SetScreen(settingsScreen, true, null);
        nicknameInput.text = PlayerPrefs.GetString(NicknameKey, "");
        RefreshServerStatus();
        bool guest = AuthService.Current != null && AuthService.Current.isGuest;
        UIKit.SetLabel(logoutButton, guest ? "로그인하기 (체험 끝내기)" : "로그아웃");
        deleteAccountButton.gameObject.SetActive(AuthService.HasToken);
        virtualWalkButton.gameObject.SetActive(VirtualWalk.Allowed);
        UIKit.SetLabel(virtualWalkButton, VirtualWalk.Instance != null && VirtualWalk.Instance.On ? "가상 산책 끄기" : "가상 산책 켜기 (체험 계정)");
        deleteConfirmUntil = 0f;
        UIKit.SetLabel(deleteAccountButton, "회원 탈퇴");
    }

    void DeleteAccount()
    {
        if (Time.unscaledTime > deleteConfirmUntil)
        {
            deleteConfirmUntil = Time.unscaledTime + 4f;
            UIKit.SetLabel(deleteAccountButton, "한 번 더 누르면 계정과 정보가 지워져요");
            return;
        }
        deleteConfirmUntil = 0f;
        StartCoroutine(AuthService.DeleteAccount(error =>
        {
            if (error != null) { Toast("탈퇴하지 못했어요: " + error); return; }
            CloseSettings();
            Toast("계정을 지웠어요");
            LoginScreen.Begin();
        }));
    }

    void CloseSettings() => SetScreen(settingsScreen, false, null);

    /// <summary>
    /// 설정: 밝은 그룹 목록 (iOS 설정 · Material 목록). 항목은 한 줄 + 오른쪽 화살표, 위험한 일(탈퇴)은 맨 아래 따로 크림슨.
    /// 메모 서버는 개발 · 시연용이라 "고급" 묶음 맨 끝으로.
    /// </summary>
    void BuildSettings()
    {
        RectTransform profileGroup = UIKit.ListGroup(settingsList, "프로필");
        RectTransform nick = Padded(profileGroup, "Nickname");
        UIKit.Text(nick, "닉네임", KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        RectTransform row = UIKit.Rect(nick, "Row");
        UIKit.Size(row, KucaTheme.TouchMin);
        var h = UIKit.Horizontal(row, 20f);
        h.childForceExpandWidth = false;
        nicknameInput = UIKit.Input(row, defaultNickname, KucaTheme.Body, false, 12);
        UIKit.Size(nicknameInput, KucaTheme.TouchMin, -1f, 1f);
        Button save = UIKit.Button(row, "저장", SaveNickname, KucaTheme.Primary, KucaTheme.Body);
        UIKit.Size(save, KucaTheme.TouchMin, 200f);
        UIKit.Text(nick, "메모를 남길 때도 이 이름이 쓰여요.", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        UIKit.ListRow(profileGroup, "캐릭터 꾸미기", () => { CloseSettings(); OpenCustomizer(); });

        RectTransform walkGroup = UIKit.ListGroup(settingsList, "지도 · 산책");
        UIKit.ListRow(walkGroup, "지도 북쪽으로 돌리기", () => { CloseSettings(); if (cameraFollow != null) cameraFollow.ResetNorthUp(); });
        UIKit.ListRow(walkGroup, "산책 취향 다시 고르기", () => { CloseSettings(); PreferenceSurvey.Open(); });
        // 체험 계정만: 캠퍼스 밖에서도 정문부터 가상으로 걸어 보기 (공모전 심사 설명용)
        virtualWalkButton = UIKit.ListRow(walkGroup, "가상 산책 켜기 (체험 계정)", () =>
        {
            CloseSettings();
            var v = VirtualWalk.Instance;
            if (v == null) return;
            if (v.On) v.Stop(); else v.Begin();
        });

        // 화면: 강조색 계절 (자동 → 봄 → 여름 → 가을 → 겨울 → 자동)
        RectTransform screenGroup = UIKit.ListGroup(settingsList, "화면");
        Button season = null;
        season = UIKit.ListRow(screenGroup, SeasonLabel(), () =>
        {
            KucaTheme.Season? o = KucaTheme.Override;
            KucaTheme.Override = o == null ? KucaTheme.Season.Spring
                : (int)o.Value == KucaTheme.Seasons.Length - 1 ? (KucaTheme.Season?)null : (KucaTheme.Season)((int)o.Value + 1);
            UIKit.SetLabel(season, SeasonLabel());
        });

        RectTransform accountGroup = UIKit.ListGroup(settingsList, "계정");
        UIKit.ListRow(accountGroup, "개인정보 안내", () => { CloseSettings(); LoginScreen.ShowPrivacy(); });
        // 체험 계정이면 "로그인하기": 로그아웃하고 로그인 화면으로 (게임 진행은 기기에 있어 그대로 이어진다)
        logoutButton = UIKit.ListRow(accountGroup, "로그아웃", () =>
        {
            CloseSettings();
            StartCoroutine(AuthService.Logout(LoginScreen.Begin));
        });

        // 메모 서버 주소 (비우면 이 기기 안의 가짜 서버)
        RectTransform advanced = UIKit.ListGroup(settingsList, "고급");
        RectTransform server = Padded(advanced, "Server");
        UIKit.Text(server, "메모 서버", KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        RectTransform srow = UIKit.Rect(server, "Row");
        UIKit.Size(srow, KucaTheme.TouchMin);
        var sh = UIKit.Horizontal(srow, 20f);
        sh.childForceExpandWidth = false;
        serverInput = UIKit.Input(srow, "비우면 기본 서버", KucaTheme.Label, false, 80);
        serverInput.keyboardType = TouchScreenKeyboardType.URL;
        UIKit.Size(serverInput, KucaTheme.TouchMin, -1f, 1f);
        Button check = UIKit.Button(srow, "연결", SaveServer, KucaTheme.PrimarySoft, KucaTheme.Body);
        check.GetComponentInChildren<Text>().color = KucaTheme.PrimaryDeep;
        UIKit.Size(check, KucaTheme.TouchMin, 200f);
        serverStatus = UIKit.Text(server, "", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkMuted);

        RectTransform danger = UIKit.ListGroup(settingsList, null);
        deleteAccountButton = UIKit.ListRow(danger, "회원 탈퇴", DeleteAccount, danger: true);

        UIKit.Size(UIKit.Text(settingsList, "KUCA · 경희대 국제캠퍼스를 함께 걷는 산책 동반자", KucaTheme.Caption, TextAnchor.MiddleCenter, KucaTheme.InkSoft), 100f);
    }

    static string SeasonLabel() => KucaTheme.Override.HasValue
        ? $"강조색: {KucaTheme.Set.name}"
        : $"강조색: 자동 (지금 {KucaTheme.Set.name})";

    /// <summary>목록 카드 안의 여백 있는 칸 (입력칸 같은 내용용)</summary>
    static RectTransform Padded(RectTransform group, string name)
    {
        RectTransform box = UIKit.Rect(group, name);
        UIKit.Vertical(box, 14f, new RectOffset(40, 40, 28, 28));
        return box;
    }

    string DefaultMemoServer
    {
        get
        {
            var board = FindAnyObjectByType<MemoBoard>();
            return board != null ? board.DefaultServerUrl : MemoBoard.DefaultServer;
        }
    }

    void RefreshServerStatus()
    {
        bool custom = PlayerPrefs.HasKey(MemoBoard.ServerUrlKey);
        string url = custom ? PlayerPrefs.GetString(MemoBoard.ServerUrlKey) : DefaultMemoServer;
        serverInput.text = custom ? url : "";
        serverStatus.color = KucaTheme.InkMuted;
        serverStatus.text = custom
            ? $"지금 쓰는 서버: {url}  (비우고 연결을 누르면 기본 서버로)"
            : $"기본 서버를 쓰고 있어요: {url}";
    }

    void SaveServer()
    {
        string url = HttpMemoService.Normalize(serverInput.text);
        if (url.Length == 0)
        {
            // 비우면 기본 서버로 되돌린다.
            PlayerPrefs.DeleteKey(MemoBoard.ServerUrlKey);
            PlayerPrefs.Save();
            RefreshServerStatus();
            Toast("기본 메모 서버를 써요");
            return;
        }
        serverStatus.color = KucaTheme.InkMuted;
        serverStatus.text = "연결 확인 중…";
        StartCoroutine(HttpMemoService.Check(url, (ok, error) =>
        {
            if (!ok)
            {
                serverStatus.color = KucaTheme.Crimson;
                serverStatus.text = error;
                return;
            }
            PlayerPrefs.SetString(MemoBoard.ServerUrlKey, url);
            PlayerPrefs.Save();
            RefreshServerStatus();
            serverStatus.color = KucaTheme.Primary;
            serverStatus.text = $"연결됐어요: {url}";
            Toast("메모 서버에 연결했어요");
        }));
    }

    void SaveNickname()
    {
        string nick = nicknameInput.text.Trim();
        PlayerPrefs.SetString(NicknameKey, nick);
        PlayerPrefs.Save();
        Toast("닉네임을 저장했어요");
        if (AuthService.HasToken && nick.Length is >= 1 and <= 20)
            StartCoroutine(AuthService.SetNickname(nick, (_, error) => { if (error != null) Toast(error); })); // 계정에도
    }

    // ---------- 공통 ----------

    void SetScreen(GameObject screen, bool open, Action fill)
    {
        screen.SetActive(open);
        UIInputBlocker.SetModal(screen, open);
        fill?.Invoke();
    }

    /// <summary>그림자 + 흰 원 얼굴(이름 "Face")을 가진 원형 버튼</summary>
    static RectTransform RoundButton(Transform parent, string name, float size, UnityEngine.Events.UnityAction onClick)
    {
        Button button = InvisibleButton(parent, name, onClick, size);
        RectTransform root = (RectTransform)button.transform;
        UIInputBlocker.Register(root);
        Image shadow = Circle(root, "Shadow", Shade, size);
        Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -5f));
        Image face = Circle(root, "Face", Color.white, size);
        Place(face.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        return root;
    }

    static Button InvisibleButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick, float size)
    {
        Image hit = UIKit.Image(parent, name, new Color(0f, 0f, 0f, 0f), raycast: true);
        if (size > 0f)
            hit.rectTransform.sizeDelta = new Vector2(size, size);
        var button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);
        hit.gameObject.AddComponent<PressScale>();
        return button;
    }

    static Image Circle(Transform parent, string name, Color color, float size)
    {
        Image img = UIKit.Image(parent, name, color);
        img.sprite = HudIcons.Circle;
        img.rectTransform.sizeDelta = new Vector2(size, size);
        return img;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    /// <summary>왼쪽 여백 left, 세로 범위 [yMin,yMax] (부모 비율), 오른쪽 여백 right 인 칸</summary>
    static void SetBox(RectTransform rt, float left, float yMin, float yMax, float right)
    {
        rt.anchorMin = new Vector2(0f, yMin);
        rt.anchorMax = new Vector2(1f, yMax);
        rt.offsetMin = new Vector2(left, 0f);
        rt.offsetMax = new Vector2(-right, 0f);
    }


    static void Clear(RectTransform content)
    {
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }
}

/// <summary>누르는 동안 살짝 작아지는 버튼 효과</summary>
public class PressScale : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    float target = 1f;

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => target = 0.92f;
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => target = 1f;
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => target = 1f;

    void Update()
    {
        float s = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 3f);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
