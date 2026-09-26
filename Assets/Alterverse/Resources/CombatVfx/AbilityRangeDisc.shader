Shader "DimensionsFront/AbilityRangeDisc"
{
    Properties { _Color("Color", Color) = (.40, .70, 1, .92) }
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
                // Derivative-sized edge stays fine at different camera zooms and resolutions.
                // All coverage is inside the authoritative radius; no expanding/pulsing boundary.
                float aa = max(fwidth(radius), .0001);
                float inward = 1.0 - radius;
                float coverage = smoothstep(0.0, aa, inward);
                float border = 1.0 - smoothstep(aa * 1.1, aa * 2.5, inward);
                float glow = exp2(-max(inward, 0.0) / max(aa * 4.0, .008));
                float interior = .035 + .025 * smoothstep(.55, 1.0, radius);
                float opacity = (interior + glow * .16 + border * .68) * coverage;
                half3 tint = lerp(_Color.rgb, half3(.78, .90, 1.0), border * .45);
                return half4(tint, saturate(opacity) * _Color.a);
            }
            ENDHLSL
        }
    }
}
