using System.Collections.Generic;
using UnityEngine;

/// <summary>수집 대상 종류 (이름, 모양, 색, 점수, 등장 가중치)</summary>
[System.Serializable]
public class CollectibleType
{
    public string id = "sprout";
    public string displayName = "새싹 조각";
    public PrimitiveType shape = PrimitiveType.Sphere;
    public Color color = new Color(0.3f, 0.85f, 0.35f);
    public int points = 10;
    [Tooltip("등장 가중치 (클수록 자주 나옴)")]
    public float weight = 60f;
}

/// <summary>
/// 지도 위 수집 대상. 동물 모델이 있는 등급은 받침 위 동물 캐릭터(CreatureLibrary)가 서서 카메라를 보며 통통 뛰고,
/// 없는 등급은 떠서 도는 도형으로 그린다. Player가 수집 반경 안에 들어오면 커지며 강조된다.
/// </summary>
public class Collectible : MonoBehaviour
{
    public CollectibleType Type { get; private set; }
    /// <summary>동물 id (도형이면 null)</summary>
    public string SpeciesId { get; private set; }
    /// <summary>토스트·도감에 보일 이름 (동물 이름, 없으면 종류 이름)</summary>
    public string DisplayName => SpeciesId != null ? CreatureLibrary.NameOf(SpeciesId) : Type.displayName;

    const float BaseSize = 12f;
    const float HoverHeight = 14f;
    const float CreatureScale = 1.15f;     // 모델은 받침 포함 약 10 m
    const float NearLodScreenHeight = 0.12f;   // 화면 높이의 12% 보다 크게 보이면 매끈한 메시, 작으면 가벼운 메시

    static Camera cachedCam;
    Transform visual;
    float phase;
    bool inRange;
    float pulse;

    /// <param name="species">정해진 동물 (노랑: 랜드마크 동물). null 이면 등급에서 무작위</param>
    public static Collectible Create(CollectibleType type, Vector3 groundPosition, Material material, Transform parent, string species = null)
    {
        var root = new GameObject($"Collectible_{type.id}");
        root.transform.SetParent(parent, false);
        root.transform.position = groundPosition;

        var c = root.AddComponent<Collectible>();
        c.Type = type;
        c.phase = Random.value * Mathf.PI * 2f;

        species = species ?? CreatureLibrary.PickSpecies(type.id);
        if (species != null && CreatureLibrary.TryGetMesh(species, out Mesh mesh))
        {
            c.SpeciesId = species;
            root.name = $"Collectible_{type.id}_{species}";
            var body = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * CreatureScale;
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            body.GetComponent<MeshRenderer>().sharedMaterial = CreatureLibrary.Material;
            if (CreatureLibrary.TryGetGlass(species, out Mesh glass) && CreatureLibrary.GlassMaterial != null)
            {
                var g = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
                g.transform.SetParent(body.transform, false);
                g.GetComponent<MeshFilter>().sharedMesh = glass;
                var gr = g.GetComponent<MeshRenderer>();
                gr.sharedMaterial = CreatureLibrary.GlassMaterial;
                gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            c.visual = body.transform;

            // 거리 LOD: 가까우면 매끈한 메시, 멀면 가벼운 메시 (화면에서 차지하는 높이 기준)
            if (CreatureLibrary.TryGetFarMesh(species, out Mesh far))
            {
                var farGo = new GameObject("VisualFar", typeof(MeshFilter), typeof(MeshRenderer));
                farGo.transform.SetParent(body.transform, false);
                farGo.GetComponent<MeshFilter>().sharedMesh = far;
                var fr = farGo.GetComponent<MeshRenderer>();
                fr.sharedMaterial = CreatureLibrary.Material;
                fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 멀리서는 그림자 생략 (가볍게)
                var nearRenderers = new List<Renderer> { body.GetComponent<MeshRenderer>() };
                foreach (Transform child in body.transform)
                    if (child.name == "Glass") nearRenderers.Add(child.GetComponent<Renderer>());
                var lod = body.AddComponent<LODGroup>();
                lod.SetLODs(new[]
                {
                    new LOD(NearLodScreenHeight, nearRenderers.ToArray()),
                    new LOD(0.004f, new Renderer[] { fr }),
                });
                lod.fadeMode = LODFadeMode.None;
                lod.RecalculateBounds();
            }

            var hitBox = root.AddComponent<SphereCollider>();
            hitBox.center = new Vector3(0f, 5.5f * CreatureScale, 0f);
            hitBox.radius = 7.5f * CreatureScale;
            hitBox.isTrigger = true;
            return c;
        }

        GameObject shape = GameObject.CreatePrimitive(type.shape);
        shape.name = "Visual";
        shape.transform.SetParent(root.transform, false);
        shape.transform.localPosition = new Vector3(0f, HoverHeight, 0f);
        shape.transform.localScale = Vector3.one * BaseSize;
        shape.GetComponent<Renderer>().sharedMaterial = material;

        // 손가락으로 누르기 쉽도록 모양보다 큰 충돌체를 쓴다.
        Object.Destroy(shape.GetComponent<Collider>());
        var hit = root.AddComponent<SphereCollider>();
        hit.center = new Vector3(0f, HoverHeight, 0f);
        hit.radius = BaseSize * 1.2f;
        hit.isTrigger = true;

        c.visual = shape.transform;
        return c;
    }

    public void SetInRange(bool value) => inRange = value;

    void Update()
    {
        if (visual == null)
            return;

        float t = Time.time + phase;
        pulse = Mathf.MoveTowards(pulse, inRange ? 1f : 0f, Time.deltaTime * 4f);

        if (SpeciesId != null)
        {
            // 동물: 카메라 쪽을 보고 가끔 통통 뛰며 살짝 몸을 흔든다. 반경 안이면 더 신나게 뛴다
            float hopRate = inRange ? 2.2f : 1.1f;
            float h = Mathf.Max(0f, Mathf.Sin(t * hopRate * Mathf.PI));
            float hop = h * h * (inRange ? 2.2f : 1.0f);
            float squash = 1f + 0.06f * Mathf.Sin(t * hopRate * Mathf.PI * 2f);
            float s = CreatureScale * (1f + pulse * 0.25f);
            visual.localPosition = new Vector3(0f, hop, 0f);
            visual.localScale = new Vector3(s / Mathf.Sqrt(squash), s * squash, s / Mathf.Sqrt(squash));

            float yaw = 0f;
            if (cachedCam == null) cachedCam = Camera.main;   // Camera.main 은 매 프레임 찾으면 비싸다
            Camera cam = cachedCam;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 1f)
                    yaw = Quaternion.LookRotation(toCam).eulerAngles.y;
            }
            visual.rotation = Quaternion.Euler(0f, yaw + 12f * Mathf.Sin(t * 0.9f), 0f);
            return;
        }

        float scale = BaseSize * (1f + pulse * (0.35f + 0.1f * Mathf.Sin(t * 6f)));
        visual.localPosition = new Vector3(0f, HoverHeight + Mathf.Sin(t * 2f) * 2f, 0f);
        visual.localRotation = Quaternion.Euler(20f, t * 60f, 0f);
        visual.localScale = Vector3.one * scale;
    }
}
