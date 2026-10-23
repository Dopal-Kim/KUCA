using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 선택한 부위 FBX들을 하나의 뼈대로 합쳐 캐릭터를 만든다.
/// 상의(Body) 파트의 뼈대를 기준으로 쓰고, 나머지 파트의 SkinnedMeshRenderer 뼈를 이름으로 다시 연결한다.
/// 이동 속도에 따라 Animator의 Speed 값을 넣어 Idle / Walk / Run 을 재생한다.
/// 경희몬과 어울리게 머리를 키우고 (headScale), 재질을 파스텔 비닐 토이(KUCA/PlayerToy)로 바꾼다.
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
    [Tooltip("머리 크기 배율. 1 = 원래 비율, 크면 귀여운 큰 머리")]
    public float headScale = 1.8f;
    [Tooltip("다리 길이 배율. 1 = 원래, 작으면 골반을 내리고 다리를 줄인다 (신발 크기는 그대로)")]
    public float legScale = 0.68f;
    [Tooltip("팔 길이 배율. 다리를 줄였을 때 손이 너무 내려오지 않게")]
    public float armScale = 0.85f;
    [Tooltip("켜면 첫 자세에서 동작을 멈춘다 (HUD 초상화처럼 가만히 있어야 할 때)")]
    public bool freezePose;
    [Tooltip("서 있으면 카메라 쪽으로 돌아서 얼굴을 보여 준다 (걸으면 다시 걷는 방향)")]
    public bool faceCameraWhenIdle = true;
    [Tooltip("이만큼 (초) 서 있으면 돌아선다")]
    public float idleTurnDelay = 1.2f;
    public float idleTurnSpeed = 3f;

    public CharacterAppearance Appearance { get; private set; }

    static readonly int SpeedId = Animator.StringToHash("Speed");

    GameObject model;
    Animator animator;
    Transform head, body, root;
    readonly Transform[] upperLegs = new Transform[2], feet = new Transform[2], upperArms = new Transform[2];
    Vector3 bodyWritten;
    int builtFrame;
    float idleTime;
    static Material toyBase;
    static readonly Dictionary<string, Material> toyMaterials = new Dictionary<string, Material>();
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

        HidePlaceholder();
        bones.TryGetValue("Head", out head);
        bones.TryGetValue("Body", out body);
        bones.TryGetValue("Root", out root);
        bones.TryGetValue("UpperLeg.L", out upperLegs[0]);
        bones.TryGetValue("UpperLeg.R", out upperLegs[1]);
        bones.TryGetValue("Foot.L", out feet[0]);
        bones.TryGetValue("Foot.R", out feet[1]);
        bones.TryGetValue("UpperArm.L", out upperArms[0]);
        bones.TryGetValue("UpperArm.R", out upperArms[1]);
        bodyWritten = new Vector3(float.NaN, 0, 0);
        ApplyToyLook();
        lastPos = Source.position;
        builtFrame = Time.frameCount;
    }

    /// <summary>
    /// 캐릭터가 생기기 전 자리표시용 캡슐(Player 의 MeshRenderer, 건물 뒤 파란 실루엣 포함)을 숨긴다.
    /// 그대로 두면 큰 머리 · 모자에 가려진 캡슐 부분이 캐릭터 위에 파랗게 겹쳐 보인다.
    /// </summary>
    void HidePlaceholder()
    {
        if (Source.TryGetComponent(out MeshRenderer placeholder))
            placeholder.enabled = false;
    }

    /// <summary>FBX 재질을 같은 이름의 토이 재질로 바꾼다. 색은 파스텔로 (경희대 색 KHU_ 는 그대로).</summary>
    void ApplyToyLook()
    {
        if (toyBase == null)
            toyBase = Resources.Load<Material>("KUCA/PlayerToy");
        if (toyBase == null)
            return;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null)
                    mats[i] = ToyMaterial(mats[i]);
            r.sharedMaterials = mats;
        }
    }

    /// <summary>
    /// 다리를 legScale 배로 줄인다. 골반(Body 뼈)을 그만큼 내리고 넓적다리를 줄인 뒤,
    /// 발(Root 아래라 다리와 따로 움직임)을 줄어든 다리 끝으로 옮겨 땅에 닿게 한다.
    /// 애니메이터가 이번 프레임에 뼈를 다시 쓰지 않았으면 (일시정지 등) 두 번 내리지 않는다.
    /// </summary>
    void ShortenLegs()
    {
        if (legScale >= 0.999f || body == null || root == null || upperLegs[0] == null || upperLegs[1] == null)
            return;
        if (body.localPosition == bodyWritten)
            return;
        Vector3 up = root.up;
        Vector3[] hip = { upperLegs[0].position, upperLegs[1].position };
        Vector3[] foot = { feet[0] != null ? feet[0].position : hip[0], feet[1] != null ? feet[1].position : hip[1] };
        float hipHeight = Vector3.Dot((hip[0] + hip[1]) * 0.5f - root.position, up);
        Vector3 drop = up * ((1f - legScale) * hipHeight);
        body.position -= drop;
        bodyWritten = body.localPosition;
        for (int i = 0; i < 2; i++)
        {
            upperLegs[i].localScale = Vector3.one * legScale;
            if (feet[i] != null)
                feet[i].position = hip[i] - drop + (foot[i] - hip[i]) * legScale;
        }
    }

    static Material ToyMaterial(Material source)
    {
        string key = source.name.Replace(" (Instance)", "");
        if (toyMaterials.TryGetValue(key, out Material m) && m != null)
            return m;
        Color c = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.color;
        m = new Material(toyBase) { name = key + "_Toy" };
        m.SetColor("_BaseColor", key.StartsWith("KHU_") || key.StartsWith("Skin") ? c : Pastel(c));
        toyMaterials[key] = m;
        return m;
    }

    /// <summary>어두운 색은 조금 밝게, 채도는 살짝 올려 비닐 장난감 느낌으로.</summary>
    public static Color Pastel(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        v = Mathf.Min(0.95f, v * 0.8f + 0.18f);
        s = Mathf.Min(1f, s * 1.05f);
        return Color.HSVToRGB(h, s, v);
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
        // 애니메이터가 뼈를 움직인 뒤에 머리 크기를 덧씌운다 (머리카락 · 모자는 Head 뼈에 붙어 같이 커진다)
        if (head != null)
            head.localScale = Vector3.one * headScale;
        foreach (Transform arm in upperArms)
            if (arm != null && armScale < 0.999f)
                arm.localScale = Vector3.one * armScale;
        ShortenLegs();
        // 대기 자세가 한 번 적용된 뒤 멈춘다 (뼈는 마지막 자세 그대로 남는다)
        if (freezePose && animator != null && animator.enabled && Time.frameCount > builtFrame + 1)
            animator.enabled = false;
        if (animator == null || Time.deltaTime <= 0f)
            return;

        Vector3 p = Source.position;
        Vector3 d = p - lastPos;
        d.y = 0f;
        lastPos = p;

        float target = d.magnitude * metersPerUnit / Time.deltaTime;
        speed = Mathf.Lerp(speed, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, speedDamping)));
        animator.SetFloat(SpeedId, speed);
        FaceCameraWhenIdle();
    }

    /// <summary>
    /// 지도 카메라는 보통 걷는 쪽 뒤에 있어서 뒤통수만 보인다. 잠시 서 있으면 몸(이 오브젝트)만 카메라 쪽으로 돌리고,
    /// 다시 걸으면 부모(Player, 걷는 방향)를 따른다. Player 의 방향은 바꾸지 않는다.
    /// </summary>
    void FaceCameraWhenIdle()
    {
        if (!faceCameraWhenIdle || freezePose || movementSource == null || transform.parent == null)
            return;
        idleTime = speed < 0.15f ? idleTime + Time.deltaTime : 0f;
        Quaternion goal = Quaternion.identity;
        Camera cam = Camera.main;
        if (idleTime > idleTurnDelay && cam != null)
        {
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.01f)
                goal = Quaternion.Inverse(transform.parent.rotation) * Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
        transform.localRotation = Quaternion.Slerp(transform.localRotation, goal, 1f - Mathf.Exp(-idleTurnSpeed * Time.deltaTime));
    }
}
