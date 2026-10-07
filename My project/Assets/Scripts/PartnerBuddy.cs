using UnityEngine;

/// <summary>
/// 파트너 경희몬. 지도에서는 캐릭터 옆에서 둥실둥실 따라다니고,
/// 프로필 화면에서는 캐릭터 옆 정해진 자리에 선다 (캐릭터 레이어라 프로필 카메라에 함께 찍힌다).
/// </summary>
public class PartnerBuddy : MonoBehaviour
{
    public Transform character;
    [Tooltip("캐릭터 로컬 단위 크기 (키 약 1.7)")]
    public float size = 0.5f;
    public float followSpeed = 4f;

    /// <summary>true 면 프로필 화면 자리에 선다</summary>
    public bool profilePose;

    string typeId;
    Transform visual;
    float phase;

    public void Show(CollectibleType type, Material material)
    {
        if (type == null)
        {
            Hide();
            return;
        }
        if (visual != null && typeId == type.id)
        {
            visual.gameObject.SetActive(true);
            return;
        }
        Hide();
        typeId = type.id;
        GameObject shape = GameObject.CreatePrimitive(type.shape);
        shape.name = "Partner_" + type.id;
        Destroy(shape.GetComponent<Collider>());
        shape.transform.SetParent(transform, false);
        shape.GetComponent<Renderer>().sharedMaterial = material;
        shape.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (character != null)
            shape.layer = character.gameObject.layer;
        visual = shape.transform;
        SnapToTarget();
    }

    public void Hide()
    {
        if (visual != null)
            Destroy(visual.gameObject);
        visual = null;
        typeId = null;
    }

    void LateUpdate()
    {
        if (visual == null || character == null)
            return;
        // 꾸미기 미리보기에는 나오지 않게 한다.
        bool show = !CharacterCustomizer.IsOpen;
        if (visual.gameObject.activeSelf != show)
            visual.gameObject.SetActive(show);

        float scale = character.lossyScale.y;
        phase += Time.deltaTime;
        float shapeScale = typeId == "star" ? 0.75f : 1f;
        visual.localScale = Vector3.one * size * scale * shapeScale * (profilePose ? 0.8f : 1f);
        if (profilePose)
        {
            visual.position = TargetPosition();
            visual.rotation = character.rotation * Quaternion.Euler(15f, phase * 40f, 0f);
            return;
        }
        visual.position = Vector3.Lerp(visual.position, TargetPosition(), 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
        visual.rotation = Quaternion.Euler(20f, phase * 60f, 0f);
    }

    Vector3 TargetPosition()
    {
        if (profilePose)
            return character.TransformPoint(-0.75f, 0.42f + Mathf.Sin(phase * 2f) * 0.04f, -0.25f);
        return character.TransformPoint(-0.9f, 0.9f + Mathf.Sin(phase * 2.4f) * 0.12f, -0.5f);
    }

    void SnapToTarget()
    {
        if (visual != null && character != null)
            visual.position = TargetPosition();
    }
}
