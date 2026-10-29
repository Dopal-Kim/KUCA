using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

/// <summary>
/// 카카오 / Apple 로그인 (iOS 네이티브 Assets/Plugins/iOS/KucaAuth.swift, 안드로이드 카카오는 Assets/Plugins/Android/KucaKakaoLogin.java).
/// done(토큰, 에러): 성공하면 토큰, 실패하면 에러 문구, 사용자가 취소하면 에러 "cancelled".
/// </summary>
public static class NativeLogin
{
    public const string Cancelled = "cancelled";

#if UNITY_IOS && !UNITY_EDITOR
    delegate void Callback(IntPtr token, IntPtr error);
    [DllImport("__Internal")] static extern void kuca_kakao_login(string appKey, Callback done);
    [DllImport("__Internal")] static extern void kuca_apple_login(Callback done);

    static Action<string, string> pending;

    [MonoPInvokeCallback(typeof(Callback))]
    static void OnDone(IntPtr token, IntPtr error)
    {
        Action<string, string> cb = pending;
        pending = null;
        cb?.Invoke(token == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(token), error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(error));
    }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    static Action<string, string> pending;

    /// <summary>자바 쪽 결과를 UnitySendMessage 로 받는 숨은 오브젝트 (메인 스레드에서 불린다)</summary>
    sealed class Receiver : MonoBehaviour
    {
        public const string Name = "KucaNativeLogin";

        void OnKakaoResult(string message)
        {
            Action<string, string> cb = pending;
            pending = null;
            if (message.StartsWith("ok:")) cb?.Invoke(message.Substring(3), null);
            else if (message == Cancelled) cb?.Invoke(null, Cancelled);
            else cb?.Invoke(null, "카카오 로그인에 실패했어요: " + (message.StartsWith("error:") ? message.Substring(6) : message));
        }
    }

    static void EnsureReceiver()
    {
        if (GameObject.Find(Receiver.Name) != null) return;
        var go = new GameObject(Receiver.Name);
        go.AddComponent<Receiver>();
        UnityEngine.Object.DontDestroyOnLoad(go);
    }
#endif

    public static bool Supported =>
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        true;
#else
        false;
#endif

    public static void Kakao(Action<string, string> done)
    {
#if UNITY_IOS && !UNITY_EDITOR
        pending = done;
        kuca_kakao_login(ShareConfig.KakaoNativeAppKey, OnDone);
#elif UNITY_ANDROID && !UNITY_EDITOR
        EnsureReceiver();
        pending = done;
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var login = new AndroidJavaClass("com.mungim.kuca.KucaKakaoLogin");
            login.CallStatic("login", activity, ShareConfig.KakaoNativeAppKey, Receiver.Name, "OnKakaoResult");
        }
        catch (Exception e)
        {
            pending = null;
            Debug.LogWarning("[NativeLogin] kakao: " + e);
            done(null, "카카오 로그인을 열 수 없어요");
        }
#else
        done(null, "카카오 로그인은 휴대폰 앱에서 할 수 있어요");
#endif
    }

    public static void Apple(Action<string, string> done)
    {
#if UNITY_IOS && !UNITY_EDITOR
        pending = done;
        kuca_apple_login(OnDone);
#else
        done(null, "Apple 로그인은 아이폰에서 할 수 있어요");
#endif
    }
}
