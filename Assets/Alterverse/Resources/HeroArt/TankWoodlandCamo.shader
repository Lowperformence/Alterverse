Shader "Alterverse/TankWoodlandCamo"
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

        HLSLINCLUDE
            #include "Assets/Resources/DF_SurfaceLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

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

            half4 CamoSample(float2 uv)
            {
                half4 texel = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                float3 source = texel.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    source = LinearToSRGB(source);
                #endif
                // Keep the supplied woodland shapes and grain; replace their four pigment tones.
                const float3 olive = float3(.3994, .4233, .2523);
                const float3 brown = float3(.3807, .3276, .2710);
                const float3 beige = float3(.6665, .6228, .5205);
                const float3 dark = float3(.2572, .2502, .2345);
                float3 a = source - olive, b = source - brown, c = source - beige, d = source - dark;
                float4 squaredDistance = float4(dot(a,a), dot(b,b), dot(c,c), dot(d,d));
                // Float precision avoids overflowing weights on exact palette matches on mobile GPUs.
                float4 weight = rcp(max(squaredDistance, .0001));
                weight *= weight; weight /= dot(weight, float4(1,1,1,1));
                float3 color = float3(.48,.30,.65) * weight.x + float3(.23,.40,.65) * weight.y +
                    beige * weight.z + float3(.16,.19,.27) * weight.w;
                const float3 luminance = float3(.299,.587,.114);
                float reference = dot(olive * weight.x + brown * weight.y + beige * weight.z + dark * weight.w, luminance);
                color *= clamp(dot(source, luminance) / max(reference, .001), .6, 1.4);
                #ifndef UNITY_COLORSPACE_GAMMA
                    color = SRGBToLinear(color);
                #endif
                texel.rgb = color;
                return texel;
            }

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
                float3 positionWS : TEXCOORD6;
                float3 rest : TEXCOORD4;
                float3 restNormal : TEXCOORD5;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.tangentWS = half4(normalize(TransformObjectToWorldDir(input.tangentOS.xyz)), input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.rest=input.rest;
                output.restNormal=input.restNormal;
                return output;
            }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            half4 Frag(Varyings input) : SV_Target
            {
                // Blend fixed bind-pose projections; never choose a different plane per vertex/frame.
                float3 weight=pow(abs(input.restNormal),4.0);
                weight/=max(dot(weight,float3(1,1,1)),.0001);
                half4 baseSample =
                    CamoSample(input.rest.yz*_BaseMap_ST.xy+_BaseMap_ST.zw)*weight.x+
                    CamoSample(input.rest.xz*_BaseMap_ST.xy+_BaseMap_ST.zw)*weight.y+
                    CamoSample(input.rest.xy*_BaseMap_ST.xy+_BaseMap_ST.zw)*weight.z;
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

                half3 albedo=baseSample.rgb*_BaseColor.rgb;
                half smoothness=_Smoothness;
                half3 rgb=DFSurfaceLighting(input.positionWS,n,albedo,_Metallic,
                    smoothness,1.0h,_EmissionColor.rgb,input.positionHCS);
                rgb = MixFog(rgb, input.fogFactor);
                return half4(rgb, baseSample.a * _BaseColor.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFShadowVert
            #pragma fragment DFShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFDepthVert
            #pragma fragment DFDepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DFNormalVert
            #pragma fragment DFNormalFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }

    Fallback Off
}


