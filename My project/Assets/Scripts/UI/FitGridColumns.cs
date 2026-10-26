using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 격자(GridLayoutGroup)의 칸 너비를 실제 폭에 맞춰 열 수만큼 나눈다. 높이는 그대로 둔다
/// (칸 안의 그림 · 이름이 높이를 기준으로 놓여 있어, 높이까지 줄이면 이름이 그림 위로 겹쳤다).
/// 기준 캔버스는 1080 폭이지만 세로로 긴 폰(예: iPhone 16 Pro)은 실제로 쓰는 폭이 1000 안쪽이라
/// 고정 칸 크기로는 3열째가 화면 밖으로 잘렸다 (도감 · 경희펫 · 스크랩북).
/// </summary>
[RequireComponent(typeof(GridLayoutGroup))]
public class FitGridColumns : MonoBehaviour
{
    GridLayoutGroup grid;
    float height, lastWidth = -1f;

    /// <summary>grid 에 붙인다. 지금 칸 높이를 기억해 둔다</summary>
    public static void Attach(GridLayoutGroup g)
    {
        var f = g.gameObject.AddComponent<FitGridColumns>();
        f.grid = g;
        f.height = g.cellSize.y;
        // 고정 칸 격자는 "최소 폭 = 칸 × 열" 을 주장해 부모 목록까지 화면보다 넓어진다: 폭은 부모가 정하게 한다
        LayoutElement le = g.GetComponent<LayoutElement>() ?? g.gameObject.AddComponent<LayoutElement>();
        le.minWidth = 0f;
        le.preferredWidth = 0f;
        le.flexibleWidth = 1f;
    }

    void OnRectTransformDimensionsChange() => Fit();

    void OnEnable() => Fit();

    void Fit()
    {
        if (grid == null || grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
            return;
        float width = ((RectTransform)transform).rect.width;
        if (width <= 1f || Mathf.Abs(width - lastWidth) < 0.5f)
            return;
        lastWidth = width;
        int n = Mathf.Max(1, grid.constraintCount);
        float cell = (width - grid.padding.horizontal - grid.spacing.x * (n - 1)) / n;
        if (cell <= 1f)
            return;
        grid.cellSize = new Vector2(Mathf.Floor(cell), height);
    }
}
