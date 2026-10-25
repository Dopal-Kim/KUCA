using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// B-2 방문한 장소의 쪽지만 열람: 한 번도 반경 안에 들어가 본 적 없는 경희스팟의 메모판은
/// 사진 대신 자물쇠 자리표시만 보여 주고 열 수 없다. 방문 기록은 GameProgress.visitedSpots
/// (KhuMenus 가 스팟 반경 안에 들어오면 남긴다). 앱에서만 막는다 (웹·API 로는 보인다).
/// 경희스팟이 아닌 건물에서 연 메모판은 막지 않는다.
/// </summary>
public partial class MemoBoard
{
    bool locked;
    int lockedCount;

    bool IsVisited(CampusBuildingInfo target)
    {
        if (currentSpot == null || target == null)
            return true;
        GameProgress progress = collectController != null ? collectController.Progress : null;
        return progress == null || progress.visitedSpots.Contains(target.buildingId);
    }

    float SpotDistance()
    {
        if (currentSpot == null || collectController == null || collectController.campusMap == null)
            return 0f;
        float r = spots != null ? spots.interactRadius : 0f;
        return Mathf.Max(0f, currentSpot.DistanceFrom(collectController.campusMap.player.position) - r);
    }

    /// <summary>스티커 가운데 자물쇠</summary>
    static void LockMark(Transform paper)
    {
        Image disk = UIKit.Image(paper, "Lock", new Color(0f, 0f, 0f, 0.45f));
        disk.sprite = HudIcons.Circle;
        Place(disk.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        disk.rectTransform.sizeDelta = new Vector2(96f, 96f);
        Image icon = UIKit.Image(disk.transform, "Icon", Color.white);
        icon.sprite = HudIcons.Lock;
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero);
        icon.rectTransform.sizeDelta = new Vector2(56f, 56f);
    }
}
