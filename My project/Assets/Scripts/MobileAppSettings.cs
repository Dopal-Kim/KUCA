using UnityEngine;

/// <summary>
/// 기기 공통 실행 설정 (iOS·안드로이드). 씬에 따로 둘 필요 없이 앱 시작 때 한 번 적용된다.
/// - 모바일 기본값은 30fps 라서 목표 프레임을 지정한다.
/// - 걸으면서 지도를 보는 앱이라 화면이 저절로 꺼지지 않게 한다.
/// </summary>
public static class MobileAppSettings
{
    /// <summary>목표 프레임. 저사양 기기 기준은 docs/안드로이드_라운드.md AR3 에서 정한다.</summary>
    public const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
