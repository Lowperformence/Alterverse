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
            ArenaVariant.Moon => new(.10f,.42f,.44f),
            ArenaVariant.Mars => new(.08f,.36f,.46f),
            ArenaVariant.GreenPlanet => new(.06f,.38f,.34f),
            ArenaVariant.Noxara => new(-.05f,.40f,.38f),
            ArenaVariant.Kryon => new(-.18f,.24f,.26f),
            ArenaVariant.Earth => new(.06f,.42f,.35f),
            ArenaVariant.Pyraxis => new(.10f,.46f,.36f),
            ArenaVariant.Neptune => new(-.16f,.34f,.32f),
            ArenaVariant.Dark00 => new(.08f,.18f,.20f),
            ArenaVariant.DiamondCore => new(-.22f,.18f,.18f),
            ArenaVariant.GJ504b => new(.10f,.36f,.40f),
            ArenaVariant.Wasp76b => new(-.09f,.38f,.34f),
            ArenaVariant.Trappist1e => new(-.06f,.35f,.32f),
            ArenaVariant.Toi715b => new(.07f,.36f,.38f),
            _ => throw new ArgumentOutOfRangeException(nameof(planet))
        };
    }
}

