Shader "DimensionsFront/CosmicNebula"
{
    Properties {}
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half4 color:COLOR;};
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float2 p=i.uv*2-1;float fade=saturate(1-dot(p,p));
                float cloud=.78+.22*sin(p.x*9+_Time.y*.3)*sin(p.y*7-_Time.y*.2);
                return half4(i.color.rgb,i.color.a*fade*fade*cloud);
            }
            ENDHLSL
        }
    }
}
