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
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
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
            half4 Frag(Varyings i):SV_Target
            {
                half3 n=normalize(i.normalWS);
                // Rest coordinates survive skinning, preventing texture swimming.
                float weave=Weave(i.rest.xy);
                float worn=.5+.5*sin(i.rest.x*17+i.rest.y*7)*sin(i.rest.y*31+i.rest.z*13);
                float fibers=.5+.5*sin(i.rest.y*92+sin(i.rest.x*47)*.65);
                half3 color=_BaseColor.rgb*(.82+.24*worn+.13*weave+.025*fibers);
                Light mainLight=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 illumination=SampleSH(n)+mainLight.color*saturate(dot(n,mainLight.direction))*mainLight.shadowAttenuation*mainLight.distanceAttenuation;
                // Broad fiber sheen only; canvas has no hard metallic highlight.
                half3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                half sheen=pow(1-saturate(abs(dot(n,view))),4)*.055;
                return half4(MixFog(color*max(illumination,half3(.16,.16,.16))+color*sheen,i.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback Off
}
