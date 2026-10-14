Shader "Alterverse/MapWater"
{
    Properties
    {
        [MainColor] _BaseColor("Deep pool colour",Color)=(.025,.22,.52,1)
        _ShallowColor("Shallow bank colour",Color)=(.07,.34,.38,1)
        _Smoothness("Water smoothness",Range(0,1))=.86
        _WaveStrength("Ripple normals",Range(0,.3))=.14
        _ShoreStrength("Restrained bank foam",Range(0,.2))=.065
        [HideInInspector] _AuthoredBanks("Shore distance from UV",Float)=0
    }
    SubShader
    {
        // Existing pool cylinders remain opaque and depth-writing. No sorting,
        // camera-colour copy, displacement or gameplay/collision dependency.
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Back ZWrite On
        HLSLINCLUDE
        #include "Assets/Resources/DF_SurfaceLighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor,_ShallowColor;
            half _Smoothness,_WaveStrength,_ShoreStrength,_AuthoredBanks;
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
            float2 bank:TEXCOORD2;
            half top:TEXCOORD3;
            half fog:TEXCOORD4;
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
            // Unity's cylinder cap radius is .5. Object coordinates keep elliptical
            // banks aligned under every existing nonuniform scale and rotation.
            output.bank=lerp(input.positionOS.xz*2,float2(input.uv.x,0),_AuthoredBanks);
            output.top=saturate(input.normalOS.y);
            output.fog=ComputeFogFactor(output.positionCS.z);
            return output;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p=input.positionWS.xz;
                float time=_Time.y;
                float a=dot(p,float2(1.61,.83))-time*.72;
                float b=dot(p,float2(-1.17,2.31))+time*.57;
                float c=dot(p,float2(8.9,-5.4))-time*1.05;
                float d=dot(p,float2(4.7,12.3))+time*.83;
                // Pixel footprint suppresses subpixel capillary waves at low resolution.
                half fine=1-smoothstep(.05,.24,max(length(ddx(p)),length(ddy(p))));
                half2 slope=half2(1.61,.83)*cos(a)*.30h+half2(-1.17,2.31)*cos(b)*.18h;
                slope+=(half2(8.9,-5.4)*cos(c)*.017h+half2(4.7,12.3)*cos(d)*.011h)*fine;
                half radius=length(input.bank);
                half bank=smoothstep(.56h,.98h,radius)*input.top;
                // Calm at the margin; visible wave normals never move the pool mesh.
                slope*= _WaveStrength*input.top*lerp(1.0h,.4h,bank);
                half3 n=normalize(input.normalWS);
                half3 gradient=half3(slope.x,0,slope.y);
                n=normalize(n-(gradient-n*dot(n,gradient)));
                half3 albedo=lerp(_BaseColor.rgb,_ShallowColor.rgb,bank*.78h);
                half ripple=.5h+.25h*(sin(a)+sin(b));
                albedo*=.96h+.08h*ripple*input.top;
                // Broken, low-contrast bank detail rather than a bright solid ring.
                half fringe=smoothstep(.87h,.94h,radius)*(1-smoothstep(.97h,1.01h,radius));
                half breakup=smoothstep(.38h,.78h,.5h+.25h*(sin(dot(p,float2(3.7,1.9)))+sin(b*.73)));
                albedo=lerp(albedo,half3(.65,.76,.70),fringe*breakup*_ShoreStrength*input.top);
                half smooth=lerp(_Smoothness,.57h,bank);
                half3 rgb=DFSurfaceLighting(input.positionWS,n,albedo,0,smooth,1,half3(0,0,0),input.positionCS);
                return half4(MixFog(rgb,input.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags {"LightMode"="ShadowCaster"}
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFShadowVert
            #pragma fragment DFShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags {"LightMode"="DepthOnly"}
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFDepthVert
            #pragma fragment DFDepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags {"LightMode"="DepthNormals"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFNormalVert
            #pragma fragment DFNormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    Fallback Off
}
