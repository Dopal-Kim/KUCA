using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 가상 산책 (체험 계정 전용, 공모전 심사 설명용).
/// 캠퍼스 밖에 있거나 위치를 받을 수 없으면 걷기 · 티켓 · 경희스팟 · 코스 완료를 체험할 수 없다.
/// 체험 계정이면 "가상 산책" 을 권하고, 켜면 정문에서 시작해 화면 조이스틱으로 캠퍼스를 걷는다.
/// 거리 · 티켓 · 스팟 방문 · 코스 완료는 실제로 걸을 때와 똑같이 센다 (CampusMap.VirtualWalk).
/// 일반 계정(카카오 등)에는 보이지 않고, 체험 계정에서 나가면 꺼진다. 설정에서도 켜고 끌 수 있다 (체험 계정만).
/// </summary>
public class VirtualWalk : MonoBehaviour
{
    public const float WalkSpeed = 1.4f;   // m/s, 걷기
    /// <summary>속도 단계 (버튼을 누를 때마다 다음 단계): 걷기 · 빠르게 ×4 · 아주 빠르게 ×16 (심사위원이 캠퍼스를 빨리 둘러보게)</summary>
    static readonly (float multiplier, string label)[] Speeds = { (1f, "걷기"), (4f, "빠르게 ×4"), (16f, "아주 빠르게 ×16") };
    /// <summary>가장 빠른 단계 (m/s). 걸은 거리 집계가 이 속도까지 인정한다 (KhuMenus.TrackActivity)</summary>
    public static float TopSpeed => WalkSpeed * Speeds[Speeds.Length - 1].multiplier;
    const string GateBuildingId = "way-473963422";         // 정문
    const float OfferAfter = 8f;                            // 위치를 이만큼 못 받으면 권한다

    static readonly Color Ink = KucaTheme.Ink;

    CampusMap map;
    GameObject offer, controls;
    Text offerText, fastLabel;
    RectTransform knob, pad;
    Vector2 stick;
    int speedLevel;
    bool offered;
    float startedAt, checkAt;

    public static VirtualWalk Instance { get; private set; }

    /// <summary>체험 계정으로 로그인했는지</summary>
    public static bool Allowed => AuthService.Current != null && AuthService.Current.isGuest;

    public bool On => map != null && map.VirtualWalk;

