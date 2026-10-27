using System.IO;
#if UNITY_ANDROID
using System.Xml;
using UnityEditor.Android;
#endif
#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

/// <summary>
/// 걸음 센서 권한 (Scripts/Route/StepTracker.cs).
/// - 안드로이드: AndroidManifest 에 ACTIVITY_RECOGNITION ("신체 활동") 권한을 넣는다. 묻는 것은 산책을 시작할 때 앱이 한다.
/// - 아이폰: Info.plist 에 NSMotionUsageDescription ("동작 및 피트니스") 안내 문구를 넣는다. 없으면 iOS 가 앱을 닫는다.
/// IosBuildSettings(배포 설정)는 건드리지 않으려고 따로 둔다. 빌드 대상 모듈이 없는 PC 에서도 컴파일되게 #if 로 나눈다.
/// </summary>
public static class StepPermissions
{
    public const string MotionUsage = "산책하는 동안 걸은 걸음 수를 세기 위해 동작 및 피트니스 정보를 사용합니다. 이번 산책 걸음 수만 쓰고 저장하지 않습니다.";

#if UNITY_IOS
    [PostProcessBuild(110)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("NSMotionUsageDescription", MotionUsage);
        plist.WriteToFile(plistPath);
    }
#endif
}

#if UNITY_ANDROID
public class StepPermissionsAndroid : IPostGenerateGradleAndroidProject
{
    const string AndroidPermission = "android.permission.ACTIVITY_RECOGNITION";
    const string AndroidNs = "http://schemas.android.com/apk/res/android";
    public int callbackOrder => 100;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifest)) return;
        var doc = new XmlDocument();
        doc.Load(manifest);
        XmlElement root = doc.DocumentElement;
        foreach (XmlNode node in root.SelectNodes("uses-permission"))
            if (node is XmlElement e && e.GetAttribute("name", AndroidNs) == AndroidPermission)
                return;   // 이미 있음
        XmlElement permission = doc.CreateElement("uses-permission");
        permission.SetAttribute("name", AndroidNs, AndroidPermission);
        root.PrependChild(permission);
        doc.Save(manifest);
    }
}
#endif
