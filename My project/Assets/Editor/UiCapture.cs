using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 실제 구동 화면을 1080×1920 PNG 로 차례대로 찍는다 (인터페이스 개선 사이클, docs/인터페이스_기준.md).
///   메뉴 KUCA › UI 화면 캡처, 또는 배치 모드:
///   Unity.exe -projectPath "My project" -executeMethod UiCapture.Run -logFile ui.log   (-batchmode 에서는 UI 가 안 찍힘: 창을 띄워 실행)
/// 결과: 저장소 폴더 Builds/UiShots/NN_이름.png (git 에 올라가지 않음).
/// - 화면 캔버스(Overlay)를 찍는 순간에만 카메라 캔버스로 바꿔 지도 카메라 위에 겹친다.
/// - 사용자 저장 데이터(PlayerPrefs · 캐릭터 파일)와 섞이지 않게 제품 이름을 KUCA-Capture 로 바꿔 실행하고 끝나면 되돌린다.
/// - 게임 코드는 이름으로만 부른다 (리플렉션): 화면 코드가 바뀌어도 이 파일 때문에 컴파일이 깨지지 않게.
///   못 찾은 화면은 건너뛰고 로그에 [UiCapture] 건너뜀 을 남긴다.
/// </summary>
public static class UiCapture
{
    const int W = 1080, H = 1920;
    const double Timeout = 900;
    static readonly string OutDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "UiShots"));

    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();   // CloseAll 같은 안쪽 단계는 위에 쌓는다
    static double waitUntil, startedAt;
    static int shotIndex;
    static string savedProduct;
    static bool savedOptionsEnabled;
    static EnterPlayModeOptions savedOptions;

    [MenuItem("KUCA/UI 화면 캡처")]
    public static void Run() => Start(OutDir, Script());

    /// <summary>
    /// 캠퍼스 이름 있는 건물마다 북 · 동 · 남 · 서 (35° 내려다봄) 와 위 (80°) 에서 지도만 찍는다 → Builds/MapShots/&lt;이름&gt;_N.png …
    ///   Unity.exe -batchmode -projectPath "My project" -executeMethod UiCapture.RunMap -logFile map.log
    /// </summary>
    [MenuItem("KUCA/구조물 사방 캡처")]
    public static void RunMap() => Start(MapDir, MapScript());

    static readonly string MapDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "MapShots"));
    static string outDir;

    static void Start(string dir, IEnumerator first)
    {
        outDir = dir;
        Directory.CreateDirectory(dir);
        foreach (string f in Directory.GetFiles(dir, "*.png"))
            File.Delete(f);
        savedProduct = PlayerSettings.productName;
        savedOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        savedOptions = EditorSettings.enterPlayModeOptions;
        PlayerSettings.productName = "KUCA-Capture";
        ResetCaptureData();
        // 도메인을 다시 불러오지 않아야 이 스크립트의 진행 상태가 플레이 모드 안에서도 이어진다
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        // 명령줄 실행은 빈 씬으로 시작하므로 첫 빌드 씬(지도 · 카메라 · HUD)을 연다
        if (string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path) && EditorBuildSettings.scenes.Length > 0)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path);
        // Game 화면을 모바일 세로(1080×1920)로: 레이아웃이 PC 비율이 아니라 폰 기준으로 잡히게
        typeof(Editor).Assembly.GetType("UnityEditor.PlayModeWindow")?.GetMethod("SetCustomRenderingResolution")
            ?.Invoke(null, new object[] { (uint)W, (uint)H, "KUCA Mobile" });
        shotIndex = 0;
        steps.Clear();
        steps.Push(first);
        startedAt = EditorApplication.timeSinceStartup;
        waitUntil = startedAt + 6;
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// 지난 캡처가 남긴 산책 기록 · 진행 · 설정을 지워 매번 처음 깐 앱 상태로 찍는다.
    /// 제품 이름이 KUCA-Capture 로 바뀐 저장 폴더일 때만 지운다 (사용자 실제 데이터 보호).
    /// </summary>
    static void ResetCaptureData()
    {
        string dir = Application.persistentDataPath;
        if (!dir.Contains("KUCA-Capture"))
        {
            Debug.LogWarning("[UiCapture] 저장 폴더가 캡처 전용이 아니라 초기화를 건너뜀: " + dir);
            return;
        }
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now - startedAt > Timeout)
        {
            Debug.LogError("[UiCapture] 시간 초과");
            Finish(1);
            return;
        }
        if (!EditorApplication.isPlaying || now < waitUntil)
            return;
        IEnumerator top = steps.Peek();
        bool more;
        try
        {
            more = top.MoveNext();
        }
        catch (Exception e)
        {
            Debug.LogError("[UiCapture] 실패: " + e);
            Finish(1);
            return;
        }
        if (!more)
        {
            steps.Pop();
            if (steps.Count == 0)
                Finish(0);
            return;
        }
        if (top.Current is IEnumerator inner)
            steps.Push(inner);
        else
            waitUntil = now + (top.Current is float s ? s : 1.2f);
    }

    static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        EditorApplication.isPlaying = false;
        PlayerSettings.productName = savedProduct;
        EditorSettings.enterPlayModeOptionsEnabled = savedOptionsEnabled;
        EditorSettings.enterPlayModeOptions = savedOptions;
        Debug.Log($"[UiCapture] 끝 ({shotIndex}장) → {outDir}");
        if (Application.isBatchMode)
            EditorApplication.Exit(code);
    }

    // ---------- 찍는 순서 ----------

    static IEnumerator Script()
    {
        yield return Shot("개인정보_안내");
        Click("확인하고 시작");   // 저장 폴더를 지우고 시작하므로 동의 화면이 먼저 뜬다
        yield return 1.5f;
        yield return Shot("로그인");
        Click("에디터: 로그인 건너뛰기");
        yield return 4f;
        yield return Shot("첫_꾸미기");   // 처음 깐 앱은 로그인 뒤 꾸미기가 먼저 뜬다
        yield return CloseAll();
        // 첫 파트너 고르기는 다른 화면이 다 닫힌 뒤 뜬다 → 기다렸다 찍고 비둘기를 고른다
        yield return 1.5f;
        if (StarterShown())
            yield return Shot("첫_파트너");
        yield return CloseAll();
        yield return Shot("지도");

        object customizer = Find("CharacterCustomizer");
        if (Call(customizer, "SetOpen", true))
        {
            Type tab = customizer.GetType().GetNestedType("Tab", BindingFlags.NonPublic | BindingFlags.Public);
            foreach (string t in new[] { "Face", "Color", "Clothes" })
            {
                if (tab != null)
                    Call(customizer, "SetTab", Enum.Parse(tab, t));
                yield return 1.5f;
                yield return Shot("꾸미기_" + t);
            }
            Call(customizer, "SetOpen", false);
            yield return 0.5f;
        }

        object menus = Find("KhuMenus");
        object progress = Get(Find("CollectController"), "Progress");
        if (progress != null)
            Set(progress, "tickets", 12);   // 뽑기 화면 · 결과를 보이게
        if (Call(menus, "OpenMainMenu")) { yield return 1f; yield return Shot("경희몬_메뉴"); }
        Type menuTab = menus?.GetType().GetNestedType("Tab");
        foreach (string t in new[] { "Monsters", "Dex" })   // 도구(가방) 탭은 v2 에서 숨김 (결정 D4)
            if (menuTab != null && Call(menus, "OpenCollection", Enum.Parse(menuTab, t))) { yield return 1f; yield return Shot("경희몬_" + t); }
        if (Call(menus, "OpenGacha")) { yield return 1f; yield return Shot("뽑기"); }
        if (Click("1장 뽑기"))
        {
            yield return 1.0f;
            yield return Shot("뽑기_결과_1");
            Set(Find("HatchScreen"), "skip", true);   // 화면 누르기와 같음: 흔들림을 건너뛰고 연다
            yield return 1.5f;
            yield return Shot("뽑기_결과_2");
        }
        yield return CloseAll();
        if (Call(menus, "OpenProfile")) { yield return 1f; yield return Shot("프로필"); yield return ShotBottom("프로필_아래"); }
        yield return CloseAll();

        if (CallStatic("RoutePlanner", "Open")) { yield return 2f; yield return Shot("산책_추천"); }
        yield return CloseAll();
        if (CallStatic("MatchScreen", "Open")) { yield return 2f; yield return Shot("함께_걷기"); }
        yield return CloseAll();
        if (CallStatic("WalkHistoryScreen", "Show")) { yield return 1.5f; yield return Shot("산책_기록"); }
        yield return CloseAll();

        object done = New("WalkCompleteRequest");
        if (done != null)
        {
            Set(done, "meters", 1850);
            Set(done, "minutes", 32);
            Set(done, "durationSec", 1920);
            Set(done, "plannedMeters", 1900);
            Set(done, "stopsDone", 3);
            Set(done, "stopsTotal", 3);
            if (CallStatic("WalkCompleteScreen", "Show", done)) { yield return 3f; yield return Shot("산책_완료"); yield return ShotBottom("산책_완료_아래"); }
            yield return CloseAll();
        }

        if (CallStatic("EvolutionScreen", "Show", "library_owl", 0, 1) || CallStatic("EvolutionScreen", "Show", "pigeon", 0, 1))
        {
            yield return 1f;
            yield return Shot("진화_1");
            yield return 4.8f;   // 5.6 초 연출이 앞을 보고 멈추고 확인 버튼이 나온 뒤
            yield return Shot("진화_2");
        }
        yield return CloseAll();

        object hud = Find("GameHUD");
        if (Call(hud, "OpenSettings")) { yield return 1f; yield return Shot("설정"); }
        yield return CloseAll();
        if (Call(hud, "OpenAlbum")) { yield return 1.5f; yield return Shot("기록_앨범"); }
        yield return CloseAll();

        object spots = Get(Find("KyungHeeSpots"), "Spots");
        if (spots is IList list && list.Count > 0 && Call(Find("MemoBoard"), "OpenSpot", list[0])) { yield return 2f; yield return Shot("장소_메모"); yield return ShotBottom("장소_메모_아래"); }
        yield return CloseAll();

        if (progress != null && Call(Find("CatchCameraScreen"), "OpenPartnerPhoto", progress)) { yield return 2f; yield return Shot("파트너_사진"); }
        yield return CloseAll();

        if (CallStatic("PreferenceSurvey", "Open", null)) { yield return 1f; yield return Shot("취향_설문"); }
        yield return CloseAll();
        yield return Shot("지도_끝");
    }

    // ---------- 구조물 사방 ----------

    static IEnumerator MapScript()
    {
        Click("확인하고 시작");
        yield return 1.5f;
        Click("에디터: 로그인 건너뛰기");
        yield return 5f;
        yield return CloseAll();
        Type infoType = TypeOf("CampusBuildingInfo");
        Camera cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (infoType == null || cam == null)
            yield break;
        // 플레이 모드에서만 바꾸므로 끝나면 저절로 되돌아간다:
        // 건물 위 위치 표지(배지 + 막대)는 건물 앞을 가려 끄고, 한낮 · 봄으로 고정해 웹 미리보기(tools/keyart/preview)와 같은 조건으로 찍는다
        // 플레이어 · 파트너 · 야생 경희몬도 끈다 (체육대학관 옆에 건물 3층 높이 캐릭터가 찍혔음, MAP_REVIEW M5)
        foreach (string hide in new[] { "KyungHeeSpot", "ModularCharacter", "PartnerBuddy", "Collectible" })
        {
            Type ht = TypeOf(hide);
            if (ht != null)
                foreach (Component s in UnityEngine.Object.FindObjectsByType(ht, FindObjectsInactive.Exclude, FindObjectsSortMode.None).Cast<Component>())
                    s.gameObject.SetActive(false);
        }
        object look = Find("KeyArtLook");
        Type lookType = look?.GetType();
        if (lookType != null)
        {
            Call(look, "SetMode", Enum.Parse(lookType.GetNestedType("Mode"), "Day"));
            Call(look, "SetSeason", Enum.Parse(lookType.GetNestedType("Season"), "Spring"));
        }
        yield return 0.5f;
        var done = new HashSet<string>();
        foreach (Component info in UnityEngine.Object.FindObjectsByType(infoType, FindObjectsInactive.Include, FindObjectsSortMode.None).Cast<Component>())
        {
            string name = Get(info, "buildingName") as string;
            Collider col = info.GetComponentInChildren<Collider>(true);
            if (string.IsNullOrEmpty(name) || col == null || !done.Add(name))
                continue;
            Bounds b = col.bounds;
            float size = Mathf.Max(b.size.x, b.size.z, b.size.y * 1.5f);
            float dist = size * 1.4f + 40f;
            Vector3 target = new Vector3(b.center.x, b.min.y + b.size.y * 0.35f, b.center.z);
            foreach (var (tag, yaw, pitch) in new[] { ("N", 180f, 35f), ("E", 270f, 35f), ("S", 0f, 35f), ("W", 90f, 35f), ("T", 0f, 80f) })
            {
                // 북쪽(+z)에서 남쪽을 바라보면 yaw 180
                Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
                // 옆 건물에 막히면 (원자로센터 E·S, 도예관 S 등) 더 내려다보는 각도로
                if (Physics.Raycast(target, -(rot * Vector3.forward), out RaycastHit hit, dist) && hit.collider.GetComponentInParent(infoType) != info)
                    rot = Quaternion.Euler(Mathf.Max(pitch, 58f), yaw, 0f);
                ShotFrom(cam, target - rot * Vector3.forward * dist, rot, $"{Safe(name)}_{tag}");
            }
            yield return 0.05f;
        }
    }

    static string Safe(string s) => string.Concat(s.Split(Path.GetInvalidFileNameChars())).Replace(' ', '_');

    /// <summary>지도 카메라를 잠깐 옮겨 화면 캔버스 없이 지도만 960×720 으로 찍는다</summary>
    static void ShotFrom(Camera cam, Vector3 pos, Quaternion rot, string name)
    {
        Vector3 oldPos = cam.transform.position;
        Quaternion oldRot = cam.transform.rotation;
        float oldFar = cam.farClipPlane;
        cam.transform.SetPositionAndRotation(pos, rot);
        cam.farClipPlane = Mathf.Max(oldFar, 3000f);
        var rt = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        RenderTexture oldTarget = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = oldTarget;
        cam.transform.SetPositionAndRotation(oldPos, oldRot);
        cam.farClipPlane = oldFar;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        shotIndex++;
        File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
    }

    // ---------- 도우미 ----------

    /// <summary>첫 파트너 고르기가 떠 있으면 비둘기를 골라 닫는다 (뒤로 가기로는 안 닫히는 필수 화면)</summary>
    static bool StarterShown()
    {
        object menus = Find("KhuMenus");
        return Get(menus, "starterScreen") is GameObject s && s.activeInHierarchy;
    }

    static void PickStarterIfShown()
    {
        if (StarterShown())
            Call(Find("KhuMenus"), "PickStarter", "pigeon");
    }

    /// <summary>열린 화면을 뒤로 가기(BackButton)로 위에서부터 닫는다</summary>
    static IEnumerator CloseAll()
    {
        PickStarterIfShown();
        Type back = TypeOf("BackButton");
        FieldInfo open = back?.GetField("open", BindingFlags.NonPublic | BindingFlags.Static);
        MethodInfo handle = back?.GetMethod("HandleBack", BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < 12 && open != null && handle != null; i++)
        {
            var panels = ((IEnumerable)open.GetValue(null)).Cast<Component>();
            if (!panels.Any(p => p != null && ((Behaviour)p).isActiveAndEnabled))
                break;
            handle.Invoke(null, null);
            yield return 0.6f;
        }
        yield return 0.4f;
        PickStarterIfShown();
    }

    static bool Click(string label)
    {
        foreach (Button b in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            Text t = b.GetComponentInChildren<Text>();
            if (t != null && t.text.Trim() == label && b.IsInteractable())
            {
                b.onClick.Invoke();
                return true;
            }
        }
        Debug.Log($"[UiCapture] 버튼 없음: {label}");
        return false;
    }

    static Type TypeOf(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

    static object Find(string type)
    {
        Type t = TypeOf(type);
        return t == null ? null : UnityEngine.Object.FindAnyObjectByType(t, FindObjectsInactive.Include);
    }

    static object New(string type)
    {
        Type t = TypeOf(type);
        return t == null ? null : Activator.CreateInstance(t);
    }

    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static bool Call(object target, string method, params object[] args)
    {
        if (target == null)
        {
            Debug.Log($"[UiCapture] 건너뜀: {method} (대상 없음)");
            return false;
        }
        return Invoke(target.GetType(), target, method, args);
    }

    static bool CallStatic(string type, string method, params object[] args)
    {
        Type t = TypeOf(type);
        if (t == null)
        {
            Debug.Log($"[UiCapture] 건너뜀: {type} 없음");
            return false;
        }
        return Invoke(t, null, method, args);
    }

    static bool Invoke(Type t, object target, string method, object[] args)
    {
        args ??= new object[] { null };
        MethodInfo m = t.GetMethods(Any).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
        if (m == null)
        {
            Debug.Log($"[UiCapture] 건너뜀: {t.Name}.{method}({args.Length})");
            return false;
        }
        m.Invoke(target, args);
        return true;
    }

    static object Get(object target, string member)
    {
        if (target == null)
            return null;
        Type t = target.GetType();
        PropertyInfo p = t.GetProperty(member, Any);
        if (p != null)
            return p.GetValue(target);
        return t.GetField(member, Any)?.GetValue(target);
    }

    static void Set(object target, string field, object value) => target?.GetType().GetField(field, Any)?.SetValue(target, value);

    /// <summary>
    /// Game 화면을 그대로 1080×1920 PNG 로 저장한다. 프레임 끝에 저장되므로 파일이 생길 때까지 기다린다 (그 사이 다음 단계가 화면을 바꾸지 않게).
    /// RenderTexture 에 카메라를 따로 그리던 방식은 URP 에서 장면이 안 그려져 지도가 남색으로 찍혔음 (UI_REVIEW_v5).
    /// </summary>
    static IEnumerator Shot(string name)
    {
        shotIndex++;
        string path = Path.Combine(outDir, $"{shotIndex:00}_{name}.png");
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[UiCapture] {shotIndex:00}_{name}");
        double until = EditorApplication.timeSinceStartup + 5;
        while (!File.Exists(path) && EditorApplication.timeSinceStartup < until)
            yield return 0.1f;
        yield return 0.2f;
    }

    /// <summary>열린 스크롤 목록을 모두 맨 아래로 내리고 한 장 더 (화면 밖 카드 확인용)</summary>
    static IEnumerator ShotBottom(string name)
    {
        foreach (ScrollRect s in UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            s.verticalNormalizedPosition = 0f;
        yield return 0.6f;
        yield return Shot(name);
    }
}
