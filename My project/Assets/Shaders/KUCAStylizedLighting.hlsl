// 키아트 스타일 공통 조명 (웹 미리보기 tools/keyart/preview/main.js 와 같은 모델)
//   색 = 알베도 × (하늘/땅 반구 환경광 + 해 × max(N·L, 0) × 그림자)
//   그림자는 _KucaShadowStrength 만큼만 해를 가린다 (그늘도 밝은 일러스트 느낌).
// 환경광·그림자 세기는 KeyArtLook 컴포넌트가 전역 값으로 넣는다 (Assets/Art/KeyArt/KeyArtLook.json).
#ifndef KUCA_STYLIZED_LIGHTING_INCLUDED
#define KUCA_STYLIZED_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float4 _KucaSkyAmbient;     // 리니어 배율 (위를 보는 면)
float4 _KucaGroundAmbient;  // 리니어 배율 (아래를 보는 면)
float _KucaShadowStrength;

half3 KucaShade(half3 albedo, float3 normalWS, float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    half ndl = saturate(dot(normalWS, mainLight.direction));

    // KeyArtLook 이 아직 값을 넣지 않았으면 Unity 환경광으로 대신한다
    half3 sky = any(_KucaSkyAmbient.rgb) ? (half3)_KucaSkyAmbient.rgb : SampleSH(half3(0, 1, 0));
    half3 ground = any(_KucaGroundAmbient.rgb) ? (half3)_KucaGroundAmbient.rgb : SampleSH(half3(0, -1, 0));
    half strength = _KucaShadowStrength > 0 ? (half)_KucaShadowStrength : 0.8h;

    half3 ambient = lerp(ground, sky, normalWS.y * 0.5h + 0.5h);
    half shadow = lerp(1.0h - strength, 1.0h, mainLight.shadowAttenuation);
    half3 direct = mainLight.color * ndl * shadow;
    return albedo * (ambient + direct);
}

#endif
