using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

/// <summary>
/// 수집 동물 3D 모델 (Resources/KUCA/CreatureMeshes.bytes, tools/keyart/build_creatures.py 가 만든다).
/// 형식은 KeyArtGeometry.bytes 와 같은 v3. 같은 위치·색 정점을 합쳐 부드러운 법선으로 만든다 (말랑한 토이 느낌).
/// 등급(수집 대상 종류 id)마다 나올 수 있는 동물 목록과 도감 이름도 여기서 관리한다.
/// </summary>
public static class CreatureLibrary
{
    /// <summary>수집 대상 종류 id → 그 등급에서 나오는 동물 id (노랑은 랜드마크마다 정해진다)</summary>
    public static readonly Dictionary<string, string[]> TierSpecies = new Dictionary<string, string[]>
    {
        { "sprout", new[] { "pigeon", "snail", "ant", "ladybug" } },
        { "crystal", new[] { "stray_cat", "squirrel", "crow", "raccoon_dog" } },
    };

    /// <summary>도감 이름</summary>
    public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "pigeon", "비둘기" }, { "snail", "달팽이" }, { "ant", "개미" }, { "ladybug", "무당벌레" },
        { "stray_cat", "길고양이" }, { "squirrel", "청설모" }, { "crow", "까마귀" }, { "raccoon_dog", "너구리" },
        { "space_rabbit", "공학관 토끼" }, { "library_owl", "중앙도서관 올빼미" }, { "plaza_duck", "사색의 광장 오리" },
        { "gate_magpie", "정문 까치" }, { "lion_cub", "체육대학관 아기 사자" }, { "clock_rooster", "선승관 수탉" },
        { "art_chameleon", "예술·디자인대학 카멜레온" }, { "ceramics_mole", "도예관 두더지" },
        { "observatory_squirrel", "천문대 날다람쥐" }, { "amphitheater_frog", "평화노천극장 개구리" },
        { "stadium_jindo", "대운동장 진돗개" }, { "language_parrot", "외국어대학관 앵무새" },
        { "dorm_hamster", "우정원 햄스터" }, { "electronics_hedgehog", "전자정보대학관 고슴도치" },
        { "business_fox", "국제·경영대학관 여우" }, { "multimedia_meerkat", "멀티미디어·글로벌관 미어캣" },
    };

    /// <summary>등급 이름 (HUD)</summary>
    public static readonly Dictionary<string, string> TierLabels = new Dictionary<string, string>
    {
        { "sprout", "초록 동물" }, { "crystal", "파랑 동물" }, { "star", "황금 동물" },
    };

    /// <summary>노랑(황금) 동물이 사는 랜드마크: 월드 중심 (x, z), 건물 반 크기 (m). tools/keyart 로 계산한 값</summary>
    public class GoldZone
    {
        public string species;
        public Vector2 center;
        public float halfSize;
        public float radius;     // 실제 출현 반경 (서로 절대 겹치지 않게 줄인 값)
    }

    static readonly GoldZone[] goldZones =
    {
        Zone("space_rabbit", 49, 255, 80), Zone("library_owl", -40, -352, 56), Zone("plaza_duck", 119, -352, 26),
        Zone("gate_magpie", -138, 368, 30), Zone("lion_cub", 31, 40, 60), Zone("clock_rooster", 10, -125, 44),
        Zone("art_chameleon", 399, -249, 51), Zone("ceramics_mole", 112, -40, 16), Zone("observatory_squirrel", 170, -584, 16),
        Zone("amphitheater_frog", 276, -652, 48), Zone("stadium_jindo", -198, -20, 90), Zone("language_parrot", -205, 122, 63),
        Zone("dorm_hamster", -262, 222, 50), Zone("electronics_hedgehog", 306, -505, 52), Zone("business_fox", 112, -210, 31),
        Zone("multimedia_meerkat", -288, 47, 42),
    };
    static bool zonesReady;

    static GoldZone Zone(string id, float x, float z, float half) =>
        new GoldZone { species = id, center = new Vector2(x, z), halfSize = half };

    /// <summary>
    /// 출현 반경: 건물 둘레 45 m 까지. 두 랜드마크 범위가 겹치면 (사이 6 m 여유) 크기 비율대로 둘 다 줄인다 → 절대 겹치지 않음.
    /// </summary>
    public static IReadOnlyList<GoldZone> GoldZones
    {
        get
        {
            if (!zonesReady)
            {
                foreach (var z in goldZones)
                    z.radius = z.halfSize + 45f;
                for (int pass = 0; pass < 4; pass++)
                    for (int i = 0; i < goldZones.Length; i++)
                        for (int j = i + 1; j < goldZones.Length; j++)
                        {
                            GoldZone a = goldZones[i], b = goldZones[j];
                            float limit = Vector2.Distance(a.center, b.center) - 6f;
                            float sum = a.radius + b.radius;
                            if (sum > limit)
                            {
                                float k = Mathf.Max(0f, limit) / sum;
                                a.radius *= k;
                                b.radius *= k;
                            }
                        }
                zonesReady = true;
            }
            return goldZones;
        }
    }

    /// <summary>(x, z) 가 들어 있는 랜드마크 범위 (없으면 null)</summary>
    public static GoldZone GoldZoneAt(float x, float z)
    {
        var p = new Vector2(x, z);
        foreach (var zone in GoldZones)
            if (Vector2.Distance(zone.center, p) <= zone.radius)
                return zone;
        return null;
    }

    const float PosUnit = 0.02f;
    static Dictionary<string, Mesh> meshes;
    static Material material;

    public static Material Material
    {
        get
        {
            if (material == null)
            {
                Shader s = Shader.Find("KUCA/VertexColorLit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
                material = new Material(s) { name = "KUCA_Creature", enableInstancing = true };
            }
            return material;
        }
    }

    public static string NameOf(string id) => id != null && Names.TryGetValue(id, out string n) ? n : id;

    /// <summary>등급에서 동물 하나를 고른다. 모델이 없으면 null</summary>
    public static string PickSpecies(string tierId)
    {
        if (!TierSpecies.TryGetValue(tierId, out string[] list) || list.Length == 0)
            return null;
        string id = list[Random.Range(0, list.Length)];
        return TryGetMesh(id, out _) ? id : null;
    }

    public static bool TryGetMesh(string id, out Mesh mesh)
    {
        if (meshes == null)
            Load();
        mesh = null;
        return id != null && meshes.TryGetValue(id, out mesh);
    }

    static void Load()
    {
        meshes = new Dictionary<string, Mesh>();
        var asset = Resources.Load<TextAsset>("KUCA/CreatureMeshes");
        if (asset == null)
        {
            Debug.LogWarning("[CreatureLibrary] Resources/KUCA/CreatureMeshes.bytes 가 없습니다 (tools/keyart/build_creatures.py).");
            return;
        }
        byte[] data = asset.bytes;
        if (data.Length < 8 || data[0] != 'K' || data[1] != 'U' || data[2] != 'C' || data[3] != 'A' || System.BitConverter.ToInt32(data, 4) != 3)
        {
            Debug.LogError("[CreatureLibrary] CreatureMeshes.bytes 형식이 맞지 않습니다 (v3 필요).");
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
                byte[] pos = br.ReadBytes(vc * 6);
                byte[] col = br.ReadBytes(vc * 4);
                if ((flags & 1) != 0)
                    br.ReadBytes(vc * 4);
                meshes[name.Replace("Creature_", "")] = BuildMesh(name, origin, vc, pos, col);
            }
        }
    }

    /// <summary>같은 (위치, 색) 정점을 하나로 합쳐 인덱스 메시를 만든다 → 부드러운 법선</summary>
    static Mesh BuildMesh(string name, Vector3 origin, int vc, byte[] pos, byte[] col)
    {
        var map = new Dictionary<(short, short, short, int), int>(vc / 3);
        var verts = new List<Vector3>(vc / 3);
        var cols = new List<Color32>(vc / 3);
        var tris = new int[vc];
        for (int i = 0; i < vc; i++)
        {
            short x = System.BitConverter.ToInt16(pos, i * 6);
            short y = System.BitConverter.ToInt16(pos, i * 6 + 2);
            short z = System.BitConverter.ToInt16(pos, i * 6 + 4);
            int c = col[i * 4] | (col[i * 4 + 1] << 8) | (col[i * 4 + 2] << 16) | (col[i * 4 + 3] << 24);
            var key = (x, y, z, c);
            if (!map.TryGetValue(key, out int idx))
            {
                idx = verts.Count;
                map[key] = idx;
                verts.Add(origin + new Vector3(x, y, z) * PosUnit);
                cols.Add(new Color32(col[i * 4], col[i * 4 + 1], col[i * 4 + 2], col[i * 4 + 3]));
            }
            tris[i] = idx;
        }
        var mesh = new Mesh { name = name };
        if (verts.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        return mesh;
    }
}
