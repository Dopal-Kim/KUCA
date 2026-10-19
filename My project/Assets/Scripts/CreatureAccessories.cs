using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 퀘스트 보상 액세서리 (A-3): 머리띠·목걸이·선글라스·귀걸이 각 3종, 24종 동물마다 머리에 맞춘 3D 메시.
/// 메시 파일 Resources/KUCA/CreatureAccessories.bytes (tools/keyart/build_accessories.py), 이름 Acc_&lt;동물&gt;_&lt;액세서리 id&gt;.
/// 동물 메시와 같은 좌표라서, 동물 피규어(메시가 붙은 오브젝트) 아래에 그대로 자식으로 붙이면 맞는다.
/// 디자인: Art/Concept/06_A_Round/A3/
/// </summary>
public static class CreatureAccessories
{
    public enum Slot { Head, Neck, Eyes, Ears }

    public class Accessory
    {
        public string id;
        public string name;
        public Slot slot;
        /// <summary>버튼에 쓸 짧은 이름</summary>
        public string shortName;
    }

    /// <summary>
    /// v2: 이 부위가 열리는 진화 단계. 아기(0) 머리띠·목걸이 → 1단계 선글라스 → 2단계 귀걸이.
    /// 자랄수록 꾸밀 곳이 늘어 "다음 진화"를 기다리게 한다.
    /// </summary>
    public static int UnlockStage(Slot s) => s switch { Slot.Eyes => 1, Slot.Ears => 2, _ => 0 };

    /// <summary>부위 이름 (화면)</summary>
    public static string SlotName(Slot s) => s switch { Slot.Head => "머리띠", Slot.Neck => "목걸이", Slot.Eyes => "선글라스", _ => "귀걸이" };

    /// <summary>액세서리 목록 (퀘스트 보상 아이템 id 와 같다)</summary>
    public static readonly List<Accessory> All = new List<Accessory>
    {
        new Accessory { id = "headband_blossom", name = "벚꽃 머리띠", slot = Slot.Head, shortName = "벚꽃" },
        new Accessory { id = "headband_crimson_ears", name = "크림슨 리본 귀 머리띠", slot = Slot.Head, shortName = "리본 귀" },
        new Accessory { id = "headband_laurel", name = "황금 월계수 머리띠", slot = Slot.Head, shortName = "월계수" },
        new Accessory { id = "necklace_star", name = "별 펜던트 목걸이", slot = Slot.Neck, shortName = "별" },
        new Accessory { id = "necklace_beads", name = "알록달록 구슬 목걸이", slot = Slot.Neck, shortName = "구슬" },
        new Accessory { id = "necklace_heart", name = "크림슨 하트 목걸이", slot = Slot.Neck, shortName = "하트" },
        new Accessory { id = "glasses_round", name = "동그란 크림슨 선글라스", slot = Slot.Eyes, shortName = "동그란" },
        new Accessory { id = "glasses_heart", name = "하트 선글라스", slot = Slot.Eyes, shortName = "하트" },
        new Accessory { id = "glasses_star", name = "별 선글라스", slot = Slot.Eyes, shortName = "별" },
        new Accessory { id = "earrings_star", name = "금별 귀걸이", slot = Slot.Ears, shortName = "금별" },
        new Accessory { id = "earrings_pearl", name = "진주 귀걸이", slot = Slot.Ears, shortName = "진주" },
        new Accessory { id = "earrings_blossom", name = "벚꽃 귀걸이", slot = Slot.Ears, shortName = "벚꽃" },
    };

    public static Accessory Get(string id) => All.Find(a => a.id == id);

    static Dictionary<string, Mesh> meshes;

    /// <summary>그 동물에 맞춘 액세서리 메시 (없으면 false)</summary>
    public static bool TryGetMesh(string species, string accessoryId, out Mesh mesh)
    {
        if (meshes == null || CreatureLibrary.IsStale(meshes)) // 에디터에서 지난 플레이의 지워진 메시 (CreatureLibrary.IsStale)
            meshes = CreatureLibrary.LoadMeshes("KUCA/CreatureAccessories", quiet: true);
        mesh = null;
        return species != null && accessoryId != null && meshes.TryGetValue($"Acc_{species}_{accessoryId}", out mesh);
    }

    /// <summary>
    /// figure(동물 메시가 붙은 오브젝트) 아래에 액세서리를 붙인다. 같은 부위(slot)에 있던 것은 바꾼다.
    /// 반환: 붙인 오브젝트 (메시가 없으면 null)
    /// </summary>
    public static GameObject Attach(Transform figure, string species, string accessoryId, int layer = -1)
    {
        Accessory acc = Get(accessoryId);
        if (figure == null || acc == null || !TryGetMesh(species, accessoryId, out Mesh mesh))
            return null;
        Detach(figure, acc.slot);
        var go = new GameObject("Accessory_" + acc.slot, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(figure, false);
        go.layer = layer >= 0 ? layer : figure.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = CreatureLibrary.Material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    /// <summary>그 부위의 액세서리를 뗀다</summary>
    public static void Detach(Transform figure, Slot slot)
    {
        if (figure == null)
            return;
        Transform old = figure.Find("Accessory_" + slot);
        if (old != null)
            Object.Destroy(old.gameObject);
    }
}
