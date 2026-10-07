// 키아트 지면: CampusGround.jpg (tools/keyart/build_art.py) + 가까이서 보이는 잔디 결 디테일.
// 디테일은 초록 부분(잔디)에만 겹친다. 지도 바깥 잔디는 _BaseMap 없이 _BaseColor 만 써도 된다.
Shader "KUCA/StylizedGround"
{
    Properties
    {
        _BaseMap ("Ground", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _DetailMap ("Grass Detail (회색, 평균 0.5)", 2D) = "gray" {}
        _DetailTile ("Detail Tile (m)", Float) = 7
        _DetailStrength ("Detail Strength", Range(0, 1.5)) = 0.55
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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap); SAMPLER(sampler_DetailMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _DetailTile, _DetailStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fog : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half greenness = saturate((albedo.g - max(albedo.r, albedo.b)) * 8.0);
                half d = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, i.positionWS.xz / _DetailTile).r;
                albedo *= 1.0 + (d - 0.5) * _DetailStrength * greenness;
                half3 color = KucaShade(albedo, normalize(i.normalWS), i.positionWS);
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
