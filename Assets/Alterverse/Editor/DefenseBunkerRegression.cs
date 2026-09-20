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

                BoxCollider solid = bunker.GetComponent<BoxCollider>();
                Check(solid != null && solid.enabled, bunker.name + " has no solid bunker footprint");

                Vector3 p = bunker.transform.position;
                Check(Mathf.Abs(Mathf.Abs(p.x) - 33.25f) < .5f, bunker.name + " moved away from the core flank");
                Check(Mathf.Abs(Mathf.Abs(p.z) - 21.5f) < .5f, bunker.name + " moved away from the map-side flank");

                if (bunker.Team == 0) zero++;
                else if (bunker.Team == 1) lumen++;
                else Check(false, bunker.name + " has an invalid team");
            }

            Check(zero == 2 && lumen == 2, $"expected 2 bunkers per team, got ZERO={zero}, LUMEN={lumen}");
            Debug.Log("Defense bunker regression passed: four indestructible flank bunkers, 5 damage, long range, two per team.");
        }
    }
}
#endif
