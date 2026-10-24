using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 지도 위에 길을 따라 색 선을 그린다 (추천 코스 · 자유 산책 기록). 지형 위로 살짝 띄우고, 카메라가 멀어도 보이게 굵게.
/// </summary>
public class MapRouteLine : MonoBehaviour
{
    static Material material;
    LineRenderer line;

    /// <summary>새 선 하나. layer 를 주면 그 레이어에만 (미니맵 카메라용)</summary>
    public static MapRouteLine Create(string name, Transform parent, int layer = -1)
    {
        var go = new GameObject(name);
        if (parent != null)
            go.transform.SetParent(parent, false);
        if (layer >= 0)
            go.layer = layer;
        var r = go.AddComponent<MapRouteLine>();
        r.line = go.AddComponent<LineRenderer>();
        if (material == null)
            material = new Material(Shader.Find("Sprites/Default")) { name = "MapRouteLine" };   // 항상 빌드에 들어가는 셰이더
        r.line.sharedMaterial = material;
        r.line.useWorldSpace = true;
        r.line.numCornerVertices = 4;
        r.line.numCapVertices = 4;
        r.line.alignment = LineAlignment.TransformZ;   // 땅에 눕혀서 (위에서 보는 지도)
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        r.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.line.receiveShadows = false;
        return r;
    }

    /// <summary>점들 (월드 좌표) 을 색 · 굵기 (m) 로 그린다. lift: 지형 위로 띄우는 높이</summary>
    public void Set(IList<Vector3> points, Color color, float width, float lift = 1.2f)
    {
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            p.y = KeyArtTerrain.HeightAt(p.x, p.z) + lift;
            line.SetPosition(i, p);
        }
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
    }

    public void SetVisible(bool on) => line.enabled = on;
}
