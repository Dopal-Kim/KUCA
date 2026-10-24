using UnityEngine;

/// <summary>
/// 코스 없이 자유 산책: 시작부터 끝까지 움직인 길을 기록하고 끝나면 돌아보기를 보여 준다.
/// (기록 · 돌아보기는 다음 단계에서 채운다. 지금은 시작 버튼이 있는 자리만)
/// </summary>
public static class FreeWalk
{
    /// <summary>자유 산책 중인지</summary>
    public static bool Active { get; private set; }

    public static void Begin()
    {
        Debug.Log("[FreeWalk] 자유 산책은 다음 단계에서 연결돼요.");
    }
}
