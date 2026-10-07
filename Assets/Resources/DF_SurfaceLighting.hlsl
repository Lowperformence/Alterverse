#ifndef DF_SURFACE_LIGHTING_INCLUDED
#define DF_SURFACE_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Shared opaque surface response. Keep authored albedo, RGB normals and emission intact.
// Metallic, roughness and reflection probes now use the same BRDF as URP Lit.
half3 DFSurfaceLighting(float3 positionWS, half3 normalWS, half3 albedo,
    half metallic, half smoothness, half occlusion, half3 emission)
{
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
    lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(TransformWorldToHClip(positionWS));
    lighting.shadowMask = half4(1,1,1,1);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.metallic = saturate(metallic);
    surface.specular = half3(.04h,.04h,.04h);
    // Filter small specular highlights at the gameplay camera distance.
    half variance = min(.22h, length(fwidth(lighting.normalWS)) * .20h);
    surface.smoothness = saturate(smoothness - variance);
    surface.normalTS = half3(0,0,1);
    surface.occlusion = saturate(occlusion);
    surface.emission = emission;
    surface.alpha = 1;
    return UniversalFragmentPBR(lighting, surface).rgb;
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
half4 DFShadowFrag(DFDepthVaryings input) : SV_Target { return 0; }
half4 DFDepthFrag(DFDepthVaryings input) : SV_Target { return input.positionCS.z; }

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
