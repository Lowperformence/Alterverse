#ifndef DF_VFX_FLOW_INCLUDED
#define DF_VFX_FLOW_INCLUDED
TEXTURE2D(_DF_VfxFlow);SAMPLER(sampler_DF_VfxFlow);
float _DF_VfxFlowReady;

// Two shared texture reads at higher quality. A neutral fallback leaves every
// core effect visible when assets are missing or the adaptive budget is reduced.
half2 DFVfxFlow(float2 uv,half age,half quality)
{
    if(_DF_VfxFlowReady<.5 || quality<.5h)return 0;
    float2 dx=ddx(uv),dy=ddy(uv);
    half visible=1-smoothstep(.06,.32,max(length(dx),length(dy)));
    if(visible<=0)return 0;
    float2 move=float2(.19,-.11)*age;
    half2 a=SAMPLE_TEXTURE2D_GRAD(_DF_VfxFlow,sampler_DF_VfxFlow,uv+move,dx,dy).rg-.5h;
    float2 q=float2(-uv.y,uv.x)*1.7-move;
    half2 b=SAMPLE_TEXTURE2D_GRAD(_DF_VfxFlow,sampler_DF_VfxFlow,q,
        float2(-dx.y,dx.x)*1.7,float2(-dy.y,dy.x)*1.7).rg-.5h;
    return (a*.7h+b*.3h)*visible*saturate(quality);
}
#endif
