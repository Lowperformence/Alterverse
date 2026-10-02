#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Editor
{
    public static class SceneryBuildingClearanceRegression
    {
        [MenuItem("Alterverse/Checks/Scenery Building Clearance")]
        public static void Run()
        {
            Bounds building=new(Vector3.zero,new Vector3(10,4,8));
            Check(SceneryBuildingClearance.Overlaps(new Bounds(new Vector3(0,50,0),Vector3.one),building),"Interior must be protected at every height");
            Check(SceneryBuildingClearance.Overlaps(new Bounds(new Vector3(9,0,0),new Vector3(5,1,1)),building),"Full rock extent must be tested, not pivot");
            Check(!SceneryBuildingClearance.Overlaps(new Bounds(new Vector3(9,0,0),Vector3.one),building),"Clear exterior must remain available");
            Check(!SceneryBuildingClearance.Overlaps(new Bounds(new Vector3(7.5f,0,6.5f),Vector3.one),building),"Apron corners must be rounded");
            Bounds turned=SceneryBuildingClearance.TransformBounds(new Bounds(Vector3.zero,new Vector3(8,1,1)),Matrix4x4.TRS(new Vector3(0,0,8),Quaternion.Euler(0,90,0),new Vector3(-1,2,1)));
            Check(SceneryBuildingClearance.Overlaps(turned,building),"Rotated/scaled rock tip must be rejected");
            ArenaVariant previous=ArenaVariantRuntime.Current;
            try
            {
                foreach(ArenaVariant variant in Enum.GetValues(typeof(ArenaVariant)))
                {
                    ArenaVariantRuntime.BeginCoopMatch(variant);
                    var clearance=new SceneryBuildingClearance();
                    Vector2 bunker=FrontierPlanetLayout.Active?FrontierPlanetLayout.BunkerPosition:new Vector2(26.25f,21.5f);
                    for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                        Check(clearance.Blocks(new Bounds(new Vector3(x*bunker.x,20,z*bunker.y),Vector3.one)),"Every mirrored bunker must be protected");
                    Check(!clearance.Blocks(new Bounds(Vector3.zero,Vector3.one)),"Center must remain available");
                }

            }
            finally { ArenaVariantRuntime.BeginCoopMatch(previous); }
            Debug.Log("Scenery building clearance regression passed.");
        }
        static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
#endif
