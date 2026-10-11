using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 메모 저장소. 실제 서버(HttpMemoService)와 기기 안의 가짜 서버(MockMemoService)가 같은 모양을 따른다.
/// 모든 호출은 코루틴이고, 끝나면 onDone(결과, 에러 메시지) 를 부른다. 에러가 없으면 null.
/// </summary>
public interface IMemoService
{
    /// <summary>화면에 표시할 이름 (예: "가짜 서버", "http://192.168.0.5:5080")</summary>
    string Description { get; }

    /// <summary>deviceId 를 주면 각 메모의 likedByMe 가 채워진다.</summary>
    IEnumerator GetMemos(string buildingId, string deviceId, Action<List<MemoData>, string> onDone);
    IEnumerator CreateMemo(string buildingId, NewMemo memo, Action<MemoData, string> onDone);
    IEnumerator DeleteMemo(string memoId, string deviceId, Action<bool, string> onDone);
    IEnumerator LoadPhoto(MemoData memo, Action<Texture2D, string> onDone);
    /// <summary>좋아요 누르기(like = true)·취소. onDone(좋아요 수, 내가 눌렀는지, 에러)</summary>
    IEnumerator SetLike(string memoId, string deviceId, bool like, Action<int, bool, string> onDone);
    /// <summary>메모 하나 (사진첩용). 지워졌으면 onDone(null, 에러)</summary>
    IEnumerator GetMemo(string memoId, string deviceId, Action<MemoData, string> onDone);

    /// <summary>인터넷 어디서나 열 수 있는 사진 주소 (카카오톡 카드용). 기기 안 가짜 서버면 null</summary>
    string PublicPhotoUrl(MemoData memo);
    /// <summary>그 장소의 메모를 보여 주는 웹 페이지 주소. 가짜 서버면 null</summary>
    string PublicPageUrl(string buildingId);
}
