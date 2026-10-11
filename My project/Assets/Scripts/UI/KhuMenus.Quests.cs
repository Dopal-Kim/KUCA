using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 화면 (B-5): 메뉴 › 퀘스트. 오늘의 퀘스트 3개와 이번 주 퀘스트 1개, 진행 막대와 보상 받기 버튼.
/// 받을 보상이 있으면 가운데 경희 로고와 메뉴의 퀘스트 버튼에 빨간 점.
/// </summary>
public partial class KhuMenus
{
    static readonly Color QuestInk = new Color(0.20f, 0.14f, 0.10f);
    static readonly Color QuestQuiet = new Color(0.20f, 0.14f, 0.10f, 0.55f);
    static readonly Color QuestOrange = new Color(0.96f, 0.52f, 0.16f);
    static readonly Color QuestTrack = new Color(0.20f, 0.14f, 0.10f, 0.10f);

    GameObject questScreen;
    RectTransform questList;
    Text questTimer;
    Image questMenuBadge;

    void InitQuests()
    {
        if (progress == null)
            return;
        Quests.Ensure(progress, spots != null ? spots.Spots.Count : 30);
        progress.Save();
        Quests.Changed -= RefreshQuestBadges;
        Quests.Changed += RefreshQuestBadges;
        RefreshQuestBadges();
    }

    void OnDestroy_Quests() => Quests.Changed -= RefreshQuestBadges;

    void RefreshQuestBadges()
    {
        bool reward = Quests.HasReward(progress);
        hud?.SetMenuBadge(reward);
        if (questMenuBadge != null) questMenuBadge.gameObject.SetActive(reward);
        if (questScreen != null && questScreen.activeSelf) FillQuests();
    }

    void OpenQuests()
    {
        Show(mainMenu, false);
        Quests.Ensure(progress, spots != null ? spots.Spots.Count : 30);
        Show(questScreen, true);
        FillQuests();
    }

    void FillQuests()
    {
        for (int i = questList.childCount - 1; i >= 0; i--)
            Destroy(questList.GetChild(i).gameObject);
        System.TimeSpan left = Quests.UntilMidnight;
        questTimer.text = $"오늘의 퀘스트는 {left.Hours}시간 {left.Minutes}분 뒤에 바뀌어요";
        Section("오늘의 퀘스트");
        foreach (GameProgress.QuestState q in progress.dailyQuests)
            QuestCard(q);
        Section("이번 주 퀘스트");
        foreach (GameProgress.QuestState q in progress.weeklyQuests)
            QuestCard(q);
    }

    void Section(string label)
    {
        Text t = T(questList, label, 38, TextAnchor.LowerLeft, QuestInk, bold: true);
        UIKit.Size(t, 84f);
    }

    void QuestCard(GameProgress.QuestState q)
    {
        Quests.Def d = Quests.DefOf(q.id);
        if (d == null)
            return;
        bool done = q.progress >= d.target;
        Image card = UIKit.Image(questList, q.id, Color.white);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.7f;
        UIKit.Size(card, 210f);

        Text title = T(card.transform, d.title, 36, TextAnchor.UpperLeft, q.claimed ? QuestQuiet : QuestInk, bold: true);
        Box(title.rectTransform, 40f, 34f, 300f, 50f);
        title.resizeTextForBestFit = true; // 긴 제목은 한 줄에 맞게 줄인다
        title.resizeTextMinSize = 24;
        title.resizeTextMaxSize = 36;
        title.verticalOverflow = VerticalWrapMode.Truncate;
        title.horizontalOverflow = HorizontalWrapMode.Wrap;

        string reward = d.egg ? $"보상: 경희몬 알 + {d.xp} XP" : $"보상: {d.xp} XP";
        Text rw = T(card.transform, reward, 28, TextAnchor.UpperLeft, QuestOrange, bold: true);
        Box(rw.rectTransform, 40f, 92f, 300f, 40f);

        // 진행 막대 + 숫자
        Image track = UIKit.Image(card.transform, "Track", QuestTrack);
        track.sprite = HudIcons.Pill;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 6f;
        Box(track.rectTransform, 40f, 150f, 300f, 18f);
        Image fill = UIKit.Image(track.transform, "Fill", q.claimed ? new Color(0.6f, 0.6f, 0.6f) : QuestOrange);
        fill.sprite = HudIcons.Pill;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 6f;
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)q.progress / d.target), 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        string amount = d.kind == "walk" ? $"{q.progress:N0} / {d.target:N0}m" : $"{q.progress} / {d.target}";
        Text num = T(card.transform, amount, 26, TextAnchor.MiddleRight, QuestQuiet);
        num.rectTransform.anchorMin = num.rectTransform.anchorMax = new Vector2(1f, 1f);
        num.rectTransform.pivot = new Vector2(1f, 1f);
        num.rectTransform.anchoredPosition = new Vector2(-40f, -138f);
        num.rectTransform.sizeDelta = new Vector2(240f, 40f);

        // 오른쪽: 보상 받기 / 받았어요
        if (q.claimed)
        {
            Text got = T(card.transform, "받았어요 ✓", 32, TextAnchor.MiddleCenter, QuestQuiet, bold: true);
            got.rectTransform.anchorMin = got.rectTransform.anchorMax = got.rectTransform.pivot = new Vector2(1f, 1f);
            got.rectTransform.anchoredPosition = new Vector2(-30f, -40f);
            got.rectTransform.sizeDelta = new Vector2(230f, 84f);
        }
        else if (done)
        {
            GameProgress.QuestState state = q;
            Button claim = PillButton(card.transform, "보상 받기", QuestOrange, Color.white, () => ClaimQuest(state));
            RectTransform cr = (RectTransform)claim.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-30f, -40f);
            cr.sizeDelta = new Vector2(230f, 84f);
            claim.GetComponentInChildren<Text>().fontSize = 32;
            claim.gameObject.AddComponent<PressScale>();
        }
    }

    void ClaimQuest(GameProgress.QuestState q)
    {
        bool egg = Quests.Claim(progress, q, out int xp);
        if (xp <= 0)
            return;
        hud?.ShowProgress(progress, types); // XP·레벨 갱신 ("+N XP")
        if (egg)
            HatchEgg(questReward: true);
        else
            hud?.Toast($"퀘스트 보상 +{xp} XP");
        FillQuests();
    }

    void BuildQuests()
    {
        questScreen = FullScreen("Quests", new Color(1f, 0.95f, 0.84f), new Color(0.99f, 0.82f, 0.62f), out RectTransform inner);
        Frame(questScreen.transform, QuestOrange);
        Text title = T(inner, "퀘스트", 60, TextAnchor.MiddleLeft, QuestInk, bold: true);
        Box(title.rectTransform, 60f, 40f, 60f, 90f);
        questTimer = T(inner, "", 30, TextAnchor.MiddleLeft, QuestQuiet);
        Box(questTimer.rectTransform, 60f, 130f, 60f, 50f);

        RectTransform area = UIKit.Rect(inner, "List");
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(0f, 220f);
        area.offsetMax = new Vector2(0f, -190f);
        questList = UIKit.ScrollList(area, 22f, new RectOffset(48, 48, 0, 40));
        CloseFab(inner, () => Show(questScreen, false), filled: true, QuestOrange);
    }

    /// <summary>부모 위쪽에서 (left, top) 떨어지고 오른쪽 여백 right, 높이 height 인 칸</summary>
    static void Box(RectTransform rt, float left, float top, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
