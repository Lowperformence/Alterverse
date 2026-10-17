Shader "DimensionsFront/VoidArmorEnergy"
{
    Properties
    {
        [MainColor] _BaseColor("Violet core", Color) = (.40,.035,.95,1)
        [HDR] _EmissionColor("Violet emission", Color) = (.96,.084,2.28,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "VioletCore"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half rim=pow(1.0h-saturate(abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)))),2.0h);
                // Explicit self illumination: independent of scene lights and stripped Lit keywords.
                // Retain a violet core when an LDR portrait clamps the blue channel.
                return half4(_BaseColor.rgb+_EmissionColor.rgb*.35h+half3(.10h,.025h,.15h)*rim,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
