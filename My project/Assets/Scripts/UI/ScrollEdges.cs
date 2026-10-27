using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스크롤 화면 위 · 아래 끝 표시: 위로 밀려 올라간 내용이 있으면 제목 아래 구분선, 아래에 더 있으면 아래 구분선 + 그림자.
/// ScrollRect.onValueChanged 는 화면이 처음 열릴 때 (레이아웃이 이전 값을 덮어써) 불리지 않아
/// 아래 표시가 사용자가 한 번 밀기 전까지 안 보였다 (REVIEW_v15 · v16, 3회) → 매 프레임 직접 본다.
/// </summary>
public class ScrollEdges : MonoBehaviour
{
    public ScrollRect scroll;
    public Behaviour top;
    public GameObject bottom;

    void LateUpdate()
    {
        RectTransform view = (RectTransform)scroll.transform;
        bool longer = scroll.content.rect.height > view.rect.height + 1f;
        float y = scroll.verticalNormalizedPosition;
        if (top != null)   // 시트처럼 위 구분선이 없는 곳
            top.enabled = longer && y < 0.995f;
        bottom.SetActive(longer && y > 0.005f);
    }
}
