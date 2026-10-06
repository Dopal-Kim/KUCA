using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건물 메모판. 지도에서 건물을 탭하면 열린다.
/// - 목록: 그 건물의 메모(사진, 작성자, 시간, 글)를 최신순으로 보여 준다. 내가 쓴 메모는 지울 수 있다.
/// - 쓰기: 닉네임, 글, 사진(앱 안 카메라)을 넣어 올린다.
/// serverUrl 이 비어 있으면 기기 안의 가짜 서버(MockMemoService)를 쓴다.
/// </summary>
public class MemoBoard : MonoBehaviour
{
    public CollectController collectController;
    public PhotoCaptureScreen photoCapture;

    [Tooltip("메모 서버 주소 (예: http://192.168.0.5:5080). 비우면 가짜 서버")]
    public string serverUrl = "";

    const string AuthorKey = "memo_author";
    const string DeviceKey = "memo_device_id";
    const string MineKey = "memo_mine";

    IMemoService service;
    CampusBuildingInfo building;
    readonly List<Texture2D> loadedPhotos = new List<Texture2D>();

    GameObject root, listView, writeView;
    Text title, subtitle, listStatus, writeError;
    RectTransform listContent;
    Button writeButton, postButton, photoButton, removePhotoButton;
    InputField authorInput, textInput;
    RawImage photoPreview;
    AspectRatioFitter photoFitter;
    GameObject photoBox;
    byte[] photoJpg;
    Texture2D photoTexture;
    bool posting;

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
            collectController.BuildingTapped += Open;
    }

    void OnDisable()
    {
        if (collectController != null)
            collectController.BuildingTapped -= Open;
        UIInputBlocker.SetModal(this, false);
    }

    public void Open(CampusBuildingInfo target)
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
        UIInputBlocker.SetModal(this, false);
        ClearList();
    }

    // ---------- 목록 ----------

    void ShowList()
    {
        listView.SetActive(true);
        writeView.SetActive(false);
        writeButton.gameObject.SetActive(true);
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
            if (memos.Count == 0) { listStatus.text = "아직 메모가 없어요.\n첫 메모를 남겨 보세요!"; return; }
            listStatus.gameObject.SetActive(false);
            foreach (MemoData m in memos)
                AddCard(m);
        }));
    }

    void ClearList()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            if (listContent.GetChild(i) != listStatus.transform)
                Destroy(listContent.GetChild(i).gameObject);
        foreach (Texture2D t in loadedPhotos)
            if (t != null)
                Destroy(t);
        loadedPhotos.Clear();
    }

    void AddCard(MemoData m)
    {
        Image card = UIKit.Image(listContent, "Memo", UIKit.Card);
        UIKit.Vertical(card, 16f, new RectOffset(32, 32, 28, 28));

        // 작성자 · 시간 (+ 내 메모면 삭제)
        RectTransform meta = UIKit.Rect(card.transform, "Meta");
        UIKit.Size(meta, 56f);
        var h = meta.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        Text who = UIKit.Text(meta, $"{m.author}  ·  {FormatTime(m.CreatedAtLocal)}", 34, TextAnchor.MiddleLeft, UIKit.Quiet);
        UIKit.Size(who, flexibleWidth: 1f);
        if (IsMine(m.id))
        {
            Button del = UIKit.Button(meta, "삭제", () => Delete(m), UIKit.Danger, 32);
            UIKit.Size(del, width: 130f);
        }

        if (m.HasPhoto)
        {
            RawImage raw = UIKit.PhotoBox(card.transform, 620f, out AspectRatioFitter fitter);
            StartCoroutine(service.LoadPhoto(m, (tex, error) =>
            {
                if (raw == null) return;
                if (tex == null) return;
                // 내려받은 사진만 나중에 지운다 (예시 사진은 에셋이라 지우면 안 됨)
                if (!m.photoUrl.StartsWith("resource://")) loadedPhotos.Add(tex);
                UIKit.ShowPhoto(raw, fitter, tex);
            }));
        }

        UIKit.Text(card.transform, m.text, 42, TextAnchor.UpperLeft);
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
            Reload();
        }));
    }

    // ---------- 쓰기 ----------

    void ShowWrite()
    {
        listView.SetActive(false);
        writeView.SetActive(true);
        writeButton.gameObject.SetActive(false);
        authorInput.text = PlayerPrefs.GetString(AuthorKey, "");
        textInput.text = "";
        writeError.text = "";
        SetPhoto(null, null);
    }

    void TakePhoto()
    {
        if (photoCapture == null) return;
        photoCapture.Open((jpg, tex) => SetPhoto(jpg, tex));
    }

    void SetPhoto(byte[] jpg, Texture2D tex)
    {
        if (photoTexture != null && photoTexture != tex)
            Destroy(photoTexture);
        photoJpg = jpg;
        photoTexture = tex;
        photoBox.SetActive(tex != null);
        UIKit.ShowPhoto(photoPreview, photoFitter, tex);
        UIKit.SetLabel(photoButton, tex != null ? "다시 찍기" : "사진 찍기");
        removePhotoButton.gameObject.SetActive(tex != null);
    }

    void Post()
    {
        if (posting) return;
        string author = authorInput.text.Trim();
        string text = textInput.text.Trim();
        if (author.Length == 0) { writeError.text = "닉네임을 써 주세요."; return; }
        if (text.Length == 0) { writeError.text = "메모 내용을 써 주세요."; return; }

        posting = true;
        UIKit.SetLabel(postButton, "올리는 중…");
        writeError.text = "";
        PlayerPrefs.SetString(AuthorKey, author);
        var memo = new NewMemo { author = author, text = text, deviceId = DeviceId, photoJpg = photoJpg };
        StartCoroutine(service.CreateMemo(building.buildingId, memo, (created, error) =>
        {
            posting = false;
            UIKit.SetLabel(postButton, "올리기");
            if (created == null) { writeError.text = error; return; }
            SetMine(created.id, true);
            SetPhoto(null, null);
            ShowList();
            ShowToast("메모를 남겼어요!");
        }));
    }

    void ShowToast(string message)
    {
        var hud = FindFirstObjectByType<GameHUD>();
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

    // ---------- 화면 만들기 ----------

    const float HeaderHeight = 200f;
    const float FooterHeight = 200f;

    void BuildUI(Transform canvas)
    {
        root = UIKit.Image(canvas, "MemoBoard", UIKit.Background, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)root.transform);

        // 머리: 건물 이름, 서버 종류, 닫기
        RectTransform header = UIKit.Rect(root.transform, "Header");
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, HeaderHeight);
        header.anchoredPosition = new Vector2(0f, -40f);
        title = UIKit.Text(header, "", 56, TextAnchor.UpperLeft);
        title.fontStyle = FontStyle.Bold;
        title.rectTransform.anchorMin = new Vector2(0f, 0.45f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.offsetMin = new Vector2(48f, 0f);
        title.rectTransform.offsetMax = new Vector2(-260f, 0f);
        subtitle = UIKit.Text(header, "", 30, TextAnchor.UpperLeft, UIKit.Quiet);
        subtitle.rectTransform.anchorMin = new Vector2(0f, 0f);
        subtitle.rectTransform.anchorMax = new Vector2(1f, 0.45f);
        subtitle.rectTransform.offsetMin = new Vector2(48f, 0f);
        subtitle.rectTransform.offsetMax = new Vector2(-260f, 0f);
        Button close = UIKit.Button(header, "닫기", Close, UIKit.ButtonGray, 40);
        var cr = (RectTransform)close.transform;
        cr.anchorMin = cr.anchorMax = cr.pivot = new Vector2(1f, 1f);
        cr.sizeDelta = new Vector2(200f, 110f);
        cr.anchoredPosition = new Vector2(-40f, 0f);

        float top = HeaderHeight + 60f;

        // 목록
        listView = UIKit.Rect(root.transform, "List").gameObject;
        UIKit.StretchBetween((RectTransform)listView.transform, FooterHeight, top);
        listContent = UIKit.ScrollList((RectTransform)listView.transform, 28f, new RectOffset(40, 40, 8, 40));
        listStatus = UIKit.Text(listContent, "", 40, TextAnchor.MiddleCenter, UIKit.Quiet);
        UIKit.Size(listStatus, 300f);

        // 쓰기
        writeView = UIKit.Rect(root.transform, "Write").gameObject;
        UIKit.StretchBetween((RectTransform)writeView.transform, 0f, top);
        RectTransform form = UIKit.ScrollList((RectTransform)writeView.transform, 24f, new RectOffset(40, 40, 8, 60));
        UIKit.Text(form, "닉네임", 34, TextAnchor.LowerLeft, UIKit.Quiet);
        authorInput = UIKit.Input(form, "다른 사람에게 보일 이름", 42, false, 20);
        UIKit.Size(authorInput, 110f);
        UIKit.Text(form, "메모", 34, TextAnchor.LowerLeft, UIKit.Quiet);
        textInput = UIKit.Input(form, "이 장소에 남길 이야기 (최대 500자)", 42, true, 500);
        UIKit.Size(textInput, 380f);

        photoBox = UIKit.PhotoBox(form, 620f, out photoFitter).transform.parent.gameObject;
        photoPreview = photoBox.GetComponentInChildren<RawImage>();
        RectTransform photoRow = UIKit.Rect(form, "PhotoButtons");
        UIKit.Size(photoRow, 120f);
        UIKit.Horizontal(photoRow, 24f);
        photoButton = UIKit.Button(photoRow, "사진 찍기", TakePhoto, UIKit.ButtonGray, 42);
        removePhotoButton = UIKit.Button(photoRow, "사진 빼기", () => SetPhoto(null, null), UIKit.ButtonGray, 42);

        writeError = UIKit.Text(form, "", 36, TextAnchor.MiddleLeft, new Color(1f, 0.55f, 0.55f));
        RectTransform postRow = UIKit.Rect(form, "PostButtons");
        UIKit.Size(postRow, 140f);
        UIKit.Horizontal(postRow, 24f);
        UIKit.Button(postRow, "취소", ShowList, UIKit.ButtonGray);
        postButton = UIKit.Button(postRow, "올리기", Post, UIKit.Accent);

        // 아래: 메모 쓰기
        writeButton = UIKit.Button(root.transform, "+ 메모 쓰기", ShowWrite, UIKit.Accent, 48);
        var wr = (RectTransform)writeButton.transform;
        wr.anchorMin = new Vector2(0f, 0f);
        wr.anchorMax = new Vector2(1f, 0f);
        wr.pivot = new Vector2(0.5f, 0f);
        wr.sizeDelta = new Vector2(-80f, 140f);
        wr.anchoredPosition = new Vector2(0f, 40f);

        root.SetActive(false);
    }
}
