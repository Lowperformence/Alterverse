Shader "DimensionsFront/Nerok/SharkSkin"
{
    Properties
    {
        _BaseColor("Slate grey dorsal skin", Color) = (.22,.255,.28,1)
        _VentralColor("Ivory jaw skin", Color) = (.76,.78,.75,1)
        _DenticleDensity("Fine skin denticles", Float) = 1150
        _BaseMap("Shadow pass texture", 2D) = "white" {}
        _Cutoff("Shadow pass cutoff", Float) = .5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Cull Back ZWrite On
        HLSLINCLUDE
            #include "Assets/Resources/DF_SurfaceLighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _VentralColor;
                float _DenticleDensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float3 rest:TEXCOORD2; };
            struct Varyings
            {
                float4 positionHCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float3 rest:TEXCOORD2;
                float fog:TEXCOORD3;
            };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(i.positionOS.xyz);
                o.positionHCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(i.normalOS);
                o.rest=i.rest;
                o.fog=ComputeFogFactor(o.positionHCS.z);
                return o;
            }
            float SkinRelief(float3 rest)
            {
                // Filter microdetail before it shimmers at the match camera distance.
                float3 phase=rest*_DenticleDensity;
                float frequency=max(fwidth(phase.x),max(fwidth(phase.y),fwidth(phase.z)));
                float visible=1-smoothstep(.25,1.0,frequency);
                float grain=sin(phase.x+sin(phase.z*.73))*sin(phase.y*1.31+phase.z*.91);
                return grain*visible;
            }
            half3 ReliefNormal(float3 positionWS,half3 normalWS,float relief)
            {
                float3 dx=ddx(positionWS),dy=ddy(positionWS);
                float3 r1=cross(dy,normalWS),r2=cross(normalWS,dx);
                float determinant=dot(dx,r1);
                float3 gradient=(ddx(relief)*r1+ddy(relief)*r2)/max(abs(determinant),1e-7);
                return normalize(normalWS-sign(determinant)*gradient);
            }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            half4 Frag(Varyings i):SV_Target
            {
                float grain=SkinRelief(i.rest);
                half3 n=ReliefNormal(i.positionWS,normalize(i.normalWS),grain*.00010);
                // Source coordinates are retained in TEXCOORD2 through every bone pose.
                float ivory=1-smoothstep(1.728,1.820,i.rest.y+abs(i.rest.x)*.12);
                float mottling=sin(i.rest.x*91+i.rest.z*49)*sin(i.rest.y*113-i.rest.z*71);
                half3 color=lerp(_BaseColor.rgb,_VentralColor.rgb,ivory);
                color*=.96+.032*mottling+.025*grain;
                half smoothness=.46h+.04h*grain;
                half3 rgb=DFSurfaceLighting(i.positionWS,n,color,0.0h,smoothness,
                    1.0h,half3(0,0,0),i.positionHCS);
                return half4(MixFog(rgb,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
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
            Tags { "LightMode"="DepthOnly" }
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
            Tags { "LightMode"="DepthNormals" }
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