    /// <summary>
    /// 걷는 중 판 · 자유 산책 판을 놓는다. 보통은 HUD 아래 탭 막대 바로 위 ("산책 시작" 버튼 자리, 산책 중엔 그 버튼이 숨는다).
    /// 가상 산책 중이면 조이스틱과 가운데 캐릭터를 가리지 않게 위 상태 카드 · 티켓 칩 아래로 (오른쪽 버튼 줄은 비켜서).
    /// 판의 캔버스는 안전 영역이 없어서 노치 · 홈 표시줄 높이를 더한다.
    /// </summary>
    public static void PlacePanel(RectTransform panel, float height)
    {
        Canvas root = panel.GetComponentInParent<Canvas>(true);
        float canvasH = root != null ? ((RectTransform)root.rootCanvas.transform).rect.height : 1920f;
        Rect sa = Screen.safeArea;
        float insetBottom = Screen.height > 0 ? sa.yMin / Screen.height * canvasH : 0f;
        float insetTop = Screen.height > 0 ? (Screen.height - sa.yMax) / Screen.height * canvasH : 0f;
        if (Instance != null && Instance.On)
        {
            const float belowStatus = 330f;   // GameHUD 상태 카드(156) + 티켓 칩(104) + 여백
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(0.5f, 1f);
            panel.offsetMin = new Vector2(KucaTheme.Space4, -insetTop - belowStatus - height);
            panel.offsetMax = new Vector2(-180f, -insetTop - belowStatus);
        }
        else
        {
            panel.anchorMin = new Vector2(0f, 0f);
            panel.anchorMax = new Vector2(1f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.sizeDelta = new Vector2(-2f * KucaTheme.Space4, height);
            panel.anchoredPosition = new Vector2(0f, insetBottom + GameHUD.BottomReserve);
        }
    }

    void Awake()
    {
        Instance = this;
        Build();
        startedAt = Time.unscaledTime;
    }

    void Update()
    {
        if (map == null)
        {
            var collect = FindAnyObjectByType<CollectController>();
            map = collect != null ? collect.campusMap : FindAnyObjectByType<CampusMap>();
            if (map == null) return;
        }
        if (On && !Allowed)
            Stop(); // 체험 계정에서 나갔다
        // 메뉴 · 화면이 열려 있으면 (경희대 로고 메뉴 등) 조이스틱을 숨기고 멈춘다
        bool show = On && !UIInputBlocker.IsModalOpen;
        if (controls.activeSelf && !show)
        {
            stick = Vector2.zero;
            knob.anchoredPosition = Vector2.zero;
        }
        controls.SetActive(show);
        if (On)
            Walk();
        else if (Allowed && !offered && Time.unscaledTime >= checkAt)
        {
            checkAt = Time.unscaledTime + 1f;
            MaybeOffer();
        }
    }

    // ---------- 권하기 ----------

    void MaybeOffer()
    {
        if (UIInputBlocker.IsModalOpen)
            return;
        bool noLocation = !map.HasLocation && Time.unscaledTime - startedAt > OfferAfter;
        bool outside = map.HasLocation && !map.IsInsideCampus(map.CurrentLongitude, map.CurrentLatitude);
        if (!noLocation && !outside)
            return;
        offered = true;
        offerText.text = (outside ? "지금 캠퍼스 밖이에요." : "위치를 받을 수 없어요.")
            + "\n체험 계정은 정문에서부터 가상으로 걸어 볼 수 있어요.\n걸은 거리 · 티켓 · 경희스팟 · 코스 완료가 실제처럼 쌓여요.";
        offer.SetActive(true);
        offer.transform.SetAsLastSibling();
        UIInputBlocker.SetModal(offer, true);
    }

    void CloseOffer()
    {
        offer.SetActive(false);
        UIInputBlocker.SetModal(offer, false);
    }

    // ---------- 켜고 끄기 ----------

    /// <summary>정문 앞에서 가상 산책을 시작한다 (체험 계정만)</summary>
    public void Begin()
    {
        CloseOffer();
        if (!Allowed || map == null)
            return;
        Vector3 start = GatePosition();
        CampusMap.Vector2d geo = map.WorldToGeo(start);
        map.StartVirtualWalk(geo.x, geo.y);
        speedLevel = 0;
        RefreshFast();
        Toast("가상 산책을 시작했어요. 왼쪽 아래 조이스틱으로 걸어요");
    }

    public void Stop()
    {
        if (map != null && map.VirtualWalk)
        {
            map.StopVirtualWalk();
            Toast("가상 산책을 껐어요");
        }
        stick = Vector2.zero;
    }

    Vector3 GatePosition()
    {
        var spots = FindAnyObjectByType<KyungHeeSpots>();
        if (spots != null)
            foreach (KyungHeeSpot s in spots.Spots)
                if (s != null && s.Building != null && s.Building.buildingId == GateBuildingId)
                {
                    // 건물 가운데가 아니라 앞쪽 길 위에서 시작
                    Vector3 c = s.BuildingBounds.center;
                    return new Vector3(c.x, 0f, s.BuildingBounds.min.z - 20f);
                }
        return map.GeoToMapPlane(map.testLongitude, map.testLatitude);
    }

    // ---------- 걷기 ----------

    void Walk()
    {
        if (stick.sqrMagnitude < 0.01f || UIInputBlocker.IsModalOpen)
            return;
        // 조이스틱 위 = 지금 화면의 위쪽 (지도 카메라가 돌아 있어도)
        Camera cam = Camera.main;
        float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(stick.x, 0f, stick.y);
        map.MoveVirtual(dir, WalkSpeed * Speeds[speedLevel].multiplier * Mathf.Clamp01(stick.magnitude) * Time.unscaledDeltaTime);
    }

    void RefreshFast() => fastLabel.text = Speeds[speedLevel].label;

    static void Toast(string message)
    {
        var hud = FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.Toast(message);
    }

    /// <summary>조이스틱 패드: 누른 채로 끌면 그쪽으로 걷는다</summary>
    class Pad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public VirtualWalk owner;
        public void OnPointerDown(PointerEventData e) => OnDrag(e);
        public void OnDrag(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(owner.pad, e.position, e.pressEventCamera, out Vector2 local);
            local -= owner.pad.rect.center;   // 패드 가운데 기준 (기준점이 어디든 상하좌우가 고르게)
            float r = owner.pad.rect.width * 0.5f;
            Vector2 v = Vector2.ClampMagnitude(local / r, 1f);
            owner.stick = v;
            owner.knob.anchoredPosition = v * r * 0.6f;
        }
        public void OnPointerUp(PointerEventData e)
        {
            owner.stick = Vector2.zero;
            owner.knob.anchoredPosition = Vector2.zero;
        }
    }

