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
    GameObject album, albumEmptyCard, albumEmptyAction;
    RectTransform albumGrid;
    Text albumCount, albumEmptyTitle, albumEmptyBody;

    /// <summary>빈 상태 카드 글 (첫 줄 = 제목, 나머지 = 설명). 비우면 카드를 숨긴다</summary>
    string AlbumEmpty
    {
        set
        {
            albumEmptyCard.SetActive(!string.IsNullOrEmpty(value));
            int nl = (value ?? "").IndexOf('\n');
            albumEmptyTitle.text = nl >= 0 ? value.Substring(0, nl) : value ?? "";
            albumEmptyBody.text = nl >= 0 ? value.Substring(nl + 1) : "";
        }
    }
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
        AlbumEmpty = ids.Count == 0 ? "아직 올린 사진이 없어요\n지도의 경희스팟 핀을 누르면\n그곳 메모판에 사진을 남길 수 있어요" : "불러오는 중…";
        albumEmptyAction.SetActive(ids.Count == 0);
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
                albumCount.text = $"내가 남긴 사진 {currentMemos.Count}장";
                AlbumEmpty = currentMemos.Count > 0 ? ""
                    : failed > 0 ? "사진을 불러오지 못했어요\n인터넷 연결을 확인해 주세요"
                    : "아직 올린 사진이 없어요\n지도의 경희스팟 핀을 누르면\n그곳 메모판에 사진을 남길 수 있어요";
                albumEmptyAction.SetActive(currentMemos.Count == 0 && failed == 0);
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
        UIKit.Bold(t);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(52f, 0f);
        t.rectTransform.offsetMax = Vector2.zero;
    }

    void BuildAlbum()
    {
        // v4 화면 틀: ‹ 뒤로 · 큰 제목 · 부제(사진 수) · 3열 사진 격자 · 비었으면 빈 상태 카드
        UIKit.Page page = UIKit.CreatePage(root.transform, "Album", "내 사진첩", Close, " ", scroll: false);
        album = page.root;
        albumCount = page.subtitle;

        // 빈 상태에서 할 일: 가까운 경희스팟 메모판 바로 열기 (REVIEW_v14: 안내만 있고 버튼이 없었음)
        RectTransform empty = UIKit.EmptyState(page.content, " ", " ", "가까운 경희스팟 메모판 열기", OpenNearestSpot);
        albumEmptyCard = empty.gameObject;
        albumEmptyAction = empty.GetComponentInChildren<Button>(true).gameObject;
        Text[] texts = empty.GetComponentsInChildren<Text>(true);
        albumEmptyTitle = texts[0];
        albumEmptyBody = texts[1];

        RectTransform area = UIKit.Rect(page.content, "Grid");
        UIKit.Size(area).flexibleHeight = 1f;
        albumGrid = BuildGrid(area);
        albumGrid.GetComponent<GridLayoutGroup>().spacing = new Vector2(12f, 12f);
    }

    /// <summary>지금 위치에서 가장 가까운 경희스팟의 메모판을 연다</summary>
    void OpenNearestSpot()
    {
        Transform player = collectController != null && collectController.campusMap != null ? collectController.campusMap.player : null;
        if (spots == null || player == null)
        {
            Close();   // 지도가 없으면 지도로 돌아가 직접 찾게
            return;
        }
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
        else
            Close();
    }
}
