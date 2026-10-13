namespace KucaMemoServer.Models;

// 계정 (docs/api.md 의 "로그인 · 계정"). JSON 은 camelCase.

/// <summary>앱에 돌려주는 내 계정 정보</summary>
public record Account(
    string Id,
    string Provider,          // "kakao" · "apple" · "dev"
    string? Nickname,
    string? KhuEmail,         // 인증을 마친 경희대 메일 (없으면 null)
    bool EmailVerified,
    string? PreferencesJson,  // 산책 취향 체크리스트 (앱의 WalkPreferences JSON 그대로)
    DateTime CreatedAt)
{
    /// <summary>앱을 쓰려면 경희대 메일 인증과 닉네임이 있어야 한다</summary>
    public bool Ready => EmailVerified && !string.IsNullOrWhiteSpace(Nickname);
}

public record LoginResult(string Token, Account User);

public record KakaoLoginRequest(string? AccessToken);
public record AppleLoginRequest(string? IdentityToken);
public record DevLoginRequest(string? Name);
public record NicknameRequest(string? Nickname);
public record EmailStartRequest(string? Email);
public record EmailVerifyRequest(string? Code);
