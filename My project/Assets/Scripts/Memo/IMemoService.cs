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

    IEnumerator GetMemos(string buildingId, Action<List<MemoData>, string> onDone);
    IEnumerator CreateMemo(string buildingId, NewMemo memo, Action<MemoData, string> onDone);
    IEnumerator DeleteMemo(string memoId, string deviceId, Action<bool, string> onDone);
    IEnumerator LoadPhoto(MemoData memo, Action<Texture2D, string> onDone);
}
