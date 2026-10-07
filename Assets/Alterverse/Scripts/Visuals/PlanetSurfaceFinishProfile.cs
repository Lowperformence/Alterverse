using System;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    /// <summary>Dry soil, wet stone and ice retain distinct finishes under the shared arena lighting.</summary>
    public readonly struct PlanetSurfaceFinishProfile
    {
        public readonly float RoughnessBias,CavityStrength,DetailContrast;
        PlanetSurfaceFinishProfile(float roughness,float cavity,float contrast)
        {RoughnessBias=roughness;CavityStrength=cavity;DetailContrast=contrast;}

        public static PlanetSurfaceFinishProfile For(ArenaVariant planet) => planet switch
        {
            ArenaVariant.Moon => new(.12f,.30f,.34f),
            ArenaVariant.Mars => new(.10f,.25f,.36f),
            ArenaVariant.GreenPlanet => new(.08f,.28f,.26f),
            ArenaVariant.Noxara => new(-.03f,.30f,.30f),
            ArenaVariant.Kryon => new(-.15f,.16f,.18f),
            ArenaVariant.Earth => new(.08f,.32f,.27f),
            ArenaVariant.Pyraxis => new(.12f,.35f,.28f),
            ArenaVariant.Neptune => new(-.14f,.24f,.24f),
            ArenaVariant.Dark00 => new(.10f,.12f,.15f),
            ArenaVariant.DiamondCore => new(-.20f,.12f,.13f),
            ArenaVariant.GJ504b => new(.12f,.25f,.32f),
            _ => throw new ArgumentOutOfRangeException(nameof(planet))
        };
    }
}
