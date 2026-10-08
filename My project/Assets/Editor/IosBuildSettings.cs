#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// iOS 빌드 뒤 Info.plist 에 Unity 설정 화면에 없는 항목을 넣는다.
/// - NSLocalNetworkUsageDescription: 같은 와이파이의 PC 메모 서버(http://192.168.x.x:5080)에 붙으려면 iOS 14+ 의 로컬 네트워크 권한 안내 문구가 필요하다.
/// (http 주소 허용은 Player Settings 의 Allow downloads over HTTP = Always allowed 로 한다.)
/// </summary>
public static class IosBuildSettings
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS)
            return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("NSLocalNetworkUsageDescription", "같은 와이파이에 있는 KUCA 메모 서버에 연결해 장소 메모와 사진을 주고받기 위해 사용합니다.");
        plist.WriteToFile(plistPath);
    }
}
#endif
