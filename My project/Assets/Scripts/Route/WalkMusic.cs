using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 산책하며 들을 노래 (서버 POST /api/music, docs/api.md). AI 가 취향 · 날씨 · 시간대 · 코스 분위기로 고른다.
/// 앱은 재생하지 않고, 설정에서 고른 음악 앱(멜론 · Spotify · YouTube Music)의 검색을 "가수 곡 이름" 으로 연다.
/// 코스 카드에서 탭을 오가도 다시 부르지 않게 코스마다 결과를 기억한다.
/// </summary>
public static class WalkMusic
{
    [Serializable]
    public class Request
    {
        public int minutes;
        public List<string> tastes = new List<string>();
        public string pace;
        public string courseTitle;
        public string courseWhy;
        public List<string> places = new List<string>();
        public int count = 5;
    }

    [Serializable]
    public class Track
    {
        public string title;
        public string artist;
        public string why;
    }

    [Serializable]
    public class Result
    {
        public string source;   // ai · fallback
        public string mood;
        public string theme;
        public List<Track> tracks = new List<Track>();
    }

    // ---------- 음악 앱 (설정) ----------

    public enum App { Melon, Spotify, YouTubeMusic }

    const string AppKey = "music_app";
    static readonly string[] AppNames = { "멜론", "Spotify", "유튜브 뮤직" };   // 곡 줄 · 칩에 한 줄로 (YouTube Music 은 두 줄로 꺾였음)

    public static App Current
    {
        get => (App)Mathf.Clamp(PlayerPrefs.GetInt(AppKey, 0), 0, AppNames.Length - 1);
        set { PlayerPrefs.SetInt(AppKey, (int)value); PlayerPrefs.Save(); }
    }

    public static string AppName => AppNames[(int)Current];

    /// <summary>설정의 한 줄: 누를 때마다 다음 앱으로</summary>
    public static void NextApp() => Current = (App)(((int)Current + 1) % AppNames.Length);

    /// <summary>
    /// (앱 주소, 웹 주소). 멜론 웹 검색은 결과를 자바스크립트로 불러와 폰 사파리에서 비어 보였다 → 앱 주소를 먼저 연다.
    /// 앱 주소: 멜론 검색 페이지가 쓰는 melonapp://search, Spotify 의 spotify:search. YouTube Music 은 웹 주소가 앱으로 열린다
    /// </summary>
    public static (string app, string web) SearchUrls(Track t)
    {
        string q = UnityWebRequest.EscapeURL($"{t.artist} {t.title}").Replace("+", "%20");
        switch (Current)
        {
            case App.Spotify: return ("spotify:search:" + q, "https://open.spotify.com/search/" + q);
            case App.YouTubeMusic: return (null, "https://music.youtube.com/search?q=" + q);
            default: return ("melonapp://search?keyword=" + q, MelonStore);
        }
    }

    /// <summary>
    /// 멜론 앱이 없을 때: 멜론 웹 검색은 폰 브라우저에서 결과가 비어 보여 (자바스크립트로 불러옴) 앱 설치 페이지를 연다.
    /// iOS App Store 415597317 "멜론(Melon)" (Kakao M), Android com.iloen.melon
    /// </summary>
    static string MelonStore =>
#if UNITY_IOS && !UNITY_EDITOR
        "itms-apps://apps.apple.com/kr/app/id415597317";
#elif UNITY_ANDROID && !UNITY_EDITOR
        "market://details?id=com.iloen.melon";
#else
        "https://apps.apple.com/kr/app/id415597317";
#endif

    /// <summary>음악 앱을 연다. 앱이 없어 이 앱이 그대로 있으면 (잠깐 기다려 봄) 웹 검색 (멜론은 앱 설치 페이지) 을 연다</summary>
    public static void Open(Track t)
    {
        (string app, string web) = SearchUrls(t);
        if (app == null) { Application.OpenURL(web); return; }
        Opener.Run(app, web);
    }

    /// <summary>앱 주소를 열고 1.5초 안에 이 앱이 뒤로 가지 않으면 웹 주소로</summary>
    class Opener : MonoBehaviour
    {
        static Opener instance;
        bool left;

