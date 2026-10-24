using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 경희몬 전체 화면들 (디자인 v4, docs/디자인_v4.md: 회색 바탕 · 흰 카드 · 큰 왼쪽 제목 · 왼쪽 위 ‹ 뒤로).
/// - 메인 메뉴 (가운데 경희 로고를 누르면): 도감 · 경희몬 · 도구
/// - 컬렉션 (탭 3개): 도감(종류별 발견), 경희몬(잡은 개체, CP), 도구(가방)
/// - 경희몬 상세: 파트너로 지정
/// - 프로필 (프로필 사진을 누르면): 캐릭터와 파트너, 레벨, 메뉴 4개, 활동 기록
/// 걷은 거리와 방문한 경희스팟도 여기서 기록한다. GameHUD 가 같은 캔버스에 만든다.
/// </summary>
public partial class KhuMenus : MonoBehaviour
{
    public enum Tab { Dex, Monsters, Items }

    static readonly Color Teal = KucaTheme.Primary;
    static readonly Color TealDeep = KucaTheme.PrimaryDeep;
    static readonly Color Mint = new Color(0.38f, 0.86f, 0.70f);
    static readonly Color Gray = KucaTheme.InkMuted;
    static readonly Color Orange = new Color(0.96f, 0.58f, 0.10f);
    static readonly Color Green = KucaTheme.Primary;
    static readonly Color ValueGreen = KucaTheme.Primary;
    // 메인 메뉴 (파란 그라데이션)
    static readonly Color MenuNavy = KucaTheme.PrimaryDeep;
    // 프로필 (버건디 그라데이션)
    static readonly Color PText = new Color(1f, 0.94f, 0.90f);
    static readonly Color PGold = new Color(0.96f, 0.79f, 0.45f);
    static readonly Color PCircle = new Color(0.84f, 0.64f, 0.32f);

    const int MonsterStorage = 300;
    const int ItemStorage = 350;

    GameHUD hud;
    Transform canvasRoot;
    RectTransform safe;
    CollectController collect;
    CollectibleSpawner spawner;
    ModularCharacter character;
    CharacterCustomizer customizer;
    KyungHeeSpots spots;
    PartnerBuddy buddy;

    GameProgress progress;
    IList<CollectibleType> types;

    // 메인 메뉴 (허브)
    GameObject mainMenu;
    readonly List<Transform> menuButtons = new List<Transform>();
    Text hubMonsters, hubDex;

    // 컬렉션
    GameObject collection;
    UIKit.Page collectionPage;
    Tab tab;
    readonly Text[] tabLabels = new Text[3];
    readonly Text[] tabCounts = new Text[3];
    readonly Image[] tabSegs = new Image[3];
    InputField search;
    RectTransform grid;
    ScrollRect gridScroll;
    GameObject sortButton;
    Button sortChip;
    int sortMode; // 0 최근, 1 CP, 2 이름
    static readonly string[] SortNames = { "최근 잡은 순", "쿠옹력 높은 순", "이름 순" };

    // 상세 / 목록 팝업
    GameObject detail;
    RawImage detailThumb;
    Text detailCp, detailName, detailInfo, detailPartner;
    Button detailPartnerButton;
    GameProgress.Caught detailTarget;
    GameObject listPopup;
    Text listTitle;
    RectTransform listContent;

    // 스크랩북
    GameObject scrapbook, scrapViewer;
    RectTransform scrapGrid;
    Text scrapEmpty, scrapCaption;
    RawImage scrapPhoto;
    AspectRatioFitter scrapFitter;
    readonly List<Texture2D> scrapTextures = new List<Texture2D>();

    // 프로필
    GameObject profile;
    GameObject profileMe, profileOther;
    Text profileOtherTitle, profileOtherText;
    readonly Text[] profileTabs = new Text[3];
    readonly Image[] profileSegs = new Image[3];
    RawImage profileView;
    RectTransform profileHero;
    RectTransform profileRays;
    Text profileName, profilePartner, profileLevel, profileXp;
    RectTransform profileXpFill;
    Text statWalk, statCaught, statSpots;
    Camera profileCam;
    Light profileLight;
    RenderTexture profileTexture;
    float profileYaw;

    // 활동 기록
    // 걸은 거리: 마지막 기준점에서 AnchorMeters 이상 벗어날 때만, 걷는 속도일 때만 센다 (GPS 튐 거르기)
    const float AnchorMeters = 8f, MaxGpsAccuracy = 30f;
#if UNITY_EDITOR
    const float MaxWalkSpeed = 20f; // 에디터 키보드 걷기(5m/s, Shift 15m/s)도 세지게
#else
    const float MaxWalkSpeed = 3.5f; // 뛰는 정도까지. 차·버스·순간이동은 세지 않는다
#endif
    Vector3 anchorPos;
    float anchorTime;
    bool hasAnchor;
    float saveTimer, spotTimer;
    bool dirty;

    public void Build(GameHUD owner, Transform canvas, RectTransform safeArea)
    {
        hud = owner;
        canvasRoot = canvas;
        safe = safeArea;
        BuildMainMenu();
        BuildCollection();
        BuildDetail();
        BuildAccessoryPopup();
        BuildProfile();
        BuildListPopup();
        BuildScrapbook();
        BuildQuests();
        BuildGacha();
        BuildStarter();
    }

    void Start()
    {
        collect = FindAnyObjectByType<CollectController>();
        spawner = collect != null ? collect.spawner : FindAnyObjectByType<CollectibleSpawner>();
        character = hud.character != null ? hud.character : FindAnyObjectByType<ModularCharacter>();
        customizer = hud.customizer != null ? hud.customizer : FindAnyObjectByType<CharacterCustomizer>();
        spots = FindAnyObjectByType<KyungHeeSpots>();
        if (progress == null && collect != null)
            progress = collect.Progress;
        if (types == null && spawner != null)
            types = spawner.types;

        if (character != null)
        {
            buddy = new GameObject("PartnerBuddy").AddComponent<PartnerBuddy>();
            buddy.character = character.transform;
            RefreshBuddy();
        }
        OnProgressChanged(progress, types);
        InitQuests();
    }

    void OnDestroy()
    {
        OnDestroy_Quests();
        foreach (var s in new[] { mainMenu, collection, profile, questScreen })
            UIInputBlocker.SetModal(s, false);
        if (profileTexture != null)
            profileTexture.Release();
    }

    /// <summary>점수가 바뀔 때마다 GameHUD 가 부른다. 레벨 보상을 주고 열린 화면을 새로 그린다.</summary>
    public void OnProgressChanged(GameProgress p, IList<CollectibleType> t)
    {
        if (p != null) progress = p;
        if (t != null) types = t;
        if (progress == null)
            return;

        // v2: 도구(가방)는 숨겼으니 레벨 보상 도구는 주지 않고 알림만 (보상은 C-V4 서버 값으로)
        GameHUD.LevelOf(progress.score, out int level, out _, out _);
        if (progress.rewardedLevel < level)
        {
            bool levelUp = progress.rewardedLevel > 0;
            progress.rewardedLevel = level;
            progress.Save();
            if (levelUp && hud != null)
                hud.Toast($"레벨 {level} 달성!");
        }

        if (mainMenu != null && mainMenu.activeSelf)
            FillHub();
        if (collection != null && collection.activeSelf)
            FillCollection();
        if (gachaScreen != null && gachaScreen.activeSelf)
            FillGacha();
        RefreshGachaBadge();
        if (profile != null && profile.activeSelf)
            FillProfile();
    }

