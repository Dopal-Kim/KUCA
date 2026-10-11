using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// 친구에게 공유: 게시한 직후와 사진첩의 게시물에서 연다.
/// - 인스타그램 스토리: 앱에서 먼저 자를 부분을 고르고, 메모판 콜라주처럼 테이프 붙인 사진 스티커로 보낸다.
/// - 카카오톡: 사진·장소·캡션이 있는 카드. 버튼은 그 장소의 웹 메모판으로 간다.
/// </summary>
public partial class MemoBoard
{
    static readonly Color InstaPink = new Color(0.84f, 0.19f, 0.47f);
    static readonly Color KakaoYellow = new Color(1f, 0.9f, 0f);
    static readonly Color KakaoInk = new Color(0.1f, 0.1f, 0.1f);
    static readonly Color StoryTop = new Color(0.48f, 0.12f, 0.17f);
    static readonly Color StoryBottom = new Color(0.23f, 0.05f, 0.09f);

    GameObject shareSheet, cropView;
    RawImage sharePreview, cropImage;
    AspectRatioFitter sharePreviewFit;
    Text shareSubtitle;
    MemoData shareMemo;
    Texture2D shareTexture;
    bool shareOwnsTexture, makingSticker;
    RectTransform cropArea, cropFrame;
    CropPan cropPan;
    Button cropNext;
    readonly (float aspect, string label)[] cropAspects = { (1f, "1:1"), (0.8f, "4:5"), (9f / 16f, "9:16"), (0f, "원본") };
    Image[] cropChips;
    int cropChoice;

    void OpenShare(MemoData m, Texture2D tex, bool ownsTexture)
    {
        CloseShare();
        if (m == null || tex == null)
            return;
        shareMemo = m;
        shareTexture = tex;
        shareOwnsTexture = ownsTexture;
        sharePreview.texture = tex;
        sharePreviewFit.aspectRatio = (float)tex.width / Mathf.Max(1, tex.height);
        shareSubtitle.text = ownsTexture ? "방금 올린 사진을 친구에게도 보여 주세요" : "이 사진을 친구에게 보여 주세요";
        shareSheet.SetActive(true);
        shareSheet.transform.SetAsLastSibling();
    }

    void CloseShare()
    {
        if (shareSheet == null)
            return;
        shareSheet.SetActive(false);
        cropView.SetActive(false);
        if (shareOwnsTexture && shareTexture != null)
            Destroy(shareTexture);
        shareTexture = null;
        shareOwnsTexture = false;
        shareMemo = null;
        makingSticker = false;
    }

    // ---------- 카카오톡 ----------

    void ShareKakao()
    {
        MemoData m = shareMemo;
        if (m == null)
            return;
        string image = service.PublicPhotoUrl(m), page = service.PublicPageUrl(m.buildingId);
        Debug.Log($"[MemoBoard] 카카오톡 공유: image={image} page={page}");
        if (image == null || page == null)
        {
            ShowToast("카카오톡 공유는 온라인 메모 서버에 올린 사진만 할 수 있어요");
            return;
        }
        ExternalShare.Kakao($"{PlaceName(m.buildingId)}에 남긴 사진", $"{m.author} · {Clip(m.text, 60)}", image, page, error =>
        {
            if (error != null) { ShowToast(error); return; }
#if UNITY_EDITOR
            ShowToast("에디터에서는 보내지 않아요 (아이폰에서 카카오톡이 열려요)");
#endif
            CloseShare();
        });
    }

    string PlaceName(string buildingId)
    {
        if (building != null && building.buildingId == buildingId)
            return building.DisplayName;
        if (spots != null)
            foreach (KyungHeeSpot s in spots.Spots)
                if (s != null && s.Building != null && s.Building.buildingId == buildingId)
                    return s.Building.DisplayName;
        return "경희대학교 국제캠퍼스";
    }

    // ---------- 인스타그램 스토리 (자르기 → 스티커) ----------

    void OpenCrop()
    {
        if (shareTexture == null)
            return;
        shareSheet.SetActive(false);
        cropView.SetActive(true);
        cropView.transform.SetAsLastSibling();
        cropImage.texture = shareTexture;
        SetCropAspect(0);
    }

