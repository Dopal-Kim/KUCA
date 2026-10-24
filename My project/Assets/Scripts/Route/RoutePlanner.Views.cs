using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>RoutePlanner 의 화면: 조건 시트 · 장소 고르기 · 기다리기 · 코스 고르기 · 걷는 중 판 · 장소 팝업 · 위에서 본 지도</summary>
public partial class RoutePlanner
{
    GameObject sheet, picker, loading, chooser, walkPanel, placePopup;
    Image[] timeChips, tasteChips;
    Image mealChip;
    Text mealNote, startValue, destValue, pickerTitle;
    RectTransform pickerList;
    RawImage loadingPartner;

    RawImage chooserMap;
    RectTransform chooserOverlay, chooserCards;
    Button chooserGo;

    Image walkDot, pathToggle;
    Text walkTitle, walkNext, walkReason, walkProgress, pathToggleText;

    Text popupName, popupReason;
    RectTransform popupNotices;
    Button popupBoardButton;
    RawImage miniMap;
    RectTransform miniOverlay;

    RenderTexture miniTexture, chooserTexture;

    // ---------- 조건 시트 ----------

    void ShowSheet()
    {
        EnsureService();
        if (active != null)
        {
            Toast("걷고 있는 코스가 있어요. 아래 판에서 끝내고 새로 고를 수 있어요");
            return;
        }
        if (FreeWalk.Active)
        {
            Toast("자유 산책 중이에요. 아래 판에서 끝낼 수 있어요");
            return;
        }
        if (afterMeal == false && RouteMath.MealOf(System.DateTime.Now) != null)
            afterMeal = true; // 점심 · 저녁 시간대엔 식후 걷기를 켜고 연다
        RefreshChips();
        sheet.SetActive(true);
        sheet.transform.SetAsLastSibling();
        UIInputBlocker.SetModal(this, true);
    }

    void HideSheet()
    {
        sheet.SetActive(false);
        UIInputBlocker.SetModal(this, loading.activeSelf || placePopup.activeSelf || chooser.activeSelf || picker.activeSelf);
    }

    void RefreshChips()
    {
        for (int i = 0; i < timeChips.Length; i++)
            SetChip(timeChips[i], TimeChoices[i].minutes == minutes);
        for (int i = 0; i < tasteChips.Length; i++)
            SetChip(tasteChips[i], tastes.Contains(TasteChoices[i].id));
        string meal = RouteMath.MealOf(System.DateTime.Now);
        SetChip(mealChip, afterMeal);
        UIKit.SetLabel(mealChip.GetComponent<Button>(), meal != null ? $"식후 걷기  ·  지금 {meal}시간" : "식후 걷기");
        mealNote.text = afterMeal ? $"가까운 평지 위주로 골라요.  {RouteMath.HealthNote}" : "";
        startValue.text = $"{Escape(StartLabel)}  ›";
        destValue.text = roundTrip ? "출발지로 돌아오기 (한 바퀴)  ›"
            : destSpot != null ? $"{Escape(destSpot.Building.buildingName)}  ›" : "정하지 않음 (추천대로)  ›";
    }

    static void SetChip(Image chip, bool on)
    {
        chip.color = on ? RouteBlue : ChipOff;
        chip.GetComponentInChildren<Text>().color = on ? Color.white : Ink;
    }

