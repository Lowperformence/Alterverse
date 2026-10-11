#ifndef DF_MATERIAL_FINISH_INCLUDED
#define DF_MATERIAL_FINISH_INCLUDED
#include "Assets/Resources/DF_SurfaceMicro.hlsl"

TEXTURE2D(_DF_MetalFinish);SAMPLER(sampler_DF_MetalFinish);
TEXTURE2D(_DF_FabricFinish);SAMPLER(sampler_DF_FabricFinish);
float _DF_FinishReady;

// Object/rest coordinates keep brush and weave attached to moving visual shells.
// One shared 256px data map, three triplanar taps, no per-pixel procedural noise.
void DFApplyBakedFinish(TEXTURE2D_PARAM(dataMap,dataSampler),float3 rest,
    half3 normalWS,float scale,half strength,half roughnessWeight,
    inout half3 normal,inout half3 albedo,inout half smoothness)
{
    if(strength<=0)return;
    float3 p=rest*scale;
    float3 dx=ddx(p),dy=ddy(p);
    half visibility=1-smoothstep(.05,.28,max(length(dx),length(dy)));
    if(visibility<=0)return;
    // Row multiplication is the inverse normal transform under non-uniform scale.
    half3 normalOS=normalize(mul(normalWS,(float3x3)unity_ObjectToWorld));
    half3 gradientOS;
    half4 data=DFMicroTriplanar(TEXTURE2D_ARGS(dataMap,dataSampler),p,normalOS,dx,dy,gradientOS);
    half basisLength=max(length(TransformObjectToWorldNormal(normalOS,false)),.001h);
    half3 gradientWS=TransformObjectToWorldNormal(gradientOS,false)/basisLength;
    gradientWS-=normalWS*dot(normalWS,gradientWS);
    normal=normalize(normal-gradientWS*(strength*visibility));
    albedo*=1+(data.a-.5h)*(.12h*visibility);
    smoothness=saturate(lerp(smoothness,1-data.b,roughnessWeight*visibility));
}

void DFApplySharedFinish(float3 rest,half3 normalWS,half metal,half energy,
    inout half3 normal,inout half3 albedo,inout half smoothness)
{
    if(_DF_FinishReady<.5 || energy>.5h)return;
    if(metal>.35h)
        DFApplyBakedFinish(TEXTURE2D_ARGS(_DF_MetalFinish,sampler_DF_MetalFinish),
            rest,normalWS,2.0h,.055h,.28h,normal,albedo,smoothness);
    else if(smoothness<.30h)
        DFApplyBakedFinish(TEXTURE2D_ARGS(_DF_FabricFinish,sampler_DF_FabricFinish),
            rest,normalWS,4.0h,.06h,.25h,normal,albedo,smoothness);
}
#endif
