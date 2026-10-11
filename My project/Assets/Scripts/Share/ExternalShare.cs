using System;
using System.IO;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
using AOT;
#endif

/// <summary>
/// 인스타그램 스토리 · 카카오톡 공유 (iOS 네이티브 플러그인 Assets/Plugins/iOS/KucaShare.swift).
/// 에디터·다른 플랫폼에서는 실제로 보내지 않고, 만든 스티커를 파일로 남겨 확인할 수 있게 한다.
/// </summary>
public static class ExternalShare
{
#if UNITY_IOS && !UNITY_EDITOR
    delegate void NativeCallback(IntPtr error);

    [DllImport("__Internal")] static extern bool kuca_instagram_available();
    [DllImport("__Internal")] static extern bool kuca_share_instagram_story(string stickerPath, string appId, string topColor, string bottomColor);
    [DllImport("__Internal")] static extern void kuca_share_kakao(string appKey, string title, string desc, string imageUrl, string webUrl, string buttonTitle, NativeCallback done);

    static Action<string> kakaoDone;

    [MonoPInvokeCallback(typeof(NativeCallback))]
    static void OnKakaoDone(IntPtr error)
    {
        Action<string> cb = kakaoDone;
        kakaoDone = null;
        cb?.Invoke(error == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(error));
    }
#endif

    /// <summary>스티커 PNG 를 인스타그램 스토리로 보낸다. 에러 메시지를 돌려주고, 성공하면 null.</summary>
    public static string InstagramStory(byte[] stickerPng, Color top, Color bottom)
    {
        string path = Path.Combine(Application.temporaryCachePath, "kuca_story_sticker.png");
        File.WriteAllBytes(path, stickerPng);
#if UNITY_IOS && !UNITY_EDITOR
        if (!kuca_instagram_available())
            return "인스타그램 앱이 설치되어 있지 않아요";
        return kuca_share_instagram_story(path, ShareConfig.MetaAppId, "#" + ColorUtility.ToHtmlStringRGB(top), "#" + ColorUtility.ToHtmlStringRGB(bottom))
            ? null : "인스타그램을 열 수 없어요";
#else
        Debug.Log($"[ExternalShare] 인스타그램 스토리 스티커를 만들었어요 (에디터에서는 보내지 않음): {path}");
        return null;
#endif
    }

    /// <summary>카카오톡 피드 카드로 보낸다. done(에러 메시지 또는 null)</summary>
    public static void Kakao(string title, string description, string imageUrl, string webUrl, Action<string> done)
    {
#if UNITY_IOS && !UNITY_EDITOR
        kakaoDone = done;
        kuca_share_kakao(ShareConfig.KakaoNativeAppKey, title, description, imageUrl ?? "", webUrl, ShareConfig.KakaoButtonTitle, OnKakaoDone);
#else
        Debug.Log($"[ExternalShare] 카카오톡 카드 (에디터에서는 보내지 않음): {title} / {description} / {imageUrl} / {webUrl}");
        done?.Invoke(null);
#endif
    }
}
