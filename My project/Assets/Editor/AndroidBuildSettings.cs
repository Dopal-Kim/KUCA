#if UNITY_ANDROID
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// 안드로이드 Gradle 프로젝트를 만든 뒤 카카오 로그인(Assets/Plugins/Android/KucaKakaoLogin.java)에 필요한 것을 넣는다.
/// iOS 의 IosBuildSettings 와 같은 역할 — Player Settings 의 Custom Gradle Template 을 켜지 않아도 된다.
/// - settings.gradle: 카카오 SDK 저장소 (devrepo.kakao.com, Maven Central 에는 없음)
/// - unityLibrary/build.gradle: 카카오 로그인 모듈 com.kakao.sdk:v2-user
/// - gradle.properties: AndroidX 사용 (카카오 SDK 가 AndroidX 를 쓴다)
/// - AndroidManifest.xml: 카카오계정(웹) 로그인 뒤 앱으로 돌아오는 주소 kakao{네이티브 앱 키}://oauth
/// 카카오 개발자 콘솔 › 플랫폼 › Android 에 패키지명 com.mungim.kuca 와 서명 키의 키 해시가 등록되어 있어야 로그인이 된다.
/// </summary>
public class AndroidBuildSettings : IPostGenerateGradleAndroidProject
{
    const string KakaoRepo = "https://devrepo.kakao.com/nexus/content/groups/public/";
    const string KakaoUser = "com.kakao.sdk:v2-user:2.26.0";

    public int callbackOrder => 100;

    /// <param name="path">unityLibrary 폴더</param>
    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string root = Path.GetDirectoryName(path);

        Patch(Path.Combine(root, "settings.gradle"), KakaoRepo, s =>
            Regex.Replace(s, @"(dependencyResolutionManagement\s*\{[^{]*?repositories\s*\{)", $"$1\n        maven {{ url '{KakaoRepo}' }}"));

        Patch(Path.Combine(path, "build.gradle"), KakaoUser, s =>
            new Regex(@"(dependencies\s*\{)").Replace(s, $"$1\n    implementation '{KakaoUser}'", 1));

        Patch(Path.Combine(root, "gradle.properties"), "android.useAndroidX=true", s =>
            s.TrimEnd() + "\nandroid.useAndroidX=true\n");

        string scheme = "kakao" + ShareConfig.KakaoNativeAppKey;
        Patch(Path.Combine(path, "src", "main", "AndroidManifest.xml"), scheme, s =>
        {
            int end = s.LastIndexOf("</application>");
            if (end < 0) return s;
            string activity =
                "    <activity android:name=\"com.kakao.sdk.auth.AuthCodeHandlerActivity\" android:exported=\"true\">\n" +
                "      <intent-filter>\n" +
                "        <action android:name=\"android.intent.action.VIEW\" />\n" +
                "        <category android:name=\"android.intent.category.DEFAULT\" />\n" +
                "        <category android:name=\"android.intent.category.BROWSABLE\" />\n" +
                $"        <data android:host=\"oauth\" android:scheme=\"{scheme}\" />\n" +
                "      </intent-filter>\n" +
                "    </activity>\n  ";
            return s.Insert(end, activity);
        });
    }

    /// <summary>파일에 marker 가 아직 없을 때만 edit 를 적용한다 (Gradle 프로젝트를 다시 쓰지 않고 이어 빌드해도 두 번 들어가지 않게)</summary>
    static void Patch(string file, string marker, System.Func<string, string> edit)
    {
        if (!File.Exists(file))
        {
            Debug.LogWarning($"[AndroidBuildSettings] {file} 이 없어 카카오 설정을 넣지 못했어요");
            return;
        }
        string text = File.ReadAllText(file);
        if (text.Contains(marker)) return;
        string patched = edit(text);
        if (!patched.Contains(marker))
            Debug.LogWarning($"[AndroidBuildSettings] {Path.GetFileName(file)} 에 {marker} 를 넣을 자리를 찾지 못했어요");
        File.WriteAllText(file, patched);
    }
}
#endif
