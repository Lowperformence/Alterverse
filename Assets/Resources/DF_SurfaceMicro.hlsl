#ifndef DF_SURFACE_MICRO_INCLUDED
#define DF_SURFACE_MICRO_INCLUDED

// Numerical data shares the established linear RG slope / B roughness / A value contract.
// Fade below the pixel footprint rather than sharpening grains into distant shimmer.
half DFMicroVisibility(float3 positionWS,float frequency)
{
    float footprint=max(length(ddx(positionWS)),length(ddy(positionWS)))*frequency;
    return 1-smoothstep(.06,.30,footprint);
}

half4 DFMicroTriplanar(TEXTURE2D_PARAM(textureData,textureSampler),float3 p,half3 n,float3 dx,float3 dy,out half3 gradient)
{
    half3 weights=pow(abs(n),4);
    weights/=max(dot(weights,half3(1,1,1)),.001h);
    half4 x=SAMPLE_TEXTURE2D_GRAD(textureData,textureSampler,p.zy,dx.zy,dy.zy);
    half4 y=SAMPLE_TEXTURE2D_GRAD(textureData,textureSampler,p.xz,dx.xz,dy.xz);
    half4 z=SAMPLE_TEXTURE2D_GRAD(textureData,textureSampler,p.xy,dx.xy,dy.xy);
    gradient=(half3(0,x.g-.5h,x.r-.5h)*weights.x+
        half3(y.r-.5h,0,y.g-.5h)*weights.y+
        half3(z.r-.5h,z.g-.5h,0)*weights.z)*2;
    return x*weights.x+y*weights.y+z*weights.z;
}
#endif
