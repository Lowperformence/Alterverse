Shader "DimensionsFront/AbilityRangeDisc"
{
    Properties { _Color("Color", Color) = (1, .08, .10, 1) }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float radius = length(input.uv * 2.0 - 1.0);
                float aa = max(fwidth(radius), .001);
                // Exact outer radius; antialiasing and the bright border extend inward.
                float coverage = 1.0 - smoothstep(1.0 - aa * 1.5, 1.0, radius);
                float border = smoothstep(.978 - aa, .978 + aa, radius);
                float halo = smoothstep(.68, .97, radius);
                float opacity = lerp(.065 + halo * .16, .94, border) * coverage;
                return half4(_Color.rgb, opacity * _Color.a);
            }
            ENDHLSL
        }
    }
}
