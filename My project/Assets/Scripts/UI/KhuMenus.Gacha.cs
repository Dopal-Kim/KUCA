using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 화면과 첫 파트너 고르기 (B-V1). 규칙 값은 GachaRules, 저장은 GameProgress.
/// - 뽑기: 메뉴 › 뽑기. 1장 뽑기 (C~A) / 5장 뽑기 (B~S), 확률표 보기. 결과 연출은 HatchScreen.
/// - 첫 파트너: 파트너가 없으면 (처음 깐 앱) 다른 화면이 모두 닫혔을 때 C 등급 3마리 중 고르게 한다.
/// </summary>
public partial class KhuMenus
{
    static readonly Color GachaInk = new Color(0.16f, 0.11f, 0.30f);
    static readonly Color GachaQuiet = new Color(0.16f, 0.11f, 0.30f, 0.6f);
    static readonly Color GachaPurple = new Color(0.45f, 0.30f, 0.85f);
    static readonly Color GachaGold = new Color(0.95f, 0.66f, 0.12f);

    GameObject gachaScreen, oddsPanel, starterScreen;
    Text gachaCount, gachaNext, oddsText;
    Button singleButton, premiumButton;
    Image gachaMenuBadge;
    Text gachaMenuBadgeText;
    RectTransform starterRow;
    bool starterOffered;
    float starterCheckAt;

    // ---------- 뽑기 ----------

    void OpenGacha()
    {
        Show(mainMenu, false);
        Show(gachaScreen, true);
        FillGacha();
    }

    void FillGacha()
    {
        if (progress == null)
            return;
        gachaCount.text = $"×{progress.tickets}";
        float left = Mathf.Max(0f, GachaRules.TicketMeters - progress.ticketMeters);
        gachaNext.text = progress.TicketCapped
            ? $"오늘 티켓 {GachaRules.TicketsPerDay}장을 다 받았어요. 내일 또 걸어요"
            : $"다음 티켓까지 {left:N0}m  ·  오늘 {progress.TicketsToday}/{GachaRules.TicketsPerDay}장";
        SetDrawButton(singleButton, progress.tickets >= GachaRules.SingleCost);
        SetDrawButton(premiumButton, progress.tickets >= GachaRules.PremiumCost);
        RefreshGachaBadge();
    }

    static void SetDrawButton(Button b, bool enough)
    {
        b.GetComponent<Image>().color = enough ? Color.white : new Color(1f, 1f, 1f, 0.5f);
    }

    /// <summary>메뉴의 뽑기 버튼과 가운데 로고에 티켓 수</summary>
    void RefreshGachaBadge()
    {
        if (progress == null)
            return;
        bool any = progress.tickets > 0;
        if (gachaMenuBadge != null)
        {
            gachaMenuBadge.gameObject.SetActive(any);
            gachaMenuBadgeText.text = progress.tickets > 99 ? "99+" : progress.tickets.ToString();
        }
        hud?.SetMenuBadge(any);
    }

    void Draw(bool premium)
    {
        int cost = premium ? GachaRules.PremiumCost : GachaRules.SingleCost;
        if (progress.tickets < cost)
        {
            hud?.Toast($"티켓이 {cost - progress.tickets}장 모자라요. 1km 걸을 때마다 1장!");
            return;
        }
        if (!progress.Draw(premium, Random.value, Random.value, out GameProgress.DrawResult r))
            return;
        progress.Save();
        if (r.evolved && progress.Partner != null && progress.Partner.speciesId == r.species)
            RefreshBuddy();
        if (r.evolved)
            EvolutionScreen.Queue(r.species, r.fromStage, r.stage); // 뽑기 결과를 닫으면 뜬다
        hud?.ShowTickets(progress);
        FillGacha();
        HatchScreen.Show(r, premium, progress.tickets, FillGacha);
    }

    void ToggleOdds()
    {
        if (oddsPanel.activeSelf)
        {
            SetActive(oddsPanel, false);
            return;
        }
        var sb = new System.Text.StringBuilder();
        foreach (bool premium in new[] { false, true })
        {
            sb.Append(premium ? "<b>5장 뽑기</b>\n" : "<b>1장 뽑기</b>\n");
            foreach (var (grade, chance) in GachaRules.OddsOf(premium))
            {
                int n = GachaRules.SpeciesOf(grade).Count;
                sb.Append($"  <color=#{ColorUtility.ToHtmlStringRGB(GachaRules.ColorOf(grade))}>{GachaRules.Label(grade)} 등급</color>  {chance * 100f:0.#}%  ·  {n}종 (한 종 {chance * 100f / Mathf.Max(1, n):0.##}%)\n");
            }
            sb.Append('\n');
        }
        sb.Append("같은 경희몬이 또 나오면 그 경희몬의 진화 경험치가 올라요\n");
        sb.Append($"C +{GachaRules.DuplicateGrowth(GachaRules.Grade.C)} · B +{GachaRules.DuplicateGrowth(GachaRules.Grade.B)} · A +{GachaRules.DuplicateGrowth(GachaRules.Grade.A)} · S +{GachaRules.DuplicateGrowth(GachaRules.Grade.S)}");
        oddsText.text = sb.ToString();
        SetActive(oddsPanel, true);
    }

