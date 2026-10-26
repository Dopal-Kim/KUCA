/// <summary>로그인 설정</summary>
public static class AuthConfig
{
    /// <summary>
    /// Sign in with Apple. 유료 Apple Developer 계정이 있어야 동작한다 (무료 계정은 이 기능을 켜면 서명이 실패).
    /// true 면 버튼과 Xcode 기능(entitlement)이 함께 켜진다. 10/25 유료 팀(HAYUF8Y8PV)으로 켬:
    /// 카카오 같은 외부 로그인이 있으면 Apple 로그인도 있어야 App Store 심사를 통과한다 (지침 4.8).
    /// </summary>
    public const bool AppleLoginEnabled = true;

    /// <summary>로그인할 수 있는 학교 메일 도메인</summary>
    public const string SchoolDomain = "khu.ac.kr";
}