        public static void Run(string app, string web)
        {
            if (instance == null)
            {
                instance = new GameObject("MusicOpener").AddComponent<Opener>();
                instance.gameObject.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.Go(app, web));
        }

        IEnumerator Go(string app, string web)
        {
            left = false;
            Application.OpenURL(app);
            float until = Time.realtimeSinceStartup + 1.5f;
            while (!left && Time.realtimeSinceStartup < until)
                yield return null;
            if (!left)
                Application.OpenURL(web);
        }

        void OnApplicationPause(bool paused) { if (paused) left = true; }
        void OnApplicationFocus(bool focus) { if (!focus) left = true; }
    }

    // ---------- 서버 ----------

    static readonly Dictionary<string, Result> cache = new Dictionary<string, Result>();

    static string ServerUrl =>
        (PlayerPrefs.HasKey(MemoBoard.ServerUrlKey) ? PlayerPrefs.GetString(MemoBoard.ServerUrlKey) : MemoBoard.DefaultServer).Trim().TrimEnd('/');

    /// <summary>노래 목록을 받는다. cacheKey 가 있으면 같은 키는 한 번만 부른다. 실패하면 (null, 오류 문장)</summary>
    public static IEnumerator Recommend(Request request, string cacheKey, Action<Result, string> onDone)
    {
        if (cacheKey != null && cache.TryGetValue(cacheKey, out Result hit))
        {
            onDone(hit, null);
            yield break;
        }
        string url = ServerUrl;
        if (url.Length == 0)
        {
            onDone(null, "서버에 연결하지 않아 노래를 추천할 수 없어요");
            yield break;
        }
        using var req = new UnityWebRequest(url + "/api/music", "POST", new DownloadHandlerBuffer(),
            new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(request))));
        req.SetRequestHeader("Content-Type", "application/json");
        if (AuthService.HasToken) req.SetRequestHeader("Authorization", "Bearer " + AuthService.Token);
        req.timeout = 30;   // AI 가 고르는 시간 + 무료 요금제 서버가 깨어나는 시간
        yield return req.SendWebRequest();
        Result result = null;
        if (req.result == UnityWebRequest.Result.Success)
        {
            try { result = JsonUtility.FromJson<Result>(req.downloadHandler.text); }
            catch (ArgumentException) { }
        }
        if (result?.tracks == null || result.tracks.Count == 0)
        {
            Debug.Log($"[Music] 노래 추천을 못 받았어요 ({req.responseCode} {req.error})");
            onDone(null, "노래를 불러오지 못했어요");
            yield break;
        }
        if (cacheKey != null) cache[cacheKey] = result;
        onDone(result, null);
    }

    /// <summary>
    /// parent 에 "불러오는 중" 을 넣고, 받으면 주제 한 줄 + 곡 줄로 바꾼다. host 가 코루틴을 돌린다.
    /// 그사이 parent 가 지워졌으면 (코스 탭을 바꿈) 아무것도 하지 않는다.
    /// </summary>
    public static void Fill(MonoBehaviour host, RectTransform parent, Request request, string cacheKey)
    {
        Text wait = UIKit.Text(parent, "노래를 고르는 중…", KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
        host.StartCoroutine(Recommend(request, cacheKey, (result, error) =>
        {
            if (parent == null) return;
            if (wait != null) UnityEngine.Object.Destroy(wait.gameObject);
            if (result == null)
            {
                UIKit.Text(parent, error, KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
                return;
            }
            Render(parent, result);
        }));
    }

    /// <summary>주제 한 줄 · 들을 앱 고르기 칩 · 곡 줄. 칩을 누르면 같은 곡으로 다시 그린다</summary>
    static void Render(RectTransform parent, Result result)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        string by = result.source == "ai" ? "AI 추천" : "기본 추천";
        UIKit.Paragraph(parent, string.IsNullOrEmpty(result.theme) ? by : $"{by}  ·  {Escape(result.theme)}", KucaTheme.Label, KucaTheme.PrimaryDeep);
        foreach (Track t in result.tracks)
            TrackRow(parent, t);
        UIKit.Text(parent, "누르면 이 앱에서 찾아요", KucaTheme.Caption, TextAnchor.UpperLeft, KucaTheme.InkMuted);
        RectTransform chips = UIKit.Rect(parent, "Apps");
        UIKit.Size(chips, 104f);
        HorizontalLayoutGroup h = UIKit.Horizontal(chips, KucaTheme.Space2);
        h.childControlWidth = h.childForceExpandWidth = true;   // 세 칩을 같은 너비로 (긴 이름 칩이 줄을 넘쳤음)
        for (int i = 0; i < AppNames.Length; i++)
        {
            App app = (App)i;
            Button chip = UIKit.Chip(chips, AppNames[i], app == Current, () =>
            {
                Current = app;   // 설정의 "노래 들을 앱" 과 같은 값
                Render(parent, result);
            });
            AddLogo(chip, app);
        }
    }

    public static Sprite Logo(App app) =>
        app == App.Spotify ? HudIcons.SpotifyLogo : app == App.YouTubeMusic ? HudIcons.YouTubeMusicLogo : HudIcons.MelonLogo;

    /// <summary>칩 글자 왼쪽에 앱 로고 (흰 원 안): 어떤 앱인지 한눈에. 고른 칩은 초록 바탕이라 흰 원이 로고를 받쳐 준다</summary>
    static void AddLogo(Button chip, App app)
    {
        Sprite logo = Logo(app);
        if (logo == null) return;
        const float size = 64f, left = 20f;
        Image disc = UIKit.Image(chip.transform, "LogoDisc", Color.white);
        disc.sprite = HudIcons.Circle;
        RectTransform d = disc.rectTransform;
        d.anchorMin = d.anchorMax = new Vector2(0f, 0.5f);
        d.pivot = new Vector2(0f, 0.5f);
        d.anchoredPosition = new Vector2(left, 0f);
        d.sizeDelta = new Vector2(size, size);
        disc.gameObject.AddComponent<Mask>();   // 정사각형 로고 그림의 흰 모서리가 원 밖으로 튀어나왔음 (고른 칩의 초록 위에서 보임)
        Image icon = UIKit.Image(disc.transform, "Logo", Color.white);
        icon.sprite = logo;
        icon.preserveAspect = true;
        UIKit.Stretch(icon.rectTransform, 4f, 4f);
        Text t = chip.GetComponentInChildren<Text>();
        // 글자 칸을 한 줄 높이로: 두 줄로 꺾이지 못하게 해서 best fit 이 글자를 줄이게 한다 ("유튜브 / 뮤직" 으로 꺾였음)
        RectTransform tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 0.5f);
        tr.anchorMax = new Vector2(1f, 0.5f);
        tr.offsetMin = new Vector2(left + size + 8f, -26f);
        tr.offsetMax = new Vector2(-16f, 26f);
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.resizeTextForBestFit = true;   // 좁은 폰에서도 한 줄
        t.resizeTextMaxSize = KucaTheme.Label;
        t.resizeTextMinSize = KucaTheme.Caption - 4;
        LayoutElement fit = chip.GetComponent<LayoutElement>();
        fit.preferredWidth = 0f;
        fit.flexibleWidth = 1f;
    }

    /// <summary>곡 한 줄: 굵은 곡 이름 + 가수 · 이유, 오른쪽 "멜론 ›". 누르면 음악 앱 검색</summary>
    static void TrackRow(RectTransform parent, Track t)
    {
        Image row = UIKit.Image(parent, "Track", KucaTheme.Fill, raycast: true);
        row.sprite = HudIcons.Rounded;
        row.type = Image.Type.Sliced;
        row.pixelsPerUnitMultiplier = 0.4f;
        UIKit.Vertical(row, KucaTheme.Space1, new RectOffset(40, 40, 28, 28));
        row.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Button b = row.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => Open(t));
        row.gameObject.AddComponent<PressScale>();

        string open = $"<color=#{ColorUtility.ToHtmlStringRGB(KucaTheme.Primary)}>{AppName} ›</color>";
        UIKit.Bold(UIKit.Text(row.transform, Escape(t.title), KucaTheme.Body, TextAnchor.UpperLeft, KucaTheme.Ink));
        string sub = string.IsNullOrEmpty(t.why) ? Escape(t.artist) : $"{Escape(t.artist)}  ·  {Escape(t.why)}";
        UIKit.Text(row.transform, $"{sub}   {open}", KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
    }

    static string Escape(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");
}
