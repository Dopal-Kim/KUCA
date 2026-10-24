using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// 경희스팟 메모판의 탭: 사진(콜라주) | 공지(이 장소와 연결된 학교 공지, GET /api/notices?buildingId=).
/// 공지 카드를 누르면 원문을 연다. 기기 안 가짜 서버(주소 없음)일 때는 공지가 없다.
/// </summary>
public partial class MemoBoard
{
    /// <summary>메모판 위쪽 (뒤로 · 큰 이름 · 부제 · 사진/공지) 높이. 사진·공지 영역은 그 아래</summary>
    const float BoardTop = 470f;

    GameObject photoArea, noticeArea;
    RectTransform boardNoticeList;
    Image[] boardTabFaces;
    int boardTab;
    int noticeRequest;

    void BuildBoardTabs(RectTransform safe)
    {
        // 세그먼트: 회색 트랙 위 두 칸, 고른 칸은 흰색 + 굵은 글자 (iOS 세그먼트 · 토스 탭)
        string[] labels = { "사진", "공지" };
        Image track = UIKit.Image(safe, "Segments", KucaTheme.Line);
        track.sprite = HudIcons.Rounded;
        track.type = Image.Type.Sliced;
        track.pixelsPerUnitMultiplier = 0.5f;
        TopBox(track.rectTransform, 338f, 112f, KucaTheme.Gutter);
        boardTabFaces = new Image[labels.Length];
        for (int i = 0; i < labels.Length; i++)
        {
            int tab = i;
            Button b = UIKit.Button(track.transform, labels[i], () => SetBoardTab(tab), Color.white, KucaTheme.Label);
            Image img = b.GetComponent<Image>();
            img.pixelsPerUnitMultiplier = 0.55f;
            RectTransform r = img.rectTransform;
            r.anchorMin = new Vector2(i / (float)labels.Length, 0f);
            r.anchorMax = new Vector2((i + 1) / (float)labels.Length, 1f);
            r.offsetMin = new Vector2(8f, 8f);
            r.offsetMax = new Vector2(-8f, -8f);
            boardTabFaces[i] = img;
        }

        RectTransform area = UIKit.Rect(safe, "Notices");
        UIKit.StretchBetween(area, 0f, BoardTop);
        noticeArea = area.gameObject;
        boardNoticeList = UIKit.ScrollList(area, KucaTheme.Space3, new RectOffset((int)KucaTheme.Gutter, (int)KucaTheme.Gutter, 10, 60));
        noticeArea.SetActive(false);
    }

    void SetBoardTab(int tab)
    {
        boardTab = tab;
        for (int i = 0; i < boardTabFaces.Length; i++)
        {
            boardTabFaces[i].color = i == tab ? Color.white : new Color(1f, 1f, 1f, 0f);
            boardTabFaces[i].GetComponentInChildren<Text>().color = i == tab ? Ink : InkQuiet;
        }
        photoArea.SetActive(tab == 0);
        noticeArea.SetActive(tab == 1);
        UpdateWriteState();
        if (tab == 1)
            ReloadBoardNotices();
    }

    void ReloadBoardNotices()
    {
        ClearBoardNotices();
        NoticeMessage("공지를 불러오는 중…");
        int request = ++noticeRequest;
        string buildingId = building != null ? building.buildingId : null;
        StartCoroutine(FetchNotices(buildingId, (list, error) =>
        {
            if (request != noticeRequest || !root.activeSelf)
                return;
            ClearBoardNotices();
            if (error != null) { NoticeMessage($"{error}\n잠시 뒤 다시 열어 주세요"); return; }
            if (list.Count == 0) { NoticeMessage("이 장소와 관련된 공지가 아직 없어요"); return; }
            foreach (PlaceNotice n in list)
            {
                RoutePlanner.NoticeRow(boardNoticeList, n, false);
                // 메모판 종이 위에서는 흰 카드가 더 잘 보인다
                boardNoticeList.GetChild(boardNoticeList.childCount - 1).GetComponent<Image>().color = Color.white;
            }
        }));
    }

    IEnumerator FetchNotices(string buildingId, Action<List<PlaceNotice>, string> onDone)
    {
        string url = CurrentServerUrl.TrimEnd('/');
        if (url.Length == 0 || string.IsNullOrEmpty(buildingId))
        {
            onDone(new List<PlaceNotice>(), null);
            yield break;
        }
        using var req = UnityWebRequest.Get($"{url}/api/notices?buildingId={UnityWebRequest.EscapeURL(buildingId)}");
        req.timeout = 20;
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            PlaceNoticeList list = null;
            try { list = JsonUtility.FromJson<PlaceNoticeList>("{\"items\":" + req.downloadHandler.text + "}"); }
            catch (ArgumentException) { }
            if (list?.items != null) { onDone(list.items, null); yield break; }
        }
        onDone(new List<PlaceNotice>(), req.responseCode == 404 ? null : "공지를 불러오지 못했어요");
    }

    void ClearBoardNotices()
    {
        for (int i = boardNoticeList.childCount - 1; i >= 0; i--)
            Destroy(boardNoticeList.GetChild(i).gameObject);
    }

    void NoticeMessage(string message)
    {
        Text t = UIKit.Text(boardNoticeList, message, KucaTheme.Body, TextAnchor.MiddleCenter, InkQuiet);
        UIKit.Size(t, 240f);
    }
}
