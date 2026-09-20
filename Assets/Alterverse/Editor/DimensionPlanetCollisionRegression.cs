#if UNITY_EDITOR
using System;
using Alterverse.Match;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class DimensionPlanetCollisionRegression
    {
        private static void Check(bool value,string reason)
        {
            if(!value)throw new InvalidOperationException("Dimension planet collision regression: "+reason);
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Dimension planet movement blockers (Play mode)")]
        public static void Run()
        {
            if(!Application.isPlaying)
            {
                Debug.LogWarning("Start a match and wait for a dimension planet before running this check.");
                return;
            }

            DimensionPlanet[] planets=UnityEngine.Object.FindObjectsByType<DimensionPlanet>();
            Check(planets.Length>0,"no active dimension planet found");

            foreach(DimensionPlanet planet in planets)
            {
                Collider[] colliders=planet.GetComponentsInChildren<Collider>(true);
                Check(colliders.Length>0,planet.name+" has no query collider");
                foreach(Collider collider in colliders)
                    Check(collider==null||!collider.enabled||collider.isTrigger,
                        planet.name+" still has a solid collider: "+collider.name);
            }

            Debug.Log("Dimension planet collision regression passed: active planet colliders are non-blocking triggers.");
        }
    }
}
#endif
