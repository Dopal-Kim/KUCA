// 플레이어 캐릭터: 경희몬(CreatureToy)과 같은 비닐 토이 조명. 색은 버텍스 색 대신 재질 색(_BaseColor).
// ModularCharacter 가 FBX 재질 색을 파스텔로 바꿔 이 셰이더 재질로 갈아 끼운다.
Shader "KUCA/PlayerToy"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Gloss ("Gloss (광택 날카로움)", Range(8, 256)) = 64
        _Spec ("Specular (광택 세기)", Range(0, 1)) = 0.35
        _Rim ("Rim Light", Range(0, 1)) = 0.25
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
            #pragma multi_compile_instancing
            #include "KUCAStylizedLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Gloss, _Spec, _Rim;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
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
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 albedo = _BaseColor.rgb;
                half3 color = KucaShade(albedo, n, i.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half spec = pow(saturate(dot(n, normalize(sun.direction + v))), _Gloss) * _Spec * sun.shadowAttenuation;
                half rim = pow(1.0 - saturate(dot(n, v)), 4.0) * _Rim;
                color += sun.color * spec + rim * half3(1.0, 0.97, 0.92) * (0.5 + 0.5 * albedo);
                color += KucaNightLight(albedo, i.positionWS, 0.6h);
                color = KucaSunWash(color, i.positionCS);
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
