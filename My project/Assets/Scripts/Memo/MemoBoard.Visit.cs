using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// B-2 방문한 장소의 쪽지만 열람: 한 번도 반경 안에 들어가 본 적 없는 경희스팟의 메모판은
/// 사진을 흐리게(자물쇠 표시) 보여 주고 열 수 없다. 방문 기록은 GameProgress.visitedSpots
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

    /// <summary>아주 작게 줄였다가 늘려 보이게 해서 무엇인지 알아볼 수 없을 만큼 흐린 사진</summary>
    static Texture2D Blurred(Texture source)
    {
        const int w = 10;
        int h = Mathf.Max(1, Mathf.RoundToInt(w * (float)source.height / Mathf.Max(1, source.width)));
        RenderTexture mid = RenderTexture.GetTemporary(64, Mathf.Max(1, h * 64 / w), 0);
        RenderTexture small = RenderTexture.GetTemporary(w, h, 0);
        Graphics.Blit(source, mid);
        Graphics.Blit(mid, small);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = small;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply(false, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(mid);
        RenderTexture.ReleaseTemporary(small);
        return tex;
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
