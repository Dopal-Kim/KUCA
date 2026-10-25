using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 경희스팟 표지 하나: 지붕 위에 선 얼굴 핀. 둥근 테 안에 그 건물의 경희몬 얼굴, 아래에 건물 이름 리본.
/// (예전 기둥 + 도는 원판은 포켓스탑과 너무 닮아 바꿈 — 10/25 시안 C 선택. 경희몬이 없는 건물은 학교 배지)
/// - 멀리 있을 때: 흰 테 · 검은 리본, 카메라를 향한 채 천천히 떠다닌다. 뒤에 은은한 후광.
/// - 가까이(InRange) 있을 때: 살짝 튀어 오르며 커지고, 테는 금색 · 리본은 초록, 후광이 숨 쉬듯 밝아진다 → 메모를 남길 수 있다는 신호.
/// 화면 공간이 아니라 월드 공간 캔버스라 건물 뒤에선 가려진다. 탭하면 CollectController 가 SpotTapped 로 알려 준다.
/// </summary>
public class KyungHeeSpot : MonoBehaviour
{
    public CampusBuildingInfo Building { get; private set; }
    public Bounds BuildingBounds { get; private set; }
    public bool InRange { get; private set; }

    Transform badge, halo;        // badge = 핀 캔버스 (아래 끝 = 지붕)
    Image ring, ribbon;
    RawImage face;
    string species;
    Renderer haloRenderer;
    MaterialPropertyBlock haloProps;
    float phase;
    float near, nearVel;          // 0 = 멀리, 1 = 가까이 (부드럽게)

    const float PoleHeight = 13f;   // 지붕에서 얼굴 아래까지 (m). 18 · 26 은 가까운 지도 시점에서 건물보다 커서 화면 아래를 가렸음 (UI_REVIEW_v5)
    const float BadgeSize = 17f;    // 얼굴 원 지름 (m)
    const float FacePx = 220f;      // 캔버스 안 얼굴 원 지름 (px): 글자 크기를 화면 부품과 같은 단위로 쓰려고
    const float StemPx = FacePx * PoleHeight / BadgeSize;

    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    static Mesh haloMesh;