    void BuildSheet()
    {
        sheet = UIKit.Image(canvasRect, "RouteSheet", Dim, raycast: true).gameObject;
        BackButton.Attach(sheet, HideSheet);
        UIKit.Stretch((RectTransform)sheet.transform);
        sheet.AddComponent<Button>().onClick.AddListener(HideSheet); // 바깥을 누르면 닫힘

        Image panel = Card(sheet.transform, "Panel");
        RectTransform pr = panel.rectTransform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(0f, 1560f);
        pr.anchoredPosition = new Vector2(0f, -40f); // 아래 둥근 모서리는 화면 밖으로
        Swallow(panel.gameObject);

        Text title = UIKit.Text(pr, "산책 길잡이", 46, TextAnchor.MiddleLeft, Ink);
        title.fontStyle = FontStyle.Bold;
        Box(title.rectTransform, 50f, 46f, 150f, 70f);
        CloseButton(pr, HideSheet);

        RectTransform rp = UIKit.Rect(pr, "Recommend");
        UIKit.StretchBetween(rp, 40f, 150f);

        Heading(rp, "어디서 어디로 걸을까요?", 0f);
        startValue = PlaceRow(rp, "출발", 70f, () => ShowPicker(true));
        destValue = PlaceRow(rp, "도착", 186f, () => ShowPicker(false));

        Heading(rp, "얼마나 걸을까요?", 320f);
        timeChips = new Image[TimeChoices.Length];
        for (int i = 0; i < TimeChoices.Length; i++)
        {
            int m = TimeChoices[i].minutes;
            Image c = Chip(rp, TimeChoices[i].label, () => { minutes = m; RefreshChips(); }, 32);
            Place(c.rectTransform, new Vector2(0f, 1f), new Vector2(50f + i * 230f, -390f));
            c.rectTransform.sizeDelta = new Vector2(210f, 92f);
            timeChips[i] = c;
        }

        Heading(rp, "무엇을 하고 싶어요?  <size=28><color=#8C8C91>여러 개 고를 수 있어요</color></size>", 520f);
        tasteChips = new Image[TasteChoices.Length];
        for (int i = 0; i < TasteChoices.Length; i++)
        {
            string id = TasteChoices[i].id;
            Image c = Chip(rp, TasteChoices[i].label, () =>
            {
                if (!tastes.Remove(id)) tastes.Add(id);
                RefreshChips();
            }, 32);
            Place(c.rectTransform, new Vector2(0f, 1f), new Vector2(50f + (i % 2) * 330f, -600f - (i / 2) * 114f));
            c.rectTransform.sizeDelta = new Vector2(310f, 92f);
            tasteChips[i] = c;
        }
        mealChip = Chip(rp, "식후 걷기", () => { afterMeal = !afterMeal; RefreshChips(); }, 32);
        Place(mealChip.rectTransform, new Vector2(0f, 1f), new Vector2(50f, -846f));
        mealChip.rectTransform.sizeDelta = new Vector2(500f, 92f);
        mealNote = UIKit.Text(rp, "", 26, TextAnchor.UpperLeft, InkQuiet);
        Box(mealNote.rectTransform, 50f, 952f, 50f, 70f);

        Button go = Pill(rp, "코스 3개 추천 받기", RouteBlue, Color.white, RequestRoutes, 40);
        Bottom((RectTransform)go.transform, 194f, 124f);
        Button free = Pill(rp, "코스 없이 자유 산책 시작", ChipOff, Ink, () => { HideSheet(); FreeWalk.Begin(); }, 34);
        Bottom((RectTransform)free.transform, 60f, 110f);

        sheet.SetActive(false);
    }

    void Heading(RectTransform parent, string text, float top)
    {
        Text t = UIKit.Text(parent, text, 36, TextAnchor.MiddleLeft, Ink);
        t.fontStyle = FontStyle.Bold;
        Box(t.rectTransform, 50f, top, 50f, 60f);
    }

    /// <summary>"출발  내 위치 ›" 처럼 이름표와 값이 있는 한 줄 버튼. 값 글자를 돌려준다</summary>
    Text PlaceRow(RectTransform parent, string label, float top, UnityEngine.Events.UnityAction onClick)
    {
        Image row = UIKit.Image(parent, label, ChipOff, raycast: true);
        row.sprite = HudIcons.Rounded;
        row.type = Image.Type.Sliced;
        row.pixelsPerUnitMultiplier = 0.7f;
        Box(row.rectTransform, 50f, top, 50f, 100f);
        var b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        row.gameObject.AddComponent<PressScale>();
        Text l = UIKit.Text(row.transform, label, 32, TextAnchor.MiddleLeft, RouteBlue);
        l.fontStyle = FontStyle.Bold;
        Box(l.rectTransform, 34f, 0f, 0f, 100f);
        Text v = UIKit.Text(row.transform, "", 34, TextAnchor.MiddleRight, Ink);
        Box(v.rectTransform, 150f, 0f, 34f, 100f);
        return v;
    }

    // ---------- 출발지 · 목적지 고르기 ----------

    bool pickingStart;

