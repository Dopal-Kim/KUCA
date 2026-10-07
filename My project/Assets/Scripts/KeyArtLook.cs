using UnityEngine;

/// <summary>
/// 키아트 셰이더(KUCA/*)가 쓰는 전역 조명 값. 에디터·실행 모두에서 켜질 때 넣는다.
/// 값은 KUCA → Map Style → Apply Key Art 가 Assets/Art/KeyArt/KeyArtLook.json 에서 채운다.
/// </summary>
[ExecuteAlways]
public class KeyArtLook : MonoBehaviour
{
    [Tooltip("위를 보는 면의 환경광 (리니어 배율)")]
    public Vector3 skyAmbient = new Vector3(0.58f, 0.62f, 0.70f);
    [Tooltip("아래를 보는 면의 환경광 (리니어 배율)")]
    public Vector3 groundAmbient = new Vector3(0.64f, 0.62f, 0.54f);
    [Range(0f, 1f), Tooltip("그림자가 햇빛을 가리는 정도")]
    public float shadowStrength = 0.82f;

    static readonly int SkyId = Shader.PropertyToID("_KucaSkyAmbient");
    static readonly int GroundId = Shader.PropertyToID("_KucaGroundAmbient");
    static readonly int ShadowId = Shader.PropertyToID("_KucaShadowStrength");

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    public void Apply()
    {
        Shader.SetGlobalVector(SkyId, skyAmbient);
        Shader.SetGlobalVector(GroundId, groundAmbient);
        Shader.SetGlobalFloat(ShadowId, shadowStrength);
    }
}
