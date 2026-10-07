// 수집 동물 피규어의 투명 부품 (헬멧 유리 등): 거의 투명, 가장자리(프레넬)가 밝고 해 쪽 반짝임
Shader "KUCA/CreatureGlass"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.92, 0.97, 1.0, 1)
        _Opacity ("Center Opacity", Range(0, 1)) = 0.08
        _RimOpacity ("Rim Opacity", Range(0, 1)) = 0.55
        _Spec ("Specular", Range(0, 2)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Glass"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Opacity, _RimOpacity, _Spec;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light sun = GetMainLight();
                half fres = pow(1.0 - saturate(dot(n, v)), 3.0);
                half spec = pow(saturate(dot(n, normalize(sun.direction + v))), 96.0) * _Spec;
                half a = saturate(lerp(_Opacity, _RimOpacity, fres) + spec);
                half3 c = _Tint.rgb * (0.85 + 0.15 * saturate(n.y)) + sun.color * spec;
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
