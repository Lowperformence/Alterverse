Shader "Alterverse/SkinOrnaments"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 0
        _Metallic("Metallic", Range(0,1)) = 0.04
        _Smoothness("Smoothness", Range(0,1)) = 0.35
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
                output.rest = input.rest;
                return output;
            }

            float OrnamentLine(float d, float width)
            {
                float aa = max(fwidth(d), .001);
                return 1 - smoothstep(width, width + aa, d);
            }

            float Ornament(float2 p)
            {
                float2 q = frac(p) - .5;
                float chain = OrnamentLine(abs(q.x), .009);
                float beads = OrnamentLine(length(float2(q.x, frac((q.y + .5) * 6) - .5)), .065);
                float moon = OrnamentLine(max(length(q - float2(.0, .08)) - .19, -(length(q - float2(.075, .12)) - .16)), .01);
                moon *= step(.03, q.y) * step(q.y, .30);
                float2 f = q - float2(0, -.27);
                float flower = 0;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * 1.04719755;
                    float2 center = float2(cos(a), sin(a)) * .085;
                    flower = max(flower, OrnamentLine(length(f - center), .031));
                }
                return saturate(max(chain, max(beads, max(moon, flower))));
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
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 n = normalize(input.normalWS);

                if (_BumpScale > 0.001h)
                {
                    half3 tangent = normalize(input.tangentWS.xyz);
                    half3 bitangent = normalize(cross(n, tangent) * input.tangentWS.w);
                    half3 normalTS = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv).xyz * 2.0h - 1.0h;
                    normalTS.xy *= _BumpScale;
                    normalTS = normalize(normalTS);
                    n = normalize(tangent * normalTS.x + bitangent * normalTS.y + n * normalTS.z);
                }

                // Subtle anatomical muscle fiber & epidermal micro-relief
                float3 poreFreq = input.rest * 95.0;
                float poreFootprint = max(fwidth(poreFreq.x), fwidth(poreFreq.y));
                float poreVis = 1.0 - smoothstep(0.25, 0.85, poreFootprint);
                float3 microGrad = float3(
                    sin(poreFreq.x * 6.2831853) * cos(poreFreq.y * 6.2831853),
                    cos(poreFreq.y * 6.2831853) * sin(poreFreq.z * 6.2831853),
                    sin(poreFreq.z * 6.2831853) * cos(poreFreq.x * 6.2831853)
                ) * (0.045 * poreVis);

                float ink = Ornament(input.rest.xy * 4.0);
                // Embossed 3D bevel along filigree ornament edges
                float dInkX = ddx(ink);
                float dInkY = ddy(ink);
                float3 dPosX = ddx(input.positionWS);
                float3 dPosY = ddy(input.positionWS);
                half3 inkBevel = SafeNormalize(dPosX * dInkX + dPosY * dInkY) * saturate(abs(dInkX) + abs(dInkY)) * 0.26h;

                half3 worldMicro = TransformObjectToWorldDir(microGrad);
                worldMicro -= n * dot(n, worldMicro);
                n = normalize(n + worldMicro - inkBevel);

                half3 skinBase = baseSample.rgb * _BaseColor.rgb;
                // Warm subsurface dermis scatter in the terminator & muscle contours
                Light mainLight = GetMainLight();
                half rawNdotL = dot(n, mainLight.direction);
                half sssTerminator = saturate((rawNdotL + 0.45h) / 1.45h) * (1.0h - saturate(rawNdotL * 1.15h));
                half3 dermisScatter = skinBase * half3(1.12h, 0.42h, 0.28h) * sssTerminator * 0.38h;

                // Filigree ornament inlay with dark burnished bronze-obsidian metallic sheen
                half3 inlayColor = half3(.078h, .042h, .034h);
                half3 albedo = lerp(skinBase + dermisScatter, inlayColor, ink * .82h);
                half metallic = lerp(max(_Metallic, 0.04h), 0.46h, ink * 0.78h);
                half smoothness = lerp(max(_Smoothness, 0.35h), 0.54h, ink * 0.75h);

                half3 rgb = DFSurfaceLighting(input.positionWS, n, albedo, metallic,
                    smoothness, 1.0h, _EmissionColor.rgb, input.positionHCS);
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