    void SetCropAspect(int choice)
    {
        cropChoice = choice;
        for (int i = 0; i < cropChips.Length; i++)
        {
            bool on = i == choice;
            cropChips[i].color = on ? Color.white : new Color(1f, 1f, 1f, 0.16f);
            cropChips[i].GetComponentInChildren<Text>().color = on ? Ink : Color.white;
        }
        float aspect = cropAspects[choice].aspect;
        if (aspect <= 0f)
            aspect = (float)shareTexture.width / Mathf.Max(1, shareTexture.height);
        cropFrame.GetComponent<AspectRatioFitter>().aspectRatio = aspect; // 틀은 영역 안에 꽉 맞춰진다
        Canvas.ForceUpdateCanvases();
        cropPan.Reset(new Vector2(shareTexture.width, shareTexture.height));
    }

    void FinishCrop()
    {
        if (makingSticker || shareTexture == null)
            return;
        StartCoroutine(SendStory(cropPan.UvRect));
    }

    IEnumerator SendStory(Rect uv)
    {
        makingSticker = true;
        UIKit.SetLabel(cropNext, "만드는 중…");
        yield return null; // 글자가 바뀐 화면을 먼저 보여 준다
        byte[] png = StorySticker.Make(shareTexture, uv, StableHash(shareMemo != null ? shareMemo.id : ""));
        UIKit.SetLabel(cropNext, "올리기");
        makingSticker = false;
        string error = ExternalShare.InstagramStory(png, StoryTop, StoryBottom);
        if (error != null)
        {
            ShowToast(error);
            yield break;
        }
#if UNITY_EDITOR
        ShowToast("에디터에서는 스티커만 만들어요 (아이폰에서 인스타그램이 열려요)");
#endif
        CloseShare();
    }

    // ---------- 화면 만들기 ----------

