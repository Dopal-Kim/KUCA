/// <summary>
/// 외부 공유에 쓰는 앱 키. 둘 다 앱 안에 들어가는 공개용 값이다 (비밀 키 아님).
/// </summary>
public static class ShareConfig
{
    /// <summary>developers.kakao.com › 내 애플리케이션 › 앱 키 › 네이티브 앱 키</summary>
    public const string KakaoNativeAppKey = "2e4ab03e5e172ad01a7fbadecb7c5a6c";

    /// <summary>Meta(페이스북) 개발자 앱 ID. 인스타그램 스토리 공유의 source_application. 비어 있으면 빼고 보낸다.</summary>
    public const string MetaAppId = "";

    /// <summary>카카오톡 카드의 버튼 글자</summary>
    public const string KakaoButtonTitle = "KUCA에서 보기";
}
