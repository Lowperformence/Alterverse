Shader "Alterverse/UrbanDigitalArmor"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 0
        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.2
        [HDR] _EmissionColor("Emission", Color) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        LOD 150
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                half4 _EmissionColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float3 rest : TEXCOORD2;
                float3 restNormal : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float3 rest : TEXCOORD4;
                float3 restNormal : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.tangentWS = half4(normalize(TransformObjectToWorldDir(input.tangentOS.xyz)), input.tangentOS.w);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.rest=input.rest;
                output.restNormal=input.restNormal;
                return output;
            }

            half3 UrbanCamo(float3 rest,float3 restNormal)
            {
                // Mipmapped texture in undeformed space. No hash thresholds or scrolling noise.
                float3 blend=pow(abs(restNormal),8);
                blend/=max(dot(blend,float3(1,1,1)),.0001);
                float3 p=rest*.85;
                half3 x=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.yz).rgb;
                half3 y=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xz).rgb;
                half3 z=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,p.xy).rgb;
                return x*blend.x+y*blend.y+z*blend.z;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = half4(UrbanCamo(input.rest,input.restNormal),1);
                half3 n = normalize(input.normalWS);

                // Procedural lunar terrain supplies a runtime-generated RGB normal texture.
                // Other runtime materials keep _BumpScale at zero, avoiding tangent dependence.
                if (_BumpScale > 0.001h)
                {
                    half3 tangent = normalize(input.tangentWS.xyz);
                    half3 bitangent = normalize(cross(n, tangent) * input.tangentWS.w);
                    half3 normalTS = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv).xyz * 2.0h - 1.0h;
                    normalTS.xy *= _BumpScale;
                    normalTS = normalize(normalTS);
                    n = normalize(tangent * normalTS.x + bitangent * normalTS.y + n * normalTS.z);
                }

                half3 key = normalize(half3(-0.38h, 0.84h, 0.39h));
                half ndl = saturate(dot(n, key));
                half diffuse = lerp(0.42h, 1.0h, ndl);
                half edge = pow(1.0h - saturate(abs(n.y)), 2.0h) * (0.04h + _Smoothness * 0.08h);
                half metallicLift = _Metallic * 0.06h;
                half3 rgb = baseSample.rgb * _BaseColor.rgb * (diffuse + edge + metallicLift) + _EmissionColor.rgb;
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, baseSample.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
