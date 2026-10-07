using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 키아트 로우폴리 지오메트리(나무, 산울타리, 건물 디테일, 랜드마크)를 KeyArtGeometry.bytes 에서 만들어 그린다.
/// 파일은 tools/keyart/build_art.py 가 만든다 (v3: 'KUCA', int32 3, 이후 gzip — 형식은 kuca_art/mesh.py write_bytes).
/// 건물 외벽(Shell) 메시는 UV2 에 (벽 위 위치, 벽 길이) m 를 담아 셰이더가 벽마다 창을 가운데 정렬한다.
/// 메시는 저장하지 않고 켜질 때마다 만든다 (씬·저장소에 수십 MB 메시 에셋을 두지 않으려고).
/// </summary>
[ExecuteAlways]
public class KeyArtGeometry : MonoBehaviour
{
    public TextAsset geometry;
    [Tooltip("버텍스 색 메시 재질 (KUCA/VertexColorLit)")]
    public Material material;
    [Tooltip("이름이 Shell<스타일>_ 로 시작하는 메시(지붕 단차 블록)에 쓸 외벽 재질. shellStyles 와 같은 순서")]
    public Material[] shellMaterials = new Material[0];
    public string[] shellStyles = new string[0];
    public ShadowCastingMode castShadows = ShadowCastingMode.On;

    const float PosUnit = 0.02f;
    readonly List<GameObject> parts = new List<GameObject>();
    readonly List<Mesh> meshes = new List<Mesh>();

    public int VertexCount { get; private set; }

    void OnEnable() => Build();
    void OnDisable() => Clear();

    public void Build()
    {
        Clear();
        if (geometry == null || material == null)
            return;
        byte[] data = geometry.bytes;
        if (data.Length < 8 || data[0] != 'K' || data[1] != 'U' || data[2] != 'C' || data[3] != 'A' || System.BitConverter.ToInt32(data, 4) != 3)
        {
            Debug.LogError("[KeyArtGeometry] KeyArtGeometry.bytes 형식이 맞지 않습니다 (v3 필요). tools/keyart/build_art.py 를 다시 실행하세요.");
            return;
        }

        using (var gz = new GZipStream(new MemoryStream(data, 8, data.Length - 8), CompressionMode.Decompress))
        using (var br = new BinaryReader(gz))
        {
            int count = br.ReadInt32();
            for (int k = 0; k < count; k++)
            {
                string name = System.Text.Encoding.UTF8.GetString(br.ReadBytes(br.ReadInt32()));
                var origin = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                int vc = br.ReadInt32();
                int flags = br.ReadInt32();

                byte[] posBytes = br.ReadBytes(vc * 6);
                var verts = new Vector3[vc];
                for (int i = 0, b = 0; i < vc; i++, b += 6)
                    verts[i] = new Vector3(
                        (short)(posBytes[b] | posBytes[b + 1] << 8) * PosUnit,
                        (short)(posBytes[b + 2] | posBytes[b + 3] << 8) * PosUnit,
                        (short)(posBytes[b + 4] | posBytes[b + 5] << 8) * PosUnit);

                byte[] colBytes = br.ReadBytes(vc * 4);
                var colors = new Color32[vc];
                for (int i = 0, b = 0; i < vc; i++, b += 4)
                    colors[i] = new Color32(colBytes[b], colBytes[b + 1], colBytes[b + 2], colBytes[b + 3]);

                Vector2[] wallUV = null;
                if ((flags & 1) != 0)
                {
                    byte[] auxBytes = br.ReadBytes(vc * 4);
                    wallUV = new Vector2[vc];
                    for (int i = 0, b = 0; i < vc; i++, b += 4)
                        wallUV[i] = new Vector2((ushort)(auxBytes[b] | auxBytes[b + 1] << 8) * 0.1f,
                                                (ushort)(auxBytes[b + 2] | auxBytes[b + 3] << 8) * 0.1f);
                }

                var tris = new int[vc];
                for (int i = 0; i < vc; i++) tris[i] = i;

                var mesh = new Mesh { name = name, indexFormat = vc > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16, hideFlags = HideFlags.DontSave };
                mesh.vertices = verts;
                mesh.colors32 = colors;
                if (wallUV != null) mesh.uv2 = wallUV;
                mesh.triangles = tris;
                mesh.RecalculateNormals();   // 정점을 공유하지 않으니 면 법선 = 각진 음영
                mesh.RecalculateBounds();
                mesh.UploadMeshData(Application.isPlaying);   // 실행 중에는 CPU 쪽 사본을 버려 메모리 절약
                meshes.Add(mesh);
                VertexCount += vc;

                var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
                go.transform.SetParent(transform, false);
                go.transform.localPosition = origin;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = MaterialFor(name);
                r.shadowCastingMode = castShadows;
                r.receiveShadows = true;
                parts.Add(go);
            }
        }
    }

    Material MaterialFor(string meshName)
    {
        if (meshName.StartsWith("Shell"))
            for (int i = 0; i < shellStyles.Length && i < shellMaterials.Length; i++)
                if (shellMaterials[i] != null && meshName.StartsWith("Shell" + shellStyles[i] + "_"))
                    return shellMaterials[i];
        return material;
    }

    void Clear()
    {
        foreach (GameObject go in parts)
            if (go != null) DestroyNow(go);
        foreach (Mesh m in meshes)
            if (m != null) DestroyNow(m);
        parts.Clear();
        meshes.Clear();
        VertexCount = 0;
    }

    static void DestroyNow(Object o)
    {
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }
}
