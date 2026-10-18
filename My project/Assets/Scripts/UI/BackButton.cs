using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 안드로이드 뒤로 가기 버튼 (에디터·PC 에서는 Esc).
/// 화면(패널)을 만들 때 BackButton.Attach(패널, 닫기) 를 불러 두면, 그 패널이 켜져 있는 동안
/// 뒤로 가기를 누를 때 가장 나중에 켜진 패널부터 닫는다.
/// 열린 패널이 없으면 2초 안에 한 번 더 눌러야 앱이 꺼진다 (실수로 꺼지지 않게).
/// </summary>
public class BackButton : MonoBehaviour
{
    const float QuitWindow = 2f;

    static readonly List<BackButton> open = new List<BackButton>();
    static float quitArmedUntil = -1f;

    Action onBack;

    /// <summary>panel 이 켜져 있을 때 뒤로 가기를 누르면 close 를 부른다.</summary>
    public static void Attach(GameObject panel, Action close)
    {
        if (panel == null || close == null)
            return;
        BackButton b = panel.GetComponent<BackButton>();
        if (b == null)
            b = panel.AddComponent<BackButton>();
        b.onBack = close;
    }

    void OnEnable()
    {
        open.Remove(this);
        open.Add(this);
    }

    void OnDisable() => open.Remove(this);

    static bool Pressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    static void HandleBack()
    {
        open.RemoveAll(b => b == null || !b.isActiveAndEnabled);
        if (open.Count > 0)
        {
            open[open.Count - 1].onBack?.Invoke();
            return;
        }
        if (Time.unscaledTime < quitArmedUntil)
        {
            Application.Quit();
            return;
        }
        quitArmedUntil = Time.unscaledTime + QuitWindow;
        GameHUD hud = FindAnyObjectByType<GameHUD>();
        if (hud != null)
            hud.Toast("한 번 더 누르면 종료돼요");
    }

    /// <summary>뒤로 가기 입력만 보는 숨은 오브젝트 (씬에 따로 둘 필요 없음)</summary>
    class Listener : MonoBehaviour
    {
        void Update()
        {
            if (Pressed())
                HandleBack();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateListener()
    {
        open.Clear();
        quitArmedUntil = -1f;
        var go = new GameObject("BackButtonListener");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<Listener>();
    }
}
