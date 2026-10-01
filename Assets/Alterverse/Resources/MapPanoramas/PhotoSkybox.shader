Shader "DimensionsFront/PhotoSkybox"
{
    Properties
    {
        [NoScaleOffset] _Panorama("Planet panorama (sRGB)",2D)="black"{}
        _Yaw("Yaw",Float)=90
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Panorama); SAMPLER(sampler_Panorama);
            CBUFFER_START(UnityPerMaterial)
            float _Yaw;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 direction:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                output.direction=input.positionOS.xyz;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float3 direction=normalize(input.direction);
                float2 uv=float2(frac(atan2(direction.z,direction.x)/(2.0*PI)+.5-_Yaw/360.0),
                    asin(clamp(direction.y,-1.0,1.0))/PI+.5);
                // Hardware sRGB sampling performs the same conversion on PC and Android.
                return half4(SAMPLE_TEXTURE2D(_Panorama,sampler_Panorama,uv).rgb,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
