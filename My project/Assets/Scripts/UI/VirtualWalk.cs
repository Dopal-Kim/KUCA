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
    public const float WalkSpeed = 1.4f, FastSpeed = 6f;   // m/s
    const string GateBuildingId = "way-473963422";         // 정문
    const float OfferAfter = 8f;                            // 위치를 이만큼 못 받으면 권한다

    static readonly Color Ink = new Color(0.11f, 0.11f, 0.12f);
    static readonly Color Accent = new Color(0.16f, 0.42f, 0.95f);

    CampusMap map;
    GameObject offer, controls;
    Text offerText, fastLabel;
    RectTransform knob, pad;
    Vector2 stick;
    bool fast, offered;
    float startedAt, checkAt;

    public static VirtualWalk Instance { get; private set; }

    /// <summary>체험 계정으로 로그인했는지</summary>
    public static bool Allowed => AuthService.Current != null && AuthService.Current.isGuest;

    public bool On => map != null && map.VirtualWalk;

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
        fast = false;
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
        map.MoveVirtual(dir, (fast ? FastSpeed : WalkSpeed) * Mathf.Clamp01(stick.magnitude) * Time.unscaledDeltaTime);
    }

    void RefreshFast() => fastLabel.text = fast ? "빠르게 ×4" : "걷기";

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

        Image tag = UIKit.Image(controls.transform, "Tag", new Color(0.96f, 0.52f, 0.16f, 0.95f), raycast: true);
        tag.sprite = HudIcons.Pill;
        tag.type = Image.Type.Sliced;
        tag.pixelsPerUnitMultiplier = 120f / 70f;
        Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(520f, 70f)); // HUD 닉네임 · 티켓 게이지 아래
        Text tt = UIKit.Text(tag.transform, "가상 산책 중 · 체험 계정", 28, TextAnchor.MiddleCenter, Color.white);
        tt.fontStyle = FontStyle.Bold;
        UIKit.Stretch(tt.rectTransform, 20f, 0f);
        UIInputBlocker.Register(tag.rectTransform);

        Image padImg = UIKit.Image(controls.transform, "Pad", new Color(1f, 1f, 1f, 0.28f), raycast: true);
        padImg.sprite = HudIcons.Circle;
        pad = padImg.rectTransform;
        // 화면 아래 가운데, 경희대 로고 버튼 바로 위 (양손 어느 쪽으로도 조작하기 쉽게)
        Place(pad, new Vector2(0.5f, 0f), new Vector2(0f, 470f), new Vector2(260f, 260f));
        pad.pivot = new Vector2(0.5f, 0.5f);
        Image ring = UIKit.Image(pad, "Ring", new Color(1f, 1f, 1f, 0.7f));
        ring.sprite = HudIcons.GlowRing;
        UIKit.Stretch(ring.rectTransform);
        Image k = UIKit.Image(pad, "Knob", Color.white);
        k.sprite = HudIcons.Circle;
        knob = k.rectTransform;
        Place(knob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
        Image foot = UIKit.Image(knob, "Icon", Accent);
        foot.sprite = HudIcons.Footprints;
        Place(foot.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
        padImg.gameObject.AddComponent<Pad>().owner = this;
        UIInputBlocker.Register(pad);

        Button speed = UIKit.Button(controls.transform, "", () => { fast = !fast; RefreshFast(); }, new Color(1f, 1f, 1f, 0.9f), 28);
        Image si = speed.GetComponent<Image>();
        si.sprite = HudIcons.Pill;
        si.type = Image.Type.Sliced;
        si.pixelsPerUnitMultiplier = 120f / 70f;
        fastLabel = speed.GetComponentInChildren<Text>();
        fastLabel.color = Ink;
        fastLabel.fontStyle = FontStyle.Bold;
        Place((RectTransform)speed.transform, new Vector2(0.5f, 0f), new Vector2(300f, 470f), new Vector2(220f, 70f));   // 조이스틱 오른쪽
        ((RectTransform)speed.transform).pivot = new Vector2(0.5f, 0.5f);
        UIInputBlocker.Register((RectTransform)speed.transform);
        controls.SetActive(false);

        // 권하는 카드
        Image dim = UIKit.Image(canvas.transform, "Offer", new Color(0f, 0f, 0f, 0.45f), raycast: true);
        UIKit.Stretch(dim.rectTransform);
        offer = dim.gameObject;
        Image card = UIKit.Image(dim.transform, "Card", Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 700f));
        Text title = UIKit.Text(card.transform, "가상 산책으로 둘러볼까요?", 46, TextAnchor.MiddleCenter, Ink);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(820f, 70f));
        offerText = UIKit.Text(card.transform, "", 32, TextAnchor.UpperCenter, new Color(Ink.r, Ink.g, Ink.b, 0.65f));
        Place(offerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(800f, 230f));
        Button go = Pill(card.transform, "가상 산책 시작", Accent, Color.white, Begin);
        Place((RectTransform)go.transform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(760f, 116f));
        Button later = Pill(card.transform, "나중에 (설정에서 켤 수 있어요)", new Color(0.94f, 0.95f, 0.96f), Ink, CloseOffer);
        Place((RectTransform)later.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(760f, 104f));
        offer.SetActive(false);
    }

    static Button Pill(Transform parent, string label, Color face, Color ink, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, face, 36);
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

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
