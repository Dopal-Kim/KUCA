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
/// 지도 위에 떠서 회전하는 수집 대상. Player가 수집 반경 안에 들어오면 커지며 강조된다.
/// </summary>
public class Collectible : MonoBehaviour
{
    public CollectibleType Type { get; private set; }

    const float BaseSize = 12f;
    const float HoverHeight = 14f;

    Transform visual;
    float phase;
    bool inRange;
    float pulse;

    public static Collectible Create(CollectibleType type, Vector3 groundPosition, Material material, Transform parent)
    {
        var root = new GameObject($"Collectible_{type.id}");
        root.transform.SetParent(parent, false);
        root.transform.position = groundPosition;

        var c = root.AddComponent<Collectible>();
        c.Type = type;
        c.phase = Random.value * Mathf.PI * 2f;

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
        float scale = BaseSize * (1f + pulse * (0.35f + 0.1f * Mathf.Sin(t * 6f)));

        visual.localPosition = new Vector3(0f, HoverHeight + Mathf.Sin(t * 2f) * 2f, 0f);
        visual.localRotation = Quaternion.Euler(20f, t * 60f, 0f);
        visual.localScale = Vector3.one * scale;
    }
}
