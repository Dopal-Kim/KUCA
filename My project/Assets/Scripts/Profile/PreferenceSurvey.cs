using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 산책 취향 체크리스트 화면. 한 화면에 질문 하나씩, 7단계 (디자인 v4: 토스 단계 질문 — 진행 막대 · 큰 질문 · 흰 카드 선택지 · 아래 고정 "다음").
/// 첫 실행 때 자동으로 열리고 (나중에는 로그인 직후에 Open 을 부르면 된다), 설정 › 취향 다시 고르기에서도 연다.
/// 답은 WalkPreferences 로 기기에 저장한다 (AI 산책메이트 매칭에 쓸 자료).
/// </summary>
public class PreferenceSurvey : MonoBehaviour
{

    class Question
    {
        public string title, hint;
        public bool multi;
        public int max = 1;
        public (string code, string label, string sub)[] options;
    }

    static readonly Question[] Questions =
    {
        new Question { title = "언제 주로 걸어요?", hint = "여러 개 고를 수 있어요", multi = true, max = 4, options = new[]
            { ("morning", "아침", "6~11시"), ("day", "낮", "11~17시"), ("evening", "저녁", "17~20시"), ("night", "밤", "20시 이후") } },
        new Question { title = "한 번에 얼마나 걸어요?", hint = "하나만 골라요", options = new[]
            { ("15", "15분", "잠깐 바람 쐬기"), ("30", "30분", "캠퍼스 반 바퀴"), ("60", "1시간 이상", "느긋하게 한 바퀴") } },
        new Question { title = "걷는 속도는요?", hint = "하나만 골라요", options = new[]
            { ("slow", "느긋하게", "구경하며 천천히"), ("normal", "보통", ""), ("fast", "빠르게", "운동하듯 성큼성큼") } },
        new Question { title = "산책하면서 하고 싶은 것", hint = "최대 3개까지 골라요", multi = true, max = 3, options = new[]
            { ("photo", "사진 명소", ""), ("quiet", "조용한 곳", ""), ("food", "맛집·카페", ""),
              ("unvisited", "안 가 본 곳", ""), ("partner", "파트너와 걷기", ""), ("exercise", "운동", "") } },
        new Question { title = "같이 걸을 때 분위기는요?", hint = "하나만 골라요", options = new[]
            { ("chatty", "수다 떨며", "이야기하는 게 좋아요"), ("some", "가끔 대화", "편하게 한두 마디"), ("quiet", "조용히 각자", "옆에서 같이 걷기만") } },
        new Question { title = "몇 명이서 걷는 게 좋아요?", hint = "하나만 골라요", options = new[]
            { ("one", "1:1", "한 명과 같이"), ("small", "3~4명 소그룹", "여럿이 함께"), ("any", "상관없어요", "") } },
        new Question { title = "같이 걸을 상대는요?", hint = "하나만 골라요", options = new[]
            { ("same", "동성만", "나와 같은 성별인 사람과만"), ("any", "상관없어요", "") } },
    };

    GameObject root;
    Text stepText, titleText, hintText;
    RectTransform optionList, progressFill;
    Button nextButton;
    int step;
    List<string>[] answers;
    Action onDone;

    /// <summary>체크리스트를 연다. 저장된 답이 있으면 그 값으로 채워 둔다.</summary>
    public static void Open(Action done = null)
    {
        PreferenceSurvey s = FindAnyObjectByType<PreferenceSurvey>();
        if (s == null)
            s = new GameObject("PreferenceSurvey").AddComponent<PreferenceSurvey>();
        s.Show(done);
    }

    /// <summary>아직 한 번도 답하지 않았으면 연다 (첫 실행).</summary>
    public static void OpenIfNeeded()
    {
        if (!WalkPreferences.HasSaved)
            Open();
    }

    void Awake() => Build();

