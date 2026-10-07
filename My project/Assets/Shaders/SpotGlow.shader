// 경희스팟 배지 뒤 은은한 후광 (더하기 섞기, 원형으로 부드럽게 사라짐). _Intensity 는 스팟마다 MaterialPropertyBlock 으로.
Shader "KUCA/SpotGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (1, 0.86, 0.55, 1)
        _Intensity ("Intensity", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float a = saturate(1.0 - d);
                a = a * a * (3.0 - 2.0 * a);
                // 배지 테두리 근처가 살짝 더 밝은 고리
                float ring = exp(-pow((d - 0.58) / 0.12, 2.0)) * 0.6;
                return half4(_Color.rgb * (a * a + ring * a) * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
