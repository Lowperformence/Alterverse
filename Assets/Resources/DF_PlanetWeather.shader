Shader "Alterverse/PlanetWeather"
{
    Properties
    {
        _EdgeOnly("Peripheral mist only",Float)=0
        _ArenaStretch("Arena length scale",Float)=1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _EdgeOnly;
                float _ArenaStretch;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; float3 world:TEXCOORD1; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.world=TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS=TransformWorldToHClip(output.world);
                output.uv=input.uv;output.color=input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2.0-1.0;
                float falloff=saturate(1.0-dot(p,p));
                // No depth texture, noise lookup, global fog, lighting pass or full-screen blend.
                float edge=max(smoothstep(39.0*_ArenaStretch,46.0*_ArenaStretch,abs(input.world.x)),
                    smoothstep(25.0,32.0,abs(input.world.z)));
                float mask=lerp(1.0,edge,_EdgeOnly);
                return half4(input.color.rgb,input.color.a*falloff*falloff*mask);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