    void Show(Action done)
    {
        onDone = done;
        WalkPreferences p = WalkPreferences.Load();
        answers = new[]
        {
            new List<string>(p.walkTimes),
            p.walkMinutes > 0 ? new List<string> { p.walkMinutes.ToString() } : new List<string>(),
            Single(p.pace), new List<string>(p.tastes), Single(p.talkStyle), Single(p.groupSize), Single(p.matchGender),
        };
        step = 0;
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);
        Fill();
    }

    static List<string> Single(string v) => string.IsNullOrEmpty(v) ? new List<string>() : new List<string> { v };

    void Fill()
    {
        Question q = Questions[step];
        stepText.text = $"{step + 1} / {Questions.Length}";
        titleText.text = q.title;
        hintText.text = q.hint;
        progressFill.anchorMax = new Vector2((step + 1) / (float)Questions.Length, 1f);
        UIKit.SetLabel(nextButton, step == Questions.Length - 1 ? "완료" : "다음");

        for (int i = optionList.childCount - 1; i >= 0; i--)
            Destroy(optionList.GetChild(i).gameObject);
        List<string> chosen = answers[step];
        foreach (var (code, label, sub) in q.options)
        {
            string c = code;
            bool on = chosen.Contains(code);
            // 흰 카드 행: 고르면 초록 테두리 + 옅은 초록 바탕 + 오른쪽 초록 ✓ 원
            Image row = UIKit.Image(optionList, code, on ? KucaTheme.PrimarySoft : KucaTheme.Card, raycast: true);
            row.sprite = HudIcons.Rounded;
            row.type = Image.Type.Sliced;
            row.pixelsPerUnitMultiplier = 0.4f;
            Outline edge = row.gameObject.AddComponent<Outline>();
            edge.effectColor = on ? KucaTheme.Primary : KucaTheme.Line;
            edge.effectDistance = on ? new Vector2(4f, 4f) : new Vector2(2f, 2f);
            UIKit.Size(row, sub.Length > 0 ? 184f : 148f);
            var b = row.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => Toggle(c));
            row.gameObject.AddComponent<PressScale>();
            Text t = UIKit.Bold(UIKit.Text(row.transform, label, KucaTheme.Body, sub.Length > 0 ? TextAnchor.LowerLeft : TextAnchor.MiddleLeft, KucaTheme.Ink));
            t.rectTransform.anchorMin = new Vector2(0f, sub.Length > 0 ? 0.5f : 0f);
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = new Vector2(48f, 0f);
            t.rectTransform.offsetMax = new Vector2(-150f, sub.Length > 0 ? -4f : 0f);
            if (sub.Length > 0)
            {
                Text st = UIKit.Text(row.transform, sub, KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
                st.rectTransform.anchorMin = Vector2.zero;
                st.rectTransform.anchorMax = new Vector2(1f, 0.5f);
                st.rectTransform.offsetMin = new Vector2(48f, 0f);
                st.rectTransform.offsetMax = new Vector2(-150f, -8f);
            }
            Image mark = UIKit.Image(row.transform, "Mark", on ? KucaTheme.Primary : KucaTheme.Fill);
            mark.sprite = HudIcons.Circle;
            if (!on)
            {
                Outline ring = mark.gameObject.AddComponent<Outline>();
                ring.effectColor = KucaTheme.InkSoft;
                ring.effectDistance = new Vector2(2f, 2f);
            }
            mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = mark.rectTransform.pivot = new Vector2(1f, 0.5f);
            mark.rectTransform.anchoredPosition = new Vector2(-48f, 0f);
            mark.rectTransform.sizeDelta = new Vector2(64f, 64f);
            if (on)
            {
                Text check = UIKit.Bold(UIKit.Text(mark.transform, "✓", KucaTheme.Label, TextAnchor.MiddleCenter, Color.white));
                UIKit.Stretch(check.rectTransform);
            }
        }
        Text why = UIKit.Text(optionList, "AI 산책메이트를 찾아 줄 때만 써요. 설정에서 언제든 바꿀 수 있어요.", KucaTheme.Caption, TextAnchor.MiddleLeft, KucaTheme.InkSoft);
        UIKit.Size(why, 90f);

        bool ready = chosen.Count > 0;
        nextButton.interactable = ready;
        nextButton.GetComponent<Image>().color = ready ? KucaTheme.Primary : KucaTheme.Line;
        nextButton.GetComponentInChildren<Text>().color = ready ? Color.white : KucaTheme.InkSoft;   // 비활성: 회색 칸 + 흐린 글자
    }

    void Toggle(string code)
    {
        Question q = Questions[step];
        List<string> chosen = answers[step];
        if (!q.multi)
        {
            chosen.Clear();
            chosen.Add(code);
        }
        else if (!chosen.Remove(code))
        {
            if (chosen.Count >= q.max)
            {
                Toast($"최대 {q.max}개까지 고를 수 있어요");
                return;
            }
            chosen.Add(code);
        }
        Fill();
    }

    void Next()
    {
        if (answers[step].Count == 0)
            return;
        if (step < Questions.Length - 1)
        {
            step++;
            Fill();
            return;
        }
        var p = new WalkPreferences
        {
            walkTimes = new List<string>(answers[0]),
            walkMinutes = int.TryParse(answers[1][0], out int m) ? m : 30,
            pace = answers[2][0],
            tastes = new List<string>(answers[3]),
            talkStyle = answers[4][0],
            groupSize = answers[5][0],
            matchGender = answers[6][0],
        };
        p.Save();
        Debug.Log("[Preferences] " + p.ToJson());
        if (AuthService.HasToken && AuthService.HasServer)
            StartCoroutine(AuthService.SavePreferences(p, (_, error) => { if (error != null) Toast("취향을 서버에 올리지 못했어요: " + error); }));
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
        Toast("산책 취향을 저장했어요");
        onDone?.Invoke();
    }

    void Back()
    {
        if (step == 0) return;
        step--;
        Fill();
    }

    /// <summary>안드로이드 뒤로 가기: 앞 질문으로, 첫 질문이면 저장하지 않고 닫는다 (첫 실행이면 다음에 다시 뜬다)</summary>
    void BackOrClose()
    {
        if (step > 0)
        {
            Back();
            return;
        }
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
    }

    static void Toast(string message)
    {
        var hud = FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.Toast(message);
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "SurveyCanvas", 45);
        // 위 ‹ 와 안드로이드 뒤로: 앞 질문으로, 첫 질문이면 닫기
        UIKit.Page page = UIKit.CreatePage(canvas.transform, "Survey", " ", BackOrClose, " ", bottomButtons: 1);
        root = page.root;
        titleText = page.title;
        hintText = page.subtitle;

        // 진행 막대 + "n / 7" (뒤로 ‹ 옆)
        Image track = UIKit.Image(page.header, "Progress", KucaTheme.Line);
        track.sprite = HudIcons.Pill;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 8f;
        RectTransform tr = track.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f);
        tr.anchorMax = Vector2.one;
        tr.pivot = new Vector2(0.5f, 1f);
        tr.offsetMin = new Vector2(KucaTheme.Gutter + 140f, -86f);
        tr.offsetMax = new Vector2(-KucaTheme.Gutter - 150f, -70f);
        Image fill = UIKit.Image(track.transform, "Fill", KucaTheme.Primary);
        fill.sprite = HudIcons.Pill;
        fill.type = Image.Type.Sliced;
        fill.pixelsPerUnitMultiplier = 8f;
        progressFill = fill.rectTransform;
        progressFill.anchorMin = Vector2.zero;
        progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
        stepText = UIKit.Bold(UIKit.Text(page.header, "", KucaTheme.Label, TextAnchor.MiddleRight, KucaTheme.InkMuted));
        Box(stepText.rectTransform, 0f, 40f, KucaTheme.Gutter, 76f);
        stepText.rectTransform.offsetMin = new Vector2(-KucaTheme.Gutter - 130f, stepText.rectTransform.offsetMin.y);
        stepText.rectTransform.anchorMin = new Vector2(1f, 1f);

        optionList = page.content;
        ((VerticalLayoutGroup)optionList.GetComponent<VerticalLayoutGroup>()).spacing = KucaTheme.Space3;
        nextButton = UIKit.PrimaryButton(page.bottom, "다음", Next);
    }

    static void Box(RectTransform rt, float left, float top, float right, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