    void BuildShare()
    {
        // 공유 창: 어두운 배경 위 가운데 카드
        shareSheet = UIKit.Image(root.transform, "ShareSheet", new Color(0f, 0f, 0f, 0.55f), raycast: true).gameObject;
        UIKit.Stretch((RectTransform)shareSheet.transform);
        Image card = UIKit.Image(shareSheet.transform, "Card", Color.white, raycast: true);
        card.sprite = HudIcons.Rounded;
        card.type = Image.Type.Sliced;
        card.pixelsPerUnitMultiplier = 0.5f;
        RectTransform cr = card.rectTransform;
        cr.anchorMin = new Vector2(0.06f, 0.5f);
        cr.anchorMax = new Vector2(0.94f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.sizeDelta = new Vector2(0f, 1060f);
        cr.anchoredPosition = Vector2.zero;

        Text title = UIKit.Text(cr, "친구에게도 공유할까요?", 44, TextAnchor.MiddleCenter, Ink);
        title.fontStyle = FontStyle.Bold;
        TopBox(title.rectTransform, 54f, 64f, 30f);
        shareSubtitle = UIKit.Text(cr, "", 30, TextAnchor.MiddleCenter, InkQuiet);
        TopBox(shareSubtitle.rectTransform, 124f, 44f, 30f);

        Image thumb = UIKit.Image(cr, "Thumb", PaperDark);
        thumb.gameObject.AddComponent<RectMask2D>();
        Place(thumb.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -200f));
        thumb.rectTransform.sizeDelta = new Vector2(400f, 400f);
        var go = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(thumb.transform, false);
        sharePreview = go.GetComponent<RawImage>();
        sharePreview.raycastTarget = false;
        sharePreviewFit = go.GetComponent<AspectRatioFitter>();
        sharePreviewFit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        ShareButton(cr, "인스타그램 스토리에 올리기", InstaPink, Color.white, HudIcons.Camera, 640f, OpenCrop);
        ShareButton(cr, "카카오톡으로 보내기", KakaoYellow, KakaoInk, HudIcons.Bubble, 772f, ShareKakao);
        Button skip = UIKit.Button(cr, "건너뛰기", CloseShare, new Color(0f, 0f, 0f, 0f), 34);
        skip.GetComponentInChildren<Text>().color = InkQuiet;
        TopBox((RectTransform)skip.transform, 910f, 100f, 200f);
        shareSheet.SetActive(false);

        // 자르기 화면
        cropView = UIKit.Image(root.transform, "StoryCrop", new Color(0.06f, 0.06f, 0.07f), raycast: true).gameObject;
        UIKit.Stretch((RectTransform)cropView.transform);
        Rect sa = Screen.safeArea;
        RectTransform safe = SafeRect(cropView.transform, "Safe",
            new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height)),
            new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height)));
        Button cancel = UIKit.Button(safe, "취소", () => { cropView.SetActive(false); shareSheet.SetActive(true); }, new Color(0f, 0f, 0f, 0f), 36);
        Place((RectTransform)cancel.transform, new Vector2(0f, 1f), new Vector2(20f, -30f));
        ((RectTransform)cancel.transform).sizeDelta = new Vector2(160f, 88f);
        Text head = UIKit.Text(safe, "스토리에 올릴 부분", 38, TextAnchor.MiddleCenter, Color.white);
        head.fontStyle = FontStyle.Bold;
        TopBox(head.rectTransform, 30f, 88f, 200f);
        cropNext = UIKit.Button(safe, "올리기", FinishCrop, new Color(0f, 0f, 0f, 0f), 36);
        Text nextLabel = cropNext.GetComponentInChildren<Text>();
        nextLabel.color = new Color(0.45f, 0.62f, 1f);
        nextLabel.fontStyle = FontStyle.Bold;
        Place((RectTransform)cropNext.transform, new Vector2(1f, 1f), new Vector2(-20f, -30f));
        ((RectTransform)cropNext.transform).sizeDelta = new Vector2(200f, 88f);

        cropArea = UIKit.Rect(safe, "Area");
        cropArea.anchorMin = Vector2.zero;
        cropArea.anchorMax = Vector2.one;
        cropArea.offsetMin = new Vector2(40f, 330f);
        cropArea.offsetMax = new Vector2(-40f, -170f);
        Image frame = UIKit.Image(cropArea, "Frame", new Color(0f, 0f, 0f, 0f), raycast: true);
        cropFrame = frame.rectTransform;
        Place(cropFrame, new Vector2(0.5f, 0.5f), Vector2.zero);
        cropFrame.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        cropFrame.gameObject.AddComponent<RectMask2D>();
        var img = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
        img.transform.SetParent(cropFrame, false);
        cropImage = img.GetComponent<RawImage>();
        cropImage.raycastTarget = false;
        cropPan = frame.gameObject.AddComponent<CropPan>();
        cropPan.content = cropImage.rectTransform;
        // 자를 테두리 (흰 선) + 3등분 안내선
        foreach (var (min, max) in new[] {
            (new Vector2(0f, 1f), new Vector2(1f, 1f)), (new Vector2(0f, 0f), new Vector2(1f, 0f)),
            (new Vector2(0f, 0f), new Vector2(0f, 1f)), (new Vector2(1f, 0f), new Vector2(1f, 1f)) })
            Edge(cropFrame, min, max, 5f, Color.white);
        for (int i = 1; i <= 2; i++)
        {
            Edge(cropFrame, new Vector2(i / 3f, 0f), new Vector2(i / 3f, 1f), 2f, new Color(1f, 1f, 1f, 0.35f));
            Edge(cropFrame, new Vector2(0f, i / 3f), new Vector2(1f, i / 3f), 2f, new Color(1f, 1f, 1f, 0.35f));
        }

        Text hint = UIKit.Text(safe, "끌어서 옮기고, 두 손가락으로 벌려 크게 볼 수 있어요", 28, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
        hint.rectTransform.anchorMin = new Vector2(0f, 0f);
        hint.rectTransform.anchorMax = new Vector2(1f, 0f);
        hint.rectTransform.pivot = new Vector2(0.5f, 0f);
        hint.rectTransform.sizeDelta = new Vector2(0f, 50f);
        hint.rectTransform.anchoredPosition = new Vector2(0f, 230f);
        cropChips = new Image[cropAspects.Length];
        const float chipW = 200f, chipGap = 24f;
        float startX = -(cropAspects.Length * chipW + (cropAspects.Length - 1) * chipGap) / 2f + chipW / 2f;
        for (int i = 0; i < cropAspects.Length; i++)
        {
            int choice = i;
            Button chip = UIKit.Button(safe, cropAspects[i].label, () => SetCropAspect(choice), Color.white, 32);
            Image ci = chip.GetComponent<Image>();
            ci.sprite = HudIcons.Pill;
            ci.type = Image.Type.Sliced;
            ci.pixelsPerUnitMultiplier = 120f / 84f;
            Place((RectTransform)chip.transform, new Vector2(0.5f, 0f), new Vector2(startX + i * (chipW + chipGap), 100f));
            ((RectTransform)chip.transform).sizeDelta = new Vector2(chipW, 84f);
            cropChips[i] = ci;
        }
        cropView.SetActive(false);
    }

    static void Edge(RectTransform parent, Vector2 min, Vector2 max, float thickness, Color color)
    {
        Image e = UIKit.Image(parent, "Edge", color);
        e.rectTransform.anchorMin = min;
        e.rectTransform.anchorMax = max;
        e.rectTransform.sizeDelta = new Vector2(min.x == max.x ? thickness : 0f, min.y == max.y ? thickness : 0f);
        e.rectTransform.anchoredPosition = Vector2.zero;
    }

    static void ShareButton(RectTransform card, string label, Color face, Color ink, Sprite icon, float top, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(card, label, onClick, face, 36);
        Image bi = b.GetComponent<Image>();
        bi.sprite = HudIcons.Pill;
        bi.type = Image.Type.Sliced;
        bi.pixelsPerUnitMultiplier = 120f / 112f;
        Text t = b.GetComponentInChildren<Text>();
        t.color = ink;
        t.fontStyle = FontStyle.Bold;
        t.rectTransform.offsetMin = new Vector2(60f, 0f);
        TopBox((RectTransform)b.transform, top, 112f, 50f);
        Image ic = UIKit.Image(b.transform, "Icon", ink);
        ic.sprite = icon;
        Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f));
        ic.rectTransform.sizeDelta = new Vector2(60f, 60f);
        b.gameObject.AddComponent<PressScale>();
    }
}

