#ifndef DF_DEATH_FADE_INCLUDED
#define DF_DEATH_FADE_INCLUDED
// Renderer property block only. Zero is the untouched appearance of every living object.
float _DFDeathFade;
void DFApplyDeathFade(float4 rasterPosition)
{
    if (_DFDeathFade <= 0.0) return;
    float noise = frac(52.9829189 * frac(dot(floor(rasterPosition.xy), float2(.06711056, .00583715))));
    clip(1.0 - _DFDeathFade - max(noise, .00001));
}
#endif
