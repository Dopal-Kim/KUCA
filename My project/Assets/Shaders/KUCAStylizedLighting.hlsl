// 키아트 '햇살' 조명 (웹 미리보기 tools/keyart/preview/main.js SUNNY_GLSL 과 같은 계산)
//   감싸는 빛(wrap)으로 부드러운 명암 → 그늘은 하늘색으로 푸르게(shadowTint) → 햇빛 받은 윗면은 따뜻하게(warmTop)
//   → 가장자리 빛(rim) → 채도(saturation) → 화면 왼쪽 위에서 번지는 햇살(KucaSunWash)
// 값은 KeyArtLook 컴포넌트가 전역으로 넣는다 (Assets/Art/KeyArt/KeyArtLook.json 의 ambient, shadowStrength, sunny).
#ifndef KUCA_STYLIZED_LIGHTING_INCLUDED
#define KUCA_STYLIZED_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float4 _KucaSkyAmbient;     // 리니어 배율 (위를 보는 면)
float4 _KucaGroundAmbient;  // 리니어 배율 (아래를 보는 면)
float4 _KucaShadowTint;     // 그늘에 곱하는 색 (푸르게)
float4 _KucaWashColor;      // 햇살 번짐 색 (리니어)
float _KucaShadowStrength, _KucaWrap, _KucaWarmTop, _KucaRim, _KucaSaturation, _KucaWash;

half3 KucaShade(half3 albedo, float3 normalWS, float3 positionWS)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    // KeyArtLook 이 아직 값을 넣지 않았으면 무난한 기본값
    bool hasLook = any(_KucaSkyAmbient.rgb);
    half3 sky = hasLook ? (half3)_KucaSkyAmbient.rgb : SampleSH(half3(0, 1, 0));
    half3 ground = hasLook ? (half3)_KucaGroundAmbient.rgb : SampleSH(half3(0, -1, 0));
    half3 tint = hasLook ? (half3)_KucaShadowTint.rgb : half3(1, 1, 1);
    half strength = hasLook ? (half)_KucaShadowStrength : 0.7h;
    half wrap = hasLook ? (half)_KucaWrap : 0.0h;
    half sat = hasLook ? (half)_KucaSaturation : 1.0h;

    half lit = saturate((dot(normalWS, mainLight.direction) + wrap) / (1.0h + wrap));
    lit = lit * lit * (3.0h - 2.0h * lit);
    half sun = lit * lerp(1.0h - strength, 1.0h, mainLight.shadowAttenuation);
    half ny = normalWS.y;
    half3 ambient = lerp(ground, sky, ny * 0.5h + 0.5h) * lerp(tint, half3(1, 1, 1), sun);
    half3 col = albedo * (ambient + mainLight.color * sun);
    col += albedo * mainLight.color * _KucaWarmTop * max(ny, 0.0h) * max(ny, 0.0h) * sun;
    half3 V = GetWorldSpaceNormalizeViewDir(positionWS);
    half rim = pow(1.0h - saturate(dot(normalWS, V)), 3.0h);
    col += half3(1.0h, 0.96h, 0.88h) * _KucaRim * rim * (0.35h + 0.65h * sun) * 0.5h;
    half l = dot(col, half3(0.299h, 0.587h, 0.114h));
    return max(lerp(l.xxx, col, sat), 0.0h);
}

/// 화면 왼쪽 위(해 쪽)에서 번지는 따뜻한 햇살. positionCS 는 프래그먼트의 SV_POSITION.
half3 KucaSunWash(half3 col, float4 positionCS)
{
    float2 uv = GetNormalizedScreenSpaceUV(positionCS);
#if UNITY_UV_STARTS_AT_TOP
    uv.y = 1.0 - uv.y;
#endif
    float d = saturate(1.0 - length((uv - float2(0.0, 1.0)) * float2(0.75, 1.0)));
    return col + (half3)_KucaWashColor.rgb * _KucaWash * d * d;
}

/// 물 (파란 지면) 위 반짝임: 두 물결을 곱해 날카로운 점만 남긴다.
half3 KucaWater(half3 albedo, float3 positionWS)
{
    half w = saturate((albedo.b - max(albedo.r, albedo.g) * 0.85h) * 5.0h);
    float t = _Time.y;
    float s = sin(dot(positionWS.xz, float2(0.9, 1.3)) * 1.7 + t * 1.5) * sin(dot(positionWS.xz, float2(-1.1, 0.7)) * 2.1 - t);
    return albedo + half3(0.9h, 0.97h, 1.0h) * pow(saturate(s), 8.0) * 0.45h * w;
}

#endif
