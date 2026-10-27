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
    static readonly string[] AppNames = { "멜론", "Spotify", "YouTube Music" };

    public static App Current
    {
        get => (App)Mathf.Clamp(PlayerPrefs.GetInt(AppKey, 0), 0, AppNames.Length - 1);
        set { PlayerPrefs.SetInt(AppKey, (int)value); PlayerPrefs.Save(); }
    }

    public static string AppName => AppNames[(int)Current];

    /// <summary>설정의 한 줄: 누를 때마다 다음 앱으로</summary>
    public static void NextApp() => Current = (App)(((int)Current + 1) % AppNames.Length);

    /// <summary>앱이 깔려 있으면 앱으로, 없으면 웹 검색으로 열리는 주소 (유니버설 링크)</summary>
    public static string SearchUrl(Track t)
    {
        string q = UnityWebRequest.EscapeURL($"{t.artist} {t.title}").Replace("+", "%20");
        switch (Current)
        {
            case App.Spotify: return "https://open.spotify.com/search/" + q;
            case App.YouTubeMusic: return "https://music.youtube.com/search?q=" + q;
            default: return "https://m.app.melon.com/search/searchMcom.htm?s=" + q;
        }
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
            string by = result.source == "ai" ? "AI 추천" : "기본 추천";
            UIKit.Paragraph(parent, string.IsNullOrEmpty(result.theme) ? by : $"{by}  ·  {Escape(result.theme)}", KucaTheme.Label, KucaTheme.PrimaryDeep);
            foreach (Track t in result.tracks)
                TrackRow(parent, t);
            UIKit.Text(parent, $"누르면 {AppName}에서 찾아요 (설정에서 바꿀 수 있어요)", KucaTheme.Caption, TextAnchor.UpperLeft, KucaTheme.InkMuted);
        }));
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
        b.onClick.AddListener(() => Application.OpenURL(SearchUrl(t)));
        row.gameObject.AddComponent<PressScale>();

        string open = $"<color=#{ColorUtility.ToHtmlStringRGB(KucaTheme.Primary)}>{AppName} ›</color>";
        UIKit.Bold(UIKit.Text(row.transform, Escape(t.title), KucaTheme.Body, TextAnchor.UpperLeft, KucaTheme.Ink));
        string sub = string.IsNullOrEmpty(t.why) ? Escape(t.artist) : $"{Escape(t.artist)}  ·  {Escape(t.why)}";
        UIKit.Text(row.transform, $"{sub}   {open}", KucaTheme.Label, TextAnchor.UpperLeft, KucaTheme.InkMuted);
    }

    static string Escape(string s) => (s ?? "").Replace("<", "‹").Replace(">", "›");
}
