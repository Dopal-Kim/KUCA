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
// 밤 불빛 (KeyArtLook: 낮 0, 노을 조금, 밤 1): 창문·가로등·현관·바닥 빛 웅덩이
float _KucaNight, _KucaGlowStrength, _KucaLitWindows, _KucaGroundLight;
float4 _KucaGlowColor;   // 리니어

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

// ---------- 밤: 넓게 퍼지는 따뜻한 빛 (KeyArtLights.png: 등 아래 웅덩이 + 넓은 빛 번짐) ----------

TEXTURE2D(_KucaLightMap); SAMPLER(sampler_KucaLightMap);
float4 _KucaMapSize;   // xy = 지도 크기 (m), 지도 중심이 원점

/// 밤 불빛이 albedo 에 비친 색 (지면 1, 벽·나무는 amount 로 조금)
half3 KucaNightLight(half3 albedo, float3 positionWS, half amount)
{
    if (_KucaNight <= 0.001 || _KucaMapSize.x <= 0) return 0;
    float2 uv = positionWS.xz / _KucaMapSize.xy + 0.5;
    half pool = SAMPLE_TEXTURE2D_LOD(_KucaLightMap, sampler_KucaLightMap, uv, 0).r;
    return _KucaGlowColor.rgb * pool * _KucaNight * _KucaGroundLight * amount * (albedo * 1.3h + 0.06h);
}

// ---------- 계절 팔레트 (KeyArtLook: 봄·여름·가을·겨울) ----------

float4 _KucaSeasonLeaf;      // rgb: 활엽 초록에 곱하는 색
float4 _KucaSeasonBlossom;   // rgb: 분홍 꽃잎이 바뀔 색, a: 바뀌는 정도
float4 _KucaSeasonGrass;     // rgb: 잔디에 곱하는 색
float _KucaAutumn, _KucaSnow;

/// 버텍스 색 물체 (나무·관목·산울타리): 초록 잎·분홍 꽃잎을 계절 색으로, 윗면엔 눈
half3 KucaSeasonFoliage(half3 c, float3 positionWS, float3 normalWS)
{
    if (_KucaSeasonLeaf.x <= 0) return c;   // 값이 없으면 그대로 (봄)
    half lum = dot(c, half3(0.3h, 0.6h, 0.1h));
    half green = saturate((c.g - max(c.r, c.b) * 1.05h) * 8.0h);
    half pink = saturate((c.r - c.g * 1.25h) * 6.0h) * saturate((c.b - c.g * 0.8h) * 6.0h);
    // 가을: 활엽(노란 기 있는 초록)만 나무마다 다른 단풍, 침엽·산울타리는 그대로
    half leafy = smoothstep(0.26h, 0.34h, c.r / max(c.g, 0.01h));
    float h = frac(sin(dot(floor(positionWS.xz / 4.0), float2(12.9898, 78.233))) * 43758.5453);
    half3 maple = lerp(half3(0.80h, 0.22h, 0.05h), half3(0.95h, 0.58h, 0.06h), (half)h);
    maple = lerp(maple, half3(0.55h, 0.10h, 0.04h), step(0.82, h));
    half3 g2 = c * _KucaSeasonLeaf.rgb;
    g2 = lerp(g2, maple * lum * 2.4h, _KucaAutumn * leafy);
    c = lerp(c, g2, green);
    c = lerp(c, _KucaSeasonBlossom.rgb * (0.7h + lum), pink * _KucaSeasonBlossom.a);
    half snow = _KucaSnow * smoothstep(0.35h, 0.75h, normalWS.y);
    return lerp(c, half3(0.90h, 0.94h, 1.0h), snow);
}

/// 지면 텍스처: 잔디 색, 겨울엔 얼룩진 눈과 언 물
half3 KucaSeasonGround(half3 c, float3 positionWS)
{
    if (_KucaSeasonLeaf.x <= 0) return c;
    half green = saturate((c.g - max(c.r, c.b)) * 8.0h);
    c = lerp(c, c * _KucaSeasonGrass.rgb, green);
    half water = saturate((c.b - max(c.r, c.g) * 0.85h) * 5.0h);
    float nse = sin(positionWS.x * 0.05) * sin(positionWS.z * 0.043) * 0.5 + 0.5;
    half snow = _KucaSnow * saturate(green * 1.2h + 0.3h) * smoothstep(0.15, 0.6, nse * 0.6 + _KucaSnow * 0.7);
    c = lerp(c, half3(0.84h, 0.88h, 0.95h), snow * (1.0h - water));
    return lerp(c, half3(0.70h, 0.82h, 0.92h), _KucaSnow * water * 0.6h);   // 얼음
}

#endif
