Shader "DimensionsFront/VoidArmorHalo"
{
    Properties
    {
        [MainColor] _BaseColor("Violet halo", Color) = (.48,.045,1,.32)
        _HaloWidth("Maximum halo width", Float) = .018
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "LocalVioletHalo"
            Tags { "LightMode"="SRPDefaultUnlit" }
            // Preserve visible alpha outside the actor in the transparent menu render texture.
            Blend SrcAlpha One, One OneMinusSrcAlpha
            Cull Back ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _HaloWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half opacity:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position=input.positionOS.xyz+normalize(input.normalOS)*(_HaloWidth*input.color.r);
                output.positionWS=TransformObjectToWorld(position);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.opacity=input.color.a;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half edge=1.0h-saturate(abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS))));
                half alpha=_BaseColor.a*input.opacity*smoothstep(.03h,.85h,edge);
                return half4(_BaseColor.rgb,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