/// <summary>
/// 자르기 틀 안의 사진을 끌어서 옮기고, 두 손가락(에디터는 휠)으로 확대한다. 틀은 항상 사진으로 꽉 찬다.
/// </summary>
public class CropPan : MonoBehaviour, IDragHandler, IScrollHandler
{
    public RectTransform content;
    Vector2 texSize = Vector2.one;
    Vector2 offset;
    float zoom = 1f;
    float pinchStart, zoomStart;

    RectTransform Frame => (RectTransform)transform;

    public void Reset(Vector2 textureSize)
    {
        texSize = textureSize;
        offset = Vector2.zero;
        zoom = 1f;
        Layout();
    }

    /// <summary>틀에 보이는 부분 (사진 텍스처의 0~1 좌표)</summary>
    public Rect UvRect
    {
        get
        {
            Vector2 f = Frame.rect.size, size = content.sizeDelta;
            Vector2 min = (-f * 0.5f - (offset - size * 0.5f)) / size;
            return new Rect(min, f / size);
        }
    }

    // 화면 크기가 바뀌어 틀 크기가 달라지면 다시 맞춘다.
    void OnRectTransformDimensionsChange()
    {
        if (content != null)
            Layout();
    }

    void Layout()
    {
        Vector2 f = Frame.rect.size;
        float cover = Mathf.Max(f.x / texSize.x, f.y / texSize.y);
        Vector2 size = texSize * cover * zoom;
        Vector2 slack = (size - f) * 0.5f;
        offset = new Vector2(Mathf.Clamp(offset.x, -slack.x, slack.x), Mathf.Clamp(offset.y, -slack.y, slack.y));
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = size;
        content.anchoredPosition = offset;
    }

    float CanvasScale
    {
        get
        {
            Canvas c = GetComponentInParent<Canvas>();
            return c != null ? c.scaleFactor : 1f;
        }
    }

    public void OnDrag(PointerEventData e)
    {
#if ENABLE_INPUT_SYSTEM
        if (Touch.activeTouches.Count >= 2)
            return;
#endif
        offset += e.delta / CanvasScale;
        Layout();
    }

    public void OnScroll(PointerEventData e)
    {
        zoom = Mathf.Clamp(zoom * (1f + e.scrollDelta.y * 0.05f), 1f, 5f);
        Layout();
    }

#if ENABLE_INPUT_SYSTEM
    void OnEnable() => EnhancedTouchSupport.Enable();
    void OnDisable() => EnhancedTouchSupport.Disable();

    void Update()
    {
        var touches = Touch.activeTouches;
        if (touches.Count < 2)
        {
            pinchStart = 0f;
            return;
        }
        float d = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
        if (pinchStart <= 0f)
        {
            pinchStart = Mathf.Max(d, 1f);
            zoomStart = zoom;
            return;
        }
        zoom = Mathf.Clamp(zoomStart * d / pinchStart, 1f, 5f);
        Layout();
    }
#endif
}
