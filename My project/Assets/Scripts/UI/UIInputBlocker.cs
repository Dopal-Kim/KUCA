using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 UI가 게임 입력(수집 탭, 카메라 드래그, 건물 탭)을 가로채야 하는지 알려 준다.
/// - 모달 화면(캐릭터 꾸미기, 메모판)이 열려 있으면 게임 입력을 모두 막는다.
/// - 등록된 UI 영역(버튼 등) 위에서 시작한 터치는 게임 입력으로 쓰지 않는다.
/// </summary>
public static class UIInputBlocker
{
    static readonly List<RectTransform> rects = new List<RectTransform>();
    static readonly HashSet<object> modals = new HashSet<object>();
    static float lastModalClose = -10f;

    /// <summary>
    /// 화면이 닫힌 직후인지. 닫기 버튼을 누른 그 손가락이 떼어지는 순간 같은 프레임에 지도도 탭으로 받아
    /// 뒤에 있던 건물·경희스팟이 눌리는 것을 막는다.
    /// </summary>
    public static bool RecentlyClosedModal => Time.unscaledTime - lastModalClose < 0.3f;

    /// <summary>모달 화면이 하나라도 열려 있는지</summary>
    public static bool IsModalOpen => modals.Count > 0;
    public static int ModalCount => modals.Count;

    /// <summary>owner 말고 다른 모달 화면이 열려 있는지 (다른 화면이 열리면 스스로 닫는 팝업용)</summary>
    public static bool IsOtherModalOpen(object owner)
    {
        foreach (object m in modals)
        {
            if (ReferenceEquals(m, owner))
                continue;
            if (m is Object o && o == null)
                continue; // 지워진 화면이 남아 있는 경우
            if (m is GameObject go && !go.activeInHierarchy)
                continue;
            return true;
        }
        return false;
    }

    public static void SetModal(object owner, bool open)
    {
        if (open) modals.Add(owner);
        else if (modals.Remove(owner)) lastModalClose = Time.unscaledTime;
    }

    public static void Register(RectTransform rt)
    {
        if (rt != null && !rects.Contains(rt))
            rects.Add(rt);
    }

    /// <summary>화면 좌표가 등록된(보이는) UI 영역 위인지</summary>
    public static bool IsOverUI(Vector2 screenPos)
    {
        rects.RemoveAll(r => r == null);
        foreach (RectTransform rt in rects)
            if (rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null))
                return true;
        return false;
    }

    /// <summary>게임 입력으로 써도 되는 위치인지</summary>
    public static bool AllowsGameInput(Vector2 screenPos) => !IsModalOpen && !RecentlyClosedModal && !IsOverUI(screenPos);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        rects.Clear();
        modals.Clear();
        lastModalClose = -10f;
    }
}
