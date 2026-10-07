using UnityEngine;

/// <summary>
/// 키아트 '햇살' 셰이더(KUCA/*, KUCAStylizedLighting.hlsl)가 쓰는 전역 값. 에디터·실행 모두에서 켜질 때 넣는다.
/// 값은 KUCA → Map Style → Apply Key Art 가 Assets/Art/KeyArt/KeyArtLook.json 에서 채운다.
/// </summary>
[ExecuteAlways]
public class KeyArtLook : MonoBehaviour
{
    [Header("환경광 (리니어 배율)")]
    public Vector3 skyAmbient = new Vector3(0.56f, 0.62f, 0.74f);
    public Vector3 groundAmbient = new Vector3(0.66f, 0.62f, 0.50f);
    [Range(0f, 1f), Tooltip("그림자가 햇빛을 가리는 정도")]
    public float shadowStrength = 0.7f;

    [Header("햇살")]
    [Range(0f, 1f), Tooltip("빛이 그늘 쪽으로 감싸 도는 정도 (클수록 부드러운 명암)")]
    public float wrap = 0.35f;
    [Tooltip("그늘에 곱하는 색 (1보다 큰 파랑 = 하늘빛 그늘)")]
    public Vector3 shadowTint = new Vector3(0.80f, 0.86f, 1.06f);
    [Range(0f, 0.6f), Tooltip("햇빛 받은 윗면을 더 따뜻하게")]
    public float warmTop = 0.16f;
    [Range(0f, 1f), Tooltip("가장자리 빛")]
    public float rim = 0.22f;
    [Range(0.5f, 1.6f)]
    public float saturation = 1.12f;
    [Range(0f, 0.5f), Tooltip("화면 왼쪽 위에서 번지는 햇살")]
    public float wash = 0.10f;
    public Color washColor = new Color(1f, 0.94f, 0.78f);

    static readonly int SkyId = Shader.PropertyToID("_KucaSkyAmbient");
    static readonly int GroundId = Shader.PropertyToID("_KucaGroundAmbient");
    static readonly int ShadowId = Shader.PropertyToID("_KucaShadowStrength");
    static readonly int WrapId = Shader.PropertyToID("_KucaWrap");
    static readonly int TintId = Shader.PropertyToID("_KucaShadowTint");
    static readonly int WarmId = Shader.PropertyToID("_KucaWarmTop");
    static readonly int RimId = Shader.PropertyToID("_KucaRim");
    static readonly int SatId = Shader.PropertyToID("_KucaSaturation");
    static readonly int WashId = Shader.PropertyToID("_KucaWash");
    static readonly int WashColorId = Shader.PropertyToID("_KucaWashColor");

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    public void Apply()
    {
        Shader.SetGlobalVector(SkyId, skyAmbient);
        Shader.SetGlobalVector(GroundId, groundAmbient);
        Shader.SetGlobalFloat(ShadowId, shadowStrength);
        Shader.SetGlobalFloat(WrapId, wrap);
        Shader.SetGlobalVector(TintId, shadowTint);
        Shader.SetGlobalFloat(WarmId, warmTop);
        Shader.SetGlobalFloat(RimId, rim);
        Shader.SetGlobalFloat(SatId, saturation);
        Shader.SetGlobalFloat(WashId, wash);
        Shader.SetGlobalColor(WashColorId, washColor.linear);
    }
}
