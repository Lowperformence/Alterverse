#if UNITY_EDITOR
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class LiveStandingPoseCheck
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Live Mixamo pose")]
        public static void Run()
        {
            var actors = Object.FindObjectsByType<MeleeBasicAttackAnimation>(FindObjectsSortMode.None);
            if (actors.Length == 0)
                Debug.LogWarning("DF: no live hero rig. Enter Play Mode and open a hero preview or match first.");
            foreach (var actor in actors)
                Debug.Log("DF MIXAMO CHECK: " + actor.name + " — " + actor.StandingPoseStatus(), actor);
        }
    }
}
#endif
