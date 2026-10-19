using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꾸미기 (A-3 최소 버전): 도감 › 경희몬 상세 › 꾸미기. 머리띠·목걸이·선글라스·귀걸이를 부위마다 하나씩 고른다.
/// 고른 것은 그 경희몬(GameProgress.Caught.accessories)에 저장되고, 파트너면 지도·프로필의 피규어에 바로 씌워진다.
/// 지금은 12종 모두 처음부터 쓸 수 있다 (퀘스트 보상으로 얻는 흐름은 다음 단계).
/// </summary>
public partial class KhuMenus
{
    GameObject accessoryPopup;
    Text accessoryTitle;
    Text accessoryNote;
    GameProgress.Caught accessoryTarget;
    readonly Dictionary<CreatureAccessories.Slot, List<(string id, Button button)>> accessoryButtons =
        new Dictionary<CreatureAccessories.Slot, List<(string, Button)>>();


    void BuildAccessoryPopup()
    {
        accessoryPopup = Overlay(canvasRoot, "AccessoryPopup", new Vector2(900f, 1180f), out RectTransform card);
        accessoryTitle = T(card, "꾸미기", KucaTheme.Title, TextAnchor.MiddleCenter, KucaTheme.Ink, bold: true);
        TopBox(accessoryTitle.rectTransform, 40f, 90f);

        float top = 160f;
        foreach (CreatureAccessories.Slot slot in System.Enum.GetValues(typeof(CreatureAccessories.Slot)))
        {
            Text label = T(card, CreatureAccessories.SlotName(slot), 38, TextAnchor.MiddleLeft, Gray, bold: true);
            TopBox(label.rectTransform, top, 60f);
            label.rectTransform.offsetMin = new Vector2(60f, label.rectTransform.offsetMin.y);

            var row = new List<(string, Button)>();
            var options = new List<CreatureAccessories.Accessory> { null };
            options.AddRange(CreatureAccessories.All.FindAll(a => a.slot == slot));
            for (int i = 0; i < options.Count; i++)
            {
                CreatureAccessories.Accessory acc = options[i];
                string id = acc?.id;
                CreatureAccessories.Slot s = slot;
                Button b = PillButton(card, acc == null ? "없음" : acc.shortName, KucaTheme.PrimarySoft, KucaTheme.PrimaryDeep, () => ChooseAccessory(s, id));
                var rt = (RectTransform)b.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(50f + i * 200f, -(top + 66f));
                rt.sizeDelta = new Vector2(188f, 100f);
                row.Add((id, b));
            }
            accessoryButtons[slot] = row;
            top += 195f;
        }

        accessoryNote = T(card, "", 30, TextAnchor.MiddleCenter, Gray);
        TopBox(accessoryNote.rectTransform, top + 10f, 60f);

        Button close = PillButton(card, "완료", Teal, Color.white, () => SetActive(accessoryPopup, false));
        Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f));
        ((RectTransform)close.transform).sizeDelta = new Vector2(360f, 110f);
    }

    void OpenAccessories(GameProgress.Caught c)
    {
        if (c == null || accessoryPopup == null)
            return;
        accessoryTarget = c;
        c.accessories ??= new List<string>();
        accessoryTitle.text = $"{NameOf(c)} 꾸미기";
        RefreshAccessoryButtons();
        SetActive(accessoryPopup, true);
    }

    void ChooseAccessory(CreatureAccessories.Slot slot, string id)
    {
        GameProgress.Caught c = accessoryTarget;
        if (c == null)
            return;
        c.accessories.RemoveAll(a => CreatureAccessories.Get(a)?.slot == slot);
        if (id != null)
            c.accessories.Add(id);
        progress.Save();
        if (c.uid == progress.partnerUid)
            RefreshBuddy();
        RefreshAccessoryButtons();
    }

    void RefreshAccessoryButtons()
    {
        GameProgress.Caught c = accessoryTarget;
        if (c == null)
            return;
        int stage = progress.StageOf(c.speciesId);
        string locked = null;
        foreach (var pair in accessoryButtons)
        {
            string worn = c.accessories.Find(a => CreatureAccessories.Get(a)?.slot == pair.Key);
            int need = CreatureAccessories.UnlockStage(pair.Key);
            if (stage < need && locked == null)
                locked = $"{CreatureAccessories.SlotName(pair.Key)}는 {need}단계로 진화하면 열려요";
            foreach ((string id, Button b) in pair.Value)
            {
                bool on = id == worn;
                // 이 동물에 맞는 모델이 없거나 (예: 개구리 귀걸이) 아직 진화가 덜 됐으면 고를 수 없다 ("없음"은 늘 가능)
                bool available = id == null || (stage >= need && CreatureAccessories.TryGetMesh(c.speciesId, id, out _));
                b.interactable = available;
                KucaTheme.PaintToggle(b.GetComponent<Image>(), b.GetComponentInChildren<Text>(), on);   // 켜짐 = 계절 강조색
            }
        }
        accessoryNote.text = locked ?? (c.uid == progress.partnerUid
            ? "파트너에게 바로 씌워져요 · 지도와 프로필에서 확인하세요"
            : "파트너로 지정하면 지도에서 함께 다녀요");
    }
}
