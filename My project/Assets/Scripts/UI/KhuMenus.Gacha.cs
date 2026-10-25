using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 뽑기 화면과 첫 파트너 고르기 (B-V1, 디자인 v4 참조: 포켓몬 슬립 보상 화면 · 토스 선택 화면). 규칙 값은 GachaRules, 저장은 GameProgress.
/// - 뽑기: 메뉴 › 뽑기. 1장 뽑기 (C~A) / 5장 뽑기 (B~S), 확률표 보기. 결과 연출은 HatchScreen.
/// - 첫 파트너: 파트너가 없으면 (처음 깐 앱) 다른 화면이 모두 닫혔을 때 C 등급 3마리 중 고르게 한다.
/// </summary>
public partial class KhuMenus
{

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
        gachaCount.text = $"{progress.tickets}장";
        float left = Mathf.Max(0f, GachaRules.TicketMeters - progress.ticketMeters);
        gachaNext.text = progress.TicketCapped
            ? $"오늘 티켓 {GachaRules.TicketsPerDay}장을 다 받았어요. 내일 또 걸어요"
            : $"다음 티켓까지 {left:N0}m  ·  오늘 받은 티켓 {progress.TicketsToday}/{GachaRules.TicketsPerDay}장";
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
            gachaMenuBadgeText.text = progress.tickets > 99 ? "99+장" : $"{progress.tickets}장";
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

    /// <summary>뽑기: 가진 티켓을 크게 (카드), 뽑기 카드 2장 (1장 · 5장, 가격 알약), 확률표는 글자 버튼</summary>
    void BuildGacha()
    {
        UIKit.Page page = UIKit.CreatePage(canvasRoot, "Gacha", "뽑기", () => Show(gachaScreen, false), "걸어서 모은 티켓으로 새 경희몬을 만나요");
        gachaScreen = page.root;

        // 가진 티켓
        RectTransform tc = UIKit.CardBox(page.content, "Tickets", KucaTheme.Space1);
        UIKit.Size(T(tc, "가진 티켓", KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.InkMuted, bold: true), 52f);
        RectTransform row = UIKit.Rect(tc, "Row");
        UIKit.Size(row, 150f);
        Image ticket = UIKit.Image(row, "Icon", KucaTheme.Gold);
        ticket.sprite = HudIcons.Ticket;
        Place(ticket.rectTransform, new Vector2(0f, 0.5f), Vector2.zero);
        ticket.rectTransform.sizeDelta = new Vector2(140f, 140f);
        gachaCount = T(row, "0장", KucaTheme.Display, TextAnchor.MiddleLeft, KucaTheme.Ink, bold: true);
        SetBox(gachaCount.rectTransform, 170f, 0f, 1f, 0f);
        gachaNext = T(tc, "", KucaTheme.Label, TextAnchor.MiddleLeft, KucaTheme.InkMuted);
        UIKit.Size(gachaNext, 60f);

        singleButton = DrawCard(page.content, "1장 뽑기", "C · B · A 등급이 나와요", KucaTheme.Primary, () => Draw(false));
        premiumButton = DrawCard(page.content, "5장 뽑기", "B · A · S 등급 · 더 귀한 경희몬", KucaTheme.Gold, () => Draw(true));

        Button odds = UIKit.Button(page.content, "확률표 보기", ToggleOdds, new Color(1f, 1f, 1f, 0f), KucaTheme.Label);
        odds.GetComponentInChildren<Text>(true).color = KucaTheme.PrimaryDeep;
        UIKit.Size(odds, KucaTheme.TouchMin);

        oddsPanel = Overlay(gachaScreen.transform, "Odds", new Vector2(940f, 1360f), out RectTransform card);
        Text ot = T(card, "확률표", KucaTheme.Title, TextAnchor.MiddleLeft, KucaTheme.Ink, bold: true);
        TopBox(ot.rectTransform, 48f, 96f);
        ot.rectTransform.offsetMin = new Vector2(56f, ot.rectTransform.offsetMin.y);
        oddsText = T(card, "", KucaTheme.Caption, TextAnchor.UpperLeft, KucaTheme.Ink);
        oddsText.supportRichText = true;
        oddsText.lineSpacing = 1.25f;
        oddsText.rectTransform.anchorMin = Vector2.zero;
        oddsText.rectTransform.anchorMax = Vector2.one;
        oddsText.rectTransform.offsetMin = new Vector2(56f, 48f + KucaTheme.ButtonHeight + KucaTheme.Space3);
        oddsText.rectTransform.offsetMax = new Vector2(-48f, -164f);
        Button oc = UIKit.SecondaryButton(card, "닫기", () => SetActive(oddsPanel, false));
        Dock((RectTransform)oc.transform, 48f, KucaTheme.ButtonHeight);
    }

