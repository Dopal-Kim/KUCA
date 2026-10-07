using UnityEngine;

/// <summary>
/// 키아트 지형 높이 (tools/keyart/build_art.py 가 만든 KeyArtHeight.bytes).
/// 플레이어(GPS)·수집물 등이 KeyArtTerrain.HeightAt(x, z) 로 언덕 위에 선다. 지형이 없으면 0.
/// 형식: 'KUHT' int32 nx, nz, float32 cell, originX, originZ, float16[nz*nx] (z 오름차순)
/// </summary>
[ExecuteAlways]
public class KeyArtTerrain : MonoBehaviour
{
    public TextAsset heightmap;

    static KeyArtTerrain active;
    float[] heights;
    int nx, nz;
    float cell, originX, originZ;

    void OnEnable()
    {
        Load();
        active = this;
    }

    void OnDisable()
    {
        if (active == this) active = null;
    }

    public void Load()
    {
        heights = null;
        if (heightmap == null) return;
        byte[] d = heightmap.bytes;
        if (d.Length < 24 || d[0] != 'K' || d[1] != 'U' || d[2] != 'H' || d[3] != 'T')
        {
            Debug.LogError("[KeyArtTerrain] KeyArtHeight.bytes 형식이 맞지 않습니다.");
            return;
        }
        nx = System.BitConverter.ToInt32(d, 4);
        nz = System.BitConverter.ToInt32(d, 8);
        cell = System.BitConverter.ToSingle(d, 12);
        originX = System.BitConverter.ToSingle(d, 16);
        originZ = System.BitConverter.ToSingle(d, 20);
        heights = new float[nx * nz];
        for (int i = 0; i < heights.Length; i++)
            heights[i] = Mathf.HalfToFloat(System.BitConverter.ToUInt16(d, 24 + i * 2));
    }

    /// <summary>월드 (x, z) 의 지면 높이 (m). 지형이 없거나 지도 밖이면 0.</summary>
    public static float HeightAt(float x, float z)
    {
        var t = active;
        if (t == null || t.heights == null) return 0f;
        float fx = (x - t.originX) / t.cell, fz = (z - t.originZ) / t.cell;
        if (fx < 0f || fz < 0f || fx >= t.nx - 1 || fz >= t.nz - 1) return 0f;
        int i = (int)fx, j = (int)fz;
        float tx = fx - i, tz = fz - j;
        float[] h = t.heights;
        int row = j * t.nx, next = row + t.nx;
        float a = Mathf.Lerp(h[row + i], h[row + i + 1], tx);
        float b = Mathf.Lerp(h[next + i], h[next + i + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }
}
