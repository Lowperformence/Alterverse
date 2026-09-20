#if UNITY_EDITOR
using System;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class DefenseBunkerRegression
    {
        private static void Check(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException("Defense bunker regression: " + reason);
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Defense bunkers (Play mode)")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Start a match before running the defense bunker check.");
                return;
            }

            DefenseBunker[] bunkers = UnityEngine.Object.FindObjectsByType<DefenseBunker>();
            Check(bunkers.Length == 4, $"expected 4 bunkers, found {bunkers.Length}");

            int zero = 0, lumen = 0;
            foreach (DefenseBunker bunker in bunkers)
            {
                Check(bunker != null, "null bunker");
                Check(bunker.GetComponent<Health>() == null, bunker.name + " unexpectedly has Health and can be attacked");
                Check(bunker.gameObject.layer == GameplayCollision.Structures, bunker.name + " is not on Structures layer");
                Check(Mathf.Abs(bunker.AttackDamage - 5f) < .001f, bunker.name + " damage is not 5");
                Check(bunker.AttackRange >= 15f, bunker.name + " lost its long defensive range");

                Check(bunker.GetComponent<BoxCollider>() == null,
                    bunker.name + " still uses the old sharp box footprint");
                MeshCollider solid = bunker.GetComponent<MeshCollider>();
                Check(solid != null && solid.enabled && solid.convex && solid.sharedMesh != null,
                    bunker.name + " has no rounded convex bunker footprint");
                Check(solid.sharedMesh.vertexCount >= 40,
                    bunker.name + " rounded footprint is too coarse to provide useful slide normals");

                Vector3 p = bunker.transform.position;
                Check(Mathf.Abs(Mathf.Abs(p.x) - 26.25f) < .5f, bunker.name + " is not one bunker-length farther toward arena center");
                Check(Mathf.Abs(Mathf.Abs(p.z) - 21.5f) < .5f, bunker.name + " moved away from the intended left/right base flank");

                if (bunker.Team == 0) zero++;
                else if (bunker.Team == 1) lumen++;
                else Check(false, bunker.name + " has an invalid team");
            }

            Check(zero == 2 && lumen == 2, $"expected 2 bunkers per team, got ZERO={zero}, LUMEN={lumen}");

            bool zeroLeft=false,zeroRight=false,lumenLeft=false,lumenRight=false;
            foreach(DefenseBunker bunker in bunkers)
            {
                Vector3 p=bunker.transform.position;
                if(bunker.Team==0){zeroLeft|=p.z>0f;zeroRight|=p.z<0f;}
                else if(bunker.Team==1){lumenLeft|=p.z<0f;lumenRight|=p.z>0f;}
            }
            Check(zeroLeft&&zeroRight&&lumenLeft&&lumenRight,"one or more base flanks are missing a bunker");
            Debug.Log("Defense bunker regression passed: four indestructible bunkers with rounded convex collision, one on each base flank, 5 damage, long range.");
        }
    }
}
#endif
