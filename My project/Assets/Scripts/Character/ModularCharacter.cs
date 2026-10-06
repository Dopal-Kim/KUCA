using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택한 부위 FBX들을 하나의 뼈대로 합쳐 캐릭터를 만든다.
/// 상의(Body) 파트의 뼈대를 기준으로 쓰고, 나머지 파트의 SkinnedMeshRenderer 뼈를 이름으로 다시 연결한다.
/// 이동 속도에 따라 Animator의 Speed 값을 넣어 Idle / Walk / Run 을 재생한다.
/// </summary>
public class ModularCharacter : MonoBehaviour
{
    public CharacterPartsDatabase database;
    [Tooltip("속도를 잴 대상 (보통 Player). 비우면 이 오브젝트")]
    public Transform movementSource;
    [Tooltip("월드 1 유닛이 실제 몇 m인지. 지도는 1 유닛 = 1 m")]
    public float metersPerUnit = 1f;
    [Tooltip("Speed 값을 부드럽게 바꾸는 시간 (초)")]
    public float speedDamping = 0.6f;

    public CharacterAppearance Appearance { get; private set; }

    static readonly int SpeedId = Animator.StringToHash("Speed");

    GameObject model;
    Animator animator;
    Vector3 lastPos;
    float speed;

    public void Build(CharacterAppearance appearance)
    {
        if (database == null || database.genders.Count == 0 || appearance == null)
            return;

        Appearance = appearance.Clone();
        Appearance.gender = Mathf.Clamp(Appearance.gender, 0, database.genders.Count - 1);
        CharacterPartsDatabase.GenderSet set = database.genders[Appearance.gender];

        if (model != null)
            Destroy(model);

        GameObject bodyPrefab = Find(set, CharacterSlot.Body, Appearance.body);
        if (bodyPrefab == null)
            return;

        model = Instantiate(bodyPrefab, transform, false);
        model.name = "Model";
        // Quaternius 모델은 -Z를 바라보므로 돌려서 이 오브젝트의 앞(+Z)을 보게 한다.
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Transform armature = model.transform.Find("CharacterArmature");
        var bones = new Dictionary<string, Transform>();
        if (armature != null)
            foreach (Transform t in armature.GetComponentsInChildren<Transform>(true))
                bones[t.name] = t;

        foreach (CharacterSlot slot in new[] { CharacterSlot.Head, CharacterSlot.Legs, CharacterSlot.Feet, CharacterSlot.Accessory })
        {
            GameObject prefab = Find(set, slot, Appearance.Get(slot));
            if (prefab != null)
                AttachPart(prefab, bones);
        }

        // 꾸미기 미리보기 카메라가 캐릭터만 찍을 수 있도록 레이어를 맞춘다.
        foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = gameObject.layer;

        // 애니메이션 FBX는 뼈대(CharacterArmature)가 루트라서 Animator도 뼈대에 붙여야 경로가 맞는다.
        GameObject animRoot = armature != null ? armature.gameObject : model;
        if (!animRoot.TryGetComponent(out animator))
            animator = animRoot.AddComponent<Animator>();
        animator.runtimeAnimatorController = set.animator;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();

        lastPos = Source.position;
    }

    /// <summary>파트를 복제해 메시만 기준 뼈대에 붙이고, 파트에 딸린 뼈대는 버린다.</summary>
    void AttachPart(GameObject prefab, Dictionary<string, Transform> bones)
    {
        GameObject part = Instantiate(prefab, model.transform, false);
        foreach (SkinnedMeshRenderer src in part.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            src.transform.SetParent(model.transform, false);
            var mapped = new Transform[src.bones.Length];
            for (int i = 0; i < mapped.Length; i++)
                mapped[i] = src.bones[i] != null && bones.TryGetValue(src.bones[i].name, out Transform b) ? b : null;
            src.bones = mapped;
            if (src.rootBone != null && bones.TryGetValue(src.rootBone.name, out Transform root))
                src.rootBone = root;
        }
        Destroy(part);
    }

    static GameObject Find(CharacterPartsDatabase.GenderSet set, CharacterSlot slot, string id)
    {
        List<CharacterPartsDatabase.PartOption> options = set.Options(slot);
        foreach (var o in options)
            if (o.id == id)
                return o.prefab;
        // 저장된 id가 없으면 첫 선택지 (액세서리는 없음)
        return slot == CharacterSlot.Accessory || options.Count == 0 ? null : options[0].prefab;
    }

    Transform Source => movementSource != null ? movementSource : transform;

    void LateUpdate()
    {
        if (animator == null || Time.deltaTime <= 0f)
            return;

        Vector3 p = Source.position;
        Vector3 d = p - lastPos;
        d.y = 0f;
        lastPos = p;

        float target = d.magnitude * metersPerUnit / Time.deltaTime;
        speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, speedDamping)));
        animator.SetFloat(SpeedId, speed);
    }
}
