using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 내 사진첩: 이 기기에서 올린 게시물(모든 장소)을 3열 격자로 모아 본다. 첫 화면 오른쪽 위 사진 버튼으로 연다.
/// 서버는 작성 기기로 찾는 기능이 없어서, 올릴 때 기기에 적어 둔 메모 id(Mine)를 하나씩 받아 온다.
/// 사진을 누르면 게시물 화면이 열리고, 거기서 인스타그램 스토리·카카오톡으로 공유할 수 있다.
/// </summary>
public partial class MemoBoard
{
    GameObject album;
    RectTransform albumGrid;
    Text albumCount, albumEmpty;
    bool albumMode;
    int albumRequest;

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
        if (ids.Count == 0)
            return;

        var found = new MemoData[ids.Count];
        int pending = ids.Count;
        for (int i = 0; i < ids.Count; i++)
        {
            int index = i;
            StartCoroutine(service.GetMemo(ids[i], DeviceId, (m, error) =>
            {
                if (request != albumRequest)
                    return;
                if (m != null && m.HasPhoto)
                    found[index] = m;
                if (--pending > 0)
                    return;
                // 다 받으면 올린 순서대로 한 번에 놓는다.
                foreach (MemoData memo in found)
                {
                    if (memo == null)
                        continue;
                    currentMemos.Add(memo);
                    AddGridPhoto(albumGrid, memo, () => OpenViewer(memo));
                }
                albumCount.text = $"게시물 {currentMemos.Count}";
                albumEmpty.text = currentMemos.Count == 0 ? "사진을 불러오지 못했어요\n인터넷 연결을 확인해 주세요" : "";
            }));
        }
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

        album.SetActive(false);
    }
}
