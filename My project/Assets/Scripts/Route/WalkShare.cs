using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 산책 기록을 카카오톡으로 공유한다 (완료 화면 · 산책 기록 상세의 "카카오톡으로 자랑하기").
/// 1) 화면의 돌아보기 지도 칸을 그대로 잘라 (선 · 표시 포함) JPEG 로 만들고
/// 2) 서버 POST /api/walks/share 에 올려 공유 페이지 (/w/{id}) · 그림 주소를 받고
/// 3) 카카오 피드 카드로 보낸다. 글은 서버가 거리 · 시간 · 장소 이름 · 닉네임으로 만든다.
/// 공유는 버튼을 눌렀을 때만 만들어진다 (링크를 받은 사람은 걸은 길 지도를 볼 수 있다).
/// </summary>
public static class WalkShare
{
    const int MaxWidth = 900;

    [Serializable]
    class Result
    {
        public string pageUrl, imageUrl, title, description;
    }

    [Serializable]
    class ErrorBody
    {
        public string error;
    }

    /// <summary>frame: 화면에 보이는 지도 칸. kind: "course" · "mate" · "free". done(에러 또는 null)</summary>
    public static IEnumerator Share(RectTransform frame, string kind, int meters, int minutes, List<string> buildingIds, Action<string> done)
    {
        var board = UnityEngine.Object.FindAnyObjectByType<MemoBoard>();
        string server = board != null ? board.ServerUrl.TrimEnd('/') : "";
        if (server.Length == 0 || !AuthService.HasToken)
        {
            done("로그인하고 서버에 연결해야 공유할 수 있어요");
            yield break;
        }

        yield return new WaitForEndOfFrame();   // 이번 프레임이 다 그려진 뒤 화면을 찍는다
        byte[] jpg = Capture(frame);
        if (jpg == null)
        {
            done("지도 그림을 만들지 못했어요");
            yield break;
        }

        var form = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("kind", kind),
            new MultipartFormDataSection("meters", meters.ToString()),
            new MultipartFormDataSection("minutes", minutes.ToString()),
            new MultipartFormDataSection("buildingIds", string.Join(",", buildingIds ?? new List<string>())),
            new MultipartFormFileSection("image", jpg, "walk.jpg", "image/jpeg"),
        };
        using UnityWebRequest req = UnityWebRequest.Post(server + "/api/walks/share", form);
        req.SetRequestHeader("Authorization", "Bearer " + AuthService.Token);
        req.timeout = 40;
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            string error = null;
            try { error = JsonUtility.FromJson<ErrorBody>(req.downloadHandler.text)?.error; } catch (ArgumentException) { }
            done(error ?? (req.result == UnityWebRequest.Result.ConnectionError ? "서버에 연결할 수 없어요" : $"공유하지 못했어요 ({req.responseCode})"));
            yield break;
        }
        Result r = JsonUtility.FromJson<Result>(req.downloadHandler.text);
        ExternalShare.Kakao(r.title, r.description, server + r.imageUrl, server + r.pageUrl, done, "산책 기록 보기");
    }

    /// <summary>화면에서 frame 칸만 잘라 JPEG 로 (너비 MaxWidth 이하)</summary>
    static byte[] Capture(RectTransform frame)
    {
        Texture2D screen = ScreenCapture.CaptureScreenshotAsTexture();
        if (screen == null)
            return null;
        try
        {
            var corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            Canvas canvas = frame.GetComponentInParent<Canvas>()?.rootCanvas;
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            int x = Mathf.Clamp(Mathf.RoundToInt(a.x), 0, screen.width - 1), y = Mathf.Clamp(Mathf.RoundToInt(a.y), 0, screen.height - 1);
            int w = Mathf.Clamp(Mathf.RoundToInt(b.x - a.x), 1, screen.width - x), h = Mathf.Clamp(Mathf.RoundToInt(b.y - a.y), 1, screen.height - y);
            var crop = new Texture2D(w, h, TextureFormat.RGB24, false);
            crop.SetPixels(screen.GetPixels(x, y, w, h));
            crop.Apply();
            if (w > MaxWidth)
            {
                Texture2D small = Scale(crop, MaxWidth, Mathf.RoundToInt(h * (MaxWidth / (float)w)));
                UnityEngine.Object.Destroy(crop);
                crop = small;
            }
            byte[] jpg = crop.EncodeToJPG(85);
            UnityEngine.Object.Destroy(crop);
            return jpg;
        }
        finally
        {
            UnityEngine.Object.Destroy(screen);
        }
    }

    static Texture2D Scale(Texture2D src, int w, int h)
    {
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var dst = new Texture2D(w, h, TextureFormat.RGB24, false);
        dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        dst.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return dst;
    }

    /// <summary>산책 종류 → 서버 kind (자유 산책은 코스 없이 걸은 것)</summary>
    public static string KindOf(string kind, int stopsTotal, int plannedMeters) =>
        kind == WalkSession.Mate ? "mate" : stopsTotal <= 0 && plannedMeters <= 0 ? "free" : "course";
}