    public static KyungHeeSpot Create(CampusBuildingInfo building, Bounds bounds, Material badgeMat, Material poleMat, Transform parent,
                                      Material glowMat = null)
    {
        // 지붕 가운데 위에 세운다
        Vector3 roof = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        var root = new GameObject($"KyungHeeSpot_{building.DisplayName}");
        root.transform.SetParent(parent, false);
        root.transform.position = roof;

        var spot = root.AddComponent<KyungHeeSpot>();
        spot.Building = building;
        spot.BuildingBounds = bounds;
        spot.phase = Random.value * Mathf.PI * 2f;
        spot.species = SpeciesOf(building, bounds);

        // 핀 캔버스: 아래 가운데가 지붕. 막대 → 얼굴 원(테 + 얼굴) → 이름 리본
        var go = new GameObject("Pin", typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(root.transform, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var pin = (RectTransform)go.transform;
        pin.pivot = new Vector2(0.5f, 0f);
        pin.sizeDelta = new Vector2(FacePx * 4f, StemPx + FacePx + 40f);
        spot.badge = pin;

        Place(UIKit.Image(pin, "Stem", KucaTheme.Card).rectTransform, StemPx / 2f, new Vector2(10f, StemPx));
        float faceY = StemPx + FacePx / 2f;
        spot.ring = UIKit.Image(pin, "Ring", KucaTheme.Card);
        spot.ring.sprite = HudIcons.Circle;
        Place(spot.ring.rectTransform, faceY, Vector2.one * (FacePx + 20f));
        Image disc = UIKit.Image(pin, "Face", KucaTheme.PrimarySoft);
        disc.sprite = HudIcons.Circle;
        disc.gameObject.AddComponent<Mask>();
        Place(disc.rectTransform, faceY, Vector2.one * FacePx);
        spot.face = new GameObject("Image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        spot.face.transform.SetParent(disc.transform, false);
        spot.face.raycastTarget = false;
        UIKit.Stretch(spot.face.rectTransform);
        spot.face.texture = badgeMat != null ? badgeMat.mainTexture : null;   // 경희몬이 없거나 아직 못 그렸으면 학교 배지

        spot.ribbon = UIKit.Image(pin, "Ribbon", KucaTheme.Ink);
        spot.ribbon.sprite = HudIcons.Pill;
        spot.ribbon.type = Image.Type.Sliced;
        Text name = UIKit.Bold(UIKit.Text(spot.ribbon.transform, building.DisplayName, KucaTheme.Label, TextAnchor.MiddleCenter, Color.white));
        UIKit.Stretch(name.rectTransform);
        Place(spot.ribbon.rectTransform, StemPx + 14f, new Vector2(name.preferredWidth + 2f * KucaTheme.Space4, 72f));

        if (glowMat != null)
        {
            var h = new GameObject("Halo");
            h.transform.SetParent(root.transform, false);
            h.AddComponent<MeshFilter>().sharedMesh = HaloMesh();
            spot.haloRenderer = h.AddComponent<MeshRenderer>();
            spot.haloRenderer.sharedMaterial = glowMat;
            spot.haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spot.haloRenderer.receiveShadows = false;
            spot.haloProps = new MaterialPropertyBlock();
            spot.halo = h.transform;
        }

        // 손가락으로 누르기 쉽게 얼굴보다 큰 충돌체 (트리거라 물리에는 영향 없음)
        var hit = root.AddComponent<SphereCollider>();
        hit.isTrigger = true;
        hit.center = new Vector3(0f, PoleHeight + BadgeSize / 2f, 0f);
        hit.radius = BadgeSize * 0.8f;
        return spot;
    }

    static void Place(RectTransform r, float y, Vector2 size)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = new Vector2(0f, y);
        r.sizeDelta = size;
    }

    /// <summary>그 건물의 경희몬: 만남 스팟 표 → 없으면 중심이 건물 안에 있는 황금 구역. 둘 다 없으면 null (학교 배지)</summary>
    static string SpeciesOf(CampusBuildingInfo building, Bounds bounds)
    {
        string s = MeetSpotCatalog.SpeciesAt(building.buildingId);
        if (s != null)
            return s;
        foreach (CreatureLibrary.GoldZone z in CreatureLibrary.GoldZones)
            if (bounds.Contains(new Vector3(z.center.x, bounds.center.y, z.center.y)))
                return z.species;
        return null;
    }

    void Start()
    {
        // 피규어 메시가 준비된 뒤(첫 프레임)에 얼굴을 한 번 찍는다
        Texture2D t = MonsterThumbnails.Get(species);
        if (t == null)
            return;
        face.texture = t;
        face.uvRect = new Rect(0.2f, 0.38f, 0.6f, 0.6f);   // shortcut: 썸네일(전신 + 받침) 위쪽을 얼굴로 자름, 피규어마다 머리 위치가 다르면 종별 값으로
    }

    /// <summary>Player 위치에서 건물 외곽까지의 수평 거리 (m). 건물 안이면 0.</summary>
    public float DistanceFrom(Vector3 p)
    {
        Bounds b = BuildingBounds;
        float dx = Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x);
        float dz = Mathf.Max(b.min.z - p.z, 0f, p.z - b.max.z);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    public void SetInRange(bool value) => InRange = value;

    void LateUpdate()
    {
        if (badge == null)
            return;
        near = Mathf.SmoothDamp(near, InRange ? 1f : 0f, ref nearVel, 0.35f);
        float t = Time.time + phase;

        // 떠다니기 (두 물결을 섞어 기계적이지 않게) + 가까우면 조금 더 위로
        float bob = Mathf.Sin(t * 1.3f) * 1.0f + Mathf.Sin(t * 0.71f) * 0.45f;
        // 카메라가 가까우면 핀을 줄인다 (캠퍼스 전체 보기에선 그대로, 플레이어 가까이선 건물 · 산책 시작 버튼을 가리지 않게)
        // 지도 홈 기본 카메라는 표지판에서 약 430 m: 기준 320 이면 배율이 늘 1 이라 줄지 않았음 (UI_REVIEW_v8) → 900 이면 약 0.5 배
        Camera cam = Camera.main;
        float size = BadgeSize * (cam != null ? Mathf.Clamp(Vector3.Distance(cam.transform.position, transform.position) / 900f, 0.4f, 1f) : 1f);
        badge.localPosition = new Vector3(0f, bob + near * 3f, 0f);
        badge.localScale = Vector3.one * (size / FacePx) * (1f + 0.28f * EaseOutBack(near));
        if (cam != null)
            badge.rotation = cam.transform.rotation;   // 늘 화면을 향한 그림 (이름이 어느 방향에서도 읽힘)
        ring.color = Color.Lerp(KucaTheme.Card, KucaTheme.Gold, near);
        ribbon.color = Color.Lerp(KucaTheme.Ink, KucaTheme.Primary, near);

        if (halo != null)
        {
            halo.position = badge.TransformPoint(0f, StemPx + FacePx / 2f, 0f)
                            + (cam != null ? cam.transform.forward : Vector3.forward) * (size * 0.3f);   // 얼굴 뒤
            halo.rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            halo.localScale = Vector3.one * size * (1.75f + 0.25f * near + 0.08f * breathe * near);
            haloProps.SetFloat(IntensityId, 0.28f + 0.55f * near * (0.7f + 0.3f * breathe));
            haloRenderer.SetPropertyBlock(haloProps);
        }
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    // ---------- 메시 (한 번 만들어 모든 스팟이 같이 씀) ----------

    static Mesh HaloMesh()
    {
        if (haloMesh != null) return haloMesh;
        haloMesh = new Mesh { name = "KyungHeeSpotHalo" };
        haloMesh.SetVertices(new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) });
        haloMesh.SetUVs(0, new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
        haloMesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
        haloMesh.RecalculateBounds();
        return haloMesh;
    }
}
