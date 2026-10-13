// 앱이 URL 로 열릴 때(카카오톡 로그인에서 돌아올 때) 카카오 SDK 가 먼저 받게 한다.
// Unity 의 기본 앱 컨트롤러를 이어받아 openURL 만 바꾼다.
#import "UnityAppController.h"

extern "C" bool kuca_handle_open_url(NSURL* url);

@interface KucaAppController : UnityAppController
@end

@implementation KucaAppController

- (BOOL)application:(UIApplication*)app openURL:(NSURL*)url options:(NSDictionary<UIApplicationOpenURLOptionsKey, id>*)options
{
    if (kuca_handle_open_url(url))
        return YES;
    return [super application:app openURL:url options:options];
}

@end

IMPL_APP_CONTROLLER_SUBCLASS(KucaAppController)
