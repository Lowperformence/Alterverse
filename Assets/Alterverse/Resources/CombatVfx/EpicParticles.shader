Shader "DimensionsFront/EpicParticles"
{
    Properties
    {
        _MainTex ("Licensed effect texture", 2D) = "white" {}
        _Tint ("Hero tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "EpicUnlit"
            Blend SrcAlpha [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color; o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // Retain the authored brightness/alpha curves while replacing the prefab's
                // blue/red palette with the exact hero/ability tint. No depth texture required.
                half light = max(tex.r,max(tex.g,tex.b)) * max(i.color.r,max(i.color.g,i.color.b));
                return half4(light * _Tint.rgb, saturate(tex.a * i.color.a * _Tint.a));
            }
            ENDHLSL
        }
    }
}
