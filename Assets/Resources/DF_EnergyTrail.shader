Shader "DimensionsFront/EnergyTrail"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent"}
        Pass
        {
            Tags {"LightMode"="SRPDefaultUnlit"}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A {float4 position:POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            struct V {float4 position:SV_POSITION;half4 color:COLOR;float2 uv:TEXCOORD0;};
            V vert(A i){V o;o.position=TransformObjectToHClip(i.position.xyz);o.color=i.color;o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                float across=abs(i.uv.y*2-1);
                float aa=max(fwidth(across),.015);
                half edge=1-smoothstep(.72,1+aa,across);
                half core=1-smoothstep(.08,.30+aa,across);
                return half4(i.color.rgb*(1+core*.35h),i.color.a*edge);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
