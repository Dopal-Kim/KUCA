using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 3D 미리보기 카메라(경희펫 썸네일 · 진화 · 프로필 · 꾸미기 · HUD 얼굴)에 붙인다.
/// 지도의 파란 하늘 주변광 대신 이 카메라가 그리는 동안만 중립 흰 주변광 · 카메라 기준 앞 위의 흰 햇빛 · 안개 없음으로 그려서,
/// 미리보기가 탁하거나 보랏빛 · 노을색으로 나오지 않게 한다 (저녁 캡처에서 꾸미기 피부색까지 분홍이었음, REVIEW_v16).
/// </summary>
[RequireComponent(typeof(Camera))]
public class NeutralAmbient : MonoBehaviour
{
    static readonly Color Ambient = new Color(0.66f, 0.65f, 0.63f);

    Camera cam;
    SphericalHarmonicsL2 saved, neutral;
    Light sun;
    Color sunColor;
    float sunIntensity;
    Quaternion sunRotation;
    bool fog;
    static KeyArtLook look;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        neutral = new SphericalHarmonicsL2();
        neutral.AddAmbientLight(Ambient);
        RenderPipelineManager.beginCameraRendering += Begin;
        RenderPipelineManager.endCameraRendering += End;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Begin;
        RenderPipelineManager.endCameraRendering -= End;
    }

    void Begin(ScriptableRenderContext context, Camera c)
    {
        if (c != cam) return;
        saved = RenderSettings.ambientProbe;
        RenderSettings.ambientProbe = neutral;
        fog = RenderSettings.fog;
        RenderSettings.fog = false;
        if (look == null) look = FindAnyObjectByType<KeyArtLook>();
        if (look != null) look.ApplyShaderGlobals(look.day);   // 캐릭터 · 경희펫 셰이더가 읽는 하늘 · 땅 빛 · 밤 정도도 낮으로
        sun = RenderSettings.sun;
        if (sun == null) return;
        sunColor = sun.color;
        sunIntensity = sun.intensity;
        sunRotation = sun.transform.rotation;
        sun.color = Color.white;
        sun.intensity = 1f;   // 밤 · 저녁에 해가 약해도 미리보기는 낮과 같은 밝기
        // 방향도: 밤엔 해가 지평선 쪽이라 얼굴에 빛이 거의 안 닿아 20시 캡처가 19시보다 25% 어두웠음 (REVIEW_v21) → 카메라 기준 앞 위에서
        sun.transform.rotation = cam.transform.rotation * Quaternion.Euler(35f, -25f, 0f);
    }

    void End(ScriptableRenderContext context, Camera c)
    {
        if (c != cam) return;
        RenderSettings.ambientProbe = saved;
        RenderSettings.fog = fog;
        KeyArtLook.Preset now = look != null ? look.Current() : null;
        if (now != null) look.ApplyShaderGlobals(now);
        if (sun == null) return;
        sun.color = sunColor;
        sun.intensity = sunIntensity;
        sun.transform.rotation = sunRotation;
    }
}
