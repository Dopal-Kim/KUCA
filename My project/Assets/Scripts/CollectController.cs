using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// 수집 대상을 탭하면, Player가 수집 반경 안에 있을 때만 획득한다.
/// Player 둘레에 수집 반경 링을 그리고, 반경 안의 대상을 강조한다.
/// </summary>
public class CollectController : MonoBehaviour
{
    public CampusMap campusMap;
    public CollectibleSpawner spawner;
    public GameHUD hud;
    public Camera cam;

    [Tooltip("수집할 수 있는 거리 (m)")]
    public float collectRadius = 40f;

    [Header("Tap")]
    [Tooltip("이보다 많이 움직이면 탭이 아니라 드래그로 본다 (픽셀)")]
    public float tapMaxMove = 20f;
    public float tapMaxDuration = 0.4f;

    public GameProgress Progress { get; private set; }

    LineRenderer ring;
    const int RingSegments = 64;

    void Awake()
    {
        Progress = GameProgress.Load();
        if (cam == null)
            cam = Camera.main;
        CreateRing();
    }

#if ENABLE_INPUT_SYSTEM
    void OnEnable() => EnhancedTouchSupport.Enable();
    void OnDisable() => EnhancedTouchSupport.Disable();
#endif

    void Start()
    {
        if (hud != null)
            hud.ShowProgress(Progress, spawner != null ? spawner.types : null);
    }

    void Update()
    {
        if (campusMap == null || campusMap.player == null)
            return;

        Vector3 p = campusMap.player.position;
        UpdateRing(p);

        if (spawner != null)
            foreach (var c in spawner.Active)
                if (c != null)
                    c.SetInRange(CollectibleSpawner.HorizontalDistance(c.transform.position, p) <= collectRadius);

        if (TryGetTap(out Vector2 screenPos) && !CharacterCustomizer.IsOpen && !CharacterCustomizer.IsOverUI(screenPos))
            HandleTap(screenPos);
    }

    bool TryGetTap(out Vector2 pos)
    {
        pos = default;
#if ENABLE_INPUT_SYSTEM
        foreach (Touch t in Touch.activeTouches)
        {
            if (t.phase != UnityEngine.InputSystem.TouchPhase.Ended)
                continue;
            if (Touch.activeTouches.Count > 1)
                return false;
            float moved = (t.screenPosition - t.startScreenPosition).magnitude;
            float duration = (float)(t.time - t.startTime);
            if (moved <= tapMaxMove && duration <= tapMaxDuration)
            {
                pos = t.screenPosition;
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (Touch.activeTouches.Count == 0 && mouse != null && mouse.leftButton.wasReleasedThisFrame)
        {
            pos = mouse.position.ReadValue();
            return true;
        }
#endif
        return false;
    }

    void HandleTap(Vector2 screenPos)
    {
        if (cam == null || spawner == null)
            return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        Collectible target = null;
        float best = float.MaxValue;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 5000f, ~0, QueryTriggerInteraction.Collide))
        {
            var c = hit.collider.GetComponent<Collectible>();
            if (c != null && hit.distance < best)
            {
                best = hit.distance;
                target = c;
            }
        }
        if (target == null)
            return;

        float dist = CollectibleSpawner.HorizontalDistance(target.transform.position, campusMap.player.position);
        if (dist > collectRadius)
        {
            hud?.Toast($"{target.Type.displayName}까지 {dist:F0}m — {collectRadius:F0}m 안으로 걸어가세요");
            return;
        }

        Progress.Add(target.Type);
        Progress.Save();
        hud?.Toast($"{target.Type.displayName} 획득! +{target.Type.points}");
        hud?.ShowProgress(Progress, spawner.types);
        spawner.Remove(target);
    }

    void CreateRing()
    {
        var go = new GameObject("CollectRadiusRing");
        go.transform.SetParent(transform, false);
        ring = go.AddComponent<LineRenderer>();
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = RingSegments;
        ring.widthMultiplier = 1.5f;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        Shader s = Shader.Find("Sprites/Default");
        ring.material = new Material(s != null ? s : Shader.Find("Universal Render Pipeline/Unlit"));
        Color c = new Color(0.15f, 0.45f, 1f, 0.8f);
        ring.startColor = c;
        ring.endColor = c;
    }

    void UpdateRing(Vector3 center)
    {
        if (ring == null)
            return;
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            ring.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * collectRadius, 0.5f, center.z + Mathf.Sin(a) * collectRadius));
        }
    }
}
