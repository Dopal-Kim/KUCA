// 키아트 건물: 크림색 벽, 연회색 지붕, 층마다 창문 줄. 메시 UV 없이 월드 좌표로 무늬를 만든다 (1 유닛 = 1 m).
Shader "KUCA/StylizedBuilding"
{
    Properties
    {
        _WallColor ("Wall", Color) = (0.93, 0.89, 0.80, 1)
        _RoofColor ("Roof", Color) = (0.74, 0.75, 0.77, 1)
        _WindowColor ("Window", Color) = (0.42, 0.55, 0.66, 1)
        _FloorHeight ("Floor Height (m)", Float) = 3.3
        _WindowSpacing ("Window Spacing (m)", Float) = 3.0
        _WindowWidth ("Window Width (0-1)", Range(0.1, 0.9)) = 0.55
        _WindowHeight ("Window Height (0-1)", Range(0.1, 0.9)) = 0.45
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "KUCAStylizedLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WallColor, _RoofColor, _WindowColor;
                float _FloorHeight, _WindowSpacing, _WindowWidth, _WindowHeight;
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

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                half3 albedo;
                if (n.y > 0.6)
                {
                    albedo = _RoofColor.rgb;
                }
                else
                {
                    albedo = _WallColor.rgb;
                    // 벽을 따라가는 가로 좌표 (벽의 수평 방향)
                    float2 side = normalize(float2(-n.z, n.x) + 1e-5);
                    float u = dot(i.positionWS.xz, side) / _WindowSpacing;
                    float v = i.positionWS.y / _FloorHeight;
                    float fu = frac(u), fv = frac(v);
                    // 지붕 바로 아래(첫 층 위 1.2m 미만)와 1층 바닥 근처에는 창을 두지 않는다
                    bool inWindow = abs(fu - 0.5) < _WindowWidth * 0.5 && abs(fv - 0.55) < _WindowHeight * 0.5 && i.positionWS.y > 1.2;
                    // 창틀 그늘로 살짝 입체감
                    half frame = (abs(fu - 0.5) < _WindowWidth * 0.5 + 0.06 && abs(fv - 0.55) < _WindowHeight * 0.5 + 0.06 && i.positionWS.y > 1.2) ? 0.9h : 1.0h;
                    albedo = inWindow ? _WindowColor.rgb * (0.9 + 0.2 * fv) : albedo * frame;
                }
                half3 color = KucaShade(albedo, n, i.positionWS);
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
