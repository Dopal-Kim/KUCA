using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인 (필수, 또는 체험 모드). 앱이 켜지면 저장된 토큰을 확인하고, 없거나 만료면 로그인 화면을 띄운다.
/// 흐름: (첫 실행) 개인정보 안내 → 카카오 / Apple 로그인 → 경희대 메일 인증 → 닉네임 → 성별 → 산책 취향 체크리스트 → 시작.
/// 체험 모드("로그인 없이 둘러보기")는 서버가 만든 체험 계정으로 메일 인증 · 닉네임을 건너뛰고 성별만 받는다.
/// 이미 끝낸 단계는 건너뛴다 (예: 토큰이 살아 있고 모두 끝났으면 바로 시작).
/// 디자인 v4 (docs/디자인_v4.md): 토스 · 카카오 로그인 — 흰 바탕, 왼쪽 큰 문구, 화면 아래 꽉 찬 카카오 버튼, 그 아래 글자 버튼.
/// </summary>
public class LoginScreen : MonoBehaviour
{
    static readonly Color Ink = KucaTheme.Ink;
    static readonly Color InkQuiet = KucaTheme.InkMuted;
    static readonly Color Accent = KucaTheme.Primary;
    // 카카오 로그인 버튼 브랜드 규칙: #FEE500 바탕 · #191919 글자 (85%) · 말풍선 아이콘
    static readonly Color KakaoYellow = new Color(0xFE / 255f, 0xE5 / 255f, 0f);
    static readonly Color KakaoInk = new Color(0x19 / 255f, 0x19 / 255f, 0x19 / 255f);
    static readonly Color Field = KucaTheme.Fill;
    static readonly Color ErrorRed = KucaTheme.Crimson;

    const string PrivacySeenKey = "privacy_seen";

    static readonly string[] HeroSpecies = { "pigeon", "library_owl", "squirrel" };
    readonly RawImage[] hero = new RawImage[3];
    GameObject root, checking, loginPage, emailPage, nickPage, genderPage, privacyPage;
    Button privacyButton;
    bool privacyOnly; // 설정에서 열었으면 확인을 눌러도 닫기만 한다
    Text checkingText, errorText, emailHint, codeHint;
    InputField emailInput, codeInput, nickInput;
    Button sendButton, verifyButton, nickButton, retryButton;
    GameObject codeRow;
    bool busy;
    float resendAt;

    /// <summary>앱 시작 때 한 번 (GameHUD 가 부른다). 로그아웃한 뒤에도 다시 부른다.</summary>
    public static void Begin()
    {
        LoginScreen s = FindAnyObjectByType<LoginScreen>();
        if (s == null)
            s = new GameObject("LoginScreen").AddComponent<LoginScreen>();
        s.StartFlow();
    }

    /// <summary>설정의 "개인정보 안내": 안내 화면만 띄웠다가 닫는다.</summary>
    public static void ShowPrivacy()
    {
        LoginScreen s = FindAnyObjectByType<LoginScreen>();
        if (s == null)
            s = new GameObject("LoginScreen").AddComponent<LoginScreen>();
        s.privacyOnly = true;
        s.root.SetActive(true);
        UIInputBlocker.SetModal(s, true);
        s.errorText.text = "";
        UIKit.SetLabel(s.privacyButton, "닫기");
        s.Show(s.privacyPage);
    }

    void Awake() => Build();

    void ConfirmPrivacy()
    {
        PlayerPrefs.SetInt(PrivacySeenKey, 1);
        PlayerPrefs.Save();
        if (privacyOnly)
        {
            privacyOnly = false;
            root.SetActive(false);
            UIInputBlocker.SetModal(this, false);
            return;
        }
        StartFlow();
    }

