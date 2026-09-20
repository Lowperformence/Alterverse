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
            Check(trenches.Length == 4, $"expected 4 mirrored defense trenches, found {trenches.Length}");

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

                foreach(Transform child in trench.GetComponentsInChildren<Transform>(true))
                    Check(child == null || child.name != "DF_Trench_DamagePerimeter",
                        trench.name + " still contains the removed orange damage-radius ring");

                Vector3 p = trench.transform.position;
                Check(Mathf.Abs(Mathf.Abs(p.x) - 25.25f) < .65f, trench.name + " is not one trench-length farther toward arena center");
                Check(Mathf.Abs(Mathf.Abs(p.z) - 10.75f) < .65f, trench.name + " moved away from the intended left/right base flanks");

                if (trench.Team == 0)
                {
                    zero++;
                    Check(p.x < 0f, trench.name + " is not on ZERO's half");
                }
                else if (trench.Team == 1)
                {
                    lumen++;
                    Check(p.x > 0f, trench.name + " is not on LUMEN's half");
                }
                else Check(false, trench.name + " has an invalid team");
            }

            Check(zero == 2 && lumen == 2, $"expected two trenches per team, got ZERO={zero}, LUMEN={lumen}");

            bool zeroLeft=false,zeroRight=false,lumenLeft=false,lumenRight=false;
            foreach(DefenseTrench trench in trenches)
            {
                Vector3 p=trench.transform.position;
                if(trench.Team==0){zeroLeft|=p.z>0f;zeroRight|=p.z<0f;}
                else if(trench.Team==1){lumenLeft|=p.z<0f;lumenRight|=p.z>0f;}
            }
            Check(zeroLeft&&zeroRight&&lumenLeft&&lumenRight,"one or more base flanks are missing a trench");
            Debug.Log("Defense trench regression passed: two mirrored trenches per team, moved toward arena center, no orange damage ring, indestructible, 5 DPS close-range hazard.");
        }
    }
}
#endif
