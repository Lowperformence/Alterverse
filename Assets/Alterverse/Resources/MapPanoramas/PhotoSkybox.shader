Shader "DimensionsFront/PhotoSkybox"
{
    Properties
    {
        [NoScaleOffset] _Panorama("Universe photograph (sRGB)",2D)="black"{}
        _ImageAspect("Photograph aspect",Float)=1.5
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
            float _ImageAspect;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                // Cover the camera viewport without stretching this non-panoramic reference.
                // Background depth leaves the arena and every gameplay object in front.
                float2 viewport=max(GetScaledScreenParams().xy,float2(1,1));
                float2 uv=input.positionCS.xy/viewport;
                #if UNITY_UV_STARTS_AT_TOP
                uv.y=1.0-uv.y;
                #endif
                float screenAspect=viewport.x/viewport.y;
                float imageAspect=max(_ImageAspect,.001);
                float2 crop=float2(min(1.0,screenAspect/imageAspect),min(1.0,imageAspect/screenAspect));
                uv=(uv-.5)*crop+.5;
                return half4(SAMPLE_TEXTURE2D(_Panorama,sampler_Panorama,saturate(uv)).rgb,1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