    void StartFlow()
    {
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);
        errorText.text = "";
        privacyOnly = false;
        if (PlayerPrefs.GetInt(PrivacySeenKey, 0) == 0)
        {
            UIKit.SetLabel(privacyButton, "확인하고 시작");
            Show(privacyPage);
            return;
        }
        if (!AuthService.HasServer)
        {
            // 서버 주소가 없는 가짜 서버 모드(개발용): 계정을 만들 곳이 없어 로그인 없이 진행한다.
            Finish();
            return;
        }
        if (!AuthService.HasToken)
        {
            Show(loginPage);
            return;
        }
        Show(checking);
        checkingText.text = "로그인 정보를 확인하고 있어요…";
        retryButton.gameObject.SetActive(false);
        StartCoroutine(AuthService.FetchMe((account, error, invalid) =>
        {
            if (account != null) { Continue(account); return; }
            if (invalid) { Show(loginPage); return; }
            checkingText.text = error ?? "서버에 연결하지 못했어요";
            retryButton.gameObject.SetActive(true); // 로그인 필수라 다시 시도만 할 수 있다
        }));
    }

    /// <summary>남은 단계로 간다.</summary>
    void Continue(KucaAccount a)
    {
        errorText.text = "";
        if (!a.emailVerified && !a.isGuest)
        {
            Show(emailPage);
            codeRow.SetActive(false);
            emailHint.text = "경희대 학생만 쓸 수 있어요. 학교 메일로 인증번호를 보내 드려요.";
            return;
        }
        if (string.IsNullOrWhiteSpace(a.nickname))
        {
            Show(nickPage);
            nickInput.text = PlayerPrefs.GetString("memo_author", "");
            return;
        }
        PlayerPrefs.SetString("memo_author", a.nickname); // 메모·HUD 에 쓰는 닉네임
        PlayerPrefs.Save();
        if (string.IsNullOrEmpty(a.gender))
        {
            Show(genderPage);
            return;
        }
        if (!PreferencesComplete(a.preferencesJson)) // 없거나, 새 문항(매칭 상대)에 아직 답하지 않음
        {
            WalkPreferences local = WalkPreferences.Load();
            if (local.IsComplete)
            {
                // 예전에 기기에서 답한 체크리스트가 있으면 그대로 올린다.
                StartCoroutine(AuthService.SavePreferences(local, (_, __) => Finish()));
                return;
            }
            root.SetActive(false); // 체크리스트 화면이 위에 뜬다 (저장하면 서버에도 올린다)
            PreferenceSurvey.Open(Finish);
            return;
        }
        Finish();
    }

    static bool PreferencesComplete(string json)
    {
        if (string.IsNullOrEmpty(json))
            return false;
        try { return JsonUtility.FromJson<WalkPreferences>(json)?.IsComplete ?? false; }
        catch (System.ArgumentException) { return false; }
    }

    void Finish()
    {
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
        if (AuthService.Current != null && !string.IsNullOrEmpty(AuthService.Current.nickname))
            Toast($"{AuthService.Current.nickname}님, 반가워요!");
    }

    // ---------- 로그인 ----------

    void LoginWith(string provider)
    {
        if (busy) return;
        busy = true;
        errorText.text = "로그인하는 중…";
        System.Action<string, string> afterNative = (token, error) =>
        {
            if (token == null)
            {
                busy = false;
                errorText.text = error == NativeLogin.Cancelled ? "" : error;
                return;
            }
            StartCoroutine(AuthService.Login(provider, token, (account, err) =>
            {
                busy = false;
                if (account == null) { errorText.text = err; return; }
                Continue(account);
            }));
        };
        if (provider == "kakao") NativeLogin.Kakao(afterNative);
        else if (provider == "apple") NativeLogin.Apple(afterNative);
        StopCoroutine(nameof(LoginTimeout));
        StartCoroutine(nameof(LoginTimeout));
    }

    /// <summary>체험 모드: 서버가 체험 계정을 만들어 준다 (메일 인증 없음, 7일 뒤 삭제).</summary>
    void StartGuest()
    {
        if (busy) return;
        busy = true;
        errorText.text = "체험 계정을 만드는 중…";
        StartCoroutine(AuthService.Login("guest", null, (account, err) =>
        {
            busy = false;
            if (account == null) { errorText.text = err; return; }
            errorText.text = "";
            Continue(account);
        }));
    }

    /// <summary>로그인 앱에서 돌아왔는데 답이 없으면 (중간에 닫는 등) 다시 누를 수 있게 한다.</summary>
    IEnumerator LoginTimeout()
    {
        yield return new WaitForSecondsRealtime(60f);
        if (busy && loginPage.activeSelf)
        {
            busy = false;
            errorText.text = "로그인 응답이 없어요. 다시 눌러 주세요.";
        }
    }

    // ---------- 경희대 메일 ----------

    string EmailAddress
    {
        get
        {
            string id = emailInput.text.Trim();
            int at = id.IndexOf('@');
            if (at >= 0) id = id.Substring(0, at); // "@khu.ac.kr" 까지 써도 된다
            return id.Length > 0 ? $"{id}@{AuthConfig.SchoolDomain}" : "";
        }
    }

    void SendCode()
    {
        if (busy) return;
        if (EmailAddress.Length == 0) { errorText.text = "학교 메일 아이디를 써 주세요."; return; }
        if (Time.unscaledTime < resendAt) return;
        busy = true;
        errorText.text = "";
        UIKit.SetLabel(sendButton, "보내는 중…");
        StartCoroutine(AuthService.StartEmail(EmailAddress, (error, devCode) =>
        {
            busy = false;
            UIKit.SetLabel(sendButton, "다시 받기");
            if (error != null) { errorText.text = error; return; }
            resendAt = Time.unscaledTime + 60f;
            codeRow.SetActive(true);
            codeInput.text = devCode ?? "";
            codeHint.text = devCode != null
                ? $"개발용 서버라 번호를 바로 채웠어요 ({devCode})"
                : $"{EmailAddress} 로 보낸 6자리 번호를 넣어 주세요. 메일이 안 오면 스팸함도 확인해 주세요.";
            codeInput.ActivateInputField();
        }));
    }

    void VerifyCode()
    {
        if (busy) return;
        string code = codeInput.text.Trim();
        if (code.Length != 6) { errorText.text = "6자리 번호를 넣어 주세요."; return; }
        busy = true;
        errorText.text = "";
        StartCoroutine(AuthService.VerifyEmail(code, (account, error) =>
        {
            busy = false;
            if (account == null) { errorText.text = error; return; }
            Continue(account);
        }));
    }

    void Update()
    {
        if (sendButton != null && emailPage.activeSelf && codeRow.activeSelf)
        {
            float left = resendAt - Time.unscaledTime;
            sendButton.interactable = left <= 0f && !busy;
            if (!busy) UIKit.SetLabel(sendButton, left > 0f ? $"다시 받기 ({Mathf.CeilToInt(left)}초)" : "다시 받기");
        }
    }

    // ---------- 닉네임 ----------

    void SaveNickname()
    {
        if (busy) return;
        string nick = nickInput.text.Trim();
        if (nick.Length is < 1 or > 20) { errorText.text = "닉네임은 1~20자로 써 주세요."; return; }
        busy = true;
        errorText.text = "";
        StartCoroutine(AuthService.SetNickname(nick, (account, error) =>
        {
            busy = false;
            if (account == null) { errorText.text = error; return; }
            Continue(account);
        }));
    }

    // ---------- 성별 ----------

    void SaveGender(string gender)
    {
        if (busy) return;
        busy = true;
        errorText.text = "";
        StartCoroutine(AuthService.SetGender(gender, (account, error) =>
        {
            busy = false;
            if (account == null) { errorText.text = error; return; }
            Continue(account);
        }));
    }

    // ---------- 화면 ----------

    void Show(GameObject page)
    {
        foreach (GameObject p in new[] { checking, loginPage, emailPage, nickPage, genderPage, privacyPage })
            p.SetActive(p == page);
        if (page == loginPage)
            for (int i = 0; i < hero.Length; i++)
                if (hero[i].texture == null)
                {
                    hero[i].texture = MonsterThumbnails.Get(HeroSpecies[i]);   // 모델이 없으면 null → 숨김
                    hero[i].enabled = hero[i].texture != null;
                }
    }

    static void Toast(string message)
    {
        var hud = FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.Toast(message);
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "LoginCanvas", 60);
        root = UIKit.Image(canvas.transform, "Login", KucaTheme.Card, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)root.transform);

        Rect sa = Screen.safeArea;
        RectTransform safe = UIKit.Rect(root.transform, "Safe");
        safe.anchorMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        safe.anchorMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));
        safe.offsetMin = safe.offsetMax = Vector2.zero;

        // 왼쪽 위 작은 상표 (큰 크림슨 띠 대신)
        Text brand = UIKit.Bold(UIKit.Text(safe, "KUCA", KucaTheme.Heading, TextAnchor.MiddleLeft, KucaTheme.Primary));
        Box(brand.rectTransform, G, 48f, G, 72f);

        RectTransform panel = UIKit.Rect(safe, "Panel");
        UIKit.Stretch(panel);

        errorText = UIKit.Text(safe, "", KucaTheme.Label, TextAnchor.MiddleCenter, ErrorRed);
        errorText.rectTransform.anchorMin = new Vector2(0f, 0f);
        errorText.rectTransform.anchorMax = new Vector2(1f, 0f);
        errorText.rectTransform.pivot = new Vector2(0.5f, 0f);
        errorText.rectTransform.offsetMin = new Vector2(G, 24f);
        errorText.rectTransform.offsetMax = new Vector2(-G, 124f);

        // 확인 중
        checking = UIKit.Rect(panel, "Checking").gameObject;
        UIKit.Stretch((RectTransform)checking.transform);
        checkingText = UIKit.Text(checking.transform, "", KucaTheme.Body, TextAnchor.MiddleLeft, InkQuiet);
        Box(checkingText.rectTransform, G, 160f, G, 140f);
        retryButton = Pill(checking.transform, "다시 시도", Accent, Color.white, StartFlow, 340f);

        // 로그인: 위 큰 문구, 아래 버튼 묶음 (엄지 닿는 곳)
        loginPage = UIKit.Rect(panel, "LoginPage").gameObject;
        UIKit.Stretch((RectTransform)loginPage.transform);
        Text welcome = UIKit.Bold(UIKit.Text(loginPage.transform, "경희대 국제캠퍼스,\n오늘은 어디로 걸어 볼까요?", KucaTheme.Title, TextAnchor.UpperLeft, Ink));
        Box(welcome.rectTransform, G, 180f, G, 190f);
        Text sub = UIKit.Text(loginPage.transform, "파트너 경희몬과 함께 걷고, 장소마다 메모를 남겨요.\n로그인한 뒤 경희대 메일 인증을 한 번 해요.", KucaTheme.Body, TextAnchor.UpperLeft, InkQuiet);
        Box(sub.rectTransform, G, 390f, G, 130f);

        // 가운데: 경희몬 셋 (문구와 버튼 사이가 통째로 비어 있었음 — 무슨 앱인지 그림으로 바로 보이게)
        RectTransform heroRow = UIKit.Rect(loginPage.transform, "Hero");
        Box(heroRow, G, 560f, G, 310f);   // 칸 너비 (960 - 32) / 3 ≈ 310: 정사각 썸네일이 늘어나지 않게
        UIKit.Horizontal(heroRow, KucaTheme.Space2);
        for (int i = 0; i < hero.Length; i++)
        {
            hero[i] = new GameObject(HeroSpecies[i], typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            hero[i].transform.SetParent(heroRow, false);
            hero[i].raycastTarget = false;
            hero[i].enabled = false;
        }

        RectTransform actions = UIKit.Rect(loginPage.transform, "Actions");
        actions.anchorMin = Vector2.zero;
        actions.anchorMax = new Vector2(1f, 0f);
        actions.pivot = new Vector2(0.5f, 0f);
        actions.offsetMin = new Vector2(0f, 140f);
        actions.offsetMax = new Vector2(0f, 900f);
        VerticalLayoutGroup stack = UIKit.Vertical(actions, KucaTheme.Space2, new RectOffset((int)G, (int)G, 0, 0));
        stack.childAlignment = TextAnchor.LowerCenter;
        Button kakao = UIKit.BigButton(actions, "카카오로 시작하기", () => LoginWith("kakao"), KakaoYellow, KakaoInk);
        kakao.gameObject.AddComponent<PressScale>();
        AddIcon(kakao, HudIcons.Bubble, KakaoInk);
        if (AuthConfig.AppleLoginEnabled)
            UIKit.BigButton(actions, "Apple로 계속하기", () => LoginWith("apple"), Color.black, Color.white).gameObject.AddComponent<PressScale>();
        // 보조 버튼 하나로 통일 (글자만 있어 눌리는지 애매했음, REVIEW_v14)
        UIKit.SecondaryButton(actions, "로그인 없이 둘러보기", StartGuest);
        Text guestHint = UIKit.Text(actions, "체험 계정은 7일 뒤 지워져요. 나중에 로그인해도 진행은 이어져요.", KucaTheme.Caption, TextAnchor.MiddleCenter, KucaTheme.InkSoft);
        UIKit.Size(guestHint, 60f);
#if UNITY_EDITOR
        // 에디터에는 카카오·Apple 이 없으니 테스트용으로 건너뛸 수 있게 한다 (빌드에는 없음). 캡처 도구가 이 글자로 누른다.
        Button skip = UIKit.Button(actions, "에디터: 로그인 건너뛰기", Finish, new Color(0f, 0f, 0f, 0f), KucaTheme.Caption);
        skip.GetComponentInChildren<Text>().color = InkQuiet;
        UIKit.Size(skip, 90f);
#endif

        // 경희대 메일 인증
        emailPage = UIKit.Rect(panel, "EmailPage").gameObject;
        UIKit.Stretch((RectTransform)emailPage.transform);
        Heading(emailPage.transform, "경희대 메일 인증");
        emailHint = UIKit.Text(emailPage.transform, "", KucaTheme.Label, TextAnchor.UpperLeft, InkQuiet);
        Box(emailHint.rectTransform, G, 260f, G, 110f);
        emailInput = Input(emailPage.transform, "학교 메일 아이디", 390f, G, 400f);
        emailInput.contentType = InputField.ContentType.EmailAddress;
        emailInput.characterLimit = 64;
        Text domain = UIKit.Bold(UIKit.Text(emailPage.transform, "@" + AuthConfig.SchoolDomain, KucaTheme.Body, TextAnchor.MiddleLeft, Ink));
        domain.rectTransform.anchorMin = domain.rectTransform.anchorMax = new Vector2(1f, 1f);
        domain.rectTransform.pivot = new Vector2(1f, 1f);
        domain.rectTransform.anchoredPosition = new Vector2(-G, -390f);
        domain.rectTransform.sizeDelta = new Vector2(320f, InputHeight);
        sendButton = Pill(emailPage.transform, "인증번호 받기", KucaTheme.Fill, Ink, SendCode, 550f);
        codeRow = UIKit.Rect(emailPage.transform, "Code").gameObject;
        UIKit.Stretch((RectTransform)codeRow.transform);
        codeInput = Input(codeRow.transform, "6자리 번호", 760f, G, G);
        codeInput.contentType = InputField.ContentType.IntegerNumber;
        codeInput.characterLimit = 6;
        codeHint = UIKit.Text(codeRow.transform, "", KucaTheme.Caption, TextAnchor.UpperLeft, InkQuiet);
        Box(codeHint.rectTransform, G, 904f, G, 90f);
        verifyButton = Pill(codeRow.transform, "확인", Accent, Color.white, VerifyCode, 1000f);

        // 닉네임
        nickPage = UIKit.Rect(panel, "NickPage").gameObject;
        UIKit.Stretch((RectTransform)nickPage.transform);
        Heading(nickPage.transform, "닉네임을 정해 주세요");
        Text nh = UIKit.Text(nickPage.transform, "메모판과 산책메이트에서 보이는 이름이에요 (1~20자).", KucaTheme.Label, TextAnchor.UpperLeft, InkQuiet);
        Box(nh.rectTransform, G, 260f, G, 110f);
        nickInput = Input(nickPage.transform, "닉네임", 390f, G, G);
        nickInput.characterLimit = 20;
        nickButton = Pill(nickPage.transform, "다음", Accent, Color.white, SaveNickname, 560f);

        // 성별 (필수): 큰 선택 칸 두 개
        genderPage = UIKit.Rect(panel, "GenderPage").gameObject;
        UIKit.Stretch((RectTransform)genderPage.transform);
        Heading(genderPage.transform, "성별을 알려 주세요");
        Text gh = UIKit.Text(genderPage.transform, "상대에게 보이지 않아요. 산책메이트 '동성만' 조건에만 써요.", KucaTheme.Label, TextAnchor.UpperLeft, InkQuiet);
        Box(gh.rectTransform, G, 260f, G, 110f);
        Pill(genderPage.transform, "남자", Field, Ink, () => SaveGender("male"), 400f);
        Pill(genderPage.transform, "여자", Field, Ink, () => SaveGender("female"), 600f);

        BuildPrivacy(panel);

        root.SetActive(false);
    }

    const float G = KucaTheme.Gutter;
    const float InputHeight = 140f;

    static void Heading(Transform parent, string text)
    {
        Text h = UIKit.Bold(UIKit.Text(parent, text, KucaTheme.Title, TextAnchor.MiddleLeft, KucaTheme.Ink));
        Box(h.rectTransform, G, 150f, G, 100f);
    }

    /// <summary>개인정보 안내: 무엇을 왜 받고 언제 지우는지 (흰 바탕 위 회색 카드 행)</summary>
    void BuildPrivacy(RectTransform panel)
    {
        privacyPage = UIKit.Rect(panel, "PrivacyPage").gameObject;
        UIKit.Stretch((RectTransform)privacyPage.transform);
        Heading(privacyPage.transform, "개인정보 안내");
        Text ph = UIKit.Text(privacyPage.transform, "KUCA 가 받는 정보와 쓰는 곳, 보관 기간이에요. 초록 줄은 민감한 정보예요.", KucaTheme.Label, TextAnchor.UpperLeft, InkQuiet);
        Box(ph.rectTransform, G, 254f, G, 60f);

        (string item, string use, string keep)[] rows =
        {
            ("경희대 메일", "경희대 학생인지 확인", "탈퇴할 때까지"),
            ("성별", "산책메이트 '동성만' 조건에만 (상대에게 안 보임)", "탈퇴할 때까지"),
            ("산책 취향", "산책 추천 · 산책메이트 찾기", "탈퇴할 때까지"),
            ("위치", "건물 단위로만, 산책 추천 · 매칭에", "산책이 끝나면 삭제"),
            ("인상착의", "만나기로 한 상대에게만", "산책이 끝나면 삭제"),
            ("사진 · 메모", "경희스팟 메모판에 공개", "직접 지울 때까지"),
            ("체험 계정", "로그인 없이 둘러보기", "만든 지 7일 뒤 삭제"),
        };
        float top = 330f;
        foreach (var (item, use, keep) in rows)
        {
            // 민감한 셋(성별 · 위치 · 인상착의)은 옅은 초록 + 왼쪽 띠로 먼저 눈에 띄게: 7줄이 같은 무게라 훑기 어려웠음 (REVIEW_v14)
            bool sensitive = item == "성별" || item == "위치" || item == "인상착의";
            Image row = UIKit.Image(privacyPage.transform, item, sensitive ? KucaTheme.PrimarySoft : Field);
            row.sprite = HudIcons.Rounded;
            row.type = Image.Type.Sliced;
            row.pixelsPerUnitMultiplier = 0.5f;
            Box(row.rectTransform, G, top, G, 124f);
            if (sensitive)
            {
                Image bar = UIKit.Image(row.transform, "Mark", KucaTheme.Primary);
                RectTransform br = bar.rectTransform;
                br.anchorMin = new Vector2(0f, 0.18f);
                br.anchorMax = new Vector2(0f, 0.82f);
                br.offsetMin = new Vector2(12f, 0f);
                br.offsetMax = new Vector2(20f, 0f);
            }
            Text name = UIKit.Bold(UIKit.Text(row.transform, item, KucaTheme.Label, TextAnchor.UpperLeft, Ink));
            Box(name.rectTransform, 36f, 16f, 36f, 50f);
            Text k = UIKit.Bold(UIKit.Text(row.transform, keep, KucaTheme.Caption, TextAnchor.UpperRight, KucaTheme.PrimaryDeep));
            Box(k.rectTransform, 36f, 20f, 36f, 44f);
            Text u = UIKit.Text(row.transform, use, KucaTheme.Caption, TextAnchor.UpperLeft, InkQuiet);
            Box(u.rectTransform, 36f, 70f, 36f, 44f);
            top += 136f;
        }
        Text foot = UIKit.Text(privacyPage.transform, "설정 › 회원 탈퇴로 계정과 정보를 언제든 지울 수 있어요.", KucaTheme.Caption, TextAnchor.UpperLeft, InkQuiet);
        Box(foot.rectTransform, G, top + 8f, G, 50f);
        privacyButton = Pill(privacyPage.transform, "확인하고 시작", Accent, Color.white, ConfirmPrivacy, top + 80f);
    }

    InputField Input(Transform parent, string placeholder, float top, float left, float right)
    {
        InputField f = UIKit.Input(parent, placeholder, KucaTheme.Body, false, 64);
        Image bg = f.GetComponent<Image>();
        bg.color = Field;
        bg.pixelsPerUnitMultiplier = 0.4f;
        Box((RectTransform)f.transform, left, top, right, InputHeight);
        return f;
    }

    /// <summary>v4 큰 버튼 (둥근 사각 · 168 높이) 을 위에서 top 만큼 내려 꽉 차게</summary>
    static Button Pill(Transform parent, string label, Color face, Color ink, UnityEngine.Events.UnityAction onClick, float top)
    {
        Button b = UIKit.BigButton(parent, label, onClick, face, ink);
        Box((RectTransform)b.transform, G, top, G, KucaTheme.ButtonHeight);
        b.gameObject.AddComponent<PressScale>();
        return b;
    }

    static void AddIcon(Button b, Sprite icon, Color color)
    {
        Image ic = UIKit.Image(b.transform, "Icon", color);
        ic.sprite = icon;
        ic.rectTransform.anchorMin = ic.rectTransform.anchorMax = ic.rectTransform.pivot = new Vector2(0f, 0.5f);
        ic.rectTransform.anchoredPosition = new Vector2(56f, 0f);
        ic.rectTransform.sizeDelta = new Vector2(64f, 64f);
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
