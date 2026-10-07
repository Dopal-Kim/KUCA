// 키아트 건물 외벽: 크림 벽, 층마다 창문 줄(창틀·창턱·유리 반사), 기둥 띠, 층 띠, 벽돌 줄눈, 지붕 판넬.
// 메시 UV 없이 월드 좌표로 무늬를 만든다 (1 유닛 = 1 m). 웹 미리보기 main.js 의 kucaBuilding 과 같은 무늬.
Shader "KUCA/StylizedBuilding"
{
    Properties
    {
        _WallColor ("Wall", Color) = (0.94, 0.90, 0.82, 1)
        _TrimColor ("Trim (창틀·기둥·띠)", Color) = (0.99, 0.97, 0.92, 1)
        _RoofColor ("Roof", Color) = (0.73, 0.72, 0.71, 1)
        _WindowColor ("Window", Color) = (0.36, 0.52, 0.64, 1)
        _FloorHeight ("Floor Height (m)", Float) = 3.4
        _WindowSpacing ("Window Spacing (m)", Float) = 3.2
        _WindowWidth ("Window Width (0-1)", Range(0.1, 1.0)) = 0.52
        _WindowHeight ("Window Height (0-1)", Range(0.1, 0.9)) = 0.5
        _PilasterEvery ("Pilaster Every N Windows (0 = 없음)", Float) = 3
        _Brick ("Brick Courses (0/1)", Float) = 0
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
                float _FloorHeight, _WindowSpacing, _WindowWidth, _WindowHeight, _PilasterEvery, _Brick;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 BuildingAlbedo(float3 p, float3 n)
            {
                if (n.y > 0.6)
                {
                    float2 g = abs(frac(p.xz / 4.0) - 0.5);
                    return _RoofColor.rgb * (1.0 - 0.05 * step(0.47, max(g.x, g.y)));
                }
                float2 side = normalize(float2(-n.z, n.x) + 1e-5);
                float u = dot(p.xz, side) / _WindowSpacing;
                float v = p.y / _FloorHeight;
                float fu = frac(u), fv = frac(v);
                half3 c = _WallColor.rgb;
                if (_Brick > 0.5) c *= 1.0 - 0.08 * step(frac(p.y / 0.32), 0.14);
                if (_PilasterEvery > 0.5)
                {
                    float colI = floor(u + 0.5);
                    float gap = abs(frac(u + 0.5) - 0.5);
                    if (fmod(abs(colI), _PilasterEvery) < 0.5 && gap < 0.17 && p.y > 0.9) c = lerp(c, _TrimColor.rgb, 0.85);
                }
                if (fv < 0.06 && p.y > 2.0) c = lerp(c, _TrimColor.rgb, 0.7);
                float hw = _WindowWidth * 0.5, hh = _WindowHeight * 0.5;
                float du = abs(fu - 0.5), dv = abs(fv - 0.55);
                bool above = p.y > 1.4;
                if (above && du < hw + 0.05 && dv < hh + 0.07)
                {
                    c = _TrimColor.rgb * 0.96;
                    if (du < hw && dv < hh)
                    {
                        float t = (fv - (0.55 - hh)) / (2.0 * hh);
                        half3 glass = _WindowColor.rgb * (0.78 + 0.34 * t);
                        float streak = step(0.8, frac((u * _WindowSpacing + p.y) * 0.11));
                        c = lerp(glass, half3(0.80, 0.88, 0.96), 0.25 * streak);
                    }
                }
                if (above && du < hw + 0.1 && fv > 0.55 - hh - 0.12 && fv < 0.55 - hh - 0.06) c = _TrimColor.rgb;
                return c * lerp(0.84, 1.0, saturate(p.y / 4.0));
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half3 color = KucaShade(BuildingAlbedo(i.positionWS, n), n, i.positionWS);
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
