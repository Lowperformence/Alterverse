#if UNITY_EDITOR
using System;
using Alterverse.Combat;
using Alterverse.Match;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class MapCornerCollisionRegression
    {
        private static void Check(bool value,string reason)
        {
            if(!value)throw new InvalidOperationException("Map corner collision regression: "+reason);
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Map rounded collision (Play mode)")]
        public static void Run()
        {
            ArenaVariant[] variants=(ArenaVariant[])Enum.GetValues(typeof(ArenaVariant));
            Check(variants.Length>0,"no arena variants registered");
            Check(Array.IndexOf(variants,ArenaVariant.Moon)>=0,"Moon arena missing");
            Check(Array.IndexOf(variants,ArenaVariant.Mars)>=0,"Mars arena missing");
            Check(Array.IndexOf(variants,ArenaVariant.GreenPlanet)>=0,"Green Planet arena missing");

            // Player and bot bounds use slightly different historical safety margins.
            // Both now share the same rounded-corner projection.
            CheckRoundedBounds(40f,33f,"bot");
            CheckRoundedBounds(41f,35f,"player");

            if(!Application.isPlaying)
            {
                Debug.Log("Map rounded collision math passed for all registered arena variants. Start a match to scan live structure colliders.");
                return;
            }

            int roundedBunkers=0,roundedTrenches=0,roundedCores=0;
            Collider[] colliders=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach(Collider collider in colliders)
            {
                if(collider==null||!collider.enabled||collider.isTrigger)continue;
                bool mapBlockingLayer=collider.gameObject.layer==GameplayCollision.Structures||
                    collider.gameObject.layer==GameplayCollision.Ground;
                if(!mapBlockingLayer)continue;

                // Any enabled blocking BoxCollider on map collision layers reintroduces a sharp
                // corner that can catch capsule-driven bots.
                Check(collider is not BoxCollider,collider.name+" still has a sharp blocking BoxCollider");

                DefenseBunker bunker=collider.GetComponentInParent<DefenseBunker>();
                if(bunker!=null)
                {
                    Check(collider is MeshCollider bunkerMesh&&bunkerMesh.convex&&bunkerMesh.sharedMesh!=null,
                        bunker.name+" is not using rounded convex collision");
                    roundedBunkers++;
                    continue;
                }

                DefenseTrench trench=collider.GetComponentInParent<DefenseTrench>();
                if(trench!=null)
                {
                    Check(collider is MeshCollider trenchMesh&&trenchMesh.convex&&trenchMesh.sharedMesh!=null,
                        trench.name+" is not using rounded convex collision");
                    roundedTrenches++;
                    continue;
                }

                RealityCore core=collider.GetComponentInParent<RealityCore>();
                if(core!=null)
                {
                    Check(collider is CapsuleCollider,core.name+" core collision lost its rounded capsule");
                    roundedCores++;
                }
            }

            Check(roundedBunkers==4,$"expected 4 rounded bunkers, found {roundedBunkers}");
            Check(roundedTrenches==4,$"expected 4 rounded trenches, found {roundedTrenches}");
            Check(roundedCores==2,$"expected 2 rounded reality cores, found {roundedCores}");

            GameObject floor=GameObject.Find("DF_ArenaFloor");
            Check(floor!=null,"arena floor missing");
            MeshCollider floorCollider=floor.GetComponent<MeshCollider>();
            Check(floor.layer==GameplayCollision.Ground&&floorCollider!=null&&floorCollider.enabled&&floorCollider.convex,
                "arena floor is not using rounded mesh collision");
            Check(floor.GetComponent<BoxCollider>()==null,"arena floor still has square box collision");

            Debug.Log("Map rounded collision regression passed: all registered arena variants share rounded arena corners; floor, bunkers, trenches and cores are rounded and no sharp blocking BoxCollider remains.");
        }

        private static void CheckRoundedBounds(float halfWidth,float halfHeight,string label)
        {
            float worldHalfWidth=halfWidth*ArenaLayout.LengthScale;
            Vector3 rawCorner=new(worldHalfWidth,0f,halfHeight);
            Vector3 rounded=GameplayCollision.ClampToRoundedArena(rawCorner,halfWidth,halfHeight);
            Check(rounded.x<worldHalfWidth-.05f&&rounded.z<halfHeight-.05f,
                label+" arena corner is still rectangular");
            Check(GameplayCollision.ContainsRoundedArena(rounded,halfWidth,halfHeight),
                label+" rounded corner projection lies outside the arena");

            Vector3 straightEdge=new(worldHalfWidth,0f,0f);
            Vector3 edgeResult=GameplayCollision.ClampToRoundedArena(straightEdge,halfWidth,halfHeight);
            Check(Vector3.Distance(straightEdge,edgeResult)<.001f,
                label+" straight arena edge changed while rounding corners");
        }
    }
}
#endif


