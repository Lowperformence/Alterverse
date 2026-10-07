Shader "DimensionsFront/Nerok/WovenJacket"
{
    Properties
    {
        _BaseColor("Weathered brown canvas", Color) = (.24,.135,.07,1)
        _ThreadDensity("Threads per model unit", Float) = 170
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
                float _ThreadDensity;
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
            float Weave(float2 uv)
            {
                float2 thread=uv*_ThreadDensity;
                // Fade fine threads before they alias at match-camera distance.
                float2 visible=1-smoothstep(.15,.65,fwidth(thread));
                float2 strands=cos(thread*6.2831853)*visible;
                float crossing=cos((thread.x-thread.y)*3.1415927)*min(visible.x,visible.y);
                return .5+.17*(strands.x+strands.y)+.08*crossing;
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
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS);
                // Rest coordinates survive skinning, preventing texture swimming.
                float weave=Weave(i.rest.xy);
                float worn=.5+.5*sin(i.rest.x*17+i.rest.y*7)*sin(i.rest.y*31+i.rest.z*13);
                float fibers=.5+.5*sin(i.rest.y*92+sin(i.rest.x*47)*.65);
                half3 color=_BaseColor.rgb*(.82+.24*worn+.13*weave+.025*fibers);
                half3 rgb=DFSurfaceLighting(i.positionWS,n,color,0.0h,
                    lerp(.16h,.24h,saturate(weave)),1.0h,half3(0,0,0));
                // Broad fiber sheen only; canvas has no hard metallic highlight.
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                half sheen=pow(1-saturate(abs(dot(n,view))),4)*.055;
                return half4(MixFog(rgb+color*sheen,i.fog),1);
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

