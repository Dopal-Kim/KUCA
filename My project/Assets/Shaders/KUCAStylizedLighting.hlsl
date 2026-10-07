// 키아트 스타일 공통 조명: 주광(그림자 포함) 램버트 + 하늘 환경광. 부드럽고 밝은 일러스트 느낌.
#ifndef KUCA_STYLIZED_LIGHTING_INCLUDED
#define KUCA_STYLIZED_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half3 KucaShade(half3 albedo, float3 normalWS, float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half ndl = saturate(dot(normalWS, mainLight.direction));
    // 그늘이 너무 어두워지지 않게 반쯤 감싼 램버트
    half wrap = ndl * 0.75h + 0.25h;
    half shadow = lerp(0.55h, 1.0h, mainLight.shadowAttenuation);
    half3 direct = mainLight.color * wrap * shadow;
    half3 ambient = SampleSH(normalWS) * 0.9h;
    return albedo * (direct * 0.85h + ambient);
}

#endif
