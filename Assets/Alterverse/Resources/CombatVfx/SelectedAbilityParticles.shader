Shader "DimensionsFront/SelectedAbilityParticles"
{
    Properties
    {
        _MainTex ("Authored explosion texture", 2D) = "white" {}
        _Tint ("Authored material colour", Color) = (1,1,1,1)
        _AttackClip ("Impact centre XZ, real radius, enabled", Vector) = (0,0,0,0)
        _AttackTrail ("Segment start XZ and delta XZ", Vector) = (0,0,0,0)
        _AttackCone ("Cone half-angle cosine", Vector) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Tint;
                float4 _AttackClip;
                float4 _AttackTrail;
                float4 _AttackCone;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 offset = input.positionWS.xz - _AttackClip.xy;
                if (_AttackClip.w > 2.5)
                {
                    clip(_AttackClip.z * _AttackClip.z - dot(offset, offset));
                    float distanceSquared = dot(offset, offset);
                    float2 direction = _AttackTrail.zw;
                    if (distanceSquared > .00001)
                        clip(dot(offset, direction) * rsqrt(max(distanceSquared * dot(direction,direction), .000001)) - _AttackCone.x);
                }
                else if (_AttackClip.w > 1.5)
                {
                    float2 delta = _AttackTrail.zw;
                    float lengthSquared = dot(delta, delta);
                    if (lengthSquared > .00001)
                    {
                        float along = dot(input.positionWS.xz - _AttackTrail.xy, delta) / lengthSquared;
                        clip(along); clip(1.0 - along);
                        float2 crossOffset = input.positionWS.xz - (_AttackTrail.xy + delta * along);
                        clip(_AttackClip.z * _AttackClip.z - dot(crossOffset,crossOffset));
                    }
                    else clip(_AttackClip.z * _AttackClip.z - dot(offset,offset));
                }
                else if (_AttackClip.w > .5) clip(_AttackClip.z * _AttackClip.z - dot(offset, offset));
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color * _Tint;
            }
            ENDHLSL
        }
    }
}
