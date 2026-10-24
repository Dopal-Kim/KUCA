using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 진화 연출 (v2). 파트너가 진화하면 화면 가운데에서 피규어가 머리를 축으로 점점 빠르게 돌며 빛나다 번쩍
/// → 커진 모습이 돌다가 서서히 멈춰 앞을 보고
/// "아기 비둘기 → 비둘기" 와 단계 점을 보여 준다. 크기 · 테두리 빛 · 단계 이름은 A 의 CreatureLibrary 값.
/// 피규어는 화면 밖 먼 곳의 전용 카메라로 실시간 렌더해서 (MonsterThumbnails 와 같은 방식) RawImage 에 띄운다.
/// Sample(t) 는 그 시각의 모습을 그대로 만든다 (영상으로 뽑을 때도 쓴다).
/// </summary>
public class EvolutionScreen : MonoBehaviour
{
    public const float Duration = 5.6f;
    const float ShakeStart = 1.0f, FlashAt = 2.4f, SwapAt = 2.75f, RevealEnd = 3.3f, SpinStop = 3.9f, ButtonAt = 4.0f;
    /// <summary>번쩍 직전 가장 빠른 회전 (도/초)</summary>
    const float MaxSpin = 1440f;
    /// <summary>앞을 보는 각도</summary>
    const float Front = 200f;
    static readonly Vector3 StagePos = new Vector3(0f, -30000f, 0f);
    static readonly int RimId = Shader.PropertyToID("_Rim");

    GameObject root, stageRoot;
    Transform pivot, figure;
    MeshRenderer figureRenderer;
    Camera cam;
    RenderTexture rt;
    RawImage figureImage;
    Image flash, glow, rays;
    Text caption, names;
    Image[] dots;
    Button ok;
    CanvasGroup namesGroup, captionGroup;

    string species;
    int from, to;
    float t, baseScale;
    bool playing;

    static readonly System.Collections.Generic.Queue<(string species, int from, int to)> pending =
        new System.Collections.Generic.Queue<(string, int, int)>();

    /// <summary>
    /// 진화했을 때 부른다. 다른 화면 (뽑기 결과 · 완료 화면 · 메뉴 등) 이 열려 있으면 모두 닫힌 뒤에 차례로 띄운다.
    /// </summary>
    public static void Queue(string species, int from, int to)
    {
        if (string.IsNullOrEmpty(species) || to <= from)
            return;
        pending.Enqueue((species, from, to));
        if (FindAnyObjectByType<EvolutionScreen>() == null)
            new GameObject("EvolutionScreen").AddComponent<EvolutionScreen>();
    }

    // 에디터에서 도메인을 다시 불러오지 않을 때 지난 플레이의 대기열이 남지 않게
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => pending.Clear();

    /// <summary>진화 연출을 바로 띄운다 (from → to 단계)</summary>
    public static EvolutionScreen Show(string species, int from, int to)
    {
        EvolutionScreen s = FindAnyObjectByType<EvolutionScreen>();
        if (s == null)
            s = new GameObject("EvolutionScreen").AddComponent<EvolutionScreen>();
        s.Begin(species, from, to);
        return s;
    }

    void Awake() => Build();

    public void Begin(string speciesId, int fromStage, int toStage)
    {
        species = speciesId;
        from = Mathf.Clamp(fromStage, 0, CreatureLibrary.StageScales.Length - 1);
        to = Mathf.Clamp(toStage, 0, CreatureLibrary.StageScales.Length - 1);
        BuildFigure();
        string before = CreatureLibrary.StageName(species, from);
        caption.text = $"어? {before}의 모습이…";
        names.text = $"{before}  →  <b>{CreatureLibrary.StageName(species, to)}</b>";
        for (int i = 0; i < dots.Length; i++)
            dots[i].gameObject.SetActive(i < CreatureLibrary.StageScales.Length);
        root.SetActive(true);
        UIInputBlocker.SetModal(this, true);
        t = 0f;
        playing = true;
        Sample(0f);
    }

    void Update()
    {
        // 기다리던 진화: 다른 화면이 모두 닫히면 띄운다
        if (!root.activeSelf && pending.Count > 0 && !UIInputBlocker.IsModalOpen && !UIInputBlocker.RecentlyClosedModal)
        {
            var next = pending.Dequeue();
            Begin(next.species, next.from, next.to);
        }
        if (!playing)
            return;
        t = Mathf.Min(t + Time.unscaledDeltaTime, Duration);
        Sample(t);
        if (t >= Duration)
            playing = false; // 마지막 모습에서 멈추고 확인 버튼을 기다린다
    }

