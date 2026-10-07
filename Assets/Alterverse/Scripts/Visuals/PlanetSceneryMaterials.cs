using System;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    public enum SceneryFinish { Stone, Metal, Bark, Leaf, Ice, WetStone, Soil }

    /// <summary>Surface detail on existing, owned scenery materials; no renderer scan or texture baking.</summary>
    public static class PlanetSceneryMaterials
    {
        public static SceneryFinish LandscapeFinish(ArenaVariant planet,int layer)
        {
            switch(planet)
            {
                case ArenaVariant.Earth: return layer==1?SceneryFinish.Bark:layer>=2?SceneryFinish.Leaf:SceneryFinish.Stone;
                case ArenaVariant.GreenPlanet: return layer==3?SceneryFinish.Leaf:SceneryFinish.Stone;
                case ArenaVariant.Noxara: return layer==3?SceneryFinish.Leaf:SceneryFinish.Stone;
                case ArenaVariant.Kryon: return layer==0?SceneryFinish.Stone:SceneryFinish.Ice;
                case ArenaVariant.Neptune: return SceneryFinish.WetStone;
                case ArenaVariant.Dark00: return SceneryFinish.Metal;
                case ArenaVariant.DiamondCore: return layer==0?SceneryFinish.Stone:SceneryFinish.Ice;
                case ArenaVariant.Moon:
                case ArenaVariant.Mars:
                case ArenaVariant.Pyraxis:
                case ArenaVariant.GJ504b: return SceneryFinish.Stone;
                default: throw new ArgumentOutOfRangeException(nameof(planet));
            }
        }

        public static void Apply(Material material,ArenaVariant planet,SceneryFinish finish)
        {
            if(material==null)return;
            Shader shader=Resources.Load<Shader>("DF_ScenerySurface");
            if(shader==null||!shader.isSupported)return; // Retain the valid original shader if unsupported.
            string texturePath=finish==SceneryFinish.Soil?"PlanetSurfaces/"+planet+"/Ground":
                finish==SceneryFinish.Stone||finish==SceneryFinish.WetStone
                ?"PlanetSurfaces/"+planet+"/Stone":"SceneryFinishes/"+finish;
            Texture2D texture=Resources.Load<Texture2D>(texturePath);
            if(texture==null){Debug.LogWarning("Scenery finish missing: "+texturePath);return;}
            Color albedo=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
            Color emission=material.HasProperty("_EmissionColor")?material.GetColor("_EmissionColor"):Color.black;
            float metal=material.HasProperty("_Metallic")?material.GetFloat("_Metallic"):0f;
            float smooth=material.HasProperty("_Smoothness")?material.GetFloat("_Smoothness"):.2f;
            material.shader=shader;
            material.SetColor("_BaseColor",albedo);material.SetColor("_EmissionColor",emission);
            material.SetFloat("_Metallic",metal);material.SetFloat("_Smoothness",smooth);
            material.SetTexture("_DetailData",texture);
            material.SetFloat("_DetailScale",finish switch {
                SceneryFinish.Metal=>1.15f,SceneryFinish.Bark=>1.4f,SceneryFinish.Leaf=>2.6f,
                SceneryFinish.Ice=>.72f,SceneryFinish.Soil=>.65f,_=>planet==ArenaVariant.Mars?.55f:.42f});
            material.SetFloat("_Relief",finish switch {
                SceneryFinish.Metal=>.055f,SceneryFinish.Bark=>.32f,SceneryFinish.Leaf=>.15f,
                SceneryFinish.Ice=>.075f,SceneryFinish.Soil=>.16f,_=>.28f});
            material.SetFloat("_ValueContrast",finish==SceneryFinish.Metal?.10f:finish==SceneryFinish.Ice?.08f:.25f);
            material.SetFloat("_RoughnessWeight",finish==SceneryFinish.Metal?.18f:finish==SceneryFinish.Ice?.18f:.35f);
            material.SetFloat("_Sheen",finish==SceneryFinish.Leaf?.07f:0f);
            material.SetFloat("_RoughnessBias",finish==SceneryFinish.Stone||finish==SceneryFinish.WetStone||finish==SceneryFinish.Soil
                ?PlanetSurfaceFinishProfile.For(planet).RoughnessBias:0f);
            if(finish==SceneryFinish.WetStone)material.SetFloat("_Smoothness",Mathf.Max(smooth,.42f));
            material.enableInstancing=true;
        }
    }
}
