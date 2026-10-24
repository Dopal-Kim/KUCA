using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 3D 미리보기 카메라(경희몬 썸네일 · 진화 · 프로필 · 꾸미기 · HUD 얼굴)에 붙인다.
/// 지도의 파란 하늘 주변광 대신 이 카메라가 그리는 동안만 중립 흰 주변광을 써서, 미리보기가 탁하거나 보랏빛으로 나오지 않게 한다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class NeutralAmbient : MonoBehaviour
{
    static readonly Color Ambient = new Color(0.66f, 0.65f, 0.63f);

    Camera cam;
    SphericalHarmonicsL2 saved, neutral;

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
    }

    void End(ScriptableRenderContext context, Camera c)
    {
        if (c == cam) RenderSettings.ambientProbe = saved;
    }
}
