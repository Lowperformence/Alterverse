Shader "DimensionsFront/VoidKnightStealth"
{
    Properties
    {
        _StealthMap("Surface",2D)="white"{}
        _StealthTint("Tint",Color)=(1,1,1,1)
        _StealthOpacity("Visible fraction",Range(0,1))=.2
        _StealthVertexColor("Vertex painted surface",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Cloak"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_StealthMap); SAMPLER(sampler_StealthMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _StealthMap_ST;
                half4 _StealthTint;
                half _StealthOpacity;
                half _StealthVertexColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.uv=TRANSFORM_TEX(input.uv,_StealthMap);
                output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half4 color=SAMPLE_TEXTURE2D(_StealthMap,sampler_StealthMap,input.uv)*_StealthTint;
                color.rgb*=lerp(half3(1,1,1),input.color.rgb,_StealthVertexColor);
                // Emission is deliberately absent: glowing armour must fade with the whole model.
                return half4(color.rgb,saturate(color.a*_StealthOpacity));
            }
            ENDHLSL
        }
    }
}
