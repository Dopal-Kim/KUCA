using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

/// <summary>
/// 산책하는 동안 휴대폰 걸음 센서로 걸음 수를 센다 (Input System 의 StepCounter).
/// - 안드로이드: 걸음 센서 (켠 뒤로 쌓인 값이라 첫 값을 기준으로 뺀다), "신체 활동" 권한 필요
/// - 아이폰: CoreMotion 걸음 측정 (켠 때부터 0), "동작 및 피트니스" 권한 (Info.plist 문구는 Editor/StepPermissions.cs)
/// 센서가 없거나 · 권한을 거절했거나 · 설정에서 껐으면 -1 → 서버가 거리로 어림한다.
/// 걸음 수는 이번 산책 것만 서버에 보내고, 서버는 저장하지 않는다 (docs/api.md "건강").
/// </summary>
public class StepTracker : MonoBehaviour
{
    const string EnabledKey = "health_step_sensor";
    const string AndroidPermission = "android.permission.ACTIVITY_RECOGNITION";

    static StepTracker instance;
    int baseline = -1, last = -1, session = -1;

    /// <summary>설정 "걸음 센서로 걸음 세기" (기본 켜짐). 끄면 센서를 끄고 어림값만 쓴다</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        set
        {
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value) Stop();
        }
    }

    /// <summary>산책을 시작할 때 (WalkSession.Begin). 권한이 없으면 묻는다 (두 번 거절하면 OS 가 더 묻지 않는다)</summary>
    public static void Begin(int walkSession)
    {
        if (!Enabled || Application.isEditor) return;
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission(AndroidPermission))
        {
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionGranted += _ => { if (WalkSession.Active && WalkSession.Id == walkSession) StartCounting(walkSession); };
            Permission.RequestUserPermission(AndroidPermission, callbacks);
            return;
        }
#endif
        StartCounting(walkSession);
    }

    static void StartCounting(int walkSession)
    {
        StepCounter counter = StepCounter.current;
        if (counter == null) return;   // 걸음 센서가 없는 폰
        if (!counter.enabled) InputSystem.EnableDevice(counter);
        if (instance == null)
        {
            var go = new GameObject("StepTracker");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<StepTracker>();
        }
        instance.session = walkSession;
        instance.last = -1;
        // 아이폰은 켠 때부터 0 이라 바로 기준 0, 안드로이드는 첫 값이 올 때 기준을 잡는다
        instance.baseline = Application.platform == RuntimePlatform.IPhonePlayer ? 0 : -1;
    }

    /// <summary>산책이 끝날 때 이번 산책 걸음 수. 못 셌으면 -1 (다른 산책의 값이면 -1)</summary>
    public static int Finish(int walkSession)
    {
        if (instance == null || instance.session != walkSession) return -1;
        instance.Poll();
        int steps = instance.baseline >= 0 && instance.last >= instance.baseline ? instance.last - instance.baseline : -1;
        Stop();
        return steps > 0 ? steps : -1;
    }

    /// <summary>산책을 그만두거나 설정에서 껐을 때</summary>
    public static void Stop()
    {
        if (instance != null) instance.session = -1;
        StepCounter counter = StepCounter.current;
        if (counter != null && counter.enabled) InputSystem.DisableDevice(counter);
    }

    void Update()
    {
        if (session >= 0 && Time.frameCount % 30 == 0) Poll();   // 30프레임에 한 번이면 충분
    }

    void Poll()
    {
        StepCounter counter = StepCounter.current;
        if (counter == null || !counter.enabled) return;
        int value = counter.stepCounter.ReadValue();
        if (value <= 0) return;   // 아직 첫 값이 오지 않음
        if (baseline < 0) baseline = value;
        if (value < baseline) baseline = value;   // 폰을 다시 켜서 센서 값이 처음부터 시작됨
        last = value;
    }
}
