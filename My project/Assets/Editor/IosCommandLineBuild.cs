using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 명령줄(배치 모드) iOS 빌드. 에디터를 열지 않고 Xcode 프로젝트를 만든다 (폰 설치 · TestFlight 업로드 전 단계).
///   unity build "My project" --target iOS --execute-method IosCommandLineBuild.Build -o Builds/iOS
/// 장면은 Build Settings 에 켜진 것, 설정(번들 ID · 서명 팀 · 버전)은 Player Settings 그대로.
/// </summary>
public static class IosCommandLineBuild
{
    public static void Build()
    {
        string output = ArgAfter("-buildOutput") ?? "Builds/iOS";
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.iOS,
            options = BuildOptions.None,
        });
        Debug.Log($"[IosCommandLineBuild] {report.summary.result} → {output} ({report.summary.totalTime.TotalSeconds:F0}s)");
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    static string ArgAfter(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
