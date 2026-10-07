// 버텍스 색으로 칠하는 로우폴리 오브젝트 (나무, 산울타리, 건물 디테일, 랜드마크). 면마다 법선이 따로라 각진 일러스트 느낌이 난다.
// 버텍스 색은 sRGB (KeyArtGeometry.bytes 의 Color32) 로 들어오므로 리니어 색 공간에서는 여기서 바꾼다.
Shader "KUCA/VertexColorLit"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
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
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : COLOR;
                float fog : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                half4 c = v.color;
            #if !defined(UNITY_COLORSPACE_GAMMA)
                c.rgb = SRGBToLinear(c.rgb);
            #endif
                o.color = c * _Tint;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 nrm = normalize(i.normalWS);
                half3 albedo = KucaSeasonFoliage(i.color.rgb, i.positionWS, nrm);
                half3 color = KucaShade(albedo, nrm, i.positionWS);
                color += KucaNightLight(albedo, i.positionWS, 0.6h);   // 근처 등불이 나무·벤치에 비침
                color = KucaSunWash(color, i.positionCS);
                // 버텍스 알파 0 = 가로등 머리·유리문 등 밤에 빛나는 부분
                color += i.color.rgb * _KucaGlowColor.rgb * _KucaGlowStrength * _KucaNight * (1.0 - i.color.a) * 2.0;
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