    /// <summary>시각 t (초) 의 모습. 피규어 카메라도 여기서 한 번 그린다.</summary>
    public void Sample(float time)
    {
        if (figure == null)
            return;
        bool after = time >= SwapAt;
        int stage = after ? to : from;
        float scale = baseScale * CreatureLibrary.StageScales[stage];

        // 1) 처음: 통통 · 2) 흔들리며 빛이 세짐 · 3) 번쩍 · 4) 커진 모습으로 톡
        float shake = Smooth(ShakeStart, FlashAt, time);
        float bob = Mathf.Abs(Mathf.Sin(time * 5f)) * 0.06f * (1f - shake);
        float pop = after ? Pop(Mathf.InverseLerp(SwapAt, RevealEnd, time)) : 1f + Mathf.Abs(Mathf.Sin(time * 14f)) * 0.04f * shake;
        pivot.localScale = Vector3.one * scale * pop;
        pivot.localPosition = new Vector3(0f, bob, 0f);
        pivot.localRotation = Quaternion.Euler(0f, Front + SpinAngle(time), 0f); // 머리를 지나는 세로축으로 돈다

        float rim = after ? CreatureLibrary.StageRims[stage] : Mathf.Lerp(CreatureLibrary.StageRims[from], 1.4f, shake);
        var block = new MaterialPropertyBlock();
        figureRenderer.GetPropertyBlock(block);
        block.SetFloat(RimId, rim);
        figureRenderer.SetPropertyBlock(block);

        float flashA = time < FlashAt ? Mathf.Max(0f, Mathf.Sin(time * 9f)) * 0.35f * shake * shake
            : time < SwapAt ? Mathf.InverseLerp(FlashAt, FlashAt + 0.2f, time)
            : 1f - Mathf.InverseLerp(SwapAt, RevealEnd, time);
        flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flashA));

        glow.color = new Color(1f, 0.85f, 0.45f, after ? 0.55f : 0.25f + 0.45f * shake);
        glow.rectTransform.localScale = Vector3.one * (after ? 0.95f : 0.8f + 0.3f * shake);
        rays.gameObject.SetActive(after);
        rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -time * 30f);

        captionGroup.alpha = after ? 1f : Smooth(0.2f, 0.8f, time);
        if (after) caption.text = "축하해요! 진화했어요";
        namesGroup.alpha = Smooth(RevealEnd, RevealEnd + 0.6f, time);
        for (int i = 0; i < dots.Length; i++)
            dots[i].color = i <= stage ? new Color(1f, 0.72f, 0.2f) : new Color(1f, 1f, 1f, 0.25f);
        ok.gameObject.SetActive(time >= ButtonAt);

        cam.Render();
    }

    /// <summary>
    /// 회전 각도: 흔들리기 시작하면 0 → MaxSpin 까지 점점 빨라지고 (등가속), 번쩍하는 동안 그 속도로 돌다가,
    /// 바뀐 뒤에는 같은 속도에서 SpinStop 까지 서서히 멈추며 정확히 앞(Front)에서 선다.
    /// </summary>
    static float SpinAngle(float time)
    {
        float ramp = FlashAt - ShakeStart;
        if (time <= ShakeStart)
            return 0f;
        if (time <= FlashAt)
        {
            float x = time - ShakeStart;
            return 0.5f * MaxSpin / ramp * x * x;
        }
        float atFlash = 0.5f * MaxSpin * ramp;
        if (time <= SwapAt)
            return atFlash + MaxSpin * (time - FlashAt);
        // 감속: 속도 MaxSpin 에서 0 까지 고르게 줄이면 이동 각도 = MaxSpin × 시간 / 2. 끝이 360 의 배수가 되게 시간을 맞춘다
        float atSwap = atFlash + MaxSpin * (SwapAt - FlashAt);
        float total = Mathf.Ceil((atSwap + MaxSpin * (SpinStop - SwapAt) * 0.5f) / 360f) * 360f;
        float d = total - atSwap;
        float dur = 2f * d / MaxSpin;
        float k = Mathf.Clamp01((time - SwapAt) / dur);
        return atSwap + d * (1f - (1f - k) * (1f - k));
    }

    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    /// <summary>작게 → 조금 크게 → 제자리</summary>
    static float Pop(float k) => k < 0.6f ? Mathf.Lerp(0.55f, 1.12f, k / 0.6f) : Mathf.Lerp(1.12f, 1f, (k - 0.6f) / 0.4f);

    void Close()
    {
        if (t < ButtonAt)
        {
            // 연출 중 뒤로 가기 · 화면 누르기: 끝 모습(앞을 보고 멈춤 + 확인 버튼)으로 건너뛴다
            t = Duration;
            Sample(t);
            playing = false;
            return;
        }
        playing = false;
        root.SetActive(false);
        UIInputBlocker.SetModal(this, false);
        if (stageRoot != null)
            Destroy(stageRoot);
    }

    void OnDisable() => UIInputBlocker.SetModal(this, false);

    void OnDestroy()
    {
        if (stageRoot != null) Destroy(stageRoot);
        if (rt != null) rt.Release();
    }

    // ---------- 피규어 무대 ----------

    void BuildFigure()
    {
        if (stageRoot != null)
            DestroyImmediate(stageRoot);
        if (!CreatureLibrary.TryGetMesh(species, out Mesh mesh) || mesh == null)
            return;
        int layer = MonsterThumbnails.StageLayer;
        stageRoot = new GameObject("EvolutionStage");
        stageRoot.transform.position = StagePos;
        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(stageRoot.transform, false);
        Bounds b = mesh.bounds;
        var body = new GameObject("Figure", typeof(MeshFilter), typeof(MeshRenderer));
        body.layer = layer;
        body.transform.SetParent(pivot, false);
        // 회전축을 머리 가운데로: 위쪽 35% 정점들의 가운데 (x, z). 높이는 몸 가운데
        Vector3 head = HeadCenter(mesh, b);
        body.transform.localPosition = -new Vector3(head.x, b.center.y, head.z);
        body.GetComponent<MeshFilter>().sharedMesh = mesh;
        figureRenderer = body.GetComponent<MeshRenderer>();
        figureRenderer.sharedMaterial = CreatureLibrary.Material;
        if (CreatureLibrary.TryGetGlass(species, out Mesh glassMesh) && CreatureLibrary.GlassMaterial != null)
        {
            var g = new GameObject("Glass", typeof(MeshFilter), typeof(MeshRenderer));
            g.layer = layer;
            g.transform.SetParent(body.transform, false);
            g.GetComponent<MeshFilter>().sharedMesh = glassMesh;
            g.GetComponent<MeshRenderer>().sharedMaterial = CreatureLibrary.GlassMaterial;
        }
        figure = body.transform;
        // 가장 큰 단계(×1.3)도 화면 안에 들어오게, 처음 단계가 화면의 약 60% 가 되게
        baseScale = 1f;

        var lightGo = new GameObject("Light", typeof(Light));
        lightGo.transform.SetParent(stageRoot.transform, false);
        lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.cullingMask = 1 << layer;

        const float fov = 26f;
        float radius = b.extents.magnitude * CreatureLibrary.StageScales[CreatureLibrary.StageScales.Length - 1] * 1.15f;
        float dist = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad);
        var camGo = new GameObject("Camera", typeof(Camera), typeof(NeutralAmbient));
        camGo.transform.SetParent(stageRoot.transform, false);
        camGo.transform.localPosition = new Vector3(0f, radius * 0.2f, -dist);
        camGo.transform.LookAt(stageRoot.transform.position);
        cam = camGo.GetComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.cullingMask = 1 << layer;
        cam.fieldOfView = fov;
        cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
        cam.farClipPlane = dist + radius * 2f;
        if (rt == null)
        {
            rt = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            figureImage.texture = rt;
        }
        cam.targetTexture = rt;
    }

    static Vector3 HeadCenter(Mesh mesh, Bounds b)
    {
        Vector3[] v = mesh.isReadable ? mesh.vertices : null;
        if (v == null || v.Length == 0)
            return b.center;
        float top = b.min.y + b.size.y * 0.65f;
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Vector3 p in v)
            if (p.y >= top) { sum += p; n++; }
        return n > 0 ? sum / n : b.center;
    }

    // ---------- 화면 ----------

    void Build()
    {
        Canvas canvas = UIKit.CreateCanvas(transform, "EvolutionCanvas", 56);
        var bg = new GameObject("Evolution", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        bg.transform.SetParent(canvas.transform, false);
        bg.texture = HudIcons.Gradient(Color.Lerp(KucaTheme.PrimaryDeep, Color.black, 0.25f), Color.Lerp(KucaTheme.PrimaryDeep, Color.black, 0.8f));   // 밤 숲 무대
        bg.raycastTarget = true;
        UIKit.Stretch(bg.rectTransform);
        root = bg.gameObject;
        Button skip = root.AddComponent<Button>();
        skip.transition = Selectable.Transition.None;
        skip.onClick.AddListener(() => { if (t < ButtonAt) Close(); });
        BackButton.Attach(root, Close);   // 뒤로 가기로도 닫힌다 (연출 중이면 건너뛰기)
        RectTransform r = bg.rectTransform;

        glow = UIKit.Image(r, "Glow", new Color(1f, 0.85f, 0.45f, 0.3f));
        glow.sprite = HudIcons.SoftDisk;
        Center(glow.rectTransform, 80f, 1100f);
        rays = UIKit.Image(r, "Rays", new Color(1f, 0.93f, 0.62f, 0.8f));
        rays.sprite = HudIcons.Rays;
        Center(rays.rectTransform, 80f, 1500f);

        var fig = new GameObject("Figure", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        fig.transform.SetParent(r, false);
        fig.raycastTarget = false;
        figureImage = fig;
        Center(fig.rectTransform, 80f, 980f);

        captionGroup = Group(r, "Caption", out RectTransform cr);
        Place(cr, new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1000f, 100f));
        caption = UIKit.Text(cr, "", 50, TextAnchor.MiddleCenter, Color.white);
        UIKit.Bold(caption);
        UIKit.Stretch(caption.rectTransform);
        Shade(caption);

        namesGroup = Group(r, "Names", out RectTransform nr);
        Place(nr, new Vector2(0.5f, 0f), new Vector2(0f, 560f), new Vector2(1060f, 180f));
        names = UIKit.Text(nr, "", 46, TextAnchor.UpperCenter, Color.white);
        names.supportRichText = true;
        UIKit.Stretch(names.rectTransform);
        Shade(names);   // 노란 빛줄기 위에서도 읽히게
        dots = new Image[3];
        for (int i = 0; i < dots.Length; i++)
        {
            dots[i] = UIKit.Image(nr, "Dot" + i, Color.white);
            dots[i].sprite = HudIcons.Circle;
            Place(dots[i].rectTransform, new Vector2(0.5f, 0f), new Vector2((i - 1) * 64f, 20f), new Vector2(40f, 40f));
        }

        ok = UIKit.Button(r, "멋져요!", Close, new Color(1f, 0.72f, 0.2f), 42);
        Image oi = ok.GetComponent<Image>();
        oi.sprite = HudIcons.Pill;
        oi.type = Image.Type.Sliced;
        oi.pixelsPerUnitMultiplier = 120f / 120f;
        Text ot = ok.GetComponentInChildren<Text>();
        ot.color = new Color(0.2f, 0.12f, 0.02f);
        UIKit.Bold(ot);
        Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(600f, 120f));
        ok.gameObject.AddComponent<PressScale>();

        flash = UIKit.Image(r, "Flash", new Color(1f, 1f, 1f, 0f));
        UIKit.Stretch(flash.rectTransform);
        root.SetActive(false);
    }

    static void Shade(Text t)
    {
        var s = t.gameObject.AddComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.6f);
        s.effectDistance = new Vector2(0f, -4f);
        var o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.05f, 0.15f, 0.08f, 0.55f);
        o.effectDistance = new Vector2(2f, -2f);
    }

    static CanvasGroup Group(RectTransform parent, string name, out RectTransform rt)
    {
        rt = UIKit.Rect(parent, name);
        var g = rt.gameObject.AddComponent<CanvasGroup>();
        g.blocksRaycasts = false;
        return g;
    }

    /// <summary>화면 가운데에서 y 만큼 위, 정사각형</summary>
    static void Center(RectTransform rt, float y, float size) => Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(size, size));

    static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