    void Update()
    {
        TrackActivity();
        MaybeOfferStarter();
        if (profile != null && profile.activeSelf)
        {
            UpdateProfileCamera();
            profileRays.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 4f);
        }
    }

    // ---------- 활동 기록 (걸은 거리, 방문한 경희스팟) ----------

    void TrackActivity()
    {
        if (progress == null || collect == null || collect.campusMap == null || collect.campusMap.player == null)
            return;
        Vector3 p = collect.campusMap.player.position;
        if (!hasAnchor)
        {
            anchorPos = p;
            anchorTime = Time.unscaledTime;
            hasAnchor = true;
        }
        else
        {
            float d = CollectibleSpawner.HorizontalDistance(p, anchorPos);
            if (d >= AnchorMeters)
            {
                float speed = d / Mathf.Max(0.05f, Time.unscaledTime - anchorTime);
                float accuracy = collect.campusMap.CurrentAccuracy;
                // 가상 산책 (체험 계정) 은 "빠르게" 로 걸어도 센다
                float maxSpeed = collect.campusMap.VirtualWalk ? VirtualWalk.TopSpeed + 1f : MaxWalkSpeed;
                if (speed <= maxSpeed && (accuracy <= 0f || accuracy <= MaxGpsAccuracy))
                    OnWalked(d);
                anchorPos = p;
                anchorTime = Time.unscaledTime;
            }
        }

        spotTimer -= Time.unscaledDeltaTime;
        if (spotTimer <= 0f && spots != null)
        {
            spotTimer = 1f;
            foreach (KyungHeeSpot s in spots.Spots)
            {
                if (s == null || !s.InRange || s.Building == null)
                    continue;
                progress.VisitSpot(s.Building.buildingId, out bool isNew);
                Quests.Report("visit_spot", 1, s.Building.buildingId); // 이번 주 들른 스팟 (스팟마다 한 번)
                if (isNew) Quests.Report("visit_new");
                if (isNew)
                {
                    dirty = true;
                    hud?.Toast($"새 경희스팟 방문!  {s.Building.DisplayName}");
                }
            }
        }

        saveTimer -= Time.unscaledDeltaTime;
        if (dirty && saveTimer <= 0f)
        {
            saveTimer = 10f;
            dirty = false;
            progress.Save();
        }
    }

    /// <summary>걸은 거리: 티켓 (1km, 하루 상한) · 경험치 (1km 10) · 파트너 진화 경험치 (100m 1)</summary>
    void OnWalked(float meters)
    {
        GameProgress.WalkResult r = progress.AddWalk(meters);
        Quests.Report("walk", Mathf.RoundToInt(meters));
        dirty = true;
        if (r.tickets > 0)
        {
            progress.Save();
            hud?.Toast($"티켓 {r.tickets}장을 받았어요!  오늘 {progress.TicketsToday}/{GachaRules.TicketsPerDay}");
        }
        if (r.evolved && progress.Partner != null)
        {
            progress.Save();
            EvolutionScreen.Queue(progress.Partner.speciesId, r.fromStage, r.stage); // 다른 화면이 열려 있으면 닫힌 뒤에
            RefreshBuddy();
        }
        if (r.xp > 0 || r.tickets > 0)
            hud?.ShowProgress(progress, types, popup: false); // 걷기 경험치는 조금씩 쌓여서 "+1 XP" 는 띄우지 않는다
        else
            hud?.ShowTickets(progress);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && progress != null)
            progress.Save();
    }

    // ---------- 메인 메뉴 ----------

    public void OpenMainMenu()
    {
        FillHub();
        Show(mainMenu, true);
        // 카드가 살짝 작게 시작해서 PressScale 이 원래 크기로 되돌리며 톡 나온다.
        foreach (Transform b in menuButtons)
            b.localScale = Vector3.one * 0.96f;
    }

    /// <summary>
    /// 경희몬 허브 (참조: 토스 전체 탭 · 포켓몬 GO 메뉴). 원형 아이콘 4개 대신 큰 카드 목록:
    /// 아이콘 · 이름 · 한 줄 설명 · 오른쪽 숫자 알약 (마리 수 · 가진 티켓 · 도감 진행).
    /// v2 에서 퀘스트 · 도구는 숨김 (코드는 남김, 결정 D3 · D4)
    /// </summary>
    void BuildMainMenu()
    {
        UIKit.Page page = UIKit.CreatePage(canvasRoot, "MainMenu", "경희몬", () => Show(mainMenu, false), "걸어서 만나고, 함께 걸으며 키워요");
        mainMenu = page.root;
        hubMonsters = HubCard(page.content, HudIcons.Monster, "내 경희몬", "파트너 정하기 · 꾸미기", KucaTheme.Fill, KucaTheme.Ink,
            () => OpenCollection(Tab.Monsters)).GetComponentInChildren<Text>(true);
        gachaMenuBadge = HubCard(page.content, HudIcons.Ticket, "뽑기", "티켓으로 새 경희몬 만나기", KucaTheme.Primary, Color.white, OpenGacha);
        gachaMenuBadgeText = gachaMenuBadge.GetComponentInChildren<Text>(true);
        gachaMenuBadge.gameObject.SetActive(false);
        hubDex = HubCard(page.content, HudIcons.Dex, "도감", "만난 경희몬 모아 보기", KucaTheme.Fill, KucaTheme.Ink,
            () => OpenCollection(Tab.Dex)).GetComponentInChildren<Text>(true);
        HubCard(page.content, HudIcons.Footprints, "산책 기록", "지금까지 걸은 길", KucaTheme.Fill, KucaTheme.Ink,
            () => { Show(mainMenu, false); WalkHistoryScreen.Show(); }).gameObject.SetActive(false);   // 숫자 칸 없음
    }

    /// <summary>허브 카드 한 장. 반환값은 오른쪽 숫자 알약 (글자는 그 안의 Text)</summary>
    Image HubCard(Transform parent, Sprite icon, string title, string desc, Color pillBg, Color pillFg, UnityEngine.Events.UnityAction onClick)
    {
        Button b = CardButton(parent, title, onClick);
        UIKit.Size(b, 240f);
        menuButtons.Add(b.transform);
        RectTransform r = (RectTransform)b.transform;

        Image tile = UIKit.Image(r, "Tile", KucaTheme.PrimarySoft);
        tile.sprite = HudIcons.Rounded;
        tile.type = Image.Type.Sliced;
        tile.pixelsPerUnitMultiplier = 0.5f;
        Place(tile.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f));
        tile.rectTransform.sizeDelta = new Vector2(148f, 148f);
        Image ic = UIKit.Image(tile.transform, "Icon", KucaTheme.PrimaryDeep);
        ic.sprite = icon;
        Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        ic.rectTransform.sizeDelta = new Vector2(92f, 92f);

        Text t = T(r, title, KucaTheme.Heading, TextAnchor.LowerLeft, KucaTheme.Ink, bold: true);
        Corners(t.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(228f, 0f), new Vector2(-280f, -30f));
        Text d = T(r, desc, KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
        Corners(d.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(228f, 24f), new Vector2(-280f, -10f));

        Image chev = UIKit.Image(r, "Chevron", KucaTheme.InkSoft);
        chev.sprite = HudIcons.Chevron;
        Place(chev.rectTransform, new Vector2(1f, 0.5f), new Vector2(-40f, 0f));
        chev.rectTransform.sizeDelta = new Vector2(44f, 44f);

        Image pill = UIKit.Image(r, "Value", pillBg);
        pill.sprite = HudIcons.Pill;
        pill.type = Image.Type.Sliced;
        pill.pixelsPerUnitMultiplier = 1.4f;
        Place(pill.rectTransform, new Vector2(1f, 0.5f), new Vector2(-104f, 0f));
        pill.rectTransform.sizeDelta = new Vector2(176f, 84f);
        Text v = T(pill.transform, "", KucaTheme.Label, TextAnchor.MiddleCenter, pillFg, bold: true);
        UIKit.Stretch(v.rectTransform);
        return pill;
    }

    void FillHub()
    {
        if (progress == null || hubDex == null)
            return;
        hubDex.text = $"{FoundSpecies()} / {CreatureLibrary.AllSpecies.Count}";
        hubMonsters.text = $"{progress.caught.Count}마리";
        RefreshGachaBadge();
    }

    /// <summary>한 번이라도 만난 종 수 (도감 진행)</summary>
    int FoundSpecies()
    {
        int n = 0;
        foreach (string id in CreatureLibrary.AllSpecies)
            if (progress.SpeciesCountOf(id) > 0)
                n++;
        return n;
    }

    // ---------- 컬렉션 (도감 / 내 경희몬 / 도구) ----------

    public void OpenCollection(Tab which)
    {
        Show(mainMenu, false);
        Show(collection, true);
        search.text = "";
        SelectTab(which);
    }

    /// <summary>
    /// 도감 · 내 경희몬 (참조: 포켓몬 HOME 박스 · 도감): 큰 제목 + 진행 한 줄, 세그먼트 탭, 검색 · 정렬, 3열 흰 카드 격자.
    /// 못 만난 경희몬은 실루엣, 비었으면 다음 행동(뽑기 하러 가기)이 있는 빈 상태.
    /// </summary>
    void BuildCollection()
    {
        collectionPage = UIKit.CreatePage(canvasRoot, "Collection", "도감", () => Show(collection, false), " ");
        collection = collectionPage.root;
        RectTransform content = collectionPage.content;
        gridScroll = content.GetComponentInParent<ScrollRect>(true);

        // 탭 (v2: 도구 탭은 숨김, 결정 D4)
        Segmented(content, new[] { "도감", "내 경희몬", "도구" }, 2, i => SelectTab((Tab)i), tabLabels, tabCounts, tabSegs);

        // 검색 + 정렬 (정렬은 내 경희몬 탭에서만)
        RectTransform tools = UIKit.Rect(content, "Tools");
        UIKit.Size(tools, KucaTheme.TouchMin);
        HorizontalLayoutGroup h = UIKit.Horizontal(tools, KucaTheme.Space2);
        h.childForceExpandWidth = false;
        search = UIKit.Input(tools, "이름으로 찾기", KucaTheme.Body, false, 20);
        search.GetComponent<Image>().color = KucaTheme.Card;
        UIKit.Size(search, KucaTheme.TouchMin, -1f, 1f);
        search.onValueChanged.AddListener(_ => FillCollection());
        sortChip = UIKit.Button(tools, SortNames[0], CycleSort, KucaTheme.Card, KucaTheme.Label);
        sortChip.GetComponentInChildren<Text>(true).color = KucaTheme.Ink;
        UIKit.Size(sortChip, KucaTheme.TouchMin, 330f);
        sortButton = sortChip.gameObject;

        grid = UIKit.Rect(content, "Grid");
        UIKit.Vertical(grid, KucaTheme.Space2);
    }

    void SelectTab(Tab which)
    {
        tab = which;
        PaintSegments((int)which, tabLabels, tabCounts, tabSegs);
        collectionPage.title.text = which == Tab.Dex ? "도감" : which == Tab.Monsters ? "내 경희몬" : "가방";
        sortButton.SetActive(which == Tab.Monsters);
        gridScroll.verticalNormalizedPosition = 1f;
        FillCollection();
    }

    void CycleSort()
    {
        sortMode = (sortMode + 1) % SortNames.Length;
        UIKit.SetLabel(sortChip, SortNames[sortMode]);
        FillCollection();
    }

    void FillCollection()
    {
        Clear(grid);
        if (progress == null)
            return;
        string q = search != null ? search.text.Trim() : "";

        IReadOnlyList<string> all = CreatureLibrary.AllSpecies;
        int found = FoundSpecies();
        tabCounts[0].text = $"{found} / {all.Count}";
        tabCounts[1].text = $"{progress.caught.Count}마리";
        tabCounts[2].text = $"{progress.TotalItems}개";
        collectionPage.subtitle.text = tab == Tab.Dex ? $"{all.Count}종 중 {found}종을 만났어요"
            : tab == Tab.Monsters ? "누르면 파트너로 정하거나 꾸밀 수 있어요" : "레벨을 올리면 도구를 받아요";

        int shown = 0;
        switch (tab)
        {
            case Tab.Dex:
                // C → B → A → S 구역 (v2 등급). 번호는 도감 전체 순서, 못 만난 동물은 그림자로.
                foreach (GachaRules.Grade grade in System.Enum.GetValues(typeof(GachaRules.Grade)))
                {
                    IReadOnlyList<string> species = GachaRules.SpeciesOf(grade);
                    var visible = new List<string>();
                    int tierFound = 0;
                    foreach (string id in species)
                    {
                        bool known = progress.SpeciesCountOf(id) > 0;
                        if (known) tierFound++;
                        if (q.Length == 0 || (known && CreatureLibrary.NameOf(id).Contains(q)))
                            visible.Add(id);
                    }
                    if (visible.Count == 0)
                        continue;
                    RectTransform section = Section($"{GachaRules.Label(grade)} 등급", GachaRules.ColorOf(grade),
                        $"{tierFound} / {species.Count} 발견");
                    foreach (string id in visible)
                    {
                        DexCell(section, IndexOf(all, id) + 1, id, progress.SpeciesCountOf(id) > 0);
                        shown++;
                    }
                }
                if (shown == 0)
                    UIKit.EmptyState(grid, "검색 결과가 없어요", "만난 경희몬의 이름으로 찾아보세요");
                break;

            case Tab.Monsters:
                var list = new List<GameProgress.Caught>(progress.caught);
                if (sortMode == 0) list.Sort((x, y) => y.caughtAt.CompareTo(x.caughtAt));
                else if (sortMode == 1) list.Sort((x, y) => y.cp.CompareTo(x.cp));
                else list.Sort((x, y) => string.CompareOrdinal(NameOf(x), NameOf(y)));
                // 파트너는 항상 맨 앞
                int pi = list.FindIndex(c => c.uid == progress.partnerUid);
                if (pi > 0) { var pc = list[pi]; list.RemoveAt(pi); list.Insert(0, pc); }
                RectTransform mons = Section(null, Color.clear, null);
                foreach (var c in list)
                {
                    if (q.Length > 0 && !NameOf(c).Contains(q))
                        continue;
                    MonsterCell(mons, c);
                    shown++;
                }
                if (shown == 0 && q.Length > 0)
                    UIKit.EmptyState(grid, "검색 결과가 없어요", "다른 이름으로 찾아보세요");
                else if (shown == 0)
                    UIKit.EmptyState(grid, "아직 경희몬이 없어요", "1km 걸을 때마다 티켓 1장.\n티켓으로 뽑기에서 만나요", "뽑기 하러 가기",
                        () => { Show(collection, false); OpenGacha(); });
                break;

            case Tab.Items:
                RectTransform items = Section(null, Color.clear, null);
                foreach (var item in ItemCatalog.All)
                {
                    int n = progress.ItemCount(item.id);
                    if (n <= 0 || (q.Length > 0 && !item.name.Contains(q)))
                        continue;
                    ItemCell(items, item, n);
                    shown++;
                }
                if (shown == 0)
                    UIKit.EmptyState(grid, q.Length > 0 ? "검색 결과가 없어요" : "가방이 비어 있어요", "레벨을 올리면 도구를 받아요");
                break;
        }
    }

    static int IndexOf(IReadOnlyList<string> list, string id)
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] == id)
                return i;
        return -1;
    }

    /// <summary>구역 하나 (title 이 있으면 색 점 + 제목 + 오른쪽 부제). 반환값이 칸을 넣을 3열 그리드.</summary>
    RectTransform Section(string title, Color color, string sub)
    {
        if (title != null)
        {
            RectTransform head = UIKit.Rect(grid, "Header_" + title);
            UIKit.Size(head, 104f);
            Image dot = Circle(head, "Dot", color, 28f);
            Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 0f));
            Text t = T(head, title, KucaTheme.Heading, TextAnchor.MiddleLeft, KucaTheme.Ink, bold: true);
            SetBox(t.rectTransform, 52f, 0f, 1f, 340f);
            Text s2 = T(head, sub ?? "", KucaTheme.Label, TextAnchor.MiddleRight, KucaTheme.InkMuted);
            SetBox(s2.rectTransform, 420f, 0f, 1f, 0f);
        }
        RectTransform section = UIKit.Rect(grid, "Grid_" + (title ?? "All"));
        var g = section.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(304f, 400f);   // 960 너비 (Gutter 60 양쪽) 를 3열로
        g.spacing = new Vector2(24f, 24f);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 3;
        g.childAlignment = TextAnchor.UpperLeft;
        return section;
    }

    void MonsterCell(RectTransform parent, GameProgress.Caught c)
    {
        Transform cell = CardButton(parent, c.uid, () => OpenDetail(c)).transform;
        GradeChip(cell, GachaRules.GradeOf(c.speciesId));
        Thumb(cell, c.speciesId, true, 220f, new Vector2(0f, -156f));
        Text name = T(cell, NameOf(c), KucaTheme.Label, TextAnchor.MiddleCenter, KucaTheme.Ink, bold: true);
        BottomBox(name.rectTransform, 84f, 56f);
        FitOneLine(name, KucaTheme.Label);
        Text cp = T(cell, $"쿠옹력 {c.cp}", KucaTheme.Caption, TextAnchor.MiddleCenter, KucaTheme.InkMuted);
        BottomBox(cp.rectTransform, 40f, 44f);
        StageDots(cell, progress.StageOf(c.speciesId), new Vector2(0f, -378f));

        if (c.uid == progress.partnerUid)
        {
            Image tag = UIKit.Image(cell, "Partner", KucaTheme.Primary);
            tag.sprite = HudIcons.Pill;
            tag.type = Image.Type.Sliced;
            tag.pixelsPerUnitMultiplier = 2.4f;
            Place(tag.rectTransform, new Vector2(1f, 1f), new Vector2(-16f, -20f));
            tag.rectTransform.sizeDelta = new Vector2(132f, 52f);
            Text tt = T(tag.transform, "파트너", KucaTheme.Caption - 4, TextAnchor.MiddleCenter, Color.white, bold: true);
            UIKit.Stretch(tt.rectTransform);
        }
    }

    void DexCell(RectTransform parent, int number, string species, bool known)
    {
        GachaRules.Grade grade = GachaRules.GradeOf(species);
        Transform cell = CardButton(parent, species, () =>
        {
            if (known)
                ShowList(CreatureLibrary.NameOf(species), DexRows(species));
            else
                hud?.Toast(grade >= GachaRules.Grade.B ? $"아직 만나지 못한 {GachaRules.Label(grade)} 등급이에요. 5장 뽑기에서 나와요"
                                                       : "아직 만나지 못한 경희몬이에요. 1장 뽑기에서 나와요");
        }).transform;
        Text no = T(cell, $"No.{number:000}", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkSoft, bold: true);
        TopBox(no.rectTransform, 24f, 48f);
        no.rectTransform.offsetMin = new Vector2(24f, no.rectTransform.offsetMin.y);
        GradeChip(cell, grade, right: true);
        Thumb(cell, species, known, 220f, new Vector2(0f, -170f));
        Text name = T(cell, known ? CreatureLibrary.NameOf(species) : "???", KucaTheme.Label, TextAnchor.MiddleCenter,
            known ? KucaTheme.Ink : KucaTheme.InkSoft, bold: true);
        BottomBox(name.rectTransform, 84f, 56f);
        FitOneLine(name, KucaTheme.Label);
        Text cnt = T(cell, known ? $"만난 수 {progress.SpeciesCountOf(species)}" : "아직 못 만났어요", KucaTheme.Caption, TextAnchor.MiddleCenter,
            known ? KucaTheme.PrimaryDeep : KucaTheme.InkSoft);
        BottomBox(cnt.rectTransform, 40f, 44f);
        if (known)
            StageDots(cell, progress.StageOf(species), new Vector2(0f, -378f));
    }

    /// <summary>칸 위 모서리 등급 원 (C · B · A · S)</summary>
    static void GradeChip(Transform cell, GachaRules.Grade grade, bool right = false)
    {
        Image chip = Circle(cell, "Grade", GachaRules.ColorOf(grade), 56f);
        Place(chip.rectTransform, new Vector2(right ? 1f : 0f, 1f), new Vector2(right ? -18f : 18f, -18f));
        Text g = T(chip.transform, GachaRules.Label(grade), KucaTheme.Caption, TextAnchor.MiddleCenter, Color.white, bold: true);
        UIKit.Stretch(g.rectTransform);
    }

    /// <summary>진화 단계 점 ●●○ (0~2단계 → 1~3개 채움)</summary>
    static void StageDots(Transform cell, int stage, Vector2 pos)
    {
        RectTransform row = UIKit.Rect(cell, "Stage");
        row.anchorMin = row.anchorMax = row.pivot = new Vector2(0.5f, 1f);
        row.anchoredPosition = pos;
        row.sizeDelta = new Vector2(96f, 22f);
        for (int i = 0; i <= GachaRules.MaxStage; i++)
        {
            Image dot = Circle(row, "Dot" + i, i <= stage ? KucaTheme.Gold : KucaTheme.Line, 20f);
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 32f, 0f));
        }
    }

    List<(string, string)> DexRows(string species)
    {
        var mine = progress.caught.FindAll(c => c.speciesId == species);
        int best = 0;
        long first = long.MaxValue;
        foreach (var c in mine)
        {
            best = Mathf.Max(best, c.cp);
            first = Math.Min(first, c.caughtAt);
        }
        GachaRules.Grade grade = GachaRules.GradeOf(species);
        int growth = progress.GrowthOf(species);
        int next = GachaRules.NextThreshold(growth);
        var rows = new List<(string, string)>
        {
            ("등급", $"{GachaRules.Label(grade)} 등급"),
            ("진화 단계", $"{progress.StageOf(species)}단계" + (next > 0 ? $"  ·  경험치 {growth}/{next}" : "  ·  최종")),
            ("만난 수", $"{progress.SpeciesCountOf(species)}마리"),
            ("가지고 있는 수", $"{mine.Count}마리"),
            ("최고 쿠옹력", best > 0 ? best.ToString() : "-"),
            ("처음 만난 날", first != long.MaxValue ? new DateTime(first).ToString("yyyy.MM.dd") : "-"),
            ("진화하려면", "함께 걷기 또는 뽑기에서 또 만나기"),
        };
        return rows;
    }

    void ItemCell(RectTransform parent, ItemCatalog.Item item, int count)
    {
        Transform cell = CardButton(parent, item.id, () => ShowList(item.name, new List<(string, string)>
        {
            ("가지고 있는 수", $"{count}개"),
            (item.description, ""),
        })).transform;
        Text n = T(cell, $"×{count}", KucaTheme.Heading, TextAnchor.MiddleCenter, KucaTheme.Ink, bold: true);
        TopBox(n.rectTransform, 16f, 70f);
        Image icon = UIKit.Image(cell, "Icon", Color.white);
        icon.sprite = HudIcons.ItemIcon(item.id);
        icon.preserveAspect = true;
        Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -170f));
        icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        icon.rectTransform.sizeDelta = new Vector2(150f, 150f);
        Text name = T(cell, item.name, KucaTheme.Label, TextAnchor.MiddleCenter, KucaTheme.Ink, bold: true);
        BottomBox(name.rectTransform, 40f, 60f);
    }

    // ---------- 경희몬 상세 ----------

    /// <summary>경희몬 상세 카드: 큰 그림 · 이름 · 쿠옹력 · 등급/단계, 아래 "파트너로 지정" 하나 + 꾸미기 · 닫기</summary>
    void BuildDetail()
    {
        detail = Overlay(collection.transform, "Detail", new Vector2(940f, 1420f), out RectTransform card);
        detailThumb = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        detailThumb.transform.SetParent(card, false);
        detailThumb.raycastTarget = false;
        Place(detailThumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -48f));
        detailThumb.rectTransform.sizeDelta = new Vector2(420f, 420f);

        detailName = T(card, "", KucaTheme.Title, TextAnchor.MiddleCenter, KucaTheme.Ink, bold: true);
        TopBox(detailName.rectTransform, 484f, 90f);
        detailCp = T(card, "", KucaTheme.Heading, TextAnchor.MiddleCenter, KucaTheme.PrimaryDeep, bold: true);
        TopBox(detailCp.rectTransform, 580f, 70f);
        detailInfo = T(card, "", KucaTheme.Label, TextAnchor.UpperCenter, KucaTheme.InkMuted);
        TopBox(detailInfo.rectTransform, 670f, 120f);
        detailPartner = T(card, "", KucaTheme.Label, TextAnchor.MiddleCenter, KucaTheme.PrimaryDeep, bold: true);
        TopBox(detailPartner.rectTransform, 800f, 60f);

        detailPartnerButton = UIKit.PrimaryButton(card, "파트너로 지정", SetDetailAsPartner);
        Dock((RectTransform)detailPartnerButton.transform, 48f + KucaTheme.ButtonHeight + KucaTheme.Space2, KucaTheme.ButtonHeight);
        Button dress = UIKit.SecondaryButton(card, "꾸미기", () => OpenAccessories(detailTarget));
        Dock((RectTransform)dress.transform, 48f, KucaTheme.ButtonHeight, 0f, 0.5f);
        Button close = UIKit.SecondaryButton(card, "닫기", () => SetActive(detail, false));
        Dock((RectTransform)close.transform, 48f, KucaTheme.ButtonHeight, 0.5f, 1f);
        BackButton.Attach(detail, () => SetActive(detail, false));
    }

    void OpenDetail(GameProgress.Caught c)
    {
        detailTarget = c;
        detailThumb.texture = ThumbOf(c.speciesId);
        detailCp.text = $"쿠옹력 {c.cp}";
        detailName.text = string.IsNullOrEmpty(c.speciesId) ? NameOf(c) : CreatureLibrary.StageName(c.speciesId, progress.StageOf(c.speciesId));
        int growth = progress.GrowthOf(c.speciesId);
        int next = GachaRules.NextThreshold(growth);
        string stage = $"{progress.StageOf(c.speciesId)}단계" + (next > 0 ? $" (진화 경험치 {growth}/{next})" : " (최종)");
        detailInfo.text = $"{GachaRules.Label(GachaRules.GradeOf(c.speciesId))} 등급  ·  {stage}\n만난 날짜  {c.CaughtAt:yyyy.MM.dd HH:mm}";
        bool isPartner = c.uid == progress.partnerUid;
        detailPartner.text = isPartner ? "♥ 지금 함께 다니는 파트너예요" : "";
        detailPartnerButton.interactable = !isPartner;
        UIKit.SetLabel(detailPartnerButton, isPartner ? "현재 파트너" : "파트너로 지정");
        detailPartnerButton.GetComponent<Image>().color = isPartner ? KucaTheme.Line : KucaTheme.Primary;
        detailPartnerButton.GetComponentInChildren<Text>(true).color = isPartner ? KucaTheme.InkMuted : Color.white;
        SetActive(detail, true);
    }

    void SetDetailAsPartner()
    {
        if (detailTarget == null)
            return;
        progress.SetPartner(detailTarget.uid);
        progress.Save();
        RefreshBuddy();
        hud?.Toast($"{NameOf(detailTarget)}(쿠옹력 {detailTarget.cp}){CatchCameraScreen.Josa(NameOf(detailTarget), "과", "와")} 함께 다녀요!");
        OpenDetail(detailTarget);
        FillCollection();
    }

    void RefreshBuddy()
    {
        if (buddy == null || progress == null)
            return;
        GameProgress.Caught p = progress.Partner;
        if (p == null)
            buddy.Hide();
        else
        {
            buddy.Show(p.speciesId, p.accessories);
            buddy.SetStage(progress.StageOf(p.speciesId)); // 진화 단계: 크기 · 테두리 빛 (A-V1)
        }
    }

    /// <summary>파트너가 진화했을 때 (완료 화면 보상 등 메뉴 밖에서) 지도 위 파트너를 다시 그린다</summary>
    public void RefreshPartner() => RefreshBuddy();

    /// <summary>"아기 비둘기가 함께 걸어서 비둘기로 진화했어요!" (단계 이름은 A 의 CreatureLibrary.StageName)</summary>
    public static string EvolvedMessage(string species, int stage, string how = "")
    {
        string before = CreatureLibrary.StageName(species, stage - 1);
        string after = CreatureLibrary.StageName(species, stage);
        return $"{before}{CatchCameraScreen.Josa(before, "이", "가")} {how}{after}{Ro(after)} 진화했어요!";
    }

    /// <summary>"으로" / "로" (받침이 없거나 ㄹ 받침이면 "로")</summary>
    static string Ro(string word)
    {
        if (string.IsNullOrEmpty(word)) return "로";
        char c = word[word.Length - 1];
        if (c < 0xAC00 || c > 0xD7A3) return "로";
        int final = (c - 0xAC00) % 28;
        return final == 0 || final == 8 ? "로" : "으로";
    }

    // ---------- 목록 팝업 (역대 파트너, 모험노트, 도감·도구 정보) ----------

    void BuildListPopup()
    {
        listPopup = Overlay(canvasRoot, "ListPopup", new Vector2(940f, 1440f), out RectTransform card);
        listTitle = T(card, "", KucaTheme.Title, TextAnchor.MiddleLeft, KucaTheme.Ink, bold: true);
        TopBox(listTitle.rectTransform, 48f, 96f);
        listTitle.rectTransform.offsetMin = new Vector2(56f, listTitle.rectTransform.offsetMin.y);
        RectTransform area = UIKit.Rect(card, "List");
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(24f, 48f + KucaTheme.ButtonHeight + KucaTheme.Space3);
        area.offsetMax = new Vector2(-24f, -160f);
        listContent = ScrollContent(area, out _);
        UIKit.Vertical(listContent, 0f, new RectOffset(32, 32, 0, 24));
        Button close = UIKit.SecondaryButton(card, "닫기", () => SetActive(listPopup, false));
        Dock((RectTransform)close.transform, 48f, KucaTheme.ButtonHeight);
        BackButton.Attach(listPopup, () => SetActive(listPopup, false));
    }

    void ShowList(string title, List<(string left, string right)> rows)
    {
        listTitle.text = title;
        Clear(listContent);
        if (rows.Count == 0)
            UIKit.Size(T(listContent, "아직 기록이 없어요", KucaTheme.Body, TextAnchor.MiddleCenter, KucaTheme.InkMuted), 200f);
        foreach (var (left, right) in rows)
        {
            RectTransform row = UIKit.Rect(listContent, "Row");
            if (string.IsNullOrEmpty(right))
            {
                T(row, left, KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
                UIKit.Vertical(row, 0f, new RectOffset(0, 0, 20, 20));
                continue;
            }
            UIKit.Size(row, 124f);
            Text l = T(row, left, KucaTheme.Body, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
            UIKit.Stretch(l.rectTransform);
            Text r = T(row, right, KucaTheme.Body, TextAnchor.MiddleRight, KucaTheme.Ink, bold: true);
            UIKit.Stretch(r.rectTransform);
            // 값은 이름 오른쪽 남은 칸에만. 길면 칸에 맞게 글자를 줄인다 (두 줄까지).
            r.rectTransform.offsetMin = new Vector2(l.preferredWidth + 28f, 0f);
            r.horizontalOverflow = HorizontalWrapMode.Wrap;
            r.verticalOverflow = VerticalWrapMode.Truncate;
            r.resizeTextForBestFit = true;
            r.resizeTextMinSize = KucaTheme.Caption - 6;
            r.resizeTextMaxSize = KucaTheme.Body;
            Image line = UIKit.Image(row, "Line", KucaTheme.Line);
            line.rectTransform.anchorMin = new Vector2(0f, 0f);
            line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.offsetMin = Vector2.zero;
            line.rectTransform.offsetMax = new Vector2(0f, 2f);
        }
        SetActive(listPopup, true);
        listPopup.transform.SetAsLastSibling();
    }


    // ---------- 스크랩북 (잡을 때 찍은 사진) ----------

    void BuildScrapbook()
    {
        scrapbook = Overlay(canvasRoot, "Scrapbook", new Vector2(960f, 1640f), out RectTransform card);
        Text t = T(card, "스크랩북", KucaTheme.Title, TextAnchor.MiddleLeft, KucaTheme.Ink, bold: true);
        TopBox(t.rectTransform, 48f, 96f);
        t.rectTransform.offsetMin = new Vector2(56f, t.rectTransform.offsetMin.y);
        RectTransform area = UIKit.Rect(card, "Grid");
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(24f, 48f + KucaTheme.ButtonHeight + KucaTheme.Space3);
        area.offsetMax = new Vector2(-24f, -160f);
        scrapGrid = ScrollContent(area, out _);
        var g = scrapGrid.gameObject.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(280f, 360f);
        g.spacing = new Vector2(16f, 20f);
        g.padding = new RectOffset(12, 12, 10, 20);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 3;
        g.childAlignment = TextAnchor.UpperCenter;
        scrapEmpty = T(area, "", KucaTheme.Body, TextAnchor.MiddleCenter, KucaTheme.InkMuted);
        UIKit.Stretch(scrapEmpty.rectTransform, 60f, 100f);
        Button close = UIKit.SecondaryButton(card, "닫기", CloseScrapbook);
        Dock((RectTransform)close.transform, 48f, KucaTheme.ButtonHeight);
        BackButton.Attach(scrapbook, CloseScrapbook);

        // 사진 크게 보기
        Image dim = UIKit.Image(canvasRoot, "ScrapViewer", new Color(0f, 0f, 0f, 0.92f), raycast: true);
        UIKit.Stretch(dim.rectTransform);
        scrapViewer = dim.gameObject;
        RectTransform frame = UIKit.Rect(dim.transform, "Frame");
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.offsetMin = new Vector2(30f, 330f);
        frame.offsetMax = new Vector2(-30f, -120f);
        var photo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        photo.transform.SetParent(frame, false);
        scrapPhoto = photo.GetComponent<RawImage>();
        scrapPhoto.raycastTarget = false;
        scrapFitter = photo.GetComponent<AspectRatioFitter>();
        scrapFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        scrapCaption = T(dim.transform, "", KucaTheme.Label, TextAnchor.MiddleCenter, Color.white, bold: true);
        BottomBox(scrapCaption.rectTransform, 230f, 90f);
        Button back = UIKit.SecondaryButton(dim.transform, "닫기", () => scrapViewer.SetActive(false));
        Dock((RectTransform)back.transform, 60f, KucaTheme.ButtonHeight);
        BackButton.Attach(scrapViewer, () => scrapViewer.SetActive(false));
        scrapViewer.SetActive(false);
    }

    void ShowScrapbook()
    {
        Clear(scrapGrid);
        FreeScrapTextures();
        var list = progress.caught.FindAll(c => !string.IsNullOrEmpty(c.photo));
        list.Sort((a, b) => b.caughtAt.CompareTo(a.caughtAt));
        int shown = 0;
        foreach (var c in list)
        {
            string path = System.IO.Path.Combine(GameProgress.ScrapbookDir, c.photo);
            if (!System.IO.File.Exists(path))
                continue;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(System.IO.File.ReadAllBytes(path)))
            {
                Destroy(tex);
                continue;
            }
            scrapTextures.Add(tex);
            GameProgress.Caught caught = c;
            Button b = InvisibleButton(scrapGrid, c.uid, () => ViewScrap(tex, caught), Vector2.zero);
            RectTransform box = UIKit.Rect(b.transform, "Box");
            TopBox(box, 0f, 270f);
            box.gameObject.AddComponent<RectMask2D>();
            Image bg = UIKit.Image(box, "Bg", KucaTheme.Fill);
            UIKit.Stretch(bg.rectTransform);
            var go = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            go.transform.SetParent(box, false);
            go.GetComponent<RawImage>().texture = tex;
            go.GetComponent<RawImage>().raycastTarget = false;
            var fit = go.GetComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = (float)tex.width / tex.height;
            Text name = T(b.transform, NameOf(c), KucaTheme.Label - 4, TextAnchor.MiddleCenter, KucaTheme.Ink, bold: true);
            BottomBox(name.rectTransform, 38f, 50f);
            Text date = T(b.transform, c.CaughtAt.ToString("yyyy.MM.dd"), KucaTheme.Caption - 4, TextAnchor.MiddleCenter, KucaTheme.InkMuted);
            BottomBox(date.rectTransform, 2f, 36f);
            shown++;
        }
        scrapEmpty.text = shown == 0 ? "아직 사진이 없어요\n경희몬을 잡을 때 찍은 사진이 여기에 모여요" : "";
        SetActive(scrapbook, true);
    }

    void ViewScrap(Texture2D tex, GameProgress.Caught c)
    {
        scrapPhoto.texture = tex;
        scrapFitter.aspectRatio = (float)tex.width / tex.height;
        scrapCaption.text = $"{NameOf(c)}  ·  쿠옹력 {c.cp}  ·  {c.CaughtAt:yyyy.MM.dd HH:mm}";
        SetActive(scrapViewer, true);
    }

    void CloseScrapbook()
    {
        scrapbook.SetActive(false);
        scrapViewer.SetActive(false);
        Clear(scrapGrid);
        FreeScrapTextures();
    }

    void FreeScrapTextures()
    {
        foreach (var t in scrapTextures)
            if (t != null)
                Destroy(t);
        scrapTextures.Clear();
    }

    // ---------- 프로필 ----------

    public void OpenProfile()
    {
        Show(profile, true);
        SelectProfileTab(0);
        profileYaw = 0f;
        FillProfile();
        EnsureProfileCamera();
        if (buddy != null)
            buddy.profilePose = true;
    }

    void CloseProfile()
    {
        Show(profile, false);
        if (profileCam != null)
            profileCam.enabled = profileLight.enabled = false;
        if (buddy != null)
            buddy.profilePose = false;
    }

    /// <summary>
    /// 프로필 (참조: 애플 피트니스 요약 · 토스 내 정보): 세그먼트(나 · 프렌드 · 소셜), 캐릭터 3D 카드(이름 아래에서 시작),
    /// 레벨 큰 숫자 + 경험치 막대, 활동 숫자 3개, 아래 목록(지난 파트너 · 스크랩북 · 모험노트 · 꾸미기).
    /// </summary>
    void BuildProfile()
    {
        UIKit.Page page = UIKit.CreatePage(canvasRoot, "Profile", "내 프로필", CloseProfile);
        profile = page.root;
        RectTransform content = page.content;
        ScrollRect scroll = content.GetComponentInParent<ScrollRect>(true);

        Segmented(content, new[] { "나", "프렌드", "소셜" }, 3, SelectProfileTab, profileTabs, null, profileSegs);

        // "나": 세로로 쌓인 카드들
        RectTransform me = UIKit.Rect(content, "Me");
        UIKit.Vertical(me, KucaTheme.Space4);
        profileMe = me.gameObject;
        BuildProfileHero(me, scroll);
        BuildProfileLevel(me);
        BuildProfileStats(me);
        RectTransform more = UIKit.ListGroup(me, "기록과 꾸미기");
        UIKit.ListRow(more, "지난 파트너", ShowPartnerHistory);
        UIKit.ListRow(more, "스크랩북", ShowScrapbook);
        UIKit.ListRow(more, "모험노트", ShowAdventureNote);
        UIKit.ListRow(more, "캐릭터 꾸미기", () => { if (customizer != null) customizer.SetOpen(true); });

        // 프렌드 / 소셜 (준비 중)
        RectTransform other = UIKit.EmptyState(content, " ", " ");
        profileOther = other.gameObject;
        profileOtherTitle = other.GetChild(0).GetComponent<Text>();
        profileOtherText = other.GetChild(1).GetComponent<Text>();
    }

    /// <summary>캐릭터 카드: 위에 이름 · 파트너 · "파트너 바꾸기", 그 아래에 3D 캐릭터 (끌어서 돌리기)</summary>
    void BuildProfileHero(RectTransform parent, ScrollRect scroll)
    {
        Image card = UIKit.Image(parent, "Hero", KucaTheme.Card);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.34f;
        UIKit.Size(card, 1000f);
        RectTransform s = card.rectTransform;
        s.gameObject.AddComponent<RectMask2D>();

        Image rays = UIKit.Image(s, "Rays", new Color(KucaTheme.Gold.r, KucaTheme.Gold.g, KucaTheme.Gold.b, 0.16f));
        rays.sprite = HudIcons.Rays;
        Place(rays.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f));
        rays.rectTransform.sizeDelta = new Vector2(860f, 860f);
        profileRays = rays.rectTransform;

        profileView = new GameObject("View", typeof(RectTransform), typeof(RawImage), typeof(ProfileDragArea)).GetComponent<RawImage>();
        profileView.transform.SetParent(s, false);
        UIKit.Stretch(profileView.rectTransform);
        profileView.rectTransform.offsetMax = new Vector2(0f, -250f);   // 이름 · 파트너 아래에서 캐릭터가 시작 (머리와 안 겹치게)
        var drag = profileView.GetComponent<ProfileDragArea>();
        drag.scroll = scroll;
        drag.onDragX = dx => profileYaw += dx * 0.4f;
        profileHero = s;

        profileName = T(s, "", KucaTheme.Title, TextAnchor.MiddleLeft, KucaTheme.Ink, bold: true);
        profileName.horizontalOverflow = HorizontalWrapMode.Overflow;
        TopBox(profileName.rectTransform, 48f, 90f);
        profileName.rectTransform.offsetMin = new Vector2(48f, profileName.rectTransform.offsetMin.y);
        profilePartner = T(s, "", KucaTheme.Body, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        profilePartner.horizontalOverflow = HorizontalWrapMode.Overflow;
        TopBox(profilePartner.rectTransform, 140f, 60f);
        profilePartner.rectTransform.offsetMin = new Vector2(48f, profilePartner.rectTransform.offsetMin.y);

        Button change = UIKit.Chip(s, "파트너 바꾸기", false, () => OpenCollection(Tab.Monsters));
        RectTransform cr = (RectTransform)change.transform;
        Place(cr, new Vector2(1f, 1f), new Vector2(-40f, -48f));
        cr.sizeDelta = new Vector2(change.GetComponent<LayoutElement>().preferredWidth, 104f);

        Text hint = T(s, "좌우로 끌어서 돌려 보기", KucaTheme.Caption, TextAnchor.MiddleCenter, KucaTheme.InkSoft);
        BottomBox(hint.rectTransform, 24f, 50f);
    }

    /// <summary>레벨 카드: "레벨 5" 큰 숫자 + 오른쪽 경험치, 아래 막대</summary>
    void BuildProfileLevel(RectTransform parent)
    {
        RectTransform card = UIKit.CardBox(parent, "Level", KucaTheme.Space2);
        RectTransform row = UIKit.Rect(card, "Row");
        UIKit.Size(row, 120f);
        profileLevel = T(row, "", KucaTheme.Display - 20, TextAnchor.MiddleLeft, KucaTheme.Ink, bold: true);
        profileLevel.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIKit.Stretch(profileLevel.rectTransform);
        profileXp = T(row, "", KucaTheme.Label, TextAnchor.MiddleRight, KucaTheme.InkMuted, bold: true);
        UIKit.Stretch(profileXp.rectTransform);

        Image bar = UIKit.Image(card, "Bar", KucaTheme.Line);
        bar.sprite = HudIcons.Pill;
        bar.type = Image.Type.Sliced;
        bar.pixelsPerUnitMultiplier = 6f;
        UIKit.Size(bar, 24f);
        Image fill = UIKit.Image(bar.transform, "Fill", KucaTheme.Primary);
        fill.sprite = HudIcons.Pill;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 6f;
        profileXpFill = fill.rectTransform;
        profileXpFill.anchorMin = Vector2.zero;
        profileXpFill.offsetMin = profileXpFill.offsetMax = Vector2.zero;
        UIKit.Size(T(card, "걸을수록 · 리뷰를 남길수록 경험치가 쌓여요", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkSoft), 50f);
    }

    /// <summary>활동 숫자 3개 (애플 피트니스 요약)</summary>
    void BuildProfileStats(RectTransform parent)
    {
        RectTransform card = UIKit.CardBox(parent, "Stats", KucaTheme.Space2);
        UIKit.SectionTitle(card, "활동");
        RectTransform row = UIKit.StatRow(card, ("0 m", "걸은 거리"), ("0", "잡은 경희몬"), ("0", "들른 경희스팟"));
        statWalk = row.GetChild(0).GetComponentInChildren<Text>(true);
        statCaught = row.GetChild(1).GetComponentInChildren<Text>(true);
        statSpots = row.GetChild(2).GetComponentInChildren<Text>(true);
    }

    void SelectProfileTab(int i)
    {
        PaintSegments(i, profileTabs, null, profileSegs);
        profileMe.SetActive(i == 0);
        profileOther.SetActive(i != 0);
        profileOtherTitle.text = i == 1 ? "프렌드는 준비 중이에요" : "소셜은 준비 중이에요";
        profileOtherText.text = i == 1 ? "같이 캠퍼스를 걷는 친구를 곧 추가할 수 있어요"
                                       : "다른 학생들의 산책 소식을 곧 볼 수 있어요";
        if (profileView != null)
            profileView.enabled = i == 0;
    }

    void FillProfile()
    {
        if (progress == null)
            return;
        profileName.text = hud != null ? hud.Nickname : "";
        GameProgress.Caught partner = progress.Partner;
        profilePartner.text = partner != null ? $"파트너  {NameOf(partner)}" : "파트너를 정해 보세요";

        GameHUD.LevelOf(progress.score, out int level, out int xpIn, out int xpNeed);
        profileLevel.text = $"레벨 {level}";
        profileXp.text = $"{xpIn:N0} / {xpNeed:N0} XP";
        profileXpFill.anchorMax = new Vector2(Mathf.Max(0.03f, xpNeed > 0 ? (float)xpIn / xpNeed : 0f), 1f);

        statWalk.text = progress.walkedMeters >= 1000f ? $"{progress.walkedMeters / 1000f:F1} km" : $"{progress.walkedMeters:F0} m";
        statCaught.text = $"{progress.totalCollected:N0}";
        statSpots.text = $"{progress.visitedSpots.Count:N0}";
    }

    void ShowPartnerHistory()
    {
        var rows = new List<(string, string)>();
        for (int i = progress.partnerHistory.Count - 1; i >= 0; i--)
        {
            var r = progress.partnerHistory[i];
            GameProgress.Caught c = progress.Find(r.uid);
            string name = c != null ? $"{NameOf(c)} (쿠옹력 {c.cp})" : "떠나간 경희몬";
            rows.Add((name, new DateTime(r.since).ToString("yyyy.MM.dd") + (r.uid == progress.partnerUid ? " ~ 지금" : "")));
        }
        ShowList("역대 파트너", rows);
    }

    void ShowAdventureNote()
    {
        var list = new List<GameProgress.Caught>(progress.caught);
        list.Sort((a, b) => b.caughtAt.CompareTo(a.caughtAt));
        var rows = new List<(string, string)>();
        for (int i = 0; i < list.Count && i < 50; i++)
            rows.Add(($"{list[i].CaughtAt:MM.dd HH:mm}  {NameOf(list[i])}", $"쿠옹력 {list[i].cp}"));
        ShowList("모험노트", rows);
    }

    void EnsureProfileCamera()
    {
        if (character == null)
            return;
        if (profileCam == null)
        {
            var camGo = new GameObject("ProfileCamera", typeof(Camera), typeof(NeutralAmbient));
            camGo.transform.SetParent(transform, false);
            profileCam = camGo.GetComponent<Camera>();
            profileCam.clearFlags = CameraClearFlags.SolidColor;
            profileCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            profileCam.cullingMask = 1 << character.gameObject.layer;
            profileCam.fieldOfView = 26f;
            profileCam.nearClipPlane = 1f;
            profileCam.farClipPlane = 500f;

            var lightGo = new GameObject("ProfileLight", typeof(Light));
            lightGo.transform.SetParent(transform, false);
            profileLight = lightGo.GetComponent<Light>();
            profileLight.type = LightType.Directional;
            profileLight.intensity = 1.45f;   // 1.1 은 캡처에서 탁하고 어두웠음
            profileLight.shadows = LightShadows.None;
            profileLight.cullingMask = 1 << character.gameObject.layer;
        }

        // 화면 영역 비율에 맞는 렌더 텍스처
        Canvas.ForceUpdateCanvases();
        Rect r = profileView.rectTransform.rect;
        float aspect = r.height > 1f ? r.width / r.height : 1f;
        int h = 900, w = Mathf.Clamp(Mathf.RoundToInt(h * aspect), 256, 2048);
        if (profileTexture == null || profileTexture.width != w || profileTexture.height != h)
        {
            if (profileTexture != null)
                profileTexture.Release();
            profileTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { name = "ProfileView", antiAliasing = 2 };
        }
        profileCam.targetTexture = profileTexture;
        profileView.texture = profileTexture;
        profileCam.enabled = profileLight.enabled = true;
    }

    void UpdateProfileCamera()
    {
        if (profileCam == null || character == null)
            return;
        // 꾸미기 화면이 위에 열려 있으면 그쪽 미리보기 카메라에 맡긴다.
        bool show = profileMe.activeSelf && !CharacterCustomizer.IsOpen;
        profileCam.enabled = profileLight.enabled = show;
        // 캐릭터와 왼쪽의 파트너가 함께 들어오도록, 둘 사이를 바라본다.
        Transform c = character.transform;
        Vector3 pivot = c.TransformPoint(-0.4f, 0.85f, 0f);
        Vector3 offset = Quaternion.Euler(0f, profileYaw, 0f) * new Vector3(0f, 0.25f, 5.2f);
        Vector3 pos = c.TransformPoint(new Vector3(-0.4f, 0.85f, 0f) + offset);
        profileCam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pivot - pos, Vector3.up));
        profileLight.transform.rotation = profileCam.transform.rotation * Quaternion.Euler(25f, -30f, 0f);
    }

    // ---------- 공통 도구 ----------

    CollectibleType TypeOf(string id) => spawner != null ? spawner.TypeOf(id) : null;

    static string NameOf(GameProgress.Caught c) => string.IsNullOrEmpty(c.speciesId) ? "경희몬" : CreatureLibrary.NameOf(c.speciesId);

    static Texture ThumbOf(string species) => MonsterThumbnails.Get(species);

    RawImage Thumb(Transform cell, string species, bool known, float size, Vector2 pos)
    {
        var raw = new GameObject("Thumb", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        raw.transform.SetParent(cell, false);
        raw.raycastTarget = false;
        raw.texture = ThumbOf(species);
        // 못 만난 동물은 실루엣(그림자)만 보인다
        raw.color = known ? Color.white : new Color(0.12f, 0.17f, 0.19f, 0.8f);
        RectTransform rt = raw.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        return raw;
    }

    void Show(GameObject screen, bool open)
    {
        screen.SetActive(open);
        UIInputBlocker.SetModal(screen, open);
        if (open)
            screen.transform.SetAsLastSibling();
    }

    static void SetActive(GameObject go, bool on)
    {
        go.SetActive(on);
        if (on)
            go.transform.SetAsLastSibling();
    }

    /// <summary>화면을 어둡게 덮고 가운데에 흰 둥근 카드를 띄운다.</summary>
    static GameObject Overlay(Transform parent, string name, Vector2 size, out RectTransform card)
    {
        Image dim = UIKit.Image(parent, name, KucaTheme.Scrim, raycast: true);
        UIKit.Stretch(dim.rectTransform);
        Image c = UIKit.Image(dim.transform, "Card", KucaTheme.Card);
        c.sprite = HudIcons.Rounded;
        c.type = Image.Type.Sliced;
        c.pixelsPerUnitMultiplier = 0.34f;
        c.raycastTarget = true;
        Place(c.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        c.rectTransform.sizeDelta = size;
        card = c.rectTransform;
        dim.gameObject.SetActive(false);
        return dim.gameObject;
    }

    /// <summary>카드 아래에서 bottom 만큼 올라온 곳, 높이 height. 가로는 x0~x1 비율 (바깥쪽 여백 48)</summary>
    static void Dock(RectTransform rt, float bottom, float height, float x0 = 0f, float x1 = 1f)
    {
        rt.anchorMin = new Vector2(x0, 0f);
        rt.anchorMax = new Vector2(x1, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(x0 <= 0f ? 48f : 8f, bottom);
        rt.offsetMax = new Vector2(x1 >= 1f ? -48f : -8f, bottom + height);
    }

    static void Corners(RectTransform rt, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
    }

    /// <summary>누를 수 있는 흰 둥근 카드 (격자 칸 · 허브 카드)</summary>
    static Button CardButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        Button b = InvisibleButton(parent, name, onClick, Vector2.zero);
        Image img = b.GetComponent<Image>();
        img.color = KucaTheme.Card;
        img.sprite = HudIcons.Rounded;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.34f;
        return b;
    }

    /// <summary>세그먼트 탭 (토스 · iOS): 회색 홈 안에서 고른 칸만 흰색. 앞의 shown 개만 보인다. counts 가 있으면 이름 아래 숫자</summary>
    static void Segmented(Transform parent, string[] names, int shown, Action<int> onSelect, Text[] labels, Text[] counts, Image[] segs)
    {
        Image track = UIKit.Image(parent, "Segments", KucaTheme.Line);
        track.sprite = HudIcons.Rounded;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 0.45f;
        UIKit.Size(track, counts != null ? 156f : 124f);
        HorizontalLayoutGroup h = UIKit.Horizontal(track, 8f);
        h.padding = new RectOffset(8, 8, 8, 8);
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            Image seg = UIKit.Image(track.transform, names[i], new Color(1f, 1f, 1f, 0f), raycast: true);
            seg.sprite = HudIcons.Rounded;
            seg.type = Image.Type.Sliced;
            seg.pixelsPerUnitMultiplier = 0.5f;
            seg.gameObject.SetActive(i < shown);
            var b = seg.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => onSelect(idx));
            segs[i] = seg;
            labels[i] = T(seg.transform, names[i], KucaTheme.Label, TextAnchor.MiddleCenter, KucaTheme.InkMuted);
            if (counts != null)
            {
                Corners(labels[i].rectTransform, new Vector2(0f, 0.42f), Vector2.one, Vector2.zero, Vector2.zero);
                counts[i] = T(seg.transform, "", KucaTheme.Caption, TextAnchor.UpperCenter, KucaTheme.InkSoft);
                Corners(counts[i].rectTransform, Vector2.zero, new Vector2(1f, 0.42f), Vector2.zero, Vector2.zero);
            }
            else
                UIKit.Stretch(labels[i].rectTransform);
        }
    }

    static void PaintSegments(int on, Text[] labels, Text[] counts, Image[] segs)
    {
        for (int i = 0; i < segs.Length; i++)
        {
            if (segs[i] == null)
                continue;
            bool sel = i == on;
            segs[i].color = sel ? KucaTheme.Card : new Color(1f, 1f, 1f, 0f);
            labels[i].color = sel ? KucaTheme.Ink : KucaTheme.InkMuted;
            if (sel) UIKit.Bold(labels[i]);
            else labels[i].font = UIKit.Font;
            if (counts != null && counts[i] != null)
                counts[i].color = sel ? KucaTheme.PrimaryDeep : KucaTheme.InkSoft;
        }
    }

    static Button PillButton(Transform parent, string label, Color bg, Color fg, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, bg, 40);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.1f;
        Text t = b.GetComponentInChildren<Text>(true);
        t.color = fg;
        UIKit.Bold(t);
        b.targetGraphic = img;
        return b;
    }

    static Button InvisibleButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick, Vector2 size)
    {
        Image hit = UIKit.Image(parent, name, new Color(0f, 0f, 0f, 0f), raycast: true);
        if (size != Vector2.zero)
            hit.rectTransform.sizeDelta = size;
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

    /// <summary>"체육대학관 아기 사자"처럼 긴 이름은 한 줄에 들어가도록 글자를 줄인다.</summary>
    static void FitOneLine(Text t, int maxSize)
    {
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = KucaTheme.Caption - 8;
        t.resizeTextMaxSize = maxSize;
        t.rectTransform.offsetMin = new Vector2(10f, t.rectTransform.offsetMin.y);
        t.rectTransform.offsetMax = new Vector2(-10f, t.rectTransform.offsetMax.y);
    }

    static Text T(Transform parent, string text, int size, TextAnchor anchor, Color color, bool bold = false)
    {
        Text t = UIKit.Text(parent, text, size, anchor, color);
        if (bold)
            UIKit.Bold(t);
        return t;
    }

    static RectTransform Section(RectTransform content, string name, float height)
    {
        RectTransform s = UIKit.Rect(content, name);
        UIKit.Size(s, height);
        return s;
    }

    /// <summary>세로로 스크롤되는 영역을 만들고 내용을 넣을 content 를 돌려준다 (레이아웃은 호출한 쪽에서).</summary>
    static RectTransform ScrollContent(RectTransform area, out ScrollRect scroll)
    {
        scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 30f;
        RectTransform viewport = UIKit.Rect(area, "Viewport");
        UIKit.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var hit = viewport.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        RectTransform content = UIKit.Rect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    /// <summary>위에서 top 만큼 내려온 곳에 높이 height 인 가로로 꽉 찬 칸</summary>
    static void TopBox(RectTransform rt, float top, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(0f, -top - height);
        rt.offsetMax = new Vector2(0f, -top);
    }

    /// <summary>아래에서 bottom 만큼 올라온 곳에 높이 height 인 가로로 꽉 찬 칸</summary>
    static void BottomBox(RectTransform rt, float bottom, float height)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, bottom);
        rt.offsetMax = new Vector2(0f, bottom + height);
    }

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

/// <summary>
/// 프로필의 캐릭터 영역: 좌우로 끌면 캐릭터를 돌리고, 위아래로 끌면 바깥 스크롤로 넘긴다.
/// </summary>
public class ProfileDragArea : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ScrollRect scroll;
    public Action<float> onDragX;
    bool vertical;

    public void OnBeginDrag(PointerEventData e)
    {
        vertical = Mathf.Abs(e.delta.y) > Mathf.Abs(e.delta.x);
        if (vertical && scroll != null)
            scroll.OnBeginDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (vertical && scroll != null)
            scroll.OnDrag(e);
        else
            onDragX?.Invoke(e.delta.x);
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (vertical && scroll != null)
            scroll.OnEndDrag(e);
    }
}
