using System.Collections.Generic;
using UnityEngine;

/// <summary>도구 종류와 레벨업 보상표</summary>
public static class ItemCatalog
{
    public class Item
    {
        public string id;
        public string name;
        public string description;
    }

    public static readonly List<Item> All = new List<Item>
    {
        new Item { id = "khball", name = "경희볼", description = "경희몬을 담는 공이에요. 포획 기능과 함께 쓸 수 있게 될 거예요." },
        new Item { id = "potion", name = "회복 음료", description = "지친 경희몬의 기운을 되찾아 주는 음료예요. (사용 기능 준비 중)" },
        new Item { id = "berry", name = "황금 열매", description = "경희몬이 좋아하는 열매예요. 파트너에게 줄 수 있게 될 거예요." },
        new Item { id = "incense", name = "탐험 향로", description = "주변에 경희몬을 불러 모으는 향로예요. (사용 기능 준비 중)" },
    };

    public static Item Get(string id) => All.Find(i => i.id == id);

    /// <summary>처음 시작할 때 받는 도구 (레벨 1 보상)</summary>
    public static readonly (string id, int count)[] StarterKit = { ("khball", 20), ("potion", 5) };

    /// <summary>레벨 level 에 올랐을 때 받는 보상 (최대 3가지)</summary>
    public static List<(string id, int count)> RewardsFor(int level)
    {
        var list = new List<(string, int)>();
        if (level <= 1)
        {
            list.AddRange(StarterKit);
            return list;
        }
        list.Add(("khball", 10 + (level / 5) * 5));
        list.Add(level % 2 == 0 ? ("potion", 5) : ("berry", 3));
        if (level % 5 == 0)
            list.Add(("incense", 1));
        else if (level % 3 == 0)
            list.Add(("berry", 2));
        return list;
    }

    public static string Describe(List<(string id, int count)> rewards)
    {
        var parts = new List<string>();
        foreach (var (id, count) in rewards)
            parts.Add($"{Get(id)?.name ?? id} ×{count}");
        return string.Join(", ", parts);
    }
}
