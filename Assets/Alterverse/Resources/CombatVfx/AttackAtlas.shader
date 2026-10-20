Shader "DimensionsFront/AttackAtlas"
{
    Properties
    {
        [MainTexture] _BaseMap("Attack masks", 2D) = "black" {}
        _Tint("Hero / ability colour", Color) = (1,1,1,1)
        _AtlasCell("Frame", Float) = 0
        _Life("Normalised age", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Resources/DF_DeathFade.hlsl"
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _AtlasCell;
            half _Life;
            CBUFFER_END
            struct A { float4 position : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A input)
            {
                V output;
                output.position = TransformObjectToHClip(input.position.xyz);
                float cell = clamp(floor(_AtlasCell), 0.0, 63.0);
                float2 origin = float2(fmod(cell, 8.0), 7.0 - floor(cell / 8.0)) * .125;
                // Four transparent pixels per cell. Stay half a texel inside the tile.
                output.uv = origin + (.5 + saturate(input.uv) * 127.0) / 1024.0;
                return output;
            }
            half4 frag(V input) : SV_Target
            {
                DFApplyDeathFade(input.position);
                half4 mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = mask.a * _Tint.a;
                clip(alpha - .002h);
                // One fetch, no depth/scene-colour reads, lighting, noise or global clock.
                half core = mask.r * mask.r * (1.0h - saturate(_Life * 2.0h));
                half3 colour = _Tint.rgb * mask.rgb * (1.0h + core * .35h);
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
