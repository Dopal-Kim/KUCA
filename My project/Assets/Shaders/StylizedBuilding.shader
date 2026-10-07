// 키아트 건물 외벽: 둥근 창(고전 양식은 아치 창), 창턱, 모서리 기둥(벽 양끝은 창 없음), 창 묶음 사이 벽, 층 띠, 벽돌 줄눈, 지붕 판넬.
// 창 배치는 메시 UV2 = (벽 위 위치 m, 벽 전체 길이 m) 로 벽마다 가운데 정렬 (tools/keyart 의 Shell 메시).
// UV2 가 없으면(길이 0) 월드 좌표로 반복. 지붕색에는 버텍스 색 × 1.1 을 곱한다 (건물마다 파스텔). 웹 미리보기 main.js 의 kucaBuilding 과 같은 무늬.
Shader "KUCA/StylizedBuilding"
{
    Properties
    {
        _WallColor ("Wall", Color) = (0.95, 0.93, 0.88, 1)
        _TrimColor ("Trim (창틀·기둥·띠)", Color) = (1, 0.99, 0.96, 1)
        _RoofColor ("Roof", Color) = (0.76, 0.74, 0.72, 1)
        _WindowColor ("Window", Color) = (0.42, 0.62, 0.80, 1)
        _FloorHeight ("Floor Height (m)", Float) = 3.4
        _WindowSpacing ("Window Spacing (m)", Float) = 3.8
        _WindowWidth ("Window Width (0-1)", Range(0.1, 1.0)) = 0.5
        _WindowHeight ("Window Height (0-1)", Range(0.1, 0.9)) = 0.5
        _PilasterEvery ("Blank Column Every N (0 = 없음)", Float) = 4
        _Brick ("Brick Courses (0/1)", Float) = 0
        _Arch ("Arched Windows (0/1)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "KUCAStylizedLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WallColor, _TrimColor, _RoofColor, _WindowColor;
                float _FloorHeight, _WindowSpacing, _WindowWidth, _WindowHeight, _PilasterEvery, _Brick, _Arch;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 wall : TEXCOORD1; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 wall : TEXCOORD2;
                float fog : TEXCOORD3;
                half3 roofTint : TEXCOORD4;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.wall = v.wall;
                o.roofTint = v.color.rgb;   // 지붕 파스텔 (Shell 메시 버텍스 색 ÷ 1.1, 그대로의 값)
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float SdRoundBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            half3 BuildingAlbedo(float3 p, float3 n, float2 wall, half3 roofTint, out half3 emit)
            {
                emit = 0;
                half ao = lerp(0.86h, 1.0h, saturate(p.y / 4.0));
                if (n.y > 0.6)
                {
                    float2 g = abs(frac(p.xz / 5.0) - 0.5);
                    half3 roof = _RoofColor.rgb * roofTint * 1.1 * (1.0 - 0.035 * smoothstep(0.46, 0.49, max(g.x, g.y)));
                    return lerp(roof, half3(0.90, 0.94, 1.0), _KucaSnow * 0.9);   // 겨울: 지붕 눈
                }
                half3 c = _WallColor.rgb * lerp(0.93h, 1.04h, saturate(p.y / 24.0));
                if (_Brick > 0.5) c *= 1.0 - 0.06 * step(frac(p.y / 0.34), 0.12);
                float fv = frac(p.y / _FloorHeight), fl = floor(p.y / _FloorHeight);
                if (fv < 0.05 && p.y > 2.0) c = lerp(c, _TrimColor.rgb, 0.55);

                float wl = wall.y, wu = wall.x;
                bool infinite = wl < 0.5;
                if (infinite)
                {
                    float2 side = normalize(float2(-n.z, n.x) + 1e-5);
                    wu = dot(p.xz, side);
                }
                float margin = infinite ? 0.0 : clamp(wl * 0.12, 1.8, 3.2);
                float nWin = infinite ? 100000.0 : floor((wl - 2.0 * margin) / _WindowSpacing);
                if (nWin < 1.0 || p.y < 1.2) return c * ao;
                float lu = (wu - (infinite ? 0.0 : (wl - nWin * _WindowSpacing) * 0.5)) / _WindowSpacing;
                if (lu < 0.0 || lu >= nWin) return lerp(c, _TrimColor.rgb, 0.22) * ao;          // 모서리 기둥
                float colI = floor(lu), fu = frac(lu);
                if (_PilasterEvery > 0.5 && fmod(abs(colI), _PilasterEvery) > _PilasterEvery - 1.5)
                    return lerp(c, _TrimColor.rgb, 0.3) * ao;                                   // 창 묶음 사이 벽

                float2 qq = float2((fu - 0.5) * _WindowSpacing, (fv - 0.55) * _FloorHeight);
                float2 hb = float2(_WindowWidth * _WindowSpacing * 0.5, _WindowHeight * _FloorHeight * 0.5);
                float d;
                if (_Arch > 0.5 && qq.y > hb.y - hb.x) d = length(qq - float2(0.0, hb.y - hb.x)) - hb.x;
                else d = SdRoundBox(qq, hb, min(hb.x, hb.y) * (_Arch > 0.5 ? 0.2 : 0.42));

                half3 res = c * ao;
                if (qq.y < -hb.y && qq.y > -hb.y - 0.24 && abs(qq.x) < hb.x + 0.22) res = _TrimColor.rgb * ao;   // 창턱
                else if (d < 0.0)
                {
                    float t = saturate((qq.y + hb.y) / (2.0 * hb.y));
                    float h = frac(sin(dot(float2(colI, fl), float2(12.9898, 78.233))) * 43758.5453);
                    half3 glass = lerp(_WindowColor.rgb * 0.72, lerp(_WindowColor.rgb, half3(0.88, 0.95, 1.0), 0.55), t);
                    glass = lerp(glass, half3(0.97, 0.99, 1.0), 0.4 * step(0.86, frac((qq.x * 0.7 + qq.y) * 0.45 + h)));
                    if (h > 0.8 && t > 0.5) glass = lerp(glass, half3(1.0, 0.93, 0.84), 0.55);   // 커튼
                    if (_WindowWidth > 0.8 && abs(qq.x) < 0.06) glass = _TrimColor.rgb * 0.95;    // 넓은 창 멀리언
                    glass *= lerp(1.0, 0.45, _KucaNight);
                    if (h < _KucaLitWindows) emit = _KucaGlowColor.rgb * _KucaGlowStrength * _KucaNight * (0.65 + 0.35 * t);   // 밤: 불 켜진 창
                    res = glass;
                }
                else if (d < 0.16) res = _TrimColor.rgb;   // 창틀

                // 멀리서 창이 몇 픽셀보다 작아지면 평균색으로 (지글거림 방지)
                float px = max(fwidth(qq.x), fwidth(qq.y));
                half3 avg = lerp(c * ao, lerp(_WindowColor.rgb, _TrimColor.rgb, 0.35), min(saturate(_WindowWidth * _WindowHeight * 1.4), 0.7));
                float fade = saturate(px * 3.0 - 0.6);
                emit = lerp(emit, _KucaGlowColor.rgb * _KucaGlowStrength * _KucaNight * _KucaLitWindows * min(saturate(_WindowWidth * _WindowHeight * 1.4), 0.7), fade);
                return lerp(res, avg, fade);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half3 emit;
                half3 albedo = BuildingAlbedo(i.positionWS, n, i.wall, i.roofTint, emit);
                half3 color = KucaShade(albedo, n, i.positionWS);
                color = KucaSunWash(color, i.positionCS) + emit;
                color += KucaNightLight(albedo, i.positionWS, 0.35h);   // 근처 등불이 벽에 은은히
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
