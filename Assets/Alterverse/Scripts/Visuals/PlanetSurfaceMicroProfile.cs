using System;
using Alterverse.Match;

namespace Alterverse.Visuals
{
    /// <summary>Material-specific microstructure; existing planet geology/palette stays authoritative.</summary>
    public readonly struct PlanetSurfaceMicroProfile
    {
        public readonly string TextureName;
        public readonly float Scale, Relief, Contrast;
        PlanetSurfaceMicroProfile(string texture,float scale,float relief,float contrast)
        {TextureName=texture;Scale=scale;Relief=relief;Contrast=contrast;}

        public static PlanetSurfaceMicroProfile Ground(ArenaVariant planet)
        {
            switch(planet)
            {
                case ArenaVariant.Mars: case ArenaVariant.GJ504b: return new PlanetSurfaceMicroProfile("Sand",2.1f,.11f,.24f);
                case ArenaVariant.Earth: case ArenaVariant.GreenPlanet: return new PlanetSurfaceMicroProfile("Soil",1.6f,.13f,.32f);
                case ArenaVariant.Moon: return new PlanetSurfaceMicroProfile("Sand",2.4f,.075f,.18f);
                case ArenaVariant.Kryon: case ArenaVariant.DiamondCore: return new PlanetSurfaceMicroProfile(null,0,0,0);
                case ArenaVariant.Neptune: return new PlanetSurfaceMicroProfile("Stone",1.8f,.045f,.12f);
                case ArenaVariant.Dark00: return new PlanetSurfaceMicroProfile("Stone",1.8f,.055f,.12f);
                case ArenaVariant.Noxara: case ArenaVariant.Pyraxis: return new PlanetSurfaceMicroProfile("Stone",1.8f,.085f,.20f);
                default: throw new ArgumentOutOfRangeException(nameof(planet));
            }
        }

        public static PlanetSurfaceMicroProfile Scenery(SceneryFinish finish)
        {
            switch(finish)
            {
                case SceneryFinish.Stone: return new PlanetSurfaceMicroProfile("Stone",1.8f,.10f,.22f);
                case SceneryFinish.WetStone: return new PlanetSurfaceMicroProfile("Stone",1.8f,.05f,.14f);
                case SceneryFinish.Soil: return new PlanetSurfaceMicroProfile("Soil",1.6f,.13f,.32f);
                case SceneryFinish.Concrete: return new PlanetSurfaceMicroProfile("Concrete",2.0f,.085f,.28f);
                case SceneryFinish.Metal: case SceneryFinish.Bark: case SceneryFinish.Leaf: case SceneryFinish.Ice:
                    return new PlanetSurfaceMicroProfile(null,0,0,0);
                default: throw new ArgumentOutOfRangeException(nameof(finish));
            }
        }
    }
}
