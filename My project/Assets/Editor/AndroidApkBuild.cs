using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 테스터 배포용 안드로이드 APK 를 한 번에 만든다 (메뉴 KUCA › 안드로이드 APK 빌드).
/// - 버전 코드를 1 올리고 버전 이름을 0.1.&lt;코드&gt; 로 맞춘다 → Firebase App Distribution 과 테스터 폰에서 새 버전으로 보인다
/// - 팀 서명 키(kuca.keystore)로 서명한다 → 테스터 폰에서 지우지 않고 업데이트된다
/// - 서명 키 경로·비밀번호는 이 PC 에만 두고 빌드가 끝나면 Player Settings 를 원래대로 돌린다 → ProjectSettings 에 개인 경로가 남지 않는다
/// - 결과: 저장소 폴더의 Builds/Android/KUCA-0.1.&lt;코드&gt;.apk (git 에 올라가지 않음)
/// 올리는 방법: docs/안드로이드_자동배포.md
/// </summary>
public class AndroidApkBuild : EditorWindow
{
    const string KeystorePathPref = "KUCA.Android.KeystorePath";
    const string AliasPref = "KUCA.Android.KeyAlias";
    const string ConsoleUrl = "https://console.firebase.google.com/project/kuca-f7095/appdistribution";

    string keystorePath;
    string alias;
    // 비밀번호는 저장하지 않는다 (Unity 를 다시 켜면 다시 입력)
    string keystorePass = "";
    string aliasPass = "";

    [MenuItem("KUCA/안드로이드 APK 빌드 (Firebase 배포용)")]
    static void Open()
    {
        var window = GetWindow<AndroidApkBuild>(true, "안드로이드 APK 빌드", true);
        window.minSize = new Vector2(480f, 320f);
    }

    void OnEnable()
    {
        keystorePath = EditorPrefs.GetString(KeystorePathPref, "");
        alias = EditorPrefs.GetString(AliasPref, "kuca");
    }

    void OnGUI()
    {
        int next = PlayerSettings.Android.bundleVersionCode + 1;
        EditorGUILayout.HelpBox("APK 를 만든 뒤 Firebase App Distribution 에 올리면 kuca-team 테스터 폰에 새 버전 알림이 갑니다.", MessageType.Info);
        EditorGUILayout.LabelField("이번 버전", $"0.1.{next}  (버전 코드 {next})");
        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("서명 키 파일", string.IsNullOrEmpty(keystorePath) ? "(선택 안 됨)" : keystorePath);
            if (GUILayout.Button("찾기", GUILayout.Width(60f)))
            {
                string start = string.IsNullOrEmpty(keystorePath) ? "" : Path.GetDirectoryName(keystorePath);
                string picked = EditorUtility.OpenFilePanel("kuca.keystore 선택", start, "keystore");
                if (!string.IsNullOrEmpty(picked))
                {
                    keystorePath = picked;
                    EditorPrefs.SetString(KeystorePathPref, picked);
                }
            }
        }
        string newAlias = EditorGUILayout.TextField("Alias", alias);
        if (newAlias != alias)
        {
            alias = newAlias;
            EditorPrefs.SetString(AliasPref, newAlias);
        }
        keystorePass = EditorGUILayout.PasswordField("키스토어 비밀번호", keystorePass);
        aliasPass = EditorGUILayout.PasswordField("Alias 비밀번호", aliasPass);
        EditorGUILayout.Space();

        bool ready = File.Exists(keystorePath) && !string.IsNullOrEmpty(alias) && keystorePass.Length > 0 && aliasPass.Length > 0;
        using (new EditorGUI.DisabledScope(!ready))
        {
            if (GUILayout.Button($"빌드 → KUCA-0.1.{next}.apk", GUILayout.Height(36f)))
                EditorApplication.delayCall += () => Build(next);
        }
        if (!ready)
            EditorGUILayout.HelpBox("서명 키 파일과 비밀번호 두 개를 넣어야 빌드할 수 있습니다.", MessageType.Warning);
        if (GUILayout.Button("Firebase App Distribution 열기"))
            Application.OpenURL(ConsoleUrl);
    }

    void Build(int code)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            if (EditorUtility.DisplayDialog("안드로이드로 전환", "먼저 빌드 대상을 안드로이드로 바꿉니다 (처음에는 오래 걸립니다). 전환이 끝나면 빌드 버튼을 다시 눌러 주세요.", "전환", "취소"))
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            return;
        }

        bool oldUseKeystore = PlayerSettings.Android.useCustomKeystore;
        string oldKeystoreName = PlayerSettings.Android.keystoreName;
        string oldAliasName = PlayerSettings.Android.keyaliasName;
        int oldCode = PlayerSettings.Android.bundleVersionCode;
        string oldVersion = PlayerSettings.bundleVersion;
        bool oldAppBundle = EditorUserBuildSettings.buildAppBundle;

        string version = $"0.1.{code}";
        string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", "Android"));
        Directory.CreateDirectory(dir);
        string apk = Path.Combine(dir, $"KUCA-{version}.apk");

        BuildReport report;
        try
        {
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystorePath;
            PlayerSettings.Android.keystorePass = keystorePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            PlayerSettings.Android.bundleVersionCode = code;
            PlayerSettings.bundleVersion = version;
            EditorUserBuildSettings.buildAppBundle = false;

            report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = oldUseKeystore;
            PlayerSettings.Android.keystoreName = oldKeystoreName;
            PlayerSettings.Android.keyaliasName = oldAliasName;
            EditorUserBuildSettings.buildAppBundle = oldAppBundle;
        }

        if (report.summary.result != BuildResult.Succeeded)
        {
            // 실패한 빌드는 버전 번호를 쓰지 않는다
            PlayerSettings.Android.bundleVersionCode = oldCode;
            PlayerSettings.bundleVersion = oldVersion;
            EditorUtility.DisplayDialog("빌드 실패", $"결과: {report.summary.result} · 오류 {report.summary.totalErrors}개\nConsole 창의 빨간 줄을 확인하세요.", "확인");
            return;
        }

        AssetDatabase.SaveAssets();
        double mb = new FileInfo(apk).Length / (1024.0 * 1024.0);
        EditorUtility.RevealInFinder(apk);
        if (EditorUtility.DisplayDialog("빌드 완료",
                $"KUCA-{version}.apk ({mb:F0} MB)\n\nFirebase App Distribution 에 이 파일을 끌어다 놓고 kuca-team 그룹에 배포하세요.\n버전이 올라간 ProjectSettings 는 커밋해 두세요.",
                "Firebase 열기", "닫기"))
            Application.OpenURL(ConsoleUrl);
    }
}
