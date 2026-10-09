#ifndef DF_HERO_ENERGY_SURFACE_INCLUDED
#define DF_HERO_ENERGY_SURFACE_INCLUDED

// Preserve the previous mesh opacity exactly; modern detail changes RGB only.
half DFEnergyOriginalOpacity(float3 local, half life, half style, half polish)
{
    float flow=dot(local,float3(2.7,1.9,3.1))-life*1.7;
    float stripe=abs(frac(flow)-.5)*2.0;
    float width=max(fwidth(flow)*2.0,.055);
    half filament=1.0-smoothstep(.10,.10+width,stripe);
    return lerp(1.0h,.86h+filament*.14h,polish*saturate(style));
}

// Bounded object-space detail. Derivative filtering keeps small effects calm at the
// gameplay camera distance. No texture, depth fetch, scene colour or global clock.
half DFEnergyFilament(float phase, float thickness)
{
    float distance = abs(frac(phase) - .5) * 2.0;
    float footprint = max(fwidth(phase) * 2.0, .001);
    half resolved = 1.0h - smoothstep(.18, .55, footprint);
    return (1.0h - smoothstep(thickness, thickness + max(footprint, .035), distance)) * resolved;
}

half DFEnergyHex(float2 uv)
{
    const float2 lattice = float2(1.0, 1.7320508);
    float2 a = frac(uv / lattice) * lattice - lattice * .5;
    float2 b = frac((uv - lattice * .5) / lattice) * lattice - lattice * .5;
    float2 cell = dot(a,a) < dot(b,b) ? a : b;
    float edge = max(abs(cell.x), dot(abs(cell), float2(.5, .8660254)));
    float footprint = max(fwidth(edge), .006);
    half gridLine = 1.0h - smoothstep(.028, .028 + footprint, abs(edge - .46));
    half resolved = 1.0h - smoothstep(.16, .40, length(fwidth(uv)));
    return gridLine * resolved;
}

half3 DFEnergySurface(float3 local, float2 surfaceUV, half facing, half heat,
    half3 hue, half life, half style, half quality)
{
    float warp = .22 * sin(dot(local, float3(3.1, 2.4, -1.8)) + life * 4.2);
    float phase = dot(local, float3(2.7, 1.9, 3.1)) + warp - life * 1.7;
    half filament = DFEnergyFilament(phase, .10);
    half release = 1.0h - smoothstep(.0h, .24h, life);
    half3 detail = hue * (filament * .16h + heat * release * .18h);

    // Cheap base detail remains on battery devices and during sustained overload.
    if (quality < .5h) return detail * quality;

    half rim = (1.0h - saturate(facing)) * (1.0h - saturate(facing));
    if (style < 1.5h)
    {
        half body = saturate(1.0h - length(local) * .92h);
        half arcs = DFEnergyFilament(local.y * 3.4 + local.x * local.z * 2.1 - life * .9, .075);
        detail += hue * (body * body * .27h + rim * .20h + arcs * .14h);
    }
    else if (style < 2.5h)
    {
        // Stationary hex cells and a travelling activation band distinguish shields
        // from plasma. Hexes never alter the authored silhouette or warning bounds.
        half cells = DFEnergyHex(surfaceUV * 4.2);
        half activation = DFEnergyFilament(local.y * .85 - life * 1.35, .16);
        detail += hue * (cells * (.16h + activation * .16h) + rim * .12h);
    }
    else if (style > 3.5h)
    {
        // Botanical seed/leaf: central vein and branches, without a shield grid.
        half vein = 1.0h - smoothstep(.018, .018 + max(fwidth(local.x), .015), abs(local.x));
        half branches = DFEnergyFilament(local.z * 4.0 + abs(local.x) * 6.0, .08);
        detail += hue * (vein * .16h + branches * .11h);
    }
    else
    {
        // A narrow hot first beat cools into coloured ribbons instead of a white tile.
        detail += hue * heat * release * .12h;
    }
    return detail * quality;
}
#endif
