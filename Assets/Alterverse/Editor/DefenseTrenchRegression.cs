#if UNITY_EDITOR
using System;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class DefenseTrenchRegression
    {
        private static void Check(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException("Defense trench regression: " + reason);
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Defense trenches (Play mode)")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("Start a match before running the defense trench check.");
                return;
            }

            DefenseTrench[] trenches = UnityEngine.Object.FindObjectsByType<DefenseTrench>();
            Check(trenches.Length == 2, $"expected 2 right-flank trenches, found {trenches.Length}");

            int zero = 0, lumen = 0;
            foreach (DefenseTrench trench in trenches)
            {
                Check(trench != null, "null trench");
                Check(trench.GetComponent<Health>() == null, trench.name + " unexpectedly has Health and can be attacked");
                Check(trench.gameObject.layer == GameplayCollision.Structures, trench.name + " is not on Structures layer");
                Check(Mathf.Abs(trench.DamagePerSecond - 5f) < .001f, trench.name + " DPS is not 5");
                Check(trench.DamageRadius >= 5f && trench.DamageRadius <= 5.75f, trench.name + " close-range radius changed");

                BoxCollider solid = trench.GetComponent<BoxCollider>();
                Check(solid != null && solid.enabled, trench.name + " has no stable gameplay footprint");

                Vector3 p = trench.transform.position;
                Check(Mathf.Abs(Mathf.Abs(p.x) - 30.375f) < .65f, trench.name + " moved away from the healpad-to-bunker midpoint");
                Check(Mathf.Abs(Mathf.Abs(p.z) - 10.75f) < .65f, trench.name + " moved away from the right-flank midpoint");

                if (trench.Team == 0)
                {
                    zero++;
                    Check(p.z < 0f, trench.name + " is not on ZERO's local right flank");
                }
                else if (trench.Team == 1)
                {
                    lumen++;
                    Check(p.z > 0f, trench.name + " is not on LUMEN's local right flank");
                }
                else Check(false, trench.name + " has an invalid team");
            }

            Check(zero == 1 && lumen == 1, $"expected one trench per team, got ZERO={zero}, LUMEN={lumen}");
            Debug.Log("Defense trench regression passed: mirrored right-flank trench per team, indestructible, 5 DPS close-range hazard.");
        }
    }
}
#endif
