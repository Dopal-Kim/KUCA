using UnityEngine;

/// <summary>
/// 경희스팟 표지판 하나 (포켓스탑 같은 것). 건물 지붕 위에 매끈한 기둥과 학교 배지(두께 있는 둥근 메달)가 떠 있다.
/// - 멀리 있을 때: 배지가 카메라 쪽으로 부드럽게 돌아보며 천천히 떠다닌다. 뒤에 은은한 후광.
/// - 가까이(InRange) 있을 때: 살짝 튀어 오르며 커지고, 천천히 돌고, 후광이 숨 쉬듯 밝아진다 → 메모를 남길 수 있다는 신호.
/// 메달·기둥은 모두 배지 텍스처(KyungHeeSpotBadge.png) 하나로 칠한다: 앞면 = 로고, 테·고리 = 금색 띠, 뒷면·기둥 = 남색 띠.
/// 탭하면 CollectController 가 SpotTapped 로 알려 준다.
/// </summary>
public class KyungHeeSpot : MonoBehaviour
{
    public CampusBuildingInfo Building { get; private set; }
    public Bounds BuildingBounds { get; private set; }
    public bool InRange { get; private set; }

    Transform badge, halo;
    Renderer haloRenderer;
    MaterialPropertyBlock haloProps;
    float phase;
    float near, nearVel;          // 0 = 멀리, 1 = 가까이 (부드럽게)
    float spinAngle, spinSpeed, spinVel;
    float yaw, yawVel;

    const float PoleHeight = 18f;
    const float BadgeSize = 26f;
    const int Segments = 64;

    // 배지 텍스처에서 색을 뽑을 자리 (512 px 원: 금 246~254, 남색 208~246, 금 202~208, 흰 0~202)
    static readonly Vector2 GoldUV = new Vector2(0.5f, 0.5f + 0.492f);
    static readonly Vector2 NavyUV = new Vector2(0.5f, 0.5f + 0.44f);
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    static Mesh badgeMesh, poleMesh, haloMesh;

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