    void BuildGacha()
    {
        gachaScreen = FullScreen("Gacha", new Color(0.93f, 0.90f, 1f), new Color(0.78f, 0.70f, 0.98f), out RectTransform inner);
        Frame(gachaScreen.transform, new Color(0.62f, 0.52f, 0.95f));

        Text title = T(inner, "뽑기", 64, TextAnchor.MiddleCenter, GachaInk, bold: true);
        TopBox(title.rectTransform, 60f, 90f);
        Text sub = T(inner, "걸어서 모은 티켓으로 새 경희몬을 만나요", 32, TextAnchor.MiddleCenter, GachaQuiet);
        TopBox(sub.rectTransform, 150f, 50f);

        // 가진 티켓
        Image ticket = UIKit.Image(inner, "Ticket", new Color(1f, 0.80f, 0.36f));
        ticket.sprite = HudIcons.Ticket;
        Place(ticket.rectTransform, new Vector2(0.5f, 1f), new Vector2(-100f, -300f));
        ticket.rectTransform.sizeDelta = new Vector2(200f, 200f);
        gachaCount = T(inner, "×0", 96, TextAnchor.MiddleLeft, GachaInk, bold: true);
        Place(gachaCount.rectTransform, new Vector2(0.5f, 1f), new Vector2(140f, -300f));
        gachaCount.rectTransform.sizeDelta = new Vector2(260f, 140f);
        gachaNext = T(inner, "", 32, TextAnchor.MiddleCenter, GachaQuiet);
        TopBox(gachaNext.rectTransform, 475f, 50f);

        singleButton = DrawCard(inner, "1장 뽑기", "C · B · A 등급", GachaPurple, 580f, () => Draw(false));
        premiumButton = DrawCard(inner, "5장 뽑기", "B · A · S 등급  ·  더 귀한 경희몬", GachaGold, 820f, () => Draw(true));

        Button odds = UIKit.Button(inner, "확률표 보기", ToggleOdds, new Color(0f, 0f, 0f, 0f), 34);
        odds.GetComponentInChildren<Text>().color = GachaInk;
        Place((RectTransform)odds.transform, new Vector2(0.5f, 1f), new Vector2(0f, -1100f));
        ((RectTransform)odds.transform).sizeDelta = new Vector2(400f, 90f);

        oddsPanel = Overlay(gachaScreen.transform, "Odds", new Vector2(900f, 1100f), out RectTransform card);
        Text ot = T(card, "확률표", 50, TextAnchor.MiddleCenter, GachaInk, bold: true);
        TopBox(ot.rectTransform, 50f, 70f);
        oddsText = T(card, "", 32, TextAnchor.UpperLeft, GachaInk);
        oddsText.supportRichText = true;
        oddsText.lineSpacing = 1.2f;
        oddsText.rectTransform.anchorMin = Vector2.zero;
        oddsText.rectTransform.anchorMax = Vector2.one;
        oddsText.rectTransform.offsetMin = new Vector2(70f, 200f);
        oddsText.rectTransform.offsetMax = new Vector2(-60f, -150f);
        Button oc = PillButton(card, "닫기", new Color(0.90f, 0.88f, 0.98f), GachaInk, () => SetActive(oddsPanel, false));
        Place((RectTransform)oc.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f));
        ((RectTransform)oc.transform).sizeDelta = new Vector2(400f, 116f);

