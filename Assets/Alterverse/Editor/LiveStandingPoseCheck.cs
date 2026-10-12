#if UNITY_EDITOR
using Alterverse.Visuals;
using Alterverse.Characters;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class LiveStandingPoseCheck
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Auren marked guard")]
        public static void AurenGuard()
        {
            foreach (var identity in UnityEngine.Object.FindObjectsByType<HeroIdentity>(FindObjectsSortMode.None))
            {
                if (identity.Hero != HeroId.Kaelon) continue;
                var pose = identity.GetComponent<MeleeBasicAttackAnimation>();
                Transform[] parts = identity.GetComponentsInChildren<Transform>(true);
                Transform head = System.Array.Find(parts, t => t.name == "Head");
                Transform hand = System.Array.Find(parts, t => t.name == "HandR");
                Transform grip = System.Array.Find(parts, t => t.name == "DF_TankHammerGrip");
                Transform top = System.Array.Find(parts, t => t.name == "DF_TankHammerTop");
                Transform model = System.Array.Find(parts, t => t.name == "DF_AuthoredModel");
                if (head == null || hand == null || grip == null || top == null || model == null)
                {
                    Debug.LogWarning("AUREN GUARD v3 CHECK: missing live head, hand, model or hammer markers on " + identity.name, identity);
                    continue;
                }
                Vector3 axis = identity.transform.InverseTransformDirection((top.position - grip.position).normalized);
                Vector3 point = model.InverseTransformPoint(top.position) - model.InverseTransformPoint(head.position);
                Debug.Log("AUREN GUARD v3 CHECK: " + identity.name + "; pose=" + (pose != null ? pose.StandingPoseStatus() : "missing") +
                    "; shaft(actor)=" + axis.ToString("F3") + "; top minus head(model)=" + point.ToString("F3") +
                    "; grip follows hand=" + grip.IsChildOf(hand), identity);
            }
        }

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
