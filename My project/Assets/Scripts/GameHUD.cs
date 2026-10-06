using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 HUD. 오른쪽 위에 점수와 종류별 획득 수, 아래쪽에 잠깐 뜨는 안내 메시지를 표시한다.
/// 캔버스와 텍스트는 실행 시 코드로 만든다.
/// </summary>
public class GameHUD : MonoBehaviour
{
    public float toastDuration = 2.5f;

    Text progressText;
    Text toastText;
    Image toastBg;
    float toastTimer;

    void Awake()
    {
        var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // 오른쪽 위 점수판
        Image panel = CreateImage(canvasGo.transform, "ProgressPanel", new Color(0f, 0f, 0f, 0.45f));
        RectTransform pr = panel.rectTransform;
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(1f, 1f);
        pr.anchoredPosition = new Vector2(-30f, -60f);
        pr.sizeDelta = new Vector2(380f, 230f);
        progressText = CreateText(panel.transform, "ProgressText", font, 38, TextAnchor.UpperLeft);
        Stretch(progressText.rectTransform, 22f);

        // 아래쪽 안내 메시지
        toastBg = CreateImage(canvasGo.transform, "Toast", new Color(0f, 0f, 0f, 0.65f));
        RectTransform tr = toastBg.rectTransform;
        tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0f);
        tr.anchoredPosition = new Vector2(0f, 160f);
        tr.sizeDelta = new Vector2(980f, 120f);
        toastText = CreateText(toastBg.transform, "ToastText", font, 40, TextAnchor.MiddleCenter);
        Stretch(toastText.rectTransform, 16f);
        toastBg.gameObject.SetActive(false);
    }

    void Update()
    {
        if (toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            if (toastTimer <= 0f)
                toastBg.gameObject.SetActive(false);
        }
    }

    public void Toast(string message)
    {
        if (toastText == null)
            return;
        toastText.text = message;
        toastBg.gameObject.SetActive(true);
        toastTimer = toastDuration;
    }

    public void ShowProgress(GameProgress progress, IList<CollectibleType> types)
    {
        if (progressText == null || progress == null)
            return;
        var sb = new StringBuilder();
        sb.Append("점수 ").Append(progress.score).Append('\n');
        if (types != null)
            foreach (var t in types)
                sb.Append(t.displayName).Append("  ×").Append(progress.CountOf(t.id)).Append('\n');
        progressText.text = sb.ToString().TrimEnd();
    }

    static Image CreateImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text CreateText(Transform parent, string name, Font font, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    static void Stretch(RectTransform rt, float padding)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(padding, padding);
        rt.offsetMax = new Vector2(-padding, -padding);
    }
}
