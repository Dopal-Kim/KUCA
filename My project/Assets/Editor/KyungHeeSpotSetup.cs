using UnityEditor;
using UnityEngine;

/// <summary>
/// 경희스팟 표지판 그림(원형 배지)과 머티리얼을 만든다.
/// 메뉴: KUCA/Build Kyung Hee Spot Assets
/// 입력: Assets/Textures/KyungHeeLogo.png (투명 배경 학교 로고)
/// 출력: Assets/Textures/KyungHeeSpotBadge.png, Assets/Materials/KyungHeeSpotBadge.mat, KyungHeeSpotPole.mat
/// </summary>
public static class KyungHeeSpotSetup
{
    const string LogoPath = "Assets/Textures/KyungHeeLogo.png";
    const string BadgePath = "Assets/Textures/KyungHeeSpotBadge.png";
    const string BadgeMatPath = "Assets/Materials/KyungHeeSpotBadge.mat";
    const string PoleMatPath = "Assets/Materials/KyungHeeSpotPole.mat";

    // 경희 색: 남색 띠, 금색 테두리
    static readonly Color Navy = new Color32(0x24, 0x2E, 0x6B, 0xFF);
    static readonly Color Gold = new Color32(0xC2, 0x9B, 0x5A, 0xFF);

    const int Size = 512;

    [MenuItem("KUCA/Build Kyung Hee Spot Assets")]
    public static void Build()
    {
        var logoImporter = (TextureImporter)AssetImporter.GetAtPath(LogoPath);
        if (!logoImporter.isReadable)
        {
            logoImporter.isReadable = true;
            logoImporter.SaveAndReimport();
        }
        var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);

        var px = new Color[Size * Size];
        float c = (Size - 1) / 2f;
        const float rGold = 254f, rNavy = 246f, rInnerGold = 208f, rWhite = 202f;
        // 로고는 흰 원 안에 들어가도록 크기를 맞춘다 (가로 기준)
        float logoW = rWhite * 1.9f, logoH = logoW * logo.height / logo.width;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy);
            Color col;
            if (r > rGold) col = Color.clear;
            else if (r > rNavy) col = Gold;
            else if (r > rInnerGold) col = Navy;
            else if (r > rWhite) col = Gold;
            else
            {
                col = Color.white;
                float u = (dx + logoW / 2f) / logoW, v = (dy + logoH / 2f) / logoH;
                if (u >= 0f && u <= 1f && v >= 0f && v <= 1f)
                {
                    Color l = logo.GetPixelBilinear(u, v);
                    col = Color.Lerp(col, new Color(l.r, l.g, l.b, 1f), l.a);
                }
            }
            // 바깥 테두리를 부드럽게
            col.a *= Mathf.Clamp01(rGold + 1f - r);
            px[y * Size + x] = col;
        }

        var badge = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
        badge.SetPixels(px);
        badge.Apply();
        System.IO.File.WriteAllBytes(BadgePath, badge.EncodeToPNG());
        Object.DestroyImmediate(badge);
        AssetDatabase.ImportAsset(BadgePath, ImportAssetOptions.ForceUpdate);
        var badgeImporter = (TextureImporter)AssetImporter.GetAtPath(BadgePath);
        badgeImporter.alphaIsTransparency = true;
        badgeImporter.wrapMode = TextureWrapMode.Clamp;
        badgeImporter.SaveAndReimport();

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        var badgeMat = LoadOrCreate(BadgeMatPath, unlit);
        badgeMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BadgePath));
        badgeMat.SetColor("_BaseColor", Color.white);
        // 투명 가장자리는 잘라내고(알파 클립), 앞뒤 모두 그린다
        badgeMat.SetFloat("_AlphaClip", 1f);
        badgeMat.SetFloat("_Cutoff", 0.5f);
        badgeMat.EnableKeyword("_ALPHATEST_ON");
        badgeMat.SetFloat("_Cull", 0f);
        badgeMat.doubleSidedGI = true;
        EditorUtility.SetDirty(badgeMat);

        var poleMat = LoadOrCreate(PoleMatPath, Shader.Find("Universal Render Pipeline/Lit"));
        poleMat.SetColor("_BaseColor", Navy);
        EditorUtility.SetDirty(poleMat);

        AssetDatabase.SaveAssets();
        Debug.Log("[KyungHeeSpotSetup] 경희스팟 배지와 머티리얼을 만들었습니다.");
    }

    static Material LoadOrCreate(string path, Shader shader)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        return mat;
    }
}