    void ShowPicker(bool start)
    {
        pickingStart = start;
        pickerTitle.text = start ? "어디서 출발할까요?" : "어디로 갈까요?";
        for (int i = pickerList.childCount - 1; i >= 0; i--)
            Destroy(pickerList.GetChild(i).gameObject);
        Vector3 me = PlayerPosition;
        if (start)
            PickerRow("내 위치", "지금 있는 곳에서 출발", startSpot == null, () => startSpot = null);
        else
        {
            PickerRow("정하지 않음", "추천에 맡기기 (마지막 장소에서 끝나요)", destSpot == null && !roundTrip, () => { destSpot = null; roundTrip = false; });
            PickerRow("출발지로 돌아오기", $"{StartLabel}에서 출발해 한 바퀴 돌고 다시 돌아와요", roundTrip, () => { destSpot = null; roundTrip = true; });
        }
        List<KyungHeeSpot> list = SpotList;
        list.Sort((a, b) => Flat(me, SpotPosition(a)).CompareTo(Flat(me, SpotPosition(b))));   // 가까운 곳부터
        foreach (KyungHeeSpot s in list)
        {
            KyungHeeSpot spot = s;
            // 출발지와 같은 곳을 목적지로 고르면 한 바퀴 코스가 된다 (FixRoundTrip)
            bool same = start ? spot == destSpot : spot == startSpot;
            bool on = start ? startSpot == spot : destSpot == spot;
            string sub = $"지금 위치에서 {RouteMath.Distance(Flat(me, SpotPosition(spot)))}" + (same ? (start ? " · 도착지와 같은 곳 (한 바퀴)" : " · 출발지 (한 바퀴)") : "");
            PickerRow(spot.Building.buildingName, sub, on, () =>
            {
                if (pickingStart) startSpot = spot; else { destSpot = spot; roundTrip = false; }
                FixRoundTrip();
            });
        }
        picker.SetActive(true);
        picker.transform.SetAsLastSibling();
        UIInputBlocker.SetModal(this, true);
    }

