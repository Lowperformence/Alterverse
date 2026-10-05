#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class SoftTargetingRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Soft target hero priority (Play Mode)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Enter Play Mode before running the targeting check.");
            var objects = new List<GameObject>();
            Vector3 origin = new Vector3(10000f, 0f, 10000f);
            try
            {
                Health actor = Spawn("TargetCheckActor", 0, Vector3.zero, false);
                var targeting = actor.gameObject.AddComponent<SoftTargeting>();
                Health soldier = Spawn("TargetCheckNonHero", 1, Vector3.forward, false);
                Check(targeting.Acquire(5f, Vector3.forward) == soldier, "non-hero fallback");
                Health hero = Spawn("TargetCheckHero", 1, Vector3.back * 4f, true);
                Check(targeting.Acquire(5f, Vector3.forward) == hero, "hero behind must replace sticky non-hero");
                hero.transform.position = origin + Vector3.right * 4f;
                targeting.Release();
                Check(targeting.Acquire(5f, Vector3.forward) == hero, "side hero must beat closer non-hero");
                hero.transform.position = origin + Vector3.back * 50f;
                Check(targeting.Acquire(5f, Vector3.forward) == soldier, "out-of-range hero must not block fallback");
                hero.transform.position = origin + Vector3.back * 4f;
                hero.GetComponent<TeamMember>().Initialize(0);
                targeting.Release();
                Check(targeting.Acquire(5f, Vector3.forward) == soldier, "friendly hero excluded");
                hero.GetComponent<TeamMember>().Initialize(1);
                hero.enabled = false;
                targeting.Release();
                Check(targeting.Acquire(5f, Vector3.forward) == soldier, "disabled hero excluded");
                hero.enabled = true;
                targeting.Release();
                Check(targeting.Acquire(5f, Vector3.forward, false) == soldier, "explicit non-priority mode preserved");
                Check(targeting.Acquire(5f, Vector3.forward, true) == hero, "priority re-enabled replaces sticky non-hero");
                Debug.Log("Soft target checks passed: hero priority, stickiness, range, teams and fallback.");
            }
            finally
            {
                foreach (GameObject obj in objects) UnityEngine.Object.DestroyImmediate(obj);
            }

            Health Spawn(string name, int team, Vector3 offset, bool hero)
            {
                var obj = new GameObject(name); objects.Add(obj);
                obj.transform.position = origin + offset;
                obj.AddComponent<TeamMember>().Initialize(team);
                Health health = obj.AddComponent<Health>();
                if (hero) obj.AddComponent<HeroIdentity>();
                return health;
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Soft target regression: " + message);
        }
    }
}
#endif
