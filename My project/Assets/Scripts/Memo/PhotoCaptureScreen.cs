using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// 앱 안의 카메라 화면. 후면 카메라 영상을 보여 주고, 셔터를 누르면 그 순간을 JPEG로 돌려준다.
/// 카메라가 없으면(에디터 등) 지금 게임 화면을 대신 찍는다.
/// </summary>
public class PhotoCaptureScreen : MonoBehaviour
{
    [Tooltip("저장할 사진의 긴 변 최대 픽셀")]
    public int maxPhotoSize = 1280;
    [Range(30, 100)] public int jpgQuality = 80;

    GameObject root;
    RawImage preview;
    AspectRatioFitter previewFitter;
    Text status;
    WebCamTexture cam;
    Action<byte[], Texture2D> onTaken;

    public void Build(Transform canvas)
    {
        root = UIKit.Image(canvas, "CameraScreen", Color.black, raycast: true).gameObject;
        BackButton.Attach(root, Close);
        UIKit.Stretch((RectTransform)root.transform);

        var go = new GameObject("Preview", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(root.transform, false);
        preview = go.GetComponent<RawImage>();
        preview.raycastTarget = false;
        previewFitter = go.GetComponent<AspectRatioFitter>();
        previewFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        status = UIKit.Text(root.transform, "", 40, TextAnchor.MiddleCenter);
        UIKit.Stretch(status.rectTransform, 60f, 0f);

        RectTransform bar = UIKit.Rect(root.transform, "Buttons");
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = new Vector2(-80f, 140f);
        bar.anchoredPosition = new Vector2(0f, 80f);
        UIKit.Horizontal(bar, 40f);
        UIKit.Button(bar, "취소", Close, UIKit.ButtonGray);
        UIKit.Button(bar, "찍기", Shoot, UIKit.Accent, 48);

        root.SetActive(false);
    }

    /// <summary>카메라를 켠다. 찍으면 onTaken(JPEG 바이트, 미리보기 텍스처)을 부르고 화면을 닫는다.</summary>
    public void Open(Action<byte[], Texture2D> taken)
    {
        onTaken = taken;
        root.SetActive(true);
        root.transform.SetAsLastSibling();
        StartCoroutine(StartCamera());
    }

    IEnumerator StartCamera()
    {
        status.text = "카메라 준비 중";
        preview.texture = null;
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            Permission.RequestUserPermission(Permission.Camera);
            float wait = 10f;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera) && wait > 0f) { wait -= Time.unscaledDeltaTime; yield return null; }
        }
