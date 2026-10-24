Shader "DimensionsFront/EpicParticles"
{
    Properties
    {
        _MainTex ("Licensed effect texture", 2D) = "white" {}
        _Tint ("Hero tint", Color) = (1,1,1,1)
        _NativePalette ("Authored palette", Float) = 0
        _AttackClip ("World footprint: center XZ, radius, enabled", Vector) = (0,0,0,0)
        _AttackCone ("World cone: direction XZ, cosine, enabled", Vector) = (0,1,-1,0)
        _AttackRange ("World cast range: origin XZ, radius, enabled", Vector) = (0,0,0,0)
        _AttackTrail ("World projectile segment: start XZ, delta XZ", Vector) = (0,0,0,0)
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
                half _NativePalette;
                float4 _AttackClip, _AttackCone, _AttackRange, _AttackTrail;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.color = v.color; o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 footprint=i.positionWS.xz-_AttackClip.xy;
                if(_AttackClip.w>1.5)
                {
                    float2 relative=i.positionWS.xz-_AttackTrail.xy;
                    float along=saturate(dot(relative,_AttackTrail.zw)/max(.000001,dot(_AttackTrail.zw,_AttackTrail.zw)));
                    footprint=relative-_AttackTrail.zw*along;
                }
                if(_AttackClip.w>.5) clip(_AttackClip.z*_AttackClip.z-dot(footprint,footprint));
                float2 reach=i.positionWS.xz-_AttackRange.xy;
                if(_AttackRange.w>.5) clip(_AttackRange.z*_AttackRange.z-dot(reach,reach));
                if(_AttackCone.w>.5 && dot(reach,reach)>.000001)
                    clip(dot(normalize(reach),_AttackCone.xy)-_AttackCone.z);
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                // Retain the authored brightness/alpha curves while replacing the prefab's
                // blue/red palette with the exact hero/ability tint. No depth texture required.
                half light = max(tex.r,max(tex.g,tex.b)) * max(i.color.r,max(i.color.g,i.color.b));
                return half4(lerp(light * _Tint.rgb,tex.rgb*i.color.rgb,_NativePalette), saturate(tex.a * i.color.a * _Tint.a));
            }
            ENDHLSL
        }
    }
}
