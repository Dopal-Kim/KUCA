using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 내 사진첩: 이 기기에서 올린 게시물(모든 장소)을 3열 격자로 모아 본다. 첫 화면 오른쪽 위 사진 버튼으로 연다.
/// 서버는 작성 기기로 찾는 기능이 없어서, 올릴 때 기기에 적어 둔 메모 id(Mine)를 하나씩 받아 온다.
/// 사진을 누르면 게시물 화면이 열리고, 거기서 인스타그램 스토리·카카오톡으로 공유할 수 있다.
/// 앱을 열 때(또는 다시 돌아올 때) 내 게시물의 좋아요 수를 지난번 본 값과 비교해, 늘었으면 HUD 사진첩 버튼에 점을 켠다.
/// </summary>
public partial class MemoBoard
{
    GameObject album;
    RectTransform albumGrid;
    Text albumCount, albumEmpty;
    Button albumEmptyAction;   // 사진이 없을 때: 가까운 경희스팟 메모판 열기 (빈 화면이 막다른 길이 되지 않게)
    bool albumMode;
    int albumRequest;

    const string SeenLikesKey = "memo_seen_likes";
    float lastReactionCheck = -999f;

    /// <summary>사진첩에서 마지막으로 본 각 게시물의 좋아요 수 (id 와 같은 순서)</summary>
    [System.Serializable] class SeenLikes { public List<string> ids = new List<string>(); public List<int> counts = new List<int>(); }

    static SeenLikes LoadSeen() => JsonUtility.FromJson<SeenLikes>(PlayerPrefs.GetString(SeenLikesKey, "{}")) ?? new SeenLikes();

    static int SeenCount(SeenLikes seen, string id)
    {
        int i = seen.ids.IndexOf(id);
        return i >= 0 && i < seen.counts.Count ? seen.counts[i] : 0;
    }

    static void SaveSeen(List<MemoData> memos)
    {
        var seen = new SeenLikes();
        foreach (MemoData m in memos)
        {
            seen.ids.Add(m.id);
            seen.counts.Add(m.likeCount);
        }
        PlayerPrefs.SetString(SeenLikesKey, JsonUtility.ToJson(seen));
        PlayerPrefs.Save();
    }

    void Start() => CheckNewReactions();

    void OnApplicationFocus(bool focus)
    {
        if (focus) CheckNewReactions();
    }

    /// <summary>내 게시물에 새 좋아요가 생겼는지 확인해 HUD 점을 켠다 (1분에 한 번까지).</summary>
    void CheckNewReactions()
    {
        if (Time.realtimeSinceStartup - lastReactionCheck < 60f || (album != null && album.activeSelf))
            return;
        lastReactionCheck = Time.realtimeSinceStartup;
        EnsureService();
        List<string> ids = Mine.ids;
        if (ids.Count == 0)
            return;
        SeenLikes seen = LoadSeen();
        int pending = ids.Count;
        bool any = false;
        foreach (string id in ids)
        {
            StartCoroutine(service.GetMemo(id, DeviceId, (m, error) =>
            {
                if (error == MemoData.NotFound)
                    SetMine(id, false); // 서버에서 지워진 메모는 내 목록에서도 뺀다
                if (m != null)
                {
                    int i = seen.ids.IndexOf(m.id);
                    if (m.likeCount > SeenCount(seen, m.id))
                        any = true;
                    else if (i >= 0 && i < seen.counts.Count)
                        seen.counts[i] = m.likeCount; // 좋아요가 취소돼 줄었으면 기준도 낮춘다 (다음 새 좋아요를 놓치지 않게)
                }
                if (--pending == 0)
                {
                    PlayerPrefs.SetString(SeenLikesKey, JsonUtility.ToJson(seen));
                    var hud = FindAnyObjectByType<GameHUD>();
                    if (hud != null) hud.SetAlbumBadge(any);
                }
            }));
        }
    }

