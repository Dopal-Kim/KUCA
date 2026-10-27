// 로컬(KUCA-check) 전용: 펫 24종을 앞 · 옆 · 뒤 · 비스듬히(가까이 메시) + 앞 · 뒤(멀리 메시) 로 찍어 한 장씩 저장한다.
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PetTurnaround
{
    const int Size = 512;

    public static void Run()
    {
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "PetShots"));
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
        int n = 0;
        float[] yaws = { 180f, 90f, 0f, 225f };   // 앞 · 옆 · 뒤 · 비스듬히 (썸네일과 같은 방향 기준)
        foreach (string id in CreatureLibrary.AllSpecies)
        {
            if (!CreatureLibrary.TryGetMesh(id, out Mesh near) || near == null) { Debug.LogWarning("[PetTurnaround] 메시 없음: " + id); continue; }
            CreatureLibrary.TryGetFarMesh(id, out Mesh far);
            var sheet = new Texture2D(Size * 6, Size, TextureFormat.RGBA32, false);
            var bg = new Color32[Size * 6 * Size];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(236, 240, 236, 255);
            sheet.SetPixels32(bg);
            for (int k = 0; k < 4; k++) Blit(sheet, Render(id, near, yaws[k], true), k);
            if (far != null) { Blit(sheet, Render(id, far, 180f, false), 4); Blit(sheet, Render(id, far, 0f, false), 5); }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(dir, $"{n:00}_{id}.png"), sheet.EncodeToPNG());
            n++;
        }
        Debug.Log($"[UiCapture] 펫 ({n}종) → {dir}");
        EditorApplication.Exit(0);
    }

    static void Blit(Texture2D sheet, Texture2D t, int slot)
    {
        Color[] px = t.GetPixels();
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            int x = i % Size, y = i / Size;
            Color b = sheet.GetPixel(slot * Size + x, y);
            sheet.SetPixel(slot * Size + x, y, Color.Lerp(b, new Color(c.r, c.g, c.b, 1f), c.a));
        }
        Object.DestroyImmediate(t);
    }

    static Texture2D Render(string id, Mesh mesh, float yaw, bool glassToo)
    {
        int layer = MonsterThumbnails.StageLayer;
        var root = new GameObject("PetStage");
        root.transform.position = new Vector3(0f, -21000f, 0f);
        var pivot = new GameObject("Pivot").transform;
        pivot.SetParent(root.transform, false);
        pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Bounds b = mesh.bounds;
        var body = new GameObject("Figure", typeof(MeshFilter), typeof(MeshRenderer));
        body.layer = layer;
        body.transform.SetParent(pivot, false);
        body.transform.localPosition = -b.center;
        body.GetComponent<MeshFilter>().sharedMesh = mesh;
        body.GetComponent<MeshRenderer>().sharedMaterial = CreatureLibrary.Material;
        if (glassToo && CreatureLibrary.TryGetGlass(id, out Mesh glass) && CreatureLibrary.GlassMaterial != null)
        {
            var g = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
            g.layer = layer;
            g.transform.SetParent(body.transform, false);
            g.GetComponent<MeshFilter>().sharedMesh = glass;
            g.GetComponent<MeshRenderer>().sharedMaterial = CreatureLibrary.GlassMaterial;
        }
        var lightGo = new GameObject("Light", typeof(Light));
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.cullingMask = 1 << layer;
        const float fov = 26f;
        float radius = b.extents.magnitude;
        float dist = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) * 0.95f;
        var camGo = new GameObject("Cam", typeof(Camera), typeof(NeutralAmbient));
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, radius * 0.25f, -dist);
        camGo.transform.LookAt(root.transform.position);
        var cam = camGo.GetComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.fieldOfView = fov;
        cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
        cam.farClipPlane = dist + radius * 2f;
        var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(root);
        return tex;
    }
}
