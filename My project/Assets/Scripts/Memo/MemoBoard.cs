using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 경희스팟 메모판. 지도에서 경희스팟(또는 건물)을 탭하면 열린다.
/// - 메모판: 그 장소에 남긴 사진들을 종이 위 스티커처럼 기울여 붙인 콜라주로 보여 준다. 누르면 크게 보고 글을 읽는다.
///   오른쪽 위 작은 원형 버튼으로 새 메모를 쓴다 (경희스팟 근처에서만).
/// - 새 게시물: 사진 + 캡션 + 닉네임 + 위치를 넣어 공유한다 (SNS 새 게시물 화면처럼).
/// serverUrl 이 비어 있으면 기기 안의 가짜 서버(MockMemoService)를 쓴다.
/// </summary>
public class MemoBoard : MonoBehaviour
{
    public CollectController collectController;
    public PhotoCaptureScreen photoCapture;
    public KyungHeeSpots spots;

    [Tooltip("켜면 경희스팟이 없는 건물도 탭해서 메모판을 연다 (쓰기는 항상 가능)")]
    public bool openOnBuildingTap = false;
    [Tooltip("켜면 경희스팟 근처(interactRadius)에서만 메모를 쓸 수 있다")]
    public bool requireProximityToWrite = true;

    [Tooltip("메모 서버 주소 (예: http://192.168.0.5:5080). 비우면 가짜 서버")]
    public string serverUrl = "";

    const string AuthorKey = "memo_author";
    const string DeviceKey = "memo_device_id";
    const string MineKey = "memo_mine";

    // 콜라주 (종이·잉크)
    static readonly Color Paper = new Color(0.90f, 0.90f, 0.89f);
    static readonly Color PaperDark = new Color(0.83f, 0.83f, 0.82f);
    static readonly Color Ink = new Color(0.11f, 0.11f, 0.12f);
    static readonly Color InkQuiet = new Color(0.11f, 0.11f, 0.12f, 0.55f);
    static readonly Color NotePaper = new Color(1f, 0.98f, 0.91f);
    // 새 게시물 (흰 화면)
    static readonly Color Line = new Color(0f, 0f, 0f, 0.08f);
    static readonly Color Chip = new Color(0.94f, 0.95f, 0.96f);
    static readonly Color ShareBlue = new Color(0.29f, 0.37f, 0.98f);
    static readonly Color ErrorRed = new Color(0.86f, 0.25f, 0.25f);

    IMemoService service;
    CampusBuildingInfo building;
    readonly List<Texture2D> loadedPhotos = new List<Texture2D>();
    readonly List<(RectTransform holder, MemoData memo)> stickers = new List<(RectTransform, MemoData)>();
    float laidOutWidth;

    GameObject root, listView, writeView, viewer;
    Text title, subtitle, listStatus, writeError, writeHint, locationText;
    RectTransform collage;
    ScrollRect collageScroll;
    Button writeButton, postButton, photoButton, galleryButton, removePhotoButton;
    Image writeButtonFace;
    InputField authorInput, textInput;
    RawImage photoPreview;
    AspectRatioFitter photoFitter;
    GameObject photoEmpty;
    byte[] photoJpg;
    Texture2D photoTexture;
    bool posting;
    KyungHeeSpot currentSpot;
    bool canWrite;

    // 크게 보기
    RawImage viewerPhoto;
    AspectRatioFitter viewerFitter;
    Text viewerMeta, viewerText;
    Button viewerDelete;
    MemoData viewerMemo;