#elif UNITY_IOS
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#endif
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam) || WebCamTexture.devices.Length == 0)
        {
            status.text = Application.isEditor
                ? "카메라가 없어요.\n'찍기'를 누르면 지금 게임 화면을 사진으로 써요."
                : "카메라를 쓸 수 없어요.\n설정에서 KUCA의 카메라 권한을 켜 주세요.";
            yield break;
        }

        string device = WebCamTexture.devices[0].name;
        foreach (WebCamDevice d in WebCamTexture.devices)
            if (!d.isFrontFacing) { device = d.name; break; }

        cam = new WebCamTexture(device, 1920, 1080, 30);
        cam.Play();
        float timeout = 5f;
        while (cam.width <= 16 && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        status.text = "";
        preview.texture = cam;
    }

    void Update()
    {
        if (cam != null && cam.isPlaying && cam.width > 16)
            ShowCameraPreview(preview, previewFitter, cam, fill: false);
    }

    /// <summary>
    /// 카메라 영상은 기기 방향에 따라 돌아가 있으므로 화면에서 바로 세운다.
    /// fill 이면 화면을 꽉 채우고(넘치는 부분은 잘림), 아니면 화면 안에 다 들어오게 맞춘다.
    /// </summary>
    public static void ShowCameraPreview(RawImage preview, AspectRatioFitter fitter, WebCamTexture cam, bool fill)
    {
        int angle = cam.videoRotationAngle;
        bool sideways = angle == 90 || angle == 270;
        preview.rectTransform.localEulerAngles = new Vector3(0f, 0f, -angle);
        preview.uvRect = cam.videoVerticallyMirrored ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        if (sideways)
        {
            // 90도 돌려 보여 주므로, 돌리기 전 크기를 화면의 가로·세로를 바꾼 영역에 맞춘다.
            var parent = (RectTransform)preview.rectTransform.parent;
            float sx = parent.rect.height / cam.width, sy = parent.rect.width / cam.height;
            float s = fill ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy);
            fitter.enabled = false;
            preview.rectTransform.anchorMin = preview.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            preview.rectTransform.sizeDelta = new Vector2(cam.width * s, cam.height * s);
        }
        else
        {
            fitter.enabled = true;
            fitter.aspectMode = fill ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)cam.width / cam.height;
        }
    }

    void Shoot()
    {
        if (cam != null && cam.isPlaying && cam.width > 16)
            Finish(GrabCameraFrame());
        else
            StartCoroutine(GrabScreen());
    }

    void Finish(Texture2D photo)
    {
        photo = Downscale(photo, maxPhotoSize);
        byte[] jpg = photo.EncodeToJPG(jpgQuality);
        Action<byte[], Texture2D> done = onTaken;
        Close();
        done?.Invoke(jpg, photo);
    }

    Texture2D GrabCameraFrame()
    {
        int w = cam.width, h = cam.height;
        Color32[] src = cam.GetPixels32();
        int angle = ((cam.videoRotationAngle % 360) + 360) % 360;
        bool mirror = cam.videoVerticallyMirrored;
        bool sideways = angle == 90 || angle == 270;
        int ow = sideways ? h : w, oh = sideways ? w : h;
        var dst = new Color32[src.Length];
        for (int y = 0; y < h; y++)
        {
            int sy = mirror ? h - 1 - y : y;
            for (int x = 0; x < w; x++)
            {
                int dx, dy;
                switch (angle)
                {
                    case 90: dx = y; dy = w - 1 - x; break;          // 시계 방향 90도
                    case 180: dx = w - 1 - x; dy = h - 1 - y; break;
                    case 270: dx = h - 1 - y; dy = x; break;
                    default: dx = x; dy = y; break;
                }
                dst[dy * ow + dx] = src[sy * w + x];
            }
        }
        var tex = new Texture2D(ow, oh, TextureFormat.RGB24, false);
        tex.SetPixels32(dst);
        tex.Apply();
        return tex;
    }

    /// <summary>카메라가 없을 때(에디터 테스트) 이 화면과 다른 UI를 잠깐 숨기고 게임 화면을 찍는다.</summary>
    IEnumerator GrabScreen()
    {
        root.SetActive(false);
        var hidden = new System.Collections.Generic.List<Canvas>();
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.enabled) { c.enabled = false; hidden.Add(c); }
        yield return new WaitForEndOfFrame();
        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
        foreach (Canvas c in hidden)
            c.enabled = true;
        Finish(tex);
    }

    /// <summary>긴 변이 maxSize 를 넘으면 줄인다 (원본 텍스처는 지운다).</summary>
    public static Texture2D Downscale(Texture2D src, int maxSize)
    {
        int longSide = Mathf.Max(src.width, src.height);
        if (longSide <= maxSize)
            return src;
        float s = (float)maxSize / longSide;
        int w = Mathf.RoundToInt(src.width * s), h = Mathf.RoundToInt(src.height * s);
        var rt = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var dst = new Texture2D(w, h, TextureFormat.RGB24, false);
        dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        dst.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(src);
        return dst;
    }

    void Close()
    {
        if (cam != null)
        {
            cam.Stop();
            Destroy(cam);
            cam = null;
        }
        root.SetActive(false);
        onTaken = null;
    }

    void OnDisable()
    {
        if (cam != null)
            cam.Stop();
    }
}
