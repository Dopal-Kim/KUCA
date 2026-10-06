using UnityEngine;

/// <summary>
/// 경희스팟 표지판 하나 (포켓스탑 같은 것). 건물 지붕 위에 기둥과 학교 배지가 떠 있다.
/// - 멀리 있을 때: 배지가 카메라를 바라보며 천천히 오르내린다.
/// - 가까이(InRange) 있을 때: 배지가 커지고 빙글빙글 돈다 → 메모를 남길 수 있다는 신호.
/// 탭하면 CollectController 가 SpotTapped 로 알려 준다.
/// </summary>
public class KyungHeeSpot : MonoBehaviour
{
    public CampusBuildingInfo Building { get; private set; }
    public Bounds BuildingBounds { get; private set; }
    public bool InRange { get; private set; }

    Transform badge;
    float phase;
    float near;      // 0 = 멀리, 1 = 가까이 (부드럽게 변함)
    float spin;

    const float PoleHeight = 18f;
    const float BadgeSize = 26f;

    public static KyungHeeSpot Create(CampusBuildingInfo building, Bounds bounds, Material badgeMat, Material poleMat, Transform parent)
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

        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        Object.Destroy(pole.GetComponent<Collider>());
        pole.transform.SetParent(root.transform, false);
        pole.transform.localPosition = new Vector3(0f, PoleHeight / 2f, 0f);
        pole.transform.localScale = new Vector3(1.6f, PoleHeight / 2f, 1.6f);
        pole.GetComponent<Renderer>().sharedMaterial = poleMat;

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
        disc.name = "Badge";
        Object.Destroy(disc.GetComponent<Collider>());
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0f, PoleHeight + BadgeSize / 2f, 0f);
        disc.transform.localScale = Vector3.one * BadgeSize;
        var r = disc.GetComponent<Renderer>();
        r.sharedMaterial = badgeMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spot.badge = disc.transform;

        // 손가락으로 누르기 쉽게 배지보다 큰 충돌체 (트리거라 물리에는 영향 없음)
        var hit = root.AddComponent<SphereCollider>();
        hit.isTrigger = true;
        hit.center = disc.transform.localPosition;
        hit.radius = BadgeSize * 0.8f;
        return spot;
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
        near = Mathf.MoveTowards(near, InRange ? 1f : 0f, Time.deltaTime * 3f);

        float t = Time.time + phase;
        badge.localPosition = new Vector3(0f, PoleHeight + BadgeSize / 2f + Mathf.Sin(t * 1.6f) * 1.2f, 0f);
        badge.localScale = Vector3.one * BadgeSize * (1f + 0.3f * near);

        // 멀리서는 카메라 쪽을 보고, 가까우면 돈다
        Camera cam = Camera.main;
        float faceYaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        spin = InRange ? spin + Time.deltaTime * 180f : Mathf.MoveTowardsAngle(spin, 0f, Time.deltaTime * 360f);
        badge.rotation = Quaternion.Euler(0f, faceYaw + spin, 0f);
    }
}