    // ---------- 화면 ----------

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "VirtualWalkCanvas", 15); // HUD(10) 위, 메뉴 · 메모판 아래
        Rect sa = Screen.safeArea;
        RectTransform safe = UIKit.Rect(canvas.transform, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        // 조이스틱 · 상태 표시
        controls = UIKit.Rect(safe, "Controls").gameObject;
        UIKit.Stretch((RectTransform)controls.transform);

        // 가상 산책 중 표시: 금색 알약 + 진한 글자 (HUD 상태 카드 · 티켓 칩 아래 가운데)
        Image tag = UIKit.Image(controls.transform, "Tag", KucaTheme.Gold, raycast: true);
        tag.sprite = HudIcons.Pill;
        tag.type = Image.Type.Sliced;
        tag.pixelsPerUnitMultiplier = 1.4f;
        Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(600f, 88f));
        Text tt = UIKit.Bold(UIKit.Text(tag.transform, "가상 산책 중 · 체험 계정", KucaTheme.Caption, TextAnchor.MiddleCenter, Ink));
        UIKit.Stretch(tt.rectTransform, 20f, 0f);
        UIInputBlocker.Register(tag.rectTransform);

        // 조이스틱: 아래 가운데, "산책 시작" 버튼 위 (양손 어느 쪽으로도 조작하기 쉽게)
        const float padSize = 280f;
        float padY = GameHUD.BottomReserve + KucaTheme.ButtonHeight + KucaTheme.Space4 + padSize / 2f;
        Image padImg = UIKit.Image(controls.transform, "Pad", new Color(1f, 1f, 1f, 0.55f), raycast: true);
        padImg.sprite = HudIcons.Circle;
        pad = padImg.rectTransform;
        Place(pad, new Vector2(0.5f, 0f), new Vector2(0f, padY), new Vector2(padSize, padSize));
        pad.pivot = new Vector2(0.5f, 0.5f);
        Image ring = UIKit.Image(pad, "Ring", new Color(1f, 1f, 1f, 0.9f));
        ring.sprite = HudIcons.GlowRing;
        UIKit.Stretch(ring.rectTransform);
        Image k = UIKit.Image(pad, "Knob", KucaTheme.Primary);
        k.sprite = HudIcons.Circle;
        knob = k.rectTransform;
        Place(knob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124f, 124f));
        Image foot = UIKit.Image(knob, "Icon", Color.white);
        foot.sprite = HudIcons.Footprints;
        Place(foot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
        padImg.gameObject.AddComponent<Pad>().owner = this;
        UIInputBlocker.Register(pad);

        // 속도 단계: 흰 알약, 조이스틱 바로 위
        Button speed = UIKit.Button(controls.transform, "", () => { speedLevel = (speedLevel + 1) % Speeds.Length; RefreshFast(); }, KucaTheme.Card, KucaTheme.Label);
        Image si = speed.GetComponent<Image>();
        si.sprite = HudIcons.Pill;
        si.pixelsPerUnitMultiplier = 1.4f;
        fastLabel = speed.GetComponentInChildren<Text>();
        fastLabel.color = Ink;
        Place((RectTransform)speed.transform, new Vector2(0.5f, 0f), new Vector2(0f, padY + padSize / 2f + KucaTheme.Space2 + 48f), new Vector2(360f, 96f));
        ((RectTransform)speed.transform).pivot = new Vector2(0.5f, 0.5f);
        UIInputBlocker.Register((RectTransform)speed.transform);
        controls.SetActive(false);

        // 권하는 카드 (토스 확인 시트: 큰 제목 · 설명 · 꽉 찬 주요 버튼 + 회색 보조 버튼)
        Image dim = UIKit.Image(canvas.transform, "Offer", KucaTheme.Scrim, raycast: true);
        UIKit.Stretch(dim.rectTransform);
        offer = dim.gameObject;
        RectTransform card = UIKit.CardBox(dim.transform, "Card", KucaTheme.Space3);
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(1080f - 2f * KucaTheme.Gutter, 0f);
        UIKit.Bold(UIKit.Paragraph(card, "가상 산책으로 둘러볼까요?", KucaTheme.Title - 6, Ink));
        offerText = UIKit.Paragraph(card, "", KucaTheme.Body, KucaTheme.InkMuted);
        UIKit.Size(UIKit.Rect(card, "Gap"), KucaTheme.Space2);
        UIKit.PrimaryButton(card, "가상 산책 시작", Begin);
        UIKit.SecondaryButton(card, "나중에 (설정에서 켤 수 있어요)", CloseOffer);
        offer.SetActive(false);
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
