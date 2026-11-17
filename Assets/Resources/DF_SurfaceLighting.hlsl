#ifndef DF_SURFACE_LIGHTING_INCLUDED
#define DF_SURFACE_LIGHTING_INCLUDED

#include "Assets/Resources/DF_DeathFade.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Shared opaque surface response. Keep authored albedo, RGB normals and emission intact.
// Metallic, roughness and reflection probes use URP Lit BRDF enhanced with signed-curvature
// muscle/seam cavity occlusion, beveled edge highlights, and clearcoat studio rim response.
half3 DFSurfaceLighting(float3 positionWS, half3 normalWS, half3 albedo,
    half metallic, half smoothness, half occlusion, half3 emission, float4 rasterPosition)
{
    DFApplyDeathFade(rasterPosition);

    InputData lighting = (InputData)0;
    lighting.positionWS = positionWS;
    lighting.normalWS = normalize(normalWS);
    lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        lighting.shadowCoord = ComputeScreenPos(TransformWorldToHClip(positionWS));
    #else
        lighting.shadowCoord = TransformWorldToShadowCoord(positionWS);
    #endif
    lighting.bakedGI = SampleSH(lighting.normalWS);
    // Fragment SV_POSITION is in pixels. Reprojected clip coordinates cannot sample screen-space AO.
    lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(rasterPosition);
    lighting.shadowMask = half4(1,1,1,1);

    // Screen-space signed curvature: distinguishes concave crevices (muscle grooves / armor seams)
    // from convex ridges (muscle crowns / beveled armor edges).
    float3 dpdx = ddx(positionWS);
    float3 dpdy = ddy(positionWS);
    float3 dndx = ddx(lighting.normalWS);
    float3 dndy = ddy(lighting.normalWS);
    float metric = max(dot(dpdx, dpdx) + dot(dpdy, dpdy), 1e-6);
    half signedCurvature = clamp((dot(dndx, dpdx) + dot(dndy, dpdy)) / sqrt(metric), -1.0h, 1.0h);
    half cavity = saturate(-signedCurvature * 1.65h);
    half ridge = saturate(signedCurvature * 1.45h);
    half cavityAO = 1.0h - cavity * 0.34h;

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo * lerp(1.0h, 1.06h, ridge * (0.35h + metallic * 0.65h));
    surface.metallic = saturate(metallic);
    surface.specular = lerp(half3(.056h,.058h,.062h), albedo, saturate(metallic * 0.35h));
    // Filter small specular highlights at the gameplay camera distance while preserving crisp mineral micro-glints.
    half variance = min(.10h, length(fwidth(lighting.normalWS)) * .10h);
    surface.smoothness = saturate(smoothness - variance);
    surface.normalTS = half3(0,0,1);
    surface.occlusion = saturate(occlusion * cavityAO);
    surface.emission = emission;
    surface.alpha = 1;

    half3 pbr = UniversalFragmentPBR(lighting, surface).rgb;

    Light mainLight = GetMainLight(lighting.shadowCoord);
    half ndotl = saturate(dot(lighting.normalWS, mainLight.direction));
    half ndotv = saturate(dot(lighting.normalWS, lighting.viewDirectionWS));
    half fresnel = pow(1.0h - ndotv, 3.2h);
    half shadowTerm = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

    // Clearcoat & dual-lobe specular peak for high-grade armor enamel and sculpted muscle crowns
    half3 halfDir = SafeNormalize(mainLight.direction + lighting.viewDirectionWS);
    half ndoth = saturate(dot(lighting.normalWS, halfDir));
    half clearcoat = pow(ndoth, lerp(28.0h, 144.0h, surface.smoothness)) * surface.smoothness * (0.14h + surface.metallic * 0.22h);

    // View-aligned studio fill & rim lobes keep heroes and armor three-dimensional from every camera angle
    half3 studioKeyDir = SafeNormalize(lighting.viewDirectionWS + half3(-0.32h, 0.58h, 0.25h));
    half3 studioHalf = SafeNormalize(studioKeyDir + lighting.viewDirectionWS);
    half studioSpec = pow(saturate(dot(lighting.normalWS, studioHalf)), lerp(16.0h, 72.0h, surface.smoothness)) * surface.smoothness;
    half3 specTint = lerp(half3(0.92h, 0.95h, 1.0h), max(albedo, half3(0.22h, 0.24h, 0.28h)), surface.metallic * 0.65h);

    // Directional micro-crest contrast, bevel ridge highlight, and grazing rim
    pbr += albedo * mainLight.color * (shadowTerm * smoothstep(.42h, .94h, ndotl) * (.095h * surface.occlusion));
    pbr += specTint * mainLight.color * (clearcoat * shadowTerm * surface.occlusion);
    pbr += specTint * (studioSpec * (0.08h + surface.metallic * 0.14h) * surface.occlusion);
    pbr += specTint * (ridge * surface.smoothness * (0.065h + surface.metallic * 0.12h) * surface.occlusion);
    pbr += lerp(albedo, mainLight.color, .48h) * (fresnel * (0.22h + surface.smoothness * 0.78h) * surface.occlusion * (.065h + .075h * shadowTerm));
    return pbr;
}

// Opaque shadow/depth passes have no texture dependency or Lit material-layout dependency.
struct DFDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DFDepthVaryings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 _LightDirection;
float3 _LightPosition;

DFDepthVaryings DFShadowVert(DFDepthAttributes input)
{
    DFDepthVaryings output = (DFDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 direction = normalize(_LightPosition - positionWS);
    #else
        float3 direction = _LightDirection;
    #endif
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction));
    #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
    #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
    #endif
    return output;
}

DFDepthVaryings DFDepthVert(DFDepthAttributes input)
{
    DFDepthVaryings output = (DFDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half4 DFShadowFrag(DFDepthVaryings input) : SV_Target { DFApplyDeathFade(input.positionCS); return 0; }
half4 DFDepthFrag(DFDepthVaryings input) : SV_Target { DFApplyDeathFade(input.positionCS); return input.positionCS.z; }

struct DFNormalVaryings
{
    float4 positionCS : SV_POSITION;
    half3 normalWS : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

DFNormalVaryings DFNormalVert(DFDepthAttributes input)
{
    DFNormalVaryings output = (DFNormalVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 DFNormalFrag(DFNormalVaryings input) : SV_Target
{
    DFApplyDeathFade(input.positionCS);
    half3 normal = normalize(input.normalWS);
    #if defined(_GBUFFER_NORMALS_OCT)
        float2 oct = PackNormalOctQuadEncode(normal);
        half3 packed = PackFloat2To888(saturate(oct * .5 + .5));
        return half4(packed,0);
    #else
        return half4(normal,0);
    #endif
}
#endif
