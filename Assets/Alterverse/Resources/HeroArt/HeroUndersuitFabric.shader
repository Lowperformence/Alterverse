Shader "Alterverse/HeroUndersuitFabric"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 0
        _Metallic("Metallic", Range(0,1)) = 0.22
        _Smoothness("Smoothness", Range(0,1)) = 0.34
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
                float3 rest : TEXCOORD3;
                float3 restNormal : TEXCOORD4;
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
                output.rest = input.rest;
                output.restNormal = input.restNormal;
                return output;
            }

            // Band-limited carbon-hex & ripstop weave with analytical 2D slope for 3D normal relief.
            float WeavePlane(float2 position, out float2 slope)
            {
                float2 p = position * 110.0;
                float footprint = max(fwidth(p.x), fwidth(p.y));
                float visible = 1.0 - smoothstep(0.22, 0.78, footprint);

                float2 s = sin(p * 6.2831853);
                float2 c = cos(p * 6.2831853);
                float diagArg = (p.x + p.y) * 3.14159265;
                float crossing = cos(diagArg);
                float dCrossing = -sin(diagArg);

                float yarn = (c.x + c.y) * 0.17 + c.x * c.y * 0.11 + crossing * 0.10;
                float2 dYarn = float2(
                    -s.x * (0.17 + c.y * 0.11) + dCrossing * 0.05,
                    -s.y * (0.17 + c.x * 0.11) + dCrossing * 0.05
                ) * visible;

                // Hexagonal / tactical ripstop reinforcement grid visible at portrait and match camera range
                float2 gp = position * 26.0 * 6.2831853;
                float2 gc = cos(gp);
                float2 gs = sin(gp);
                float hexDiag = cos((gp.x * 0.8660254 + gp.y * 0.5));
                float hexDiag2 = cos((gp.x * 0.8660254 - gp.y * 0.5));
                float gridVisible = 1.0 - smoothstep(0.25, 0.85, footprint / 4.2);
                float grid = (gc.x + gc.y) * 0.052 + (hexDiag + hexDiag2) * 0.042;
                float2 dGrid = (float2(-gs.x, -gs.y) * 0.052 +
                    float2(-sin(gp.x * 0.8660254 + gp.y * 0.5) * 0.8660254, -sin(gp.x * 0.8660254 + gp.y * 0.5) * 0.5) * 0.042) * gridVisible;

                slope = dYarn * 1.35 + dGrid * 1.15;
                return 1.0 + yarn * visible + grid * gridVisible;
            }

            half FabricAndMuscle(float3 p, float3 restNormal, out float3 normalOffset, out half seamPiping, out half muscleCrown)
            {
                float3 n = normalize(restNormal + float3(0, 0, 1e-5));
                float3 weights = pow(abs(n), 4.0);
                weights /= max(dot(weights, float3(1.0, 1.0, 1.0)), 0.0001);

                float2 slopeYZ, slopeXZ, slopeXY;
                float wYZ = WeavePlane(p.yz, slopeYZ);
                float wXZ = WeavePlane(p.xz, slopeXZ);
                float wXY = WeavePlane(p.xy, slopeXY);

                float weave = wYZ * weights.x + wXZ * weights.y + wXY * weights.z;
                float3 weaveNormal =
                    float3(0.0, slopeYZ.x, slopeYZ.y) * weights.x +
                    float3(slopeXZ.x, 0.0, slopeXZ.y) * weights.y +
                    float3(slopeXY.x, slopeXY.y, 0.0) * weights.z;

                // Anatomical compression seam ribbing & sculpted muscle group contouring
                float symX = abs(p.x);
                float centerSeam = exp(-symX * symX * 520.0) * smoothstep(0.15, 0.85, abs(n.z));
                float obliqueSeam = exp(-pow(frac(symX * 5.2 - p.y * 1.8) - 0.5, 2.0) * 95.0) * 0.65;
                float panelRib = cos((p.y * 14.0 + symX * 9.0) * 6.2831853);
                float ribFootprint = max(fwidth(p.y * 14.0), 0.001);
                float ribVis = 1.0 - smoothstep(0.18, 0.65, ribFootprint);
                seamPiping = saturate(centerSeam * 0.75 + obliqueSeam * 0.45 + saturate(panelRib) * 0.14 * ribVis);

                // Muscle crown vs. inter-muscular striation shading from bind-pose normal orientation
                half crown = saturate(n.y * 0.38 + abs(n.z) * 0.44 + abs(n.x) * 0.28);
                half abdominalStriation = sin(p.y * 34.0) * exp(-symX * 14.0) * saturate(n.z) * 0.08 * ribVis;
                muscleCrown = saturate(crown + abdominalStriation + (weave - 1.0) * 0.65);

                float3 seamGrad = float3(
                    (p.x >= 0.0 ? 1.0 : -1.0) * (-centerSeam * symX * 18.0 + sin((p.y * 14.0 + symX * 9.0) * 6.2831853) * 0.08 * ribVis),
                    sin((p.y * 14.0 + symX * 9.0) * 6.2831853) * 0.11 * ribVis,
                    0.0
                );

                normalOffset = weaveNormal + seamGrad;
                return (half)weave;
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

                float3 localNormalOffset;
                half seamPiping;
                half muscleCrown;
                half weave = FabricAndMuscle(input.rest, input.restNormal, localNormalOffset, seamPiping, muscleCrown);

                // Perturb world normal with 3D carbon-weave threads and anatomical compression seams
                half3 worldOffset = TransformObjectToWorldDir(localNormalOffset);
                worldOffset -= n * dot(n, worldOffset);
                n = normalize(n + worldOffset * 0.24h);

                half3 baseTone = baseSample.rgb * _BaseColor.rgb;
                // Rich carbon-anthracite shading: deep dark recesses with subtle titanium-sheen thread crests
                half3 threadHighlight = lerp(baseTone, baseTone * 1.42h + half3(0.028h, 0.034h, 0.044h), saturate((weave - 0.96h) * 2.4h));
                half3 seamTone = baseTone * 0.72h + half3(0.038h, 0.044h, 0.056h);
                half3 albedo = lerp(threadHighlight, seamTone, seamPiping * 0.55h);
                albedo *= lerp(0.82h, 1.14h, muscleCrown);

                // High-grade nanosuit response: satin elastomer sheen on muscle crowns, metallic glint on carbon threads
                half metallic = saturate(max(_Metallic, 0.18h) + saturate(weave - 1.0h) * 0.42h + seamPiping * 0.12h);
                half smoothness = saturate(max(_Smoothness, 0.32h) * lerp(0.84h, 1.18h, saturate(weave - 0.85h)) + muscleCrown * 0.08h);
                half occlusion = saturate(lerp(0.76h, 1.0h, muscleCrown) * (1.0h - seamPiping * 0.16h));

                half3 rgb = DFSurfaceLighting(input.positionWS, n, albedo, metallic,
                    smoothness, occlusion, _EmissionColor.rgb, input.positionHCS);
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
