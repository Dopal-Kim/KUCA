using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경희몬(수집 대상) 종류마다 3D 모양을 한 번 렌더해서 도감·목록에 쓰는 그림으로 만든다.
/// 화면 밖 먼 곳에, 비어 있는 레이어 하나만 보는 임시 카메라로 찍는다.
/// </summary>
public static class MonsterThumbnails
{
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    const int Size = 256;
    static readonly Vector3 StagePos = new Vector3(0f, -20000f, 0f);

    public static Texture2D Get(CollectibleType type, Material material)
    {
        if (type == null)
            return null;
        if (cache.TryGetValue(type.id, out Texture2D tex) && tex != null)
            return tex;
        return cache[type.id] = Render(type, material);
    }

    /// <summary>쓰이지 않는 레이어 (없으면 31)</summary>
    public static int StageLayer
    {
        get
        {
            for (int l = 31; l >= 8; l--)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(l)))
                    return l;
            return 31;
        }
    }

    static Texture2D Render(CollectibleType type, Material material)
    {
        int layer = StageLayer;
        var root = new GameObject("ThumbStage");
        root.transform.position = StagePos;

        GameObject shape = GameObject.CreatePrimitive(type.shape);
        Object.DestroyImmediate(shape.GetComponent<Collider>());
        shape.transform.SetParent(root.transform, false);
        shape.transform.localRotation = Quaternion.Euler(20f, 35f, 0f);
        float s = type.shape == PrimitiveType.Cylinder || type.shape == PrimitiveType.Capsule ? 0.8f : 1.15f;
        shape.transform.localScale = Vector3.one * s;
        shape.layer = layer;
        if (material != null)
            shape.GetComponent<Renderer>().sharedMaterial = material;

        var lightGo = new GameObject("ThumbLight", typeof(Light));
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.cullingMask = 1 << layer;

        var camGo = new GameObject("ThumbCamera", typeof(Camera));
        camGo.transform.SetParent(root.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0f, -4f);
        var cam = camGo.GetComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;

        var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 1;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Thumb_" + type.id };
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply(false, false);
        RenderTexture.active = prev;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(root); // 같은 프레임에 다음 썸네일을 찍으므로 바로 지운다
        return tex;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => cache.Clear();
}
