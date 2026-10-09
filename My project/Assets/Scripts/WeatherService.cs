using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 지금 캠퍼스 날씨를 메모 서버의 GET /api/weather 에서 받아 SpawnConditions.LiveWeather 에 넣는다 (docs/api.md).
/// 서버가 기상청 초단기 실황·예보를 대신 불러 준다 (API 키는 서버에만 있다).
/// 씬에 따로 둘 필요 없이 앱 시작 때 숨은 오브젝트로 돈다. 실패하면 날씨를 모름(Unknown)으로 두고 나중에 다시 시도한다.
/// </summary>
public class WeatherService : MonoBehaviour
{
    const float RefreshSeconds = 20f * 60f;
    const float RetrySeconds = 3f * 60f;

    [System.Serializable]
    class WeatherDto
    {
        public string condition;   // clear | cloudy | rain | snow
        public string observedAt;
    }

    /// <summary>메모판과 같은 서버 주소 (앱 설정에서 바꾼 값이 있으면 그것)</summary>
    static string ServerUrl =>
        (PlayerPrefs.HasKey(MemoBoard.ServerUrlKey) ? PlayerPrefs.GetString(MemoBoard.ServerUrlKey) : MemoBoard.DefaultServer).Trim().TrimEnd('/');

    IEnumerator Start()
    {
        while (true)
        {
            string url = ServerUrl;
            bool ok = false;
            if (url.Length > 0)
            {
                using var req = UnityWebRequest.Get(url + "/api/weather");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    WeatherDto dto = null;
                    try { dto = JsonUtility.FromJson<WeatherDto>(req.downloadHandler.text); }
                    catch { }
                    SpawnConditions.LiveWeather = Parse(dto?.condition);
                    ok = SpawnConditions.LiveWeather != SpawnConditions.Weather.Unknown;
                }
                else
                    Debug.Log($"[Weather] 날씨를 받지 못했습니다 ({req.responseCode} {req.error}). 날씨 조건 없이 진행합니다.");
            }
            yield return new WaitForSecondsRealtime(ok ? RefreshSeconds : RetrySeconds);
        }
    }

    static SpawnConditions.Weather Parse(string s)
    {
        switch (s)
        {
            case "clear": return SpawnConditions.Weather.Clear;
            case "cloudy": return SpawnConditions.Weather.Cloudy;
            case "rain": return SpawnConditions.Weather.Rain;
            case "snow": return SpawnConditions.Weather.Snow;
            default: return SpawnConditions.Weather.Unknown;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var go = new GameObject("WeatherService");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<WeatherService>();
    }
}