    public void OpenAlbum()
    {
        EnsureService();
        building = null;
        currentSpot = null;
        albumMode = true;
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);
        listView.SetActive(false);
        writeView.SetActive(false);
        viewer.SetActive(false);
        profile.SetActive(false);
        album.SetActive(true);
        album.transform.SetAsLastSibling();
        ReloadAlbum();
    }

    void ReloadAlbum()
    {
        ClearGrid(albumGrid);
        foreach (Texture2D t in loadedPhotos)
            if (t != null)
                Destroy(t);
        loadedPhotos.Clear();
        currentMemos.Clear();

        List<string> ids = new List<string>(Mine.ids);
        ids.Reverse(); // 나중에 올린 것이 앞에
        int request = ++albumRequest;
        albumCount.text = "";
        albumEmpty.text = ids.Count == 0 ? "아직 올린 사진이 없어요\n경희스팟 메모판에 사진을 남겨 보세요" : "불러오는 중…";
        albumEmptyAction.gameObject.SetActive(ids.Count == 0);
        if (ids.Count == 0)
            return;

        var found = new MemoData[ids.Count];
        int pending = ids.Count, failed = 0;
        for (int i = 0; i < ids.Count; i++)
        {
            int index = i;
            string id = ids[i];
            StartCoroutine(service.GetMemo(id, DeviceId, (m, error) =>
            {
                if (request != albumRequest)
                    return;
                if (error == MemoData.NotFound)
                    SetMine(id, false); // 서버에서 지워진 메모는 내 목록에서도 뺀다
                else if (m == null)
                    failed++;
                if (m != null && m.HasPhoto)
                    found[index] = m;
                if (--pending > 0)
                    return;
                // 다 받으면 올린 순서대로 한 번에 놓는다. 새 좋아요가 생긴 사진은 빨간 하트 수로 표시.
                SeenLikes seen = LoadSeen();
                int fresh = 0;
                foreach (MemoData memo in found)
                {
                    if (memo == null)
                        continue;
                    currentMemos.Add(memo);
                    Image cell = AddGridPhoto(albumGrid, memo, () => OpenViewer(memo));
                    int added = memo.likeCount - SeenCount(seen, memo.id);
                    if (added > 0) fresh += added;
                    if (memo.likeCount > 0)
                        LikeChip(cell.transform, memo.likeCount, added > 0);
                }
                albumCount.text = $"게시물 {currentMemos.Count}";
                albumEmpty.text = currentMemos.Count > 0 ? ""
                    : failed > 0 ? "사진을 불러오지 못했어요\n인터넷 연결을 확인해 주세요"
                    : "아직 올린 사진이 없어요\n경희스팟 메모판에 사진을 남겨 보세요";
                albumEmptyAction.gameObject.SetActive(currentMemos.Count == 0 && failed == 0);
                if (currentMemos.Count > 0)
                {
                    SaveSeen(currentMemos);
                    var hud = FindAnyObjectByType<GameHUD>();
                    if (hud != null) hud.SetAlbumBadge(false);
                }
                if (fresh > 0)
                    ShowToast($"새 좋아요 {fresh}개를 받았어요");
            }));
        }
    }

    /// <summary>사진 칸 왼쪽 아래의 "♥ N" (새 좋아요가 있으면 빨간색)</summary>
    static void LikeChip(Transform cell, int count, bool fresh)
    {
        Image chip = UIKit.Image(cell, "Likes", fresh ? LikeRed : new Color(0f, 0f, 0f, 0.55f));
        chip.sprite = HudIcons.Pill;
        chip.type = Image.Type.Sliced;
        chip.pixelsPerUnitMultiplier = 120f / 52f;
        Place(chip.rectTransform, new Vector2(0f, 0f), new Vector2(12f, 12f));
        chip.rectTransform.sizeDelta = new Vector2(count >= 100 ? 150f : 116f, 52f);
        Image heart = UIKit.Image(chip.transform, "Heart", Color.white);
        heart.sprite = HudIcons.Heart;
        Place(heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 0f));
        heart.rectTransform.sizeDelta = new Vector2(30f, 30f);
        Text t = UIKit.Text(chip.transform, count.ToString(), 28, TextAnchor.MiddleLeft, Color.white);
        t.fontStyle = FontStyle.Bold;
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(52f, 0f);
        t.rectTransform.offsetMax = Vector2.zero;
    }

    void BuildAlbum()
    {
        album = UIKit.Image(root.transform, "Album", Color.white, raycast: true).gameObject;
        UIKit.Stretch((RectTransform)album.transform);
        Rect sa = Screen.safeArea;
        RectTransform safe = SafeRect(album.transform, "Safe",
            new Vector2(sa.xMin / Mathf.Max(1, Screen.width), sa.yMin / Mathf.Max(1, Screen.height)),
            new Vector2(sa.xMax / Mathf.Max(1, Screen.width), sa.yMax / Mathf.Max(1, Screen.height)));

        Button close = RoundButton(safe, "Close", HudIcons.Close, 88f, Color.white, Ink, Close);
        Place((RectTransform)close.transform, new Vector2(0f, 1f), new Vector2(32f, -30f));
        close.transform.Find("Shadow").GetComponent<Image>().color = Line;
        Text head = UIKit.Text(safe, "내 사진첩", 38, TextAnchor.MiddleCenter, Ink);
        head.fontStyle = FontStyle.Bold;
        TopBox(head.rectTransform, 30f, 88f, 140f);
        albumCount = UIKit.Text(safe, "", 30, TextAnchor.MiddleRight, InkQuiet);
        TopBox(albumCount.rectTransform, 30f, 88f, 40f);
        Image hr = UIKit.Image(safe, "Line", Line);
        TopBox(hr.rectTransform, 140f, 2f, 0f);

        RectTransform area = UIKit.Rect(safe, "Grid");
        UIKit.StretchBetween(area, 0f, 146f);
        albumGrid = BuildGrid(area);
        albumEmpty = UIKit.Text(area, "", 34, TextAnchor.UpperCenter, InkQuiet);
        UIKit.Stretch(albumEmpty.rectTransform, 40f, 120f);
        albumEmptyAction = UIKit.Button(area, "가까운 경희스팟 메모판 열기", OpenNearestSpot, ShareBlue, 36);
        RectTransform ar = (RectTransform)albumEmptyAction.transform;
        ar.anchorMin = ar.anchorMax = ar.pivot = new Vector2(0.5f, 1f);
        ar.anchoredPosition = new Vector2(0f, -250f);
        ar.sizeDelta = new Vector2(640f, 110f);
        albumEmptyAction.GetComponentInChildren<Text>().color = Color.white;
        albumEmptyAction.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        Image aimg = albumEmptyAction.GetComponent<Image>();
        aimg.sprite = HudIcons.Pill;
        aimg.type = Image.Type.Sliced;
        aimg.pixelsPerUnitMultiplier = 120f / 110f;
        albumEmptyAction.gameObject.SetActive(false);

        album.SetActive(false);
    }

    /// <summary>지금 위치에서 가장 가까운 경희스팟의 메모판을 연다</summary>
    void OpenNearestSpot()
    {
        Transform player = collectController != null && collectController.campusMap != null ? collectController.campusMap.player : null;
        if (spots == null || player == null)
            return;
        KyungHeeSpot best = null;
        float bestD = float.MaxValue;
        foreach (KyungHeeSpot s in spots.Spots)
        {
            if (s == null || s.Building == null) continue;
            Vector3 c = s.BuildingBounds.center;
            float d = new Vector2(c.x - player.position.x, c.z - player.position.z).sqrMagnitude;
            if (d < bestD) { bestD = d; best = s; }
        }
        if (best != null)
            OpenSpot(best);
    }
}
