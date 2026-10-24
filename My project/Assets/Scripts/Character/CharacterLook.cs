using UnityEngine;

/// <summary>
/// 얼굴 갈래 · 피부 · 머리 · 눈 색 · 눈 슬라이더를 이미 만든 캐릭터에 입힌다.
/// 부위를 다시 만들지 않으므로 꾸미기 화면에서 고르거나 슬라이더를 움직이는 대로 바로 보인다.
/// - 얼굴 갈래: 머리 FBX 안의 Face_&lt;부위&gt;_&lt;번호&gt; 메시 (tools/character/build_adventurer.py). 고른 번호만 켜고,
///   그 번호의 메시가 없으면 (예: 코 "없음") 모두 끈다. 다른 머리(Quaternius)에는 얼굴 갈래가 없어 색만 바뀐다.
/// - 색: 재질 이름으로 찾는다. Skin* = 피부, KHU_Adv_Hair · Hair* = 머리 · 눈썹, KHU_Adv_Eye = 눈동자.
///   0 번 색은 캐릭터 시트(Art/Concept/07_Player)의 색과 같다.
/// - 슬라이더: 눈 · 눈썹 메시의 셰이프 키 Eye_Big/Eye_Small, Eye_Wide/Eye_Narrow, Eye_Up/Eye_Down (-1 ~ 1).
/// 모동숲처럼 고르는 칸은 몇 개만 두고, 눈 크기 · 간격 · 높이만 슬라이더로 조금 더 조절하게 했다.
/// </summary>
public static class CharacterLook
{
    public enum Part { Eyes, Brows, Mouth, Nose, Cheeks }

    /// <summary>부위마다 고를 수 있는 모양 (번호 = Face_ 메시 번호, 마지막 "없음"은 메시가 없는 번호)</summary>
    public static readonly string[][] Shapes =
    {
        new[] { "기본", "동글", "웃는 눈", "또렷" },
        new[] { "기본", "아치", "짙은" },
        new[] { "미소", "활짝", "고양이", "작은 입" },
        new[] { "동글", "작게", "없음" },
        new[] { "볼터치", "동그란 볼", "주근깨", "없음" },
    };

    public static readonly string[] PartNames = { "눈", "눈썹", "입", "코", "볼" };

    public readonly struct Swatch
    {
        public readonly string name;
        public readonly Color color;
        public Swatch(string name, int rgb)
        {
            this.name = name;
            color = new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
        }
    }

    /// <summary>피부 8 (자연 6 + 캠퍼스 파스텔 2)</summary>
    public static readonly Swatch[] Skins =
    {
        new Swatch("복숭아", 0xF4C7A8), new Swatch("밝은", 0xFCE3D2), new Swatch("살구", 0xEDB48E), new Swatch("구릿빛", 0xD9976B),
        new Swatch("갈색", 0xB8774E), new Swatch("짙은 갈색", 0x8E5638), new Swatch("민트", 0xBFE3D0), new Swatch("라벤더", 0xD9CCF2),
    };

    public static readonly Swatch[] Hairs =
    {
        new Swatch("초코", 0x5A3A2A), new Swatch("흑갈색", 0x2B2522), new Swatch("밤색", 0x8A5A3C), new Swatch("카라멜", 0xC99A5B),
        new Swatch("금발", 0xE8D29A), new Swatch("크림슨", 0x8E2433), new Swatch("블루그레이", 0x6F8FAF), new Swatch("라벤더", 0xB9A3D9),
    };

    public static readonly Swatch[] EyeColors =
    {
        new Swatch("짙은 갈색", 0x3A2620), new Swatch("검정", 0x1E1E24), new Swatch("호박", 0x6B4A2E),
        new Swatch("숲", 0x3E6B4E), new Swatch("바다", 0x3F5F8F), new Swatch("자두", 0x7A3E5E),
    };

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    public static int Get(CharacterAppearance a, Part p) => p switch
    {
        Part.Eyes => a.eyes, Part.Brows => a.brows, Part.Mouth => a.mouth, Part.Nose => a.nose, _ => a.cheeks,
    };

    public static void Set(CharacterAppearance a, Part p, int value)
    {
        switch (p)
        {
            case Part.Eyes: a.eyes = value; break;
            case Part.Brows: a.brows = value; break;
            case Part.Mouth: a.mouth = value; break;
            case Part.Nose: a.nose = value; break;
            default: a.cheeks = value; break;
        }
    }

    /// <summary>model 아래 모든 렌더러에 얼굴 갈래 · 색 · 슬라이더를 입힌다</summary>
    public static void Apply(Transform model, CharacterAppearance a)
    {
        if (model == null || a == null)
            return;
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (TryFacePart(r.name, out Part part, out int n))
                r.gameObject.SetActive(n == Get(a, part));

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!TryTint(mats[i], a, out Color c))
                    continue;
                r.GetPropertyBlock(block, i);
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block, i);
            }

            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null && smr.sharedMesh.blendShapeCount > 0)
            {
                SetPair(smr, "Eye_Big", "Eye_Small", a.eyeSize);
                SetPair(smr, "Eye_Wide", "Eye_Narrow", a.eyeSpacing);
                SetPair(smr, "Eye_Up", "Eye_Down", a.eyeHeight);
            }
        }
    }

    /// <summary>이 모델에 얼굴 갈래가 있는지 (경희 모험가 머리일 때만 있다)</summary>
    public static bool HasFaceParts(Transform model)
    {
        if (model == null)
            return false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            if (TryFacePart(r.name, out _, out _))
                return true;
        return false;
    }

    /// <summary>"Face_Eyes_2" → (Eyes, 2)</summary>
    static bool TryFacePart(string name, out Part part, out int n)
    {
        part = Part.Eyes;
        n = -1;
        if (!name.StartsWith("Face_"))
            return false;
        string[] bits = name.Split('_');
        return bits.Length == 3 && System.Enum.TryParse(bits[1], out part) && int.TryParse(bits[2], out n);
    }

    static bool TryTint(Material m, CharacterAppearance a, out Color c)
    {
        c = default;
        if (m == null)
            return false;
        string n = m.name;
        if (n.StartsWith("Skin"))
            c = Pick(Skins, a.skin);
        else if (n.StartsWith("KHU_Adv_Hair") || n.StartsWith("Hair"))
            c = Pick(Hairs, a.hairColor);
        else if (n.StartsWith("KHU_Adv_Eye"))
            c = Pick(EyeColors, a.eyeColor);
        else
            return false;
        return true;
    }

    static Color Pick(Swatch[] list, int i) => list[Mathf.Clamp(i, 0, list.Length - 1)].color;

    /// <summary>슬라이더 -1 ~ 1 → 양쪽 셰이프 키 중 하나에 0 ~ 100</summary>
    static void SetPair(SkinnedMeshRenderer smr, string positive, string negative, float value)
    {
        value = Mathf.Clamp(value, -1f, 1f);
        int p = smr.sharedMesh.GetBlendShapeIndex(positive);
        int q = smr.sharedMesh.GetBlendShapeIndex(negative);
        if (p >= 0) smr.SetBlendShapeWeight(p, Mathf.Max(0f, value) * 100f);
        if (q >= 0) smr.SetBlendShapeWeight(q, Mathf.Max(0f, -value) * 100f);
    }
}
