package com.mungim.kuca;

import android.app.Activity;
import android.util.Log;

import com.kakao.sdk.auth.model.OAuthToken;
import com.kakao.sdk.common.KakaoSdk;
import com.kakao.sdk.common.model.ClientError;
import com.kakao.sdk.common.model.ClientErrorCause;
import com.kakao.sdk.user.UserApiClient;
import com.unity3d.player.UnityPlayer;

import kotlin.Unit;
import kotlin.jvm.functions.Function2;

// 카카오 로그인 (안드로이드, Unity 의 NativeLogin.cs 가 AndroidJavaClass 로 부른다).
// - 카카오톡이 있으면 카카오톡으로, 없거나 카카오톡에 연결된 계정이 없으면 카카오계정(웹)으로 로그인하고 액세스 토큰을 돌려준다.
// - 결과는 UnitySendMessage(receiver, method, "ok:<토큰>" · "cancelled" · "error:<문구>") 로 Unity 메인 스레드에 보낸다.
// 돌려준 토큰은 KUCA 서버(POST /api/auth/kakao)가 진짜인지 확인한다. (iOS 는 Assets/Plugins/iOS/KucaAuth.swift)
// 카카오 SDK 의존성 · 리다이렉트 액티비티는 Assets/Editor/AndroidBuildSettings.cs 가 Gradle 프로젝트에 넣는다.
public final class KucaKakaoLogin {
    private static final String TAG = "KucaAuth";
    private static boolean initialized;

    private KucaKakaoLogin() {}

    public static void login(final Activity activity, final String appKey, final String receiver, final String method) {
        activity.runOnUiThread(() -> {
            try {
                if (!initialized) {
                    KakaoSdk.init(activity.getApplicationContext(), appKey);
                    initialized = true;
                }
                final UserApiClient api = UserApiClient.getInstance();
                final Function2<OAuthToken, Throwable, Unit> accountDone = (token, error) -> {
                    reply(receiver, method, token, error);
                    return Unit.INSTANCE;
                };
                if (api.isKakaoTalkLoginAvailable(activity)) {
                    api.loginWithKakaoTalk(activity, (token, error) -> {
                        if (token != null || isCancelled(error)) {
                            reply(receiver, method, token, error);
                        } else {
                            // 카카오톡에 연결된 카카오계정이 없는 경우 등: 카카오계정(웹) 로그인으로 다시 시도
                            Log.w(TAG, "kakaotalk login failed, trying account login", error);
                            api.loginWithKakaoAccount(activity, accountDone);
                        }
                        return Unit.INSTANCE;
                    });
                } else {
                    api.loginWithKakaoAccount(activity, accountDone);
                }
            } catch (Throwable t) {
                reply(receiver, method, null, t);
            }
        });
    }

    private static boolean isCancelled(Throwable error) {
        return error instanceof ClientError && ((ClientError) error).getReason() == ClientErrorCause.Cancelled;
    }

    private static void reply(String receiver, String method, OAuthToken token, Throwable error) {
        String message;
        if (token != null) {
            message = "ok:" + token.getAccessToken();
        } else if (isCancelled(error)) {
            message = "cancelled";
        } else {
            Log.e(TAG, "kakao login error", error);
            String detail = error == null ? null : error.getLocalizedMessage();
            message = "error:" + (detail == null || detail.isEmpty() ? "알 수 없는 오류" : detail);
        }
        UnityPlayer.UnitySendMessage(receiver, method, message);
    }
}
