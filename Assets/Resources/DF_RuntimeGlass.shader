Shader "Alterverse/RuntimeGlass"
{
    Properties
    {
        [MainTexture] _BaseMap("Authored glass tint",2D)="white" {}
        [MainColor] _BaseColor("Glass tint and existing opacity",Color)=(.78,.80,.82,.50)
        [HDR] _EmissionColor("Authored emission",Color)=(0,0,0,0)
        _Smoothness("Glass polish",Range(0,1))=.92
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        // Premultiplied diffuse preserves optical highlights independently of opacity.
        // Single front surface, no camera-colour copy, refraction, shadow or depth pass.
        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off Cull Back
        Pass
        {
            Name "ForwardGlass"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _ALPHAPREMULTIPLY_ON 1
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Smoothness;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS:POSITION;
                float3 normalOS:NORMAL;
                float2 uv:TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                half fog:TEXCOORD2;
                float2 uv:TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.fog=ComputeFogFactor(output.positionCS.z);
                output.uv=input.uv*_BaseMap_ST.xy+_BaseMap_ST.zw;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                InputData lighting=(InputData)0;
                lighting.positionWS=input.positionWS;
                lighting.normalWS=normalize(input.normalWS);
                lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
                lighting.shadowCoord=0;
                lighting.bakedGI=SampleSH(lighting.normalWS);
                lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
                lighting.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                half4 glassTint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,input.uv)*_BaseColor;
                surface.albedo=glassTint.rgb*.35h;
                surface.emission=_EmissionColor.rgb;
                surface.metallic=0;
                surface.specular=half3(.04h,.04h,.04h);
                surface.smoothness=saturate(_Smoothness-min(.18h,length(fwidth(lighting.normalWS))*.2h));
                surface.normalTS=half3(0,0,1);
                surface.occlusion=1;
                surface.alpha=saturate(glassTint.a);
                half4 result=UniversalFragmentPBR(lighting,surface);
                result.rgb=MixFog(result.rgb,input.fog);
                result.a=surface.alpha;
                return result;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