        var pole = new GameObject("Pole");
        pole.transform.SetParent(root.transform, false);
        pole.AddComponent<MeshFilter>().sharedMesh = PoleMesh();
        var pr = pole.AddComponent<MeshRenderer>();
        pr.sharedMaterial = badgeMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        var disc = new GameObject("Badge");
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0f, PoleHeight + BadgeSize / 2f, 0f);
        disc.transform.localScale = Vector3.one * BadgeSize;
        disc.AddComponent<MeshFilter>().sharedMesh = BadgeMesh();
        var r = disc.AddComponent<MeshRenderer>();
        r.sharedMaterial = badgeMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        spot.badge = disc.transform;

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
        float dt = Time.deltaTime;
        near = Mathf.SmoothDamp(near, InRange ? 1f : 0f, ref nearVel, 0.35f);
        float t = Time.time + phase;

        // 떠다니기 (두 물결을 섞어 기계적이지 않게) + 가까우면 조금 더 위로
        float bob = Mathf.Sin(t * 1.3f) * 1.0f + Mathf.Sin(t * 0.71f) * 0.45f;
        Vector3 center = new Vector3(0f, PoleHeight + BadgeSize / 2f + bob + near * 3f, 0f);
        badge.localPosition = center;
        badge.localScale = Vector3.one * BadgeSize * (1f + 0.28f * EaseOutBack(near));

        // 카메라 쪽으로 부드럽게 돌아보고, 가까우면 천천히 회전 (멀어지면 앞면으로 부드럽게 돌아옴)
        Camera cam = Camera.main;
        float camYaw = cam != null ? cam.transform.eulerAngles.y : 0f;
        float camPitch = cam != null ? cam.transform.eulerAngles.x : 0f;
        if (camPitch > 180f) camPitch -= 360f;
        spinSpeed = Mathf.Lerp(spinSpeed, InRange ? 90f : 0f, dt * 2f);
        if (InRange) spinAngle = Mathf.Repeat(spinAngle + spinSpeed * dt, 360f);
        else spinAngle = Mathf.SmoothDampAngle(spinAngle, 0f, ref spinVel, 0.6f);
        yaw = Mathf.SmoothDampAngle(yaw, camYaw + spinAngle, ref yawVel, 0.2f);
        badge.rotation = Quaternion.Euler(camPitch * 0.3f, yaw, 0f);

        if (halo != null)
        {
            halo.localPosition = center;
            halo.rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
            halo.position += (cam != null ? cam.transform.forward : Vector3.forward) * (BadgeSize * 0.3f);   // 배지 뒤
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            halo.localScale = Vector3.one * BadgeSize * (1.75f + 0.25f * near + 0.08f * breathe * near);
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

    /// <summary>지름 1, 두께 0.14 의 둥근 메달. 앞면(-Z, 카메라 쪽)은 로고, 앞 테두리는 비스듬히 깎고, 옆은 금색, 뒷면은 남색.</summary>
    static Mesh BadgeMesh()
    {
        if (badgeMesh != null) return badgeMesh;
        const float R = 0.5f, Bevel = 0.035f, T = 0.14f;
        var v = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var tri = new System.Collections.Generic.List<int>();

        void Ring(float radius, float z, System.Func<float, float, Vector2> uvOf)
        {
            for (int i = 0; i <= Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                float x = Mathf.Cos(a) * radius, y = Mathf.Sin(a) * radius;
                v.Add(new Vector3(x, y, z));
                uv.Add(uvOf(x, y));
            }
        }
        void Strip(int r0, int r1)
        {
            for (int i = 0; i < Segments; i++)
            {
                tri.Add(r0 + i); tri.Add(r1 + i); tri.Add(r1 + i + 1);
                tri.Add(r0 + i); tri.Add(r1 + i + 1); tri.Add(r0 + i + 1);
            }
        }

        // 앞면: 가운데 점 + 로고가 그대로 보이는 원판 (배지 텍스처 UV = 위치)
        int c0 = v.Count;
        v.Add(new Vector3(0f, 0f, -T / 2f)); uv.Add(new Vector2(0.5f, 0.5f));
        int front = v.Count;
        Ring(R - Bevel, -T / 2f, (x, y) => new Vector2(0.5f + x, 0.5f + y));
        for (int i = 0; i < Segments; i++) { tri.Add(c0); tri.Add(front + i + 1); tri.Add(front + i); }
        // 앞 테두리 비스듬한 면 (금색 띠)
        int bevelIn = v.Count;
        Ring(R - Bevel, -T / 2f, (x, y) => GoldUV);
        int bevelOut = v.Count;
        Ring(R, -T / 2f + Bevel, (x, y) => GoldUV);
        Strip(bevelIn, bevelOut);
        // 옆면 (금색)
        int sideA = v.Count;
        Ring(R, -T / 2f + Bevel, (x, y) => GoldUV);
        int sideB = v.Count;
        Ring(R, T / 2f, (x, y) => GoldUV);
        Strip(sideA, sideB);
        // 뒷면 (남색)
        int c1 = v.Count;
        v.Add(new Vector3(0f, 0f, T / 2f)); uv.Add(NavyUV);
        int back = v.Count;
        Ring(R, T / 2f, (x, y) => NavyUV);
        for (int i = 0; i < Segments; i++) { tri.Add(c1); tri.Add(back + i); tri.Add(back + i + 1); }

        badgeMesh = new Mesh { name = "KyungHeeSpotBadge" };
        badgeMesh.SetVertices(v);
        badgeMesh.SetUVs(0, uv);
        badgeMesh.SetTriangles(tri, 0);
        badgeMesh.RecalculateNormals();
        badgeMesh.RecalculateBounds();
        return badgeMesh;
    }

    /// <summary>지붕 위 받침(금) + 위로 가늘어지는 기둥(남색) + 배지 아래 금색 고리</summary>
    static Mesh PoleMesh()
    {
        if (poleMesh != null) return poleMesh;
        const int S = 20;
        var v = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>();
        var tri = new System.Collections.Generic.List<int>();

        void Tube(float y0, float y1, float r0, float r1, Vector2 col, bool cap)
        {
            int b = v.Count;
            for (int i = 0; i <= S; i++)
            {
                float a = i * Mathf.PI * 2f / S;
                v.Add(new Vector3(Mathf.Cos(a) * r0, y0, Mathf.Sin(a) * r0)); uv.Add(col);
                v.Add(new Vector3(Mathf.Cos(a) * r1, y1, Mathf.Sin(a) * r1)); uv.Add(col);
            }
            for (int i = 0; i < S; i++)
            {
                int k = b + i * 2;
                tri.Add(k); tri.Add(k + 1); tri.Add(k + 3);
                tri.Add(k); tri.Add(k + 3); tri.Add(k + 2);
            }
            if (!cap) return;
            int c = v.Count;
            v.Add(new Vector3(0f, y1, 0f)); uv.Add(col);
            for (int i = 0; i < S; i++)
            {
                tri.Add(c); tri.Add(b + (i + 1) * 2 + 1); tri.Add(b + i * 2 + 1);
            }
        }

        Tube(0f, 0.5f, 3.2f, 3.0f, GoldUV, true);               // 받침
        Tube(0.5f, 1.2f, 1.6f, 1.2f, NavyUV, false);            // 받침 목
        Tube(1.2f, PoleHeight - 1.5f, 0.95f, 0.6f, NavyUV, false); // 기둥
        Tube(PoleHeight - 1.5f, PoleHeight - 0.9f, 1.3f, 1.3f, GoldUV, true);   // 금색 고리
        Tube(PoleHeight - 0.9f, PoleHeight + 1.5f, 0.6f, 0.45f, NavyUV, true);  // 배지 잡는 끝

        poleMesh = new Mesh { name = "KyungHeeSpotPole" };
        poleMesh.SetVertices(v);
        poleMesh.SetUVs(0, uv);
        poleMesh.SetTriangles(tri, 0);
        poleMesh.RecalculateNormals();
        poleMesh.RecalculateBounds();
        return poleMesh;
    }

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
