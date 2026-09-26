Shader "Alterverse/RuntimeGroundTelegraph"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.35,1,0.35,1)
        _FillAlpha("Fill Alpha", Range(0,1)) = 0.12
        _RingAlpha("Ring Alpha", Range(0,1)) = 0.68
        _RingWidth("Ring Width", Range(0.005,0.25)) = 0.045
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _FillAlpha;
                half _RingAlpha;
                half _RingWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                clip(1.0 - r);

                float ringStart = saturate(1.0 - _RingWidth);
                float ring = smoothstep(ringStart, min(0.999, ringStart + _RingWidth * 0.58), r);
                float edgeFade = 1.0 - smoothstep(0.985, 1.0, r);
                ring *= edgeFade;

                half alpha = lerp(_FillAlpha, _RingAlpha, ring) * _BaseColor.a;
                half3 rgb = MixFog(_BaseColor.rgb, input.fogFactor);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