    public static string DeviceId
    {
        get
        {
            string id = PlayerPrefs.GetString(DeviceKey, "");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(DeviceKey, id);
            }
            return id;
        }
    }

    void Awake()
    {
        service = string.IsNullOrWhiteSpace(serverUrl) ? new MockMemoService() : new HttpMemoService(serverUrl.Trim());
        Canvas canvas = UIKit.CreateCanvas(transform, "MemoCanvas", 30);
        BuildUI(canvas.transform);
        if (photoCapture != null)
            photoCapture.Build(canvas.transform);
    }

    void OnEnable()
    {
        if (collectController != null)
        {
            collectController.SpotTapped += OpenSpot;
            collectController.BuildingTapped += OnBuildingTapped;
        }
    }

    void OnBuildingTapped(CampusBuildingInfo info)
    {
        if (openOnBuildingTap)
            Open(info);
    }

    public void OpenSpot(KyungHeeSpot spot)
    {
        currentSpot = spot;
        OpenBoard(spot.Building);
    }

    public void Open(CampusBuildingInfo target)
    {
        currentSpot = null;
        OpenBoard(target);
    }

    void OnDisable()
    {
        if (collectController != null)
        {
            collectController.SpotTapped -= OpenSpot;
            collectController.BuildingTapped -= OnBuildingTapped;
        }
        UIInputBlocker.SetModal(this, false);
    }

    void OpenBoard(CampusBuildingInfo target)
    {
        building = target;
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);
        title.text = target.DisplayName;
        subtitle.text = service.Description;
        ShowList();
    }

    public void Close()
    {
        root.SetActive(false);
        viewer.SetActive(false);
        UIInputBlocker.SetModal(this, false);
        ClearList();
    }

    // ---------- 콜라주 ----------

    void ShowList()
    {
        listView.SetActive(true);
        writeView.SetActive(false);
        viewer.SetActive(false);
        Reload();
    }

    void Reload()
    {
        ClearList();
        listStatus.text = "불러오는 중…";
        listStatus.gameObject.SetActive(true);
        string requested = building.buildingId;
        StartCoroutine(service.GetMemos(requested, (memos, error) =>
        {
            if (building == null || building.buildingId != requested || !root.activeSelf)
                return;
            if (error != null) { listStatus.text = error; return; }
            if (memos.Count == 0)
            {
                listStatus.text = "아직 남겨진 사진이 없어요\n오른쪽 위 메모 버튼으로 첫 사진을 붙여 보세요";
                return;
            }
            listStatus.gameObject.SetActive(false);
            LayoutCollage(memos);
        }));
    }

    void ClearList()
    {
        for (int i = collage.childCount - 1; i >= 0; i--)
            Destroy(collage.GetChild(i).gameObject);
        stickers.Clear();
        foreach (Texture2D t in loadedPhotos)
            if (t != null)
                Destroy(t);
        loadedPhotos.Clear();
    }

    /// <summary>
    /// 두 줄로 쌓되, 크기·기울기·좌우 위치를 메모마다 조금씩 다르게 해서 종이 위에 붙인 스티커처럼 보이게 한다.
    /// 값은 메모 id 로 정하므로 열 때마다 같은 자리에 붙는다. 화면 너비가 바뀌면 Update 에서 다시 배치한다.
    /// </summary>
    void LayoutCollage(List<MemoData> memos)
    {
        stickers.Clear();
        foreach (MemoData m in memos)
            stickers.Add((Sticker(m), m));
        Relayout();
    }

    void Relayout()
    {
        Canvas.ForceUpdateCanvases();
        float width = ((RectTransform)collageScroll.viewport).rect.width;
        laidOutWidth = width;
        const float pad = 40f, gap = 22f, overlap = 34f, border = 14f;
        float colW = (width - pad * 2f - gap) / 2f;
        float[] heights = { 24f, 120f }; // 오른쪽 줄은 조금 내려서 시작
        float[] aspects = { 0.78f, 1f, 1.32f, 0.88f };

        foreach (var (holder, m) in stickers)
        {
            if (holder == null)
                continue;
            var rng = new System.Random(StableHash(m.id));
            int col = heights[0] <= heights[1] ? 0 : 1;
            float aspect = m.HasPhoto ? aspects[rng.Next(aspects.Length)] : 1f;
            // 크기를 들쭉날쭉하게 (줄 너비의 62~88%)
            float w = (colW - border * 2f) * (0.66f + (float)rng.NextDouble() * 0.28f);
            float h = w / aspect;
            float slack = colW - w - border * 2f; // 테두리까지 줄 너비 안에 들어가게
            float x = pad + col * (colW + gap) + w * 0.5f + border + slack * (float)rng.NextDouble();
            float y = heights[col] + h * 0.5f + border;
            float angle = ((float)rng.NextDouble() - 0.5f) * 14f;
            holder.anchoredPosition = new Vector2(x, -y);
            holder.sizeDelta = new Vector2(w, h) + Vector2.one * border * 2f;
            holder.localRotation = Quaternion.Euler(0f, 0f, angle);
            heights[col] += h + border * 2f - overlap + (float)rng.NextDouble() * 24f;
        }
        collage.sizeDelta = new Vector2(collage.sizeDelta.x, Mathf.Max(heights[0], heights[1]) + 220f);
    }

    /// <summary>스티커 하나를 만든다 (자리와 크기는 Relayout 이 정한다).</summary>
    RectTransform Sticker(MemoData m)
    {
        const float border = 14f;
        var holder = new GameObject(m.id, typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(collage, false);
        holder.anchorMin = holder.anchorMax = new Vector2(0f, 1f);
        holder.pivot = new Vector2(0.5f, 0.5f);

        // 종이 그림자 + 흰 테두리 (누르면 크게 보기)
        Image shadow = UIKit.Image(holder, "Shadow", new Color(0f, 0f, 0f, 0.22f));
        shadow.sprite = HudIcons.TornPaper;
        shadow.type = Image.Type.Sliced;
        UIKit.Stretch(shadow.rectTransform);
        shadow.rectTransform.anchoredPosition = new Vector2(7f, -10f);
        Image paper = UIKit.Image(holder, "Paper", m.HasPhoto ? Color.white : NotePaper, raycast: true);
        paper.sprite = HudIcons.TornPaper;
        paper.type = Image.Type.Sliced;
        UIKit.Stretch(paper.rectTransform);
        var b = paper.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(() => OpenViewer(m));
        paper.gameObject.AddComponent<PressScale>();

        if (!m.HasPhoto)
        {
            // 사진이 없는 예전 메모는 쪽지처럼
            Text t = UIKit.Text(paper.transform, Clip(m.text, 60), 32, TextAnchor.UpperLeft, Ink);
            UIKit.Stretch(t.rectTransform, border + 18f, border + 18f);
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return holder;
        }

        // 사진 (기울어진 채로 잘리도록 스텐실 마스크)
        Image frame = UIKit.Image(paper.transform, "Frame", Color.white);
        frame.sprite = HudIcons.Rounded;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 4f;
        UIKit.Stretch(frame.rectTransform, border, border);
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        Image empty = UIKit.Image(frame.transform, "Loading", PaperDark);
        UIKit.Stretch(empty.rectTransform);
        var go = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(frame.transform, false);
        var raw = go.GetComponent<RawImage>();
        raw.raycastTarget = false;
        raw.enabled = false;
        var fit = go.GetComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;

        StartCoroutine(service.LoadPhoto(m, (tex, error) =>
        {
            if (raw == null || tex == null)
                return;
            // 내려받은 사진만 나중에 지운다 (예시 사진은 에셋이라 지우면 안 됨)
            if (!m.photoUrl.StartsWith("resource://")) loadedPhotos.Add(tex);
            raw.texture = tex;
            raw.enabled = true;
            fit.aspectRatio = (float)tex.width / Mathf.Max(1, tex.height);
            empty.enabled = false;
        }));
        return holder;
    }

    // ---------- 크게 보기 ----------

    void OpenViewer(MemoData m)
    {
        viewerMemo = m;
        viewerPhoto.texture = null;
        viewerPhoto.enabled = false;
        viewerMeta.text = $"{m.author}  ·  {FormatTime(m.CreatedAtLocal)}";
        viewerText.text = m.text;
        viewerDelete.gameObject.SetActive(IsMine(m.id));
        viewer.SetActive(true);
        if (m.HasPhoto)
            StartCoroutine(service.LoadPhoto(m, (tex, error) =>
            {
                if (tex == null || viewerMemo != m)
                    return;
                if (!m.photoUrl.StartsWith("resource://")) loadedPhotos.Add(tex);
                viewerPhoto.texture = tex;
                viewerPhoto.enabled = true;
                viewerFitter.aspectRatio = (float)tex.width / Mathf.Max(1, tex.height);
            }));
    }

    static string FormatTime(DateTime t)
    {
        if (t == DateTime.MinValue) return "";
        TimeSpan ago = DateTime.Now - t;
        if (ago.TotalMinutes < 1) return "방금";
        if (ago.TotalHours < 1) return $"{(int)ago.TotalMinutes}분 전";
        if (ago.TotalDays < 1) return $"{(int)ago.TotalHours}시간 전";
        return t.ToString("M월 d일 HH:mm");
    }

    void Delete(MemoData m)
    {
        StartCoroutine(service.DeleteMemo(m.id, DeviceId, (ok, error) =>
        {
            if (!ok) { ShowToast(error); return; }
            SetMine(m.id, false);
            viewer.SetActive(false);
            Reload();
        }));
    }

    // ---------- 새 게시물 ----------

    /// <summary>경희스팟 근처에 있는지. 스팟이 아닌 건물에서 열었거나 거리 제한을 껐으면 항상 true.</summary>
    bool CanWrite(out float distance)
    {
        distance = 0f;
        if (!requireProximityToWrite || currentSpot == null || spots == null || collectController == null || collectController.campusMap == null)
            return true;
        distance = currentSpot.DistanceFrom(collectController.campusMap.player.position);
        return distance <= spots.interactRadius;
    }

    void Update()
    {
        if (root == null || !root.activeSelf || !listView.activeSelf)
            return;
        if (stickers.Count > 0 && Mathf.Abs(((RectTransform)collageScroll.viewport).rect.width - laidOutWidth) > 1f)
            Relayout();
        // 걸어서 가까워지면 바로 쓰기 버튼이 켜지도록 매 프레임 확인한다.
        canWrite = CanWrite(out float d);
        writeButtonFace.color = canWrite ? Ink : new Color(Ink.r, Ink.g, Ink.b, 0.35f);
        writeHint.transform.parent.gameObject.SetActive(!canWrite);
        if (!canWrite)
            writeHint.text = $"경희스팟까지 {d:F0}m  ·  {spots.interactRadius:F0}m 안에서 사진을 남길 수 있어요";
    }

    void OnWriteButton()
    {
        if (!canWrite)
        {
            ShowToast("경희스팟 가까이 가야 사진을 남길 수 있어요");
            return;
        }
        ShowWrite();
    }

    void ShowWrite()
    {
        listView.SetActive(false);
        viewer.SetActive(false);
        writeView.SetActive(true);
        authorInput.text = PlayerPrefs.GetString(AuthorKey, "");
        textInput.text = "";
        writeError.text = "";
        locationText.text = building != null ? building.DisplayName : "";
        SetPhoto(null, null);
    }

    void TakePhoto()
    {
        if (photoCapture == null) return;
        photoCapture.Open((jpg, tex) => SetPhoto(jpg, tex));
    }

    /// <summary>폰 앨범에서 사진을 고른다 (NativeGallery). 긴 변을 1280px 로 줄여 JPEG 로 올린다.</summary>
    void PickFromGallery()
    {
        NativeGallery.GetImageFromGallery(path =>
        {
            if (string.IsNullOrEmpty(path))
                return; // 고르지 않고 닫음
            Texture2D tex = NativeGallery.LoadImageAtPath(path, 1280, markTextureNonReadable: false, generateMipmaps: false);
            if (tex == null)
            {
                writeError.text = "사진을 불러오지 못했어요. 다른 사진을 골라 주세요.";
                return;
            }
            // 플러그인의 크기 제한이 적용되지 않는 경우(에디터 등)도 있어 한 번 더 줄인다.
            tex = PhotoCaptureScreen.Downscale(tex, 1280);
            SetPhoto(tex.EncodeToJPG(80), tex);
        }, "메모에 올릴 사진 고르기", "image/*");
    }

    void SetPhoto(byte[] jpg, Texture2D tex)
    {
        if (photoTexture != null && photoTexture != tex)
            Destroy(photoTexture);
        photoJpg = jpg;
        photoTexture = tex;
        photoPreview.texture = tex;
        photoPreview.enabled = tex != null;
        if (tex != null && tex.height > 0)
            photoFitter.aspectRatio = (float)tex.width / tex.height;
        photoEmpty.SetActive(tex == null);
        UIKit.SetLabel(photoButton, tex != null ? "다시 찍기" : "사진 찍기");
        removePhotoButton.gameObject.SetActive(tex != null);
        if (tex != null)
            writeError.text = "";
    }

    void Post()
    {
        if (posting) return;
        string author = authorInput.text.Trim();
        string text = textInput.text.Trim();
        if (photoJpg == null) { writeError.text = "사진을 추가해 주세요."; return; }
        if (text.Length == 0) { writeError.text = "캡션을 써 주세요."; return; }
        if (author.Length == 0) { writeError.text = "닉네임을 써 주세요."; return; }

        posting = true;
        UIKit.SetLabel(postButton, "공유하는 중…");
        writeError.text = "";
        PlayerPrefs.SetString(AuthorKey, author);
        var memo = new NewMemo { author = author, text = text, deviceId = DeviceId, photoJpg = photoJpg };
        StartCoroutine(service.CreateMemo(building.buildingId, memo, (created, error) =>
        {
            posting = false;
            UIKit.SetLabel(postButton, "공유하기");
            if (created == null) { writeError.text = error; return; }
            SetMine(created.id, true);
            SetPhoto(null, null);
            ShowList();
            ShowToast("사진을 붙였어요!");
        }));
    }

    void ShowToast(string message)
    {
        var hud = FindAnyObjectByType<GameHUD>();
        if (hud != null) hud.Toast(message);
    }

    // ---------- 내 메모 기록 (서버는 작성 기기를 알려 주지 않으므로 기기에 따로 적어 둔다) ----------

    [Serializable] class IdList { public List<string> ids = new List<string>(); }

    static IdList Mine => JsonUtility.FromJson<IdList>(PlayerPrefs.GetString(MineKey, "{}")) ?? new IdList();

    static bool IsMine(string id) => Mine.ids.Contains(id);

    static void SetMine(string id, bool mine)
    {
        IdList list = Mine;
        list.ids.Remove(id);
        if (mine) list.ids.Add(id);
        PlayerPrefs.SetString(MineKey, JsonUtility.ToJson(list));
        PlayerPrefs.Save();
    }

    static int StableHash(string s)
    {
        int h = 17;
        if (s != null)
            foreach (char c in s)
                h = h * 31 + c;
        return h;
    }

    static string Clip(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

    // ---------- 화면 만들기 ----------

    void BuildUI(Transform canvas)
    {
        root = UIKit.Image(canvas, "MemoBoard", Paper, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)root.transform);
        Rect sa = Screen.safeArea;
        Vector2 safeMin = new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height));
        Vector2 safeMax = new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height));

        BuildCollageView(safeMin, safeMax);
        BuildWriteView(safeMin, safeMax);
        BuildViewer();
        root.SetActive(false);
    }

    RectTransform SafeRect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        RectTransform r = UIKit.Rect(parent, name);
        r.anchorMin = min;
        r.anchorMax = max;
        r.offsetMin = r.offsetMax = Vector2.zero;
        return r;
    }

    void BuildCollageView(Vector2 safeMin, Vector2 safeMax)
    {
        // 종이 배경 (위는 밝고 아래는 살짝 어둡게)
        var bg = new GameObject("PaperBg", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        bg.transform.SetParent(root.transform, false);
        bg.texture = HudIcons.Gradient(new Color(0.93f, 0.93f, 0.92f), new Color(0.84f, 0.84f, 0.83f));
        bg.raycastTarget = false;
        UIKit.Stretch(bg.rectTransform);

        listView = UIKit.Rect(root.transform, "Collage").gameObject;
        UIKit.Stretch((RectTransform)listView.transform);
        RectTransform safe = SafeRect(listView.transform, "Safe", safeMin, safeMax);

        // 콜라주 (자유 배치라 레이아웃 그룹 없이 내용 높이를 직접 정한다)
        RectTransform area = UIKit.Rect(safe, "Area");
        UIKit.StretchBetween(area, 0f, 190f);
        collageScroll = area.gameObject.AddComponent<ScrollRect>();
        collageScroll.horizontal = false;
        collageScroll.scrollSensitivity = 30f;
        RectTransform viewport = UIKit.Rect(area, "Viewport");
        UIKit.Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        collage = UIKit.Rect(viewport, "Content");
        collage.anchorMin = new Vector2(0f, 1f);
        collage.anchorMax = new Vector2(1f, 1f);
        collage.pivot = new Vector2(0.5f, 1f);
        collage.offsetMin = collage.offsetMax = Vector2.zero;
        collage.sizeDelta = new Vector2(0f, 400f);
        collageScroll.viewport = viewport;
        collageScroll.content = collage;

        listStatus = UIKit.Text(area, "", 38, TextAnchor.MiddleCenter, InkQuiet);
        UIKit.Stretch(listStatus.rectTransform, 80f, 300f);

        // 위: 닫기 · 장소 이름 · 쓰기
        Button close = RoundButton(safe, "Close", HudIcons.Close, 96f, Color.white, Ink, Close);
        Place((RectTransform)close.transform, new Vector2(0f, 1f), new Vector2(32f, -40f));
        title = UIKit.Text(safe, "", 50, TextAnchor.MiddleCenter, Ink);
        title.fontStyle = FontStyle.Bold;
        TopBox(title.rectTransform, 40f, 70f, 150f);
        subtitle = UIKit.Text(safe, "", 26, TextAnchor.MiddleCenter, InkQuiet);
        TopBox(subtitle.rectTransform, 112f, 40f, 150f);
        writeButton = RoundButton(safe, "Write", HudIcons.MemoWrite, 96f, Ink, Color.white, OnWriteButton);
        writeButtonFace = writeButton.transform.Find("Face").GetComponent<Image>();
        Place((RectTransform)writeButton.transform, new Vector2(1f, 1f), new Vector2(-32f, -40f));

        // 아래: 멀리 있을 때만 거리 안내
        Image hintPill = UIKit.Image(safe, "Hint", new Color(Ink.r, Ink.g, Ink.b, 0.82f));
        hintPill.sprite = HudIcons.Pill;
        hintPill.type = Image.Type.Sliced;
        hintPill.pixelsPerUnitMultiplier = 120f / 84f;
        Place(hintPill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f));
        hintPill.rectTransform.sizeDelta = new Vector2(900f, 84f);
        writeHint = UIKit.Text(hintPill.transform, "", 28, TextAnchor.MiddleCenter, Color.white);
        UIKit.Stretch(writeHint.rectTransform, 24f, 0f);
    }

    void BuildViewer()
    {
        viewer = UIKit.Image(root.transform, "Viewer", new Color(0.05f, 0.05f, 0.06f, 0.92f), raycast: true).gameObject;
        UIKit.Stretch((RectTransform)viewer.transform);

        RectTransform box = UIKit.Rect(viewer.transform, "PhotoBox");
        box.anchorMin = new Vector2(0f, 0.27f);
        box.anchorMax = new Vector2(1f, 0.92f);
        box.offsetMin = new Vector2(40f, 0f);
        box.offsetMax = new Vector2(-40f, 0f);
        var go = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        go.transform.SetParent(box, false);
        viewerPhoto = go.GetComponent<RawImage>();
        viewerPhoto.raycastTarget = false;
        viewerFitter = go.GetComponent<AspectRatioFitter>();
        viewerFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

        Image card = UIKit.Image(viewer.transform, "Card", Color.white);
        card.sprite = HudIcons.TornPaper;
        card.type = Image.Type.Sliced;
        RectTransform cr = card.rectTransform;
        cr.anchorMin = new Vector2(0f, 0.05f);
        cr.anchorMax = new Vector2(1f, 0.24f);
        cr.offsetMin = new Vector2(40f, 0f);
        cr.offsetMax = new Vector2(-40f, 0f);
        viewerMeta = UIKit.Text(card.transform, "", 30, TextAnchor.UpperLeft, InkQuiet);
        TopBox(viewerMeta.rectTransform, 40f, 46f, 44f);
        viewerText = UIKit.Text(card.transform, "", 38, TextAnchor.UpperLeft, Ink);
        viewerText.rectTransform.anchorMin = Vector2.zero;
        viewerText.rectTransform.anchorMax = Vector2.one;
        viewerText.rectTransform.offsetMin = new Vector2(44f, 110f);
        viewerText.rectTransform.offsetMax = new Vector2(-44f, -100f);
        viewerText.verticalOverflow = VerticalWrapMode.Truncate;
        viewerDelete = UIKit.Button(card.transform, "삭제", () => { if (viewerMemo != null) Delete(viewerMemo); }, new Color(0f, 0f, 0f, 0f), 32);
        viewerDelete.GetComponentInChildren<Text>().color = ErrorRed;
        Place((RectTransform)viewerDelete.transform, new Vector2(1f, 0f), new Vector2(-30f, 24f));
        ((RectTransform)viewerDelete.transform).sizeDelta = new Vector2(160f, 80f);

        Button close = RoundButton(viewer.transform, "Close", HudIcons.Close, 96f, Color.white, Ink, () => viewer.SetActive(false));
        Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-40f, -60f));
        viewer.SetActive(false);
    }

    void BuildWriteView(Vector2 safeMin, Vector2 safeMax)
    {
        writeView = UIKit.Image(root.transform, "NewPost", Color.white, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)writeView.transform);
        RectTransform safe = SafeRect(writeView.transform, "Safe", safeMin, safeMax);

        // 위: 뒤로 · 새 게시물
        Button back = RoundButton(safe, "Back", HudIcons.Chevron, 88f, Color.white, Ink, ShowList);
        Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(32f, -30f));
        back.transform.Find("Face/Icon").localRotation = Quaternion.Euler(0f, 0f, 180f);
        var ring = back.transform.Find("Shadow").GetComponent<Image>();
        ring.color = Line;
        Text head = UIKit.Text(safe, "새 게시물", 40, TextAnchor.MiddleCenter, Ink);
        head.fontStyle = FontStyle.Bold;
        TopBox(head.rectTransform, 30f, 88f, 140f);
        Image hr = UIKit.Image(safe, "Line", Line);
        TopBox(hr.rectTransform, 140f, 2f, 0f);

        // 내용 (스크롤)
        RectTransform area = UIKit.Rect(safe, "Form");
        UIKit.StretchBetween(area, 190f, 142f);
        RectTransform form = UIKit.ScrollList(area, 0f, new RectOffset(0, 0, 30, 40));

        // 사진 미리보기
        RectTransform photoRow = UIKit.Rect(form, "PhotoRow");
        UIKit.Size(photoRow, 480f);
        Image frame = UIKit.Image(photoRow, "Frame", Chip, raycast: true);
        frame.sprite = HudIcons.Rounded;
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 1.2f;
        Place(frame.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        frame.rectTransform.sizeDelta = new Vector2(620f, 460f);
        frame.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var tap = frame.gameObject.AddComponent<Button>();
        tap.transition = Selectable.Transition.None;
        tap.onClick.AddListener(() => { if (photoTexture == null) TakePhoto(); });
        var pv = new GameObject("Photo", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        pv.transform.SetParent(frame.transform, false);
        photoPreview = pv.GetComponent<RawImage>();
        photoPreview.raycastTarget = false;
        photoFitter = pv.GetComponent<AspectRatioFitter>();
        photoFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        photoEmpty = UIKit.Rect(frame.transform, "Empty").gameObject;
        UIKit.Stretch((RectTransform)photoEmpty.transform);
        Image cam = UIKit.Image(photoEmpty.transform, "Icon", InkQuiet);
        cam.sprite = HudIcons.Camera;
        Place(cam.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f));
        cam.rectTransform.sizeDelta = new Vector2(110f, 110f);
        Text camHint = UIKit.Text(photoEmpty.transform, "눌러서 사진 찍기", 30, TextAnchor.MiddleCenter, InkQuiet);
        Place(camHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -70f));
        camHint.rectTransform.sizeDelta = new Vector2(500f, 50f);

        // 캡션
        textInput = UIKit.Input(form, "캡션 추가...", 38, true, 500);
        textInput.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        textInput.textComponent.color = Ink;
        ((Text)textInput.placeholder).color = InkQuiet;
        ((Text)textInput.placeholder).fontStyle = FontStyle.Normal;
        UIKit.Size(textInput, 260f);
        UIKit.Stretch(textInput.textComponent.rectTransform, 40f, 20f);
        UIKit.Stretch(((Text)textInput.placeholder).rectTransform, 40f, 20f);

        // 사진 칩
        RectTransform chips = UIKit.Rect(form, "Chips");
        UIKit.Size(chips, 110f);
        var ch = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
        ch.spacing = 16f;
        ch.padding = new RectOffset(40, 40, 10, 10);
        ch.childControlWidth = ch.childControlHeight = true;
        ch.childForceExpandWidth = false;
        ch.childForceExpandHeight = true;
        photoButton = ChipButton(chips, HudIcons.Camera, "사진 찍기", 250f, TakePhoto);
        galleryButton = ChipButton(chips, HudIcons.Scrapbook, "앨범에서 고르기", 320f, PickFromGallery);
        removePhotoButton = ChipButton(chips, HudIcons.Close, "빼기", 160f, () => SetPhoto(null, null));

        // 닉네임 줄
        RectTransform nameRow = Row(form, HudIcons.Person, "닉네임");
        authorInput = UIKit.Input(nameRow, "이름 입력", 34, false, 20);
        authorInput.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        authorInput.textComponent.color = Ink;
        authorInput.textComponent.alignment = TextAnchor.MiddleRight;
        ((Text)authorInput.placeholder).color = InkQuiet;
        ((Text)authorInput.placeholder).alignment = TextAnchor.MiddleRight;
        ((Text)authorInput.placeholder).fontStyle = FontStyle.Normal;
        RectTransform ar = (RectTransform)authorInput.transform;
        ar.anchorMin = new Vector2(0.4f, 0f);
        ar.anchorMax = new Vector2(1f, 1f);
        ar.offsetMin = Vector2.zero;
        ar.offsetMax = new Vector2(-30f, 0f);

        // 위치 줄 + 칩
        RectTransform locRow = Row(form, HudIcons.Pin, "위치");
        locationText = UIKit.Text(locRow, "", 32, TextAnchor.MiddleRight, InkQuiet);
        locationText.rectTransform.anchorMin = new Vector2(0.35f, 0f);
        locationText.rectTransform.anchorMax = new Vector2(1f, 1f);
        locationText.rectTransform.offsetMin = Vector2.zero;
        locationText.rectTransform.offsetMax = new Vector2(-40f, 0f);
        RectTransform locChips = UIKit.Rect(form, "LocationChips");
        UIKit.Size(locChips, 90f);
        var lh = locChips.gameObject.AddComponent<HorizontalLayoutGroup>();
        lh.spacing = 14f;
        lh.padding = new RectOffset(40, 40, 6, 6);
        lh.childControlWidth = lh.childControlHeight = true;
        lh.childForceExpandWidth = false;
        lh.childForceExpandHeight = true;
        StaticChip(locChips, "경희대학교 국제캠퍼스", 360f);
        StaticChip(locChips, "수원", 120f);
        RectTransform infoRow = UIKit.Rect(form, "Info");
        UIKit.Size(infoRow, 90f);
        Text info = UIKit.Text(infoRow, "이 사진은 이 장소를 찾은 다른 사람들도 볼 수 있어요. 메모판에 콜라주로 붙어요.", 26, TextAnchor.UpperLeft, InkQuiet);
        UIKit.Stretch(info.rectTransform, 40f, 8f);

        writeError = UIKit.Text(form, "", 32, TextAnchor.MiddleCenter, ErrorRed);
        UIKit.Size(writeError, 70f);

        // 아래: 공유하기
        postButton = UIKit.Button(safe, "공유하기", Post, ShareBlue, 40);
        var pimg = postButton.GetComponent<Image>();
        pimg.sprite = HudIcons.Pill;
        pimg.type = Image.Type.Sliced;
        pimg.pixelsPerUnitMultiplier = 120f / 70f;
        postButton.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        RectTransform pr = (RectTransform)postButton.transform;
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0f);
        pr.pivot = new Vector2(0.5f, 0f);
        pr.offsetMin = new Vector2(40f, 40f);
        pr.offsetMax = new Vector2(-40f, 150f);

        writeView.SetActive(false);
    }

    /// <summary>아이콘 · 이름 · (오른쪽 내용) 이 있는 설정 줄. 아래에 옅은 선.</summary>
    RectTransform Row(RectTransform form, Sprite icon, string label)
    {
        RectTransform row = UIKit.Rect(form, label);
        UIKit.Size(row, 110f);
        Image ic = UIKit.Image(row, "Icon", Ink);
        ic.sprite = icon;
        Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(40f, 0f));
        ic.rectTransform.sizeDelta = new Vector2(52f, 52f);
        Text t = UIKit.Text(row, label, 36, TextAnchor.MiddleLeft, Ink);
        t.rectTransform.anchorMin = new Vector2(0f, 0f);
        t.rectTransform.anchorMax = new Vector2(0.4f, 1f);
        t.rectTransform.offsetMin = new Vector2(116f, 0f);
        t.rectTransform.offsetMax = Vector2.zero;
        return row;
    }

    Button ChipButton(Transform parent, Sprite icon, string label, float width, UnityEngine.Events.UnityAction onClick)
    {
        Button b = UIKit.Button(parent, label, onClick, Chip, 30);
        Image img = b.GetComponent<Image>();
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 120f / 90f;
        UIKit.Size(b, -1f, width);
        Text t = b.GetComponentInChildren<Text>();
        t.color = Ink;
        t.rectTransform.offsetMin = new Vector2(64f, 0f);
        Image ic = UIKit.Image(b.transform, "Icon", Ink);
        ic.sprite = icon;
        Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(26f, 0f));
        ic.rectTransform.sizeDelta = new Vector2(40f, 40f);
        return b;
    }

    static void StaticChip(Transform parent, string label, float width)
    {
        Image img = UIKit.Image(parent, label, Chip);
        img.sprite = HudIcons.Pill;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 120f / 78f;
        UIKit.Size(img, -1f, width);
        Text t = UIKit.Text(img.transform, label, 28, TextAnchor.MiddleCenter, Ink);
        UIKit.Stretch(t.rectTransform, 16f, 0f);
    }

    /// <summary>그림자 + 둥근 얼굴(이름 "Face") + 아이콘(이름 "Icon") 인 원형 버튼</summary>
    static Button RoundButton(Transform parent, string name, Sprite icon, float size, Color face, Color iconColor, UnityEngine.Events.UnityAction onClick)
    {
        Image hit = UIKit.Image(parent, name, new Color(0f, 0f, 0f, 0f), raycast: true);
        hit.rectTransform.sizeDelta = new Vector2(size, size);
        var b = hit.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.onClick.AddListener(onClick);
        hit.gameObject.AddComponent<PressScale>();
        Image shadow = UIKit.Image(hit.transform, "Shadow", new Color(0f, 0f, 0f, 0.18f));
        shadow.sprite = HudIcons.Circle;
        Place(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -4f));
        shadow.rectTransform.sizeDelta = new Vector2(size + 4f, size + 4f);
        Image f = UIKit.Image(hit.transform, "Face", face);
        f.sprite = HudIcons.Circle;
        Place(f.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        f.rectTransform.sizeDelta = new Vector2(size, size);
        Image ic = UIKit.Image(f.transform, "Icon", iconColor);
        ic.sprite = icon;
        Place(ic.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        ic.rectTransform.sizeDelta = new Vector2(size * 0.48f, size * 0.48f);
        return b;
    }

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    /// <summary>위에서 top 만큼 내려온 높이 height 인 칸 (좌우 여백 side)</summary>
    static void TopBox(RectTransform rt, float top, float height, float side)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(side, -top - height);
        rt.offsetMax = new Vector2(-side, -top);
    }
}