        CloseFab(inner, () => Show(gachaScreen, false), filled: true, new Color(0.45f, 0.30f, 0.85f));
    }

    /// <summary>흰 카드 버튼: 왼쪽 색 띠 + 제목 + 설명 + 오른쪽 "티켓 n장"</summary>
    Button DrawCard(RectTransform parent, string label, string desc, Color accent, float top, UnityEngine.Events.UnityAction onClick)
    {
        Image card = UIKit.Image(parent, label, Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        RectTransform cr = card.rectTransform;
        cr.anchorMin = new Vector2(0f, 1f);
        cr.anchorMax = new Vector2(1f, 1f);
        cr.pivot = new Vector2(0.5f, 1f);
        cr.offsetMin = new Vector2(60f, -top - 210f);
        cr.offsetMax = new Vector2(-60f, -top);
        var b = card.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        card.gameObject.AddComponent<PressScale>();

        Image band = UIKit.Image(card.transform, "Band", accent);
        band.sprite = HudIcons.Pill;
        band.type = Image.Type.Sliced;
        band.pixelsPerUnitMultiplier = 12f;
        band.rectTransform.anchorMin = new Vector2(0f, 0.18f);
        band.rectTransform.anchorMax = new Vector2(0f, 0.82f);
        band.rectTransform.pivot = new Vector2(0f, 0.5f);
        band.rectTransform.anchoredPosition = new Vector2(36f, 0f);
        band.rectTransform.sizeDelta = new Vector2(14f, 0f);

        Text t = T(card.transform, label, 54, TextAnchor.MiddleLeft, GachaInk, bold: true);
        t.rectTransform.anchorMin = new Vector2(0f, 0.45f);
        t.rectTransform.anchorMax = new Vector2(1f, 1f);
        t.rectTransform.offsetMin = new Vector2(80f, 0f);
        t.rectTransform.offsetMax = new Vector2(-220f, -20f);
        Text d = T(card.transform, desc, 30, TextAnchor.UpperLeft, GachaQuiet);
        d.rectTransform.anchorMin = new Vector2(0f, 0f);
        d.rectTransform.anchorMax = new Vector2(1f, 0.45f);
        d.rectTransform.offsetMin = new Vector2(80f, 20f);
        d.rectTransform.offsetMax = new Vector2(-220f, 0f);

        int cost = label.StartsWith("5") ? GachaRules.PremiumCost : GachaRules.SingleCost;
        Image price = UIKit.Image(card.transform, "Price", accent);
        price.sprite = HudIcons.Pill;
        price.type = Image.Type.Sliced;
        price.pixelsPerUnitMultiplier = 120f / 80f;
        Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-36f, 0f));
        price.rectTransform.sizeDelta = new Vector2(170f, 80f);
        Text pt = T(price.transform, $"티켓 {cost}장", 30, TextAnchor.MiddleCenter, Color.white, bold: true);
        UIKit.Stretch(pt.rectTransform);
        return b;
    }

    // ---------- 첫 파트너 고르기 ----------

    /// <summary>파트너가 없으면 (첫 실행) 로그인 · 개인정보 안내 등 다른 화면이 다 닫힌 뒤 한 번 띄운다.</summary>
    void MaybeOfferStarter()
    {
        if (starterOffered || progress == null || progress.Partner != null || Time.unscaledTime < starterCheckAt)
            return;
        starterCheckAt = Time.unscaledTime + 0.5f;
        if (UIInputBlocker.IsModalOpen)
            return;
        starterOffered = true;
        OpenStarter();
    }

    void OpenStarter()
    {
        Clear(starterRow);
        foreach (string species in GachaRules.Starters(Mathf.Abs(SystemInfo.deviceUniqueIdentifier.GetHashCode())))
        {
            string id = species;
            Image card = UIKit.Image(starterRow, id, Color.white, raycast: true);
            card.sprite = HudIcons.Rounded;
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 0.6f;
            var b = card.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => PickStarter(id));
            card.gameObject.AddComponent<PressScale>();
            Thumb(card.transform, id, true, 240f, new Vector2(0f, -150f));
            Text name = T(card.transform, CreatureLibrary.NameOf(id), 40, TextAnchor.MiddleCenter, GachaInk, bold: true);
            BottomBox(name.rectTransform, 70f, 60f);
            FitOneLine(name, 40);
            Text grade = T(card.transform, "C 등급", 28, TextAnchor.MiddleCenter, GachaRules.ColorOf(GachaRules.Grade.C), bold: true);
            BottomBox(grade.rectTransform, 28f, 40f);
        }
        Show(starterScreen, true);
    }

    void PickStarter(string species)
    {
        GameProgress.Caught c = progress.AddCreature(species);
        progress.SetPartner(c.uid);
        progress.Save();
        Show(starterScreen, false);
        RefreshBuddy();
        hud?.ShowProgress(progress, types, popup: false);
        string name = CreatureLibrary.NameOf(species);
        hud?.Toast($"{name}{CatchCameraScreen.Josa(name, "과", "와")} 함께 걸어요!  걸을수록 함께 진화해요");
    }

    void BuildStarter()
    {
        starterScreen = FullScreen("Starter", new Color(0.93f, 0.90f, 1f), new Color(0.80f, 0.92f, 0.86f), out RectTransform inner);
        Text title = T(inner, "첫 파트너를 골라 주세요", 60, TextAnchor.MiddleCenter, GachaInk, bold: true);
        TopBox(title.rectTransform, 220f, 90f);
        Text sub = T(inner, "골라 준 경희몬이 늘 옆에서 같이 걸어요.\n함께 걸을수록 진화해요.", 34, TextAnchor.MiddleCenter, GachaQuiet);
        TopBox(sub.rectTransform, 320f, 110f);

        starterRow = UIKit.Rect(inner, "Choices");
        starterRow.anchorMin = new Vector2(0f, 1f);
        starterRow.anchorMax = new Vector2(1f, 1f);
        starterRow.pivot = new Vector2(0.5f, 1f);
        starterRow.offsetMin = new Vector2(40f, -960f);
        starterRow.offsetMax = new Vector2(-40f, -500f);
        var h = starterRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childForceExpandWidth = h.childForceExpandHeight = true;
        h.childControlWidth = h.childControlHeight = true;

        Text hint = T(inner, "나중에 뽑기로 만난 경희몬으로 바꿀 수도 있어요", 30, TextAnchor.MiddleCenter, GachaQuiet);
        TopBox(hint.rectTransform, 1000f, 50f);
    }
}
