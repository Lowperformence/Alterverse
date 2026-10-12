Shader "Alterverse/TargetRing"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "TargetRing"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float fogFactor : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(MixFog(input.color.rgb, input.fogFactor), input.color.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
