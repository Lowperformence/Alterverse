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
            Array variants=Enum.GetValues(typeof(ArenaVariant));
            Check(variants.Length==3,"expected exactly Moon, Mars and GreenPlanet arena variants");
            Check(Array.IndexOf(variants,ArenaVariant.Moon)>=0,"Moon arena missing");
            Check(Array.IndexOf(variants,ArenaVariant.Mars)>=0,"Mars arena missing");
            Check(Array.IndexOf(variants,ArenaVariant.GreenPlanet)>=0,"Green Planet arena missing");

            // Player and bot bounds use slightly different historical safety margins.
            // Both now share the same rounded-corner projection.
            CheckRoundedBounds(40f,33f,"bot");
            CheckRoundedBounds(41f,35f,"player");

            if(!Application.isPlaying)
            {
                Debug.Log("Map rounded collision math passed for Moon, Mars and Green Planet. Start a match to scan live structure colliders.");
                return;
            }

            int roundedBunkers=0,roundedTrenches=0,roundedCores=0;
            Collider[] colliders=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach(Collider collider in colliders)
            {
                if(collider==null||!collider.enabled||collider.isTrigger)continue;
                if(collider.gameObject.layer!=GameplayCollision.Structures)continue;

                // Any enabled blocking BoxCollider on the Structures layer reintroduces a sharp
                // map corner that can catch capsule-driven bots.
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

            Debug.Log("Map rounded collision regression passed: Moon, Mars and Green Planet share rounded arena corners; all live blocking structures are rounded and no sharp Structure BoxCollider remains.");
        }

        private static void CheckRoundedBounds(float halfWidth,float halfHeight,string label)
        {
            Vector3 rawCorner=new(halfWidth,0f,halfHeight);
            Vector3 rounded=GameplayCollision.ClampToRoundedArena(rawCorner,halfWidth,halfHeight);
            Check(rounded.x<halfWidth-.05f&&rounded.z<halfHeight-.05f,
                label+" arena corner is still rectangular");
            Check(GameplayCollision.ContainsRoundedArena(rounded,halfWidth,halfHeight),
                label+" rounded corner projection lies outside the arena");

            Vector3 straightEdge=new(halfWidth,0f,0f);
            Vector3 edgeResult=GameplayCollision.ClampToRoundedArena(straightEdge,halfWidth,halfHeight);
            Check(Vector3.Distance(straightEdge,edgeResult)<.001f,
                label+" straight arena edge changed while rounding corners");
        }
    }
}
#endif
