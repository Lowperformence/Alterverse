Shader "Alterverse/RuntimeLit"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.2
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 150
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half3 key = normalize(half3(-0.38h, 0.84h, 0.39h));
                half ndl = saturate(dot(n, key));
                half diffuse = lerp(0.42h, 1.0h, ndl);
                half edge = pow(1.0h - saturate(abs(n.y)), 2.0h) * (0.04h + _Smoothness * 0.08h);
                half metallicLift = _Metallic * 0.06h;
                half3 rgb = _BaseColor.rgb * (diffuse + edge + metallicLift) + _EmissionColor.rgb;
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
