#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Shop;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class FrontierPlanetRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Exoplanet layouts and terrain")]
        public static void Run()
        {
            ArenaVariant previous=ArenaVariantRuntime.Current;
            try
            {
                foreach(ArenaVariant arena in new[]{ArenaVariant.GJ504b,ArenaVariant.DiamondCore})
                {
                    ArenaVariantRuntime.BeginCoopMatch(arena);
                    Require(ArenaShopCatalog.For(arena).Variant==arena,"Arena catalog entry missing");
                    Require(ArenaVariantRuntime.CanSelect(arena),"Arena cannot be selected");
                    for(int x=-42;x<=42;x++)for(int z=-36;z<=36;z++)
                    {
                        FrontierPlanetLayout.Sample(x,z,out float height,out float tone,out float dust);
                        FrontierPlanetLayout.Sample(-x,-z,out float opposite,out _,out _);
                        Require(!float.IsNaN(height)&&Mathf.Abs(height)<.20f,"Terrain relief outside safe range");
                        Require(Mathf.Abs(height-opposite)<.0001f,"Terrain is not fair between both teams");
                        Require(tone>=.30f&&tone<=.68f&&dust>=0&&dust<=1,"Invalid material sample");
                        Vector3 position=LunarSurfaceField.Surface(x,z);
                        Require(Mathf.Abs(position.y-(.015f+height))<.0001f,"Shared sampler still uses old geology");
                    }
                    for(int i=0;i<FrontierPlanetLayout.ObstacleCount;i++)
                    {
                        Vector3 p=FrontierPlanetLayout.ObstaclePosition(i);
                        Require(Mathf.Abs(p.x)<=40&&Mathf.Abs(p.z)<=34,"Terrain obstacle outside movement limits");
                        Require(Mathf.Abs(p.z)>12,"Terrain obstacle covers main attack approach");
                        Require(new Vector2(p.x,p.z).magnitude>20,"Terrain obstacle covers objective court");
                    }
                }
                Debug.Log("DIMENSIONS FRONT: both exoplanet catalog, terrain, symmetry, sampler and obstacle checks passed.");
            }
            finally{ArenaVariantRuntime.BeginCoopMatch(previous);}
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Current exoplanet integration (Play Mode)")]
        public static void CheckCurrentMatch()
        {
            Require(Application.isPlaying,"Start an exoplanet match first");
            Require(FrontierPlanetLayout.Active,"Select GJ 504 b or PSR J1719-1438 b");
            GameObject world=GameObject.Find("DF_MatchWorld");Require(world!=null,"Match world missing");
            Require(world.GetComponentInChildren<ProductionLunarGround>()==null,"Inherited lunar terrain still active");
            Require(world.GetComponentsInChildren<PlanetSentinelVisual>().Length>=2,"Dedicated mech guards missing");
            if(ArenaVariantRuntime.IsGJ504b)
                Require(world.GetComponentInChildren<GJ504bArenaLandscape>()!=null,"Independent sand landscape missing");
            else Require(GameObject.Find("DF_DiamondCoreCrystalFloor")!=null,"Independent crystal floor missing");
            Debug.Log("DIMENSIONS FRONT: current exoplanet ground and guard integration passed.");
        }

        static void Require(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("Exoplanet regression: "+message);
        }
    }
}
#endif