    /// <summary>뽑기 카드: 왼쪽 티켓 타일 + 제목 + 설명 + 오른쪽 가격 알약. 제목이 카드의 첫 글자 (캡처 도구가 "1장 뽑기" 로 누른다)</summary>
    Button DrawCard(RectTransform parent, string label, string desc, Color accent, UnityEngine.Events.UnityAction onClick)
    {
        Button b = CardButton(parent, label, onClick);
        UIKit.Size(b, 260f);
        RectTransform r = (RectTransform)b.transform;

        Text t = T(r, label, KucaTheme.Heading + 6, TextAnchor.LowerLeft, KucaTheme.Ink, bold: true);
        Corners(t.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(228f, 0f), new Vector2(-260f, -30f));
        Text d = T(r, desc, KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
        Corners(d.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(228f, 24f), new Vector2(-260f, -12f));

        Image tile = UIKit.Image(r, "Tile", new Color(accent.r, accent.g, accent.b, 0.16f));
        tile.sprite = HudIcons.Rounded;
        tile.type = Image.Type.Sliced;
        tile.pixelsPerUnitMultiplier = 0.5f;
        Place(tile.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f));
        tile.rectTransform.sizeDelta = new Vector2(152f, 152f);
        Image ic = UIKit.Image(tile.transform, "Icon", accent);
        ic.sprite = HudIcons.Ticket;
        Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        ic.rectTransform.sizeDelta = new Vector2(100f, 100f);

        int cost = label.StartsWith("5") ? GachaRules.PremiumCost : GachaRules.SingleCost;
        Image price = UIKit.Image(r, "Price", accent);
        price.sprite = HudIcons.Pill;
        price.type = Image.Type.Sliced;
        price.pixelsPerUnitMultiplier = 1.4f;
        Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-44f, 0f));
        price.rectTransform.sizeDelta = new Vector2(200f, 88f);
        Text pt = T(price.transform, $"티켓 {cost}장", KucaTheme.Caption, TextAnchor.MiddleCenter, Color.white, bold: true);
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
            Button b = CardButton(starterRow, id, () => PickStarter(id));
            UIKit.Size(b, 320f);
            RectTransform r = (RectTransform)b.transform;
            RawImage thumb = Thumb(r, id, true, 260f, Vector2.zero);
            thumb.rectTransform.anchorMin = thumb.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            thumb.rectTransform.anchoredPosition = new Vector2(170f, 0f);

            Text name = T(r, CreatureLibrary.NameOf(id), KucaTheme.Heading, TextAnchor.LowerLeft, KucaTheme.Ink, bold: true);
            FitOneLine(name, KucaTheme.Heading);
            Corners(name.rectTransform, new Vector2(0f, 0.55f), Vector2.one, new Vector2(330f, 0f), new Vector2(-110f, -24f));
            Text sub = T(r, $"{GachaRules.Label(GachaRules.GradeOf(id))} 등급  ·  함께 걸으면 3단계까지 진화", KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
            Corners(sub.rectTransform, Vector2.zero, new Vector2(1f, 0.55f), new Vector2(330f, 24f), new Vector2(-110f, -12f));

            Image chev = UIKit.Image(r, "Chevron", KucaTheme.InkSoft);
            chev.sprite = HudIcons.Chevron;
            Place(chev.rectTransform, new Vector2(1f, 0.5f), new Vector2(-44f, 0f));
            chev.rectTransform.sizeDelta = new Vector2(48f, 48f);
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

    /// <summary>첫 파트너: 큰 제목 + 설명, 세로로 큰 카드 3장 (누르면 고른다). 꼭 골라야 해서 뒤로 가기는 없다</summary>
    void BuildStarter()
    {
        UIKit.Page page = UIKit.CreatePage(canvasRoot, "Starter", "첫 파트너를 골라 주세요", null, "골라 준 경희몬이 늘 옆에서 함께 걸어요");
        starterScreen = page.root;
        starterRow = UIKit.Rect(page.content, "Choices");
        UIKit.Vertical(starterRow, KucaTheme.Space3);
        UIKit.Paragraph(page.content, "함께 걸을수록 진화해요. 나중에 뽑기로 만난 경희몬으로 바꿀 수도 있어요.", KucaTheme.Label);
    }

}