    void PickerRow(string name, string sub, bool on, System.Action pick)
    {
        Image row = UIKit.Image(pickerList, "Row", on ? new Color(0.9f, 0.94f, 1f) : Color.white, raycast: true);
        row.sprite = HudIcons.Rounded;
        row.type = Image.Type.Sliced;
        row.pixelsPerUnitMultiplier = 0.8f;
        UIKit.Size(row, 120f);
        var b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => { pick(); picker.SetActive(false); RefreshChips(); UIInputBlocker.SetModal(this, sheet.activeSelf); });
        Text t = UIKit.Text(row.transform, (on ? "<color=#2A6BF2>✓</color>  " : "") + Escape(name), 36, TextAnchor.UpperLeft, Ink);
        t.fontStyle = FontStyle.Bold;
        Box(t.rectTransform, 34f, 16f, 34f, 50f);
        Text s = UIKit.Text(row.transform, sub, 26, TextAnchor.UpperLeft, InkQuiet);
        Box(s.rectTransform, 34f, 70f, 34f, 40f);
    }

    void BuildPicker()
    {
        picker = UIKit.Image(canvasRect, "RoutePicker", Dim, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)picker.transform);
        picker.AddComponent<Button>().onClick.AddListener(() => { picker.SetActive(false); UIInputBlocker.SetModal(this, sheet.activeSelf); });
        Image card = Card(picker.transform, "Card");
        RectTransform cr = card.rectTransform;
        cr.anchorMin = new Vector2(0.04f, 0.08f);
        cr.anchorMax = new Vector2(0.96f, 0.92f);
        cr.offsetMin = cr.offsetMax = Vector2.zero;
        Swallow(card.gameObject);
        pickerTitle = UIKit.Text(cr, "", 42, TextAnchor.MiddleLeft, Ink);
        pickerTitle.fontStyle = FontStyle.Bold;
        Box(pickerTitle.rectTransform, 50f, 40f, 150f, 70f);
        CloseButton(cr, () => { picker.SetActive(false); UIInputBlocker.SetModal(this, sheet.activeSelf); });
        RectTransform area = UIKit.Rect(cr, "List");
        UIKit.StretchBetween(area, 30f, 140f);
        pickerList = UIKit.ScrollList(area, 12f, new RectOffset(40, 40, 0, 20));
        picker.SetActive(false);
    }

    // ---------- 기다리기 ----------

    void ShowLoading(bool on)
    {
        loading.SetActive(on);
        if (on)
        {
            loading.transform.SetAsLastSibling();
            Texture2D partner = null;
            try
            {
                GameProgress.Caught p = collect != null && collect.Progress != null ? collect.Progress.Partner : null;
                if (p != null) partner = MonsterThumbnails.Get(p.speciesId);
                if (partner == null) partner = MonsterThumbnails.Get(CreatureLibrary.AllSpecies.Count > 0 ? CreatureLibrary.AllSpecies[0] : null);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Route] 파트너 그림을 못 만들었어요: " + e.Message); // 그림이 없어도 추천은 계속한다
            }
            loadingPartner.texture = partner;
            loadingPartner.enabled = partner != null;
        }
        UIInputBlocker.SetModal(this, on || sheet.activeSelf || placePopup.activeSelf || chooser.activeSelf);
    }

    void BuildLoading()
    {
        loading = UIKit.Image(canvasRect, "RouteLoading", Dim, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)loading.transform);
        Image card = Card(loading.transform, "Card");
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        card.rectTransform.sizeDelta = new Vector2(760f, 640f);
        var go = new GameObject("Partner", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(card.transform, false);
        loadingPartner = go.GetComponent<RawImage>();
        loadingPartner.raycastTarget = false;
        Place(loadingPartner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 70f));
        loadingPartner.rectTransform.sizeDelta = new Vector2(280f, 280f);
        Text t = UIKit.Text(card.transform, "코스 3개를 짜고 있어요…", 40, TextAnchor.MiddleCenter, Ink);
        t.fontStyle = FontStyle.Bold;
        Place(t.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 190f));
        t.rectTransform.sizeDelta = new Vector2(700f, 60f);
        Text sub = UIKit.Text(card.transform, "파트너가 길을 찾고 있어요", 30, TextAnchor.MiddleCenter, InkQuiet);
        Place(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f));
        sub.rectTransform.sizeDelta = new Vector2(700f, 44f);
        Button cancel = UIKit.Button(card.transform, "취소", () => { request++; ShowLoading(false); ShowSheet(); }, new Color(0f, 0f, 0f, 0f), 32);
        cancel.GetComponentInChildren<Text>().color = InkQuiet;
        Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f));
        ((RectTransform)cancel.transform).sizeDelta = new Vector2(200f, 84f);
        loading.SetActive(false);
    }

    // ---------- 코스 고르기 (지도 + 카드 3장) ----------

    void ShowChooser(int pick)
    {
        chosen = Mathf.Clamp(pick, 0, options.Count - 1);
        chooser.SetActive(true);
        chooser.transform.SetAsLastSibling();
        UIInputBlocker.SetModal(this, true);
        Canvas.ForceUpdateCanvases();
        DrawChooserMap();
        BuildOptionCards();
        UIKit.SetLabel(chooserGo, $"{options[chosen].variant.label}로 걷기");
        chooserGo.GetComponent<Image>().color = options[chosen].variant.color;
    }

    void HideChooser()
    {
        chooser.SetActive(false);
        UIInputBlocker.SetModal(this, sheet.activeSelf || placePopup.activeSelf);
    }

    void DrawChooserMap()
    {
        var all = new List<Vector3> { StartPosition };
        foreach (Option o in options)
            all.AddRange(o.path);
        Camera cam = RenderTop(chooserMap, ref chooserTexture, all);
        Clear(chooserOverlay);
        if (cam == null)
            return;
        Vector2 size = chooserOverlay.rect.size;
        // 고르지 않은 코스를 먼저 옅게, 고른 코스를 위에 진하게
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < options.Count; i++)
            {
                bool sel = i == chosen;
                if ((pass == 1) != sel) continue;
                Color c = options[i].variant.color;
                if (!sel) c.a = 0.8f;
                float w = sel ? 14f : 9f;
                TopMap.Polyline(chooserOverlay, cam, size, options[i].path, w + 7f, new Color(1f, 1f, 1f, sel ? 0.95f : 0.7f));   // 흰 테두리
                TopMap.Polyline(chooserOverlay, cam, size, options[i].path, w, c, sel);
            }
        Option o2 = options[chosen];
        for (int s = 0; s < o2.stops.Count; s++)
        {
            if (o2.returns && s == o2.stops.Count - 1)
                continue; // 돌아오는 곳은 출발 표시와 같은 자리
            bool last = s == o2.stops.Count - 1 && destSpot != null;
            TopMap.Marker(chooserOverlay, cam, size, o2.stops[s], o2.variant.color, last ? "도착" : (s + 1).ToString(), last ? 74f : 58f);
        }
        TopMap.Marker(chooserOverlay, cam, size, StartPosition, Ink, o2.returns ? "출발\n도착" : "출발", 74f);
    }

    void BuildOptionCards()
    {
        Clear(chooserCards);
        for (int i = 0; i < options.Count; i++)
        {
            int index = i;
            Option o = options[i];
            bool sel = i == chosen;
            Image card = UIKit.Image(chooserCards, "Option", sel ? Color.white : new Color(0.97f, 0.97f, 0.98f), raycast: true);
            card.sprite = HudIcons.Rounded;
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 0.7f;
            var v = UIKit.Vertical(card, 6f, new RectOffset(44, 34, 24, 24));
            card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var b = card.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => ShowChooser(index));
            if (sel)
            {
                Outline ol = card.gameObject.AddComponent<Outline>();
                ol.effectColor = o.variant.color;
                ol.effectDistance = new Vector2(4f, -4f);
            }
            // 왼쪽 색 띠
            Image bar = UIKit.Image(card.transform, "Bar", o.variant.color);
            bar.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            bar.rectTransform.anchorMin = new Vector2(0f, 0f);
            bar.rectTransform.anchorMax = new Vector2(0f, 1f);
            bar.rectTransform.pivot = new Vector2(0f, 0.5f);
            bar.rectTransform.sizeDelta = new Vector2(14f, -30f);
            bar.rectTransform.anchoredPosition = new Vector2(14f, 0f);

            string hex = ColorUtility.ToHtmlStringRGB(o.variant.color);
            string source = o.result.source == "ai" ? "AI 추천" : "기본 추천";
            Text head = UIKit.Text(card.transform, $"<color=#{hex}>{o.variant.label}</color>   <size=26><color=#8C8C91>{source}</color></size>", 36, TextAnchor.UpperLeft, Ink);
            head.fontStyle = FontStyle.Bold;
            int visits = o.result.stops.Count - (o.returns ? 1 : 0);
            UIKit.Text(card.transform, $"{RouteMath.Distance(o.meters)}  ·  약 {o.walkMinutes}분  ·  {visits}곳" + (o.returns ? "  ·  한 바퀴" : ""), 30, TextAnchor.UpperLeft, Ink);
            var names = o.result.stops.ConvertAll(s => Escape(s.name));
            Text route = UIKit.Text(card.transform, $"{Escape(StartLabel)} → " + string.Join(" → ", names), 27, TextAnchor.UpperLeft, InkQuiet);
            route.horizontalOverflow = HorizontalWrapMode.Wrap;
            if (sel)
            {
                // 고른 코스는 장소마다 이유까지
                for (int s = 0; s < o.result.stops.Count; s++)
                {
                    RouteStop st = o.result.stops[s];
                    string dist = st.distanceM > 0 ? $"  <color=#8C8C91>· {RouteMath.Distance(st.distanceM)} · {st.walkMinutes}분</color>" : "";
                    Text line = UIKit.Text(card.transform, $"<b><color=#{hex}>{s + 1}</color>  {Escape(st.name)}</b>{dist}\n<size=26>{Escape(st.reason)}</size>", 30, TextAnchor.UpperLeft, Ink);
                    line.horizontalOverflow = HorizontalWrapMode.Wrap;
                    line.lineSpacing = 1.05f;
                }
                if (!string.IsNullOrEmpty(o.result.healthNote))
                    UIKit.Text(card.transform, Escape(o.result.healthNote), 24, TextAnchor.UpperLeft, InkQuiet).horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }
    }

    void BuildChooser()
    {
        chooser = UIKit.Image(canvasRect, "RouteChooser", Dim, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)chooser.transform);
        Image card = Card(chooser.transform, "Card");
        RectTransform cr = card.rectTransform;
        cr.anchorMin = Vector2.zero;
        cr.anchorMax = Vector2.one;
        cr.offsetMin = new Vector2(24f, 30f);
        cr.offsetMax = new Vector2(-24f, -110f);
        Swallow(card.gameObject);
        Text title = UIKit.Text(cr, "코스를 골라요", 44, TextAnchor.MiddleLeft, Ink);
        title.fontStyle = FontStyle.Bold;
        Box(title.rectTransform, 46f, 30f, 150f, 70f);
        CloseButton(cr, () => { HideChooser(); ShowSheet(); });

        Image frame = UIKit.Image(cr, "Map", new Color(0.55f, 0.72f, 0.42f));
        frame.sprite = HudIcons.Rounded;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1f;
        Box(frame.rectTransform, 30f, 120f, 30f, 760f);
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var mm = new GameObject("Texture", typeof(RectTransform), typeof(RawImage));
        mm.transform.SetParent(frame.transform, false);
        chooserMap = mm.GetComponent<RawImage>();
        chooserMap.raycastTarget = false;
        UIKit.Stretch(chooserMap.rectTransform);
        chooserOverlay = UIKit.Rect(frame.transform, "Routes");
        UIKit.Stretch(chooserOverlay);

        RectTransform area = UIKit.Rect(cr, "Options");
        UIKit.StretchBetween(area, 190f, 900f);
        chooserCards = UIKit.ScrollList(area, 18f, new RectOffset(30, 30, 0, 20));

        chooserGo = Pill(cr, "이 코스로 걷기", RouteBlue, Color.white, () => StartWalk(options[chosen]), 40);
        Bottom((RectTransform)chooserGo.transform, 40f, 124f);
        chooser.SetActive(false);
    }

    // ---------- 걷는 중 판 ----------

    void RefreshWalkPanel()
    {
        if (active == null)
            return;
        Option o = active;
        walkDot.color = o.variant.color;
        walkTitle.text = $"{o.variant.label}  <size=28><color=#8C8C91>{RouteMath.Distance(o.meters)} · 약 {o.walkMinutes}분</color></size>";
        int next = done.FindIndex(x => !x);
        int count = done.FindAll(x => x).Count;
        if (next >= 0)
        {
            RouteStop s = o.result.stops[next];
            float left = Flat(PlayerPosition, o.stops[next]);
            bool last = next == o.result.stops.Count - 1;
            walkNext.text = $"<color=#{ColorUtility.ToHtmlStringRGB(o.variant.color)}>{(last ? "마지막" : "다음")} {next + 1}</color>  {Escape(s.name)}  <size=30><color=#8C8C91>· {RouteMath.Distance(left)}</color></size>";
            walkReason.text = Escape(s.reason);
        }
        walkProgress.text = $"{count}/{o.result.stops.Count}곳 들렀어요";
        pathToggle.color = showPath ? o.variant.color : ChipOff;
        pathToggleText.color = showPath ? Color.white : Ink;
        pathToggleText.text = showPath ? "경로 켜짐" : "경로 꺼짐";
    }

    void BuildWalkPanel()
    {
        walkPanel = Card(canvasRect, "WalkPanel").gameObject;
        RectTransform pr = (RectTransform)walkPanel.transform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.sizeDelta = new Vector2(-48f, 360f);
        pr.anchoredPosition = new Vector2(0f, 320f);
        UIInputBlocker.Register(pr);

        walkDot = UIKit.Image(pr, "Dot", RouteBlue);
        walkDot.sprite = HudIcons.Circle;
        Place(walkDot.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -42f));
        walkDot.rectTransform.sizeDelta = new Vector2(28f, 28f);
        walkTitle = UIKit.Text(pr, "", 32, TextAnchor.MiddleLeft, Ink);
        walkTitle.fontStyle = FontStyle.Bold;
        Box(walkTitle.rectTransform, 84f, 26f, 380f, 60f);

        Button toggle = Pill(pr, "경로 켜짐", RouteBlue, Color.white, () => { showPath = !showPath; RefreshWalkPanel(); }, 26);
        pathToggle = toggle.GetComponent<Image>();
        pathToggleText = toggle.GetComponentInChildren<Text>();
        Place((RectTransform)toggle.transform, new Vector2(1f, 1f), new Vector2(-200f, -24f));
        ((RectTransform)toggle.transform).sizeDelta = new Vector2(170f, 64f);
        Button end = Pill(pr, "끝내기", ChipOff, Ink, () => EndRoute(true), 26);
        Place((RectTransform)end.transform, new Vector2(1f, 1f), new Vector2(-24f, -24f));
        ((RectTransform)end.transform).sizeDelta = new Vector2(160f, 64f);

        // 다음 장소 (누르면 장소 팝업)
        Image next = UIKit.Image(pr, "Next", new Color(0f, 0f, 0f, 0f), raycast: true);
        Box(next.rectTransform, 30f, 100f, 30f, 200f);
        var nb = next.gameObject.AddComponent<Button>();
        nb.transition = Selectable.Transition.None;
        nb.onClick.AddListener(() => { int i = done.FindIndex(x => !x); OpenPlace(i < 0 ? 0 : i); });
        walkNext = UIKit.Text(next.transform, "", 40, TextAnchor.UpperLeft, Ink);
        walkNext.fontStyle = FontStyle.Bold;
        Box(walkNext.rectTransform, 10f, 0f, 10f, 60f);
        walkReason = UIKit.Text(next.transform, "", 28, TextAnchor.UpperLeft, InkQuiet);
        walkReason.verticalOverflow = VerticalWrapMode.Truncate;
        Box(walkReason.rectTransform, 10f, 66f, 10f, 84f);
        walkProgress = UIKit.Text(next.transform, "", 26, TextAnchor.UpperLeft, RouteBlue);
        Box(walkProgress.rectTransform, 10f, 156f, 10f, 40f);
        walkPanel.SetActive(false);
    }

    // ---------- 장소 팝업 (미니맵 + 공지) ----------

    void OpenPlace(int index)
    {
        if (active == null || index < 0 || index >= active.result.stops.Count)
            return;
        RouteStop s = active.result.stops[index];
        string hex = ColorUtility.ToHtmlStringRGB(active.variant.color);
        popupName.text = $"<color=#{hex}>{index + 1}</color>  {Escape(s.name)}";
        popupReason.text = $"{Escape(s.reason)}  ·  도보 {s.walkMinutes}분";
        Clear(popupNotices);
        List<PlaceNotice> list = NoticesOf(s.buildingId);
        if (list.Count == 0)
            UIKit.Size(UIKit.Text(popupNotices, "이 장소의 공지는 아직 없어요", 30, TextAnchor.MiddleCenter, InkQuiet), 90f);
        foreach (PlaceNotice n in list)
            NoticeRow(popupNotices, n, false);
        KyungHeeSpot spot = SpotOf(s.buildingId);
        popupBoardButton.gameObject.SetActive(spot != null);
        popupBoardButton.onClick.RemoveAllListeners();
        popupBoardButton.onClick.AddListener(() =>
        {
            ClosePlace();
            var board = FindAnyObjectByType<MemoBoard>();
            if (board != null && spot != null) board.OpenSpot(spot);
        });
        placePopup.SetActive(true);
        placePopup.transform.SetAsLastSibling();
        UIInputBlocker.SetModal(this, true);
        Canvas.ForceUpdateCanvases();
        DrawPlaceMap(index);
    }

    void ClosePlace()
    {
        placePopup.SetActive(false);
        UIInputBlocker.SetModal(this, sheet.activeSelf || loading.activeSelf || chooser.activeSelf);
    }

    void DrawPlaceMap(int focus)
    {
        var all = new List<Vector3>(active.path) { PlayerPosition };
        Camera cam = RenderTop(miniMap, ref miniTexture, all);
        Clear(miniOverlay);
        if (cam == null)
            return;
        Vector2 size = miniOverlay.rect.size;
        TopMap.Polyline(miniOverlay, cam, size, active.path, 19f, new Color(1f, 1f, 1f, 0.95f));
        TopMap.Polyline(miniOverlay, cam, size, active.path, 12f, active.variant.color, true);
        for (int i = 0; i < active.stops.Count; i++)
        {
            bool arrived = i < done.Count && done[i];
            TopMap.Marker(miniOverlay, cam, size, active.stops[i], arrived ? DoneGreen : active.variant.color, arrived ? "✓" : (i + 1).ToString(), i == focus ? 84f : 58f);
        }
        TopMap.Marker(miniOverlay, cam, size, PlayerPosition, Ink, "나", 58f);
    }

    void BuildPlacePopup()
    {
        placePopup = UIKit.Image(canvasRect, "PlacePopup", Dim, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)placePopup.transform);
        placePopup.AddComponent<Button>().onClick.AddListener(ClosePlace);
        Image card = Card(placePopup.transform, "Card");
        RectTransform cr = card.rectTransform;
        cr.anchorMin = new Vector2(0.05f, 0.5f);
        cr.anchorMax = new Vector2(0.95f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(0f, 1560f);
        Swallow(card.gameObject);
        popupName = UIKit.Text(cr, "", 44, TextAnchor.MiddleLeft, Ink);
        popupName.fontStyle = FontStyle.Bold;
        Box(popupName.rectTransform, 50f, 46f, 140f, 64f);
        CloseButton(cr, ClosePlace);
        popupReason = UIKit.Text(cr, "", 30, TextAnchor.UpperLeft, InkQuiet);
        Box(popupReason.rectTransform, 50f, 116f, 50f, 50f);

        Image frame = UIKit.Image(cr, "MiniMap", new Color(0.55f, 0.72f, 0.42f));
        frame.sprite = HudIcons.Rounded;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1f;
        Box(frame.rectTransform, 50f, 186f, 50f, 680f);
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var mm = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
        mm.transform.SetParent(frame.transform, false);
        miniMap = mm.GetComponent<RawImage>();
        miniMap.raycastTarget = false;
        UIKit.Stretch(miniMap.rectTransform);
        miniOverlay = UIKit.Rect(frame.transform, "Route");
        UIKit.Stretch(miniOverlay);

        Text head = UIKit.Text(cr, "장소 공지", 32, TextAnchor.MiddleLeft, Ink);
        head.fontStyle = FontStyle.Bold;
        Box(head.rectTransform, 50f, 896f, 50f, 50f);
        RectTransform area = UIKit.Rect(cr, "List");
        UIKit.StretchBetween(area, 190f, 960f);
        popupNotices = UIKit.ScrollList(area, 16f, new RectOffset(50, 50, 0, 20));
        popupBoardButton = Pill(cr, "이 장소 쪽지판 열기", Ink, Color.white, () => { }, 34);
        Bottom((RectTransform)popupBoardButton.transform, 50f, 110f);
        placePopup.SetActive(false);
    }

    /// <summary>공지 한 줄: (장소) 제목 · 요약 · 날짜 · 원문 보기. 누르면 원문을 연다 (메모판 공지 탭도 씀)</summary>
    internal static void NoticeRow(RectTransform parent, PlaceNotice n, bool withPlace)
    {
        Image row = UIKit.Image(parent, "Notice", ChipOff, raycast: true);
        row.sprite = HudIcons.Rounded;
        row.type = Image.Type.Sliced;
        row.pixelsPerUnitMultiplier = 0.8f;
        UIKit.Vertical(row, 8f, new RectOffset(32, 32, 26, 26));
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        string url = n.url;
        b.onClick.AddListener(() => { if (!string.IsNullOrEmpty(url)) Application.OpenURL(url); });
        if (withPlace)
        {
            Text place = UIKit.Text(row.transform, n.buildingName, 26, TextAnchor.UpperLeft, RouteBlue);
            place.fontStyle = FontStyle.Bold;
        }
        Text title = UIKit.Text(row.transform, n.title, 32, TextAnchor.UpperLeft, Ink);
        title.fontStyle = FontStyle.Bold;
        if (!string.IsNullOrEmpty(n.summary))
            UIKit.Text(row.transform, n.summary, 27, TextAnchor.UpperLeft, InkQuiet);
        UIKit.Text(row.transform, $"{n.postedAt}   <color=#2A6BF2>원문 보기 ›</color>", 26, TextAnchor.UpperLeft, InkQuiet);
    }

    // ---------- 위에서 본 지도 ----------

    /// <summary>위에서 본 지도를 찍는다. 본 지도의 코스 선은 찍히지 않게 잠깐 숨긴다 (위에 다시 그린다)</summary>
    Camera RenderTop(RawImage target, ref RenderTexture texture, List<Vector3> points)
    {
        if (guide != null) guide.SetVisible(false);
        Camera cam = TopMap.Render(target, ref texture, points);
        if (guide != null) guide.SetVisible(active != null && showPath);
        return cam;
    }

    // ---------- 작은 도우미 ----------

    static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    static Image Card(Transform parent, string name)
    {
        Image card = UIKit.Image(parent, name, Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        return card;
    }

    static void CloseButton(RectTransform parent, UnityEngine.Events.UnityAction onClick)
    {
        Image close = UIKit.Image(parent, "Close", new Color(0f, 0f, 0f, 0f), raycast: true);
        Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -36f));
        close.rectTransform.sizeDelta = new Vector2(84f, 84f);
        close.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
        Image ci = UIKit.Image(close.transform, "Icon", Ink);
        ci.sprite = HudIcons.Close;
        Place(ci.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        ci.rectTransform.sizeDelta = new Vector2(40f, 40f);
    }

    static Button Pill(Transform parent, string label, Color color, Color textColor, UnityEngine.Events.UnityAction onClick, int fontSize)
    {
        Button b = UIKit.Button(parent, label, onClick, color, fontSize);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        Text t = b.GetComponentInChildren<Text>();
        t.color = textColor;
        t.fontStyle = FontStyle.Bold;
        b.gameObject.AddComponent<PressScale>();
        return b;
    }

    static void Bottom(RectTransform rt, float bottom, float height)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(-100f, height);
        rt.anchoredPosition = new Vector2(0f, bottom);
    }

    static Image Circle(Transform parent, Color color, float size, string label)
    {
        Image c = UIKit.Image(parent, "Badge", color);
        c.sprite = HudIcons.Circle;
        c.rectTransform.anchorMin = c.rectTransform.anchorMax = c.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        c.rectTransform.sizeDelta = new Vector2(size, size);
        Text t = UIKit.Text(c.transform, label, Mathf.RoundToInt(size * 0.48f), TextAnchor.MiddleCenter, Color.white);
        t.fontStyle = FontStyle.Bold;
        UIKit.Stretch(t.rectTransform);
        return c;
    }

    static Image Chip(Transform parent, string label, UnityEngine.Events.UnityAction onClick, int fontSize)
    {
        Button b = UIKit.Button(parent, label, onClick, ChipOff, fontSize);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 120f / 92f;
        b.GetComponentInChildren<Text>().color = Ink;
        b.gameObject.AddComponent<PressScale>();
        return img;
    }

    static void Swallow(GameObject go) => go.AddComponent<Button>().transition = Selectable.Transition.None;

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    /// <summary>부모 왼쪽 위에서 (left, top) 만큼 떨어지고 오른쪽 여백 right, 높이 height 인 칸</summary>
    static void Box(RectTransform rt, float left, float top, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
