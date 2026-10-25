using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 퀘스트 화면 (B-5): 메뉴 › 퀘스트. 오늘의 퀘스트 3개와 이번 주 퀘스트 1개, 진행 막대와 보상 받기 버튼.
/// 받을 보상이 있으면 가운데 경희 로고와 메뉴의 퀘스트 버튼에 빨간 점.
/// v2: 잡기가 없어져 퀘스트는 잠시 숨김 (QuestsEnabled, 결정 D3). 코드는 남겨 둔다.
/// </summary>
public partial class KhuMenus
{
    static readonly Color QuestInk = KucaTheme.Ink;
    static readonly Color QuestQuiet = KucaTheme.InkMuted;
    static readonly Color QuestOrange = KucaTheme.Primary;
    static readonly Color QuestTrack = KucaTheme.Line;

    GameObject questScreen;
    RectTransform questList;
    Text questTimer;
    Image questMenuBadge;

    /// <summary>v2 에서 퀘스트 화면 · 배지를 숨긴다 (다시 켜려면 true, 메뉴 버튼도 다시 넣어야 함)</summary>
    const bool QuestsEnabled = false;

    void InitQuests()
    {
        if (progress == null || !QuestsEnabled)
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

        string reward = d.egg ? $"보상: 경희펫 알 + {d.xp} XP" : $"보상: {d.xp} XP";
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
            claim.GetComponentInChildren<Text>(true).fontSize = 32;
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
        {
            progress.tickets++; // v2: 알 대신 티켓 1장
            progress.Save();
            hud?.Toast($"퀘스트 보상 +{xp} XP  ·  티켓 1장");
        }
        else
            hud?.Toast($"퀘스트 보상 +{xp} XP");
        FillQuests();
    }

    void BuildQuests()
    {
        UIKit.Page page = UIKit.CreatePage(canvasRoot, "Quests", "퀘스트", () => Show(questScreen, false), " ");
        questScreen = page.root;
        questTimer = page.subtitle;
        questList = page.content;
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
