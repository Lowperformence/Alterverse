#if UNITY_EDITOR
using System;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class HeroMovementRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Movement snapshot interpolation")]
        public static void Snapshots()
        {
            // Feed real 12 Hz positions into the production buffer, inspect at 60/120 Hz.
            foreach (int fps in new[] { 60, 120 })
            {
                var buffer = new HeroPresentationBuffer();
                int packet = 0; Vector3 previous = Vector3.zero;
                for (int frame = 0; frame <= fps * 3; frame++)
                {
                    float now = (float)frame / fps;
                    while (packet / 12f <= now)
                    {
                        float at = packet++ / 12f;
                        buffer.Push(Vector3.forward * 6f * at, Quaternion.Euler(0, at * 30f, 0), at, false);
                    }
                    Require(buffer.Evaluate(now, out Vector3 position, out Quaternion rotation), "Missing snapshot pose");
                    float expected = Mathf.Max(0f, now - HeroPresentationBuffer.Delay);
                    Require(Vector3.Distance(position, Vector3.forward * 6f * expected) < .001f, "Packet stair-step at " + fps + "fps");
                    Require(Quaternion.Angle(rotation, Quaternion.Euler(0, expected * 30f, 0)) < .05f, "Yaw interpolation mismatch");
                    if (frame > fps) Require(Mathf.Abs((position - previous).magnitude * fps - 6f) < .002f, "Gait speed pulses between packets");
                    previous = position;
                }
                buffer.Evaluate(10f, out Vector3 stopped, out _);
                Require(Mathf.Abs(stopped.z - 18f) < .001f, "Lost packets invented travel");
                var respawn = new Vector3(20f, 1f, -10f);
                buffer.Push(respawn, Quaternion.identity, 11f, true);
                buffer.Evaluate(11f, out Vector3 relocated, out _);
                Require(relocated == respawn, "Respawn dragged previous position across arena");
                buffer.Push(respawn + Vector3.right, Quaternion.identity, 11f, false);
                buffer.Evaluate(11f, out relocated, out _);
                Require(relocated == respawn + Vector3.right, "Same-frame snapshot replacement failed");
            }
            Debug.Log("DF: production snapshot buffer passed 12Hz to 60/120fps, yaw, packet loss and respawn checks.");
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Movement live foot contacts")]
        public static void LiveFeet()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start Play Mode, move a hero, then run this check.");
            int checkedHeroes = 0;
            foreach (HeroGroundedLocomotion feet in UnityEngine.Object.FindObjectsByType<HeroGroundedLocomotion>(FindObjectsSortMode.None))
            {
                var motion = feet.GetComponent<HeroVisualMotion>();
                if (!feet.isActiveAndEnabled || motion == null || motion.GroundingSuspended) continue;
                Require(feet.Ready, "Visible foot chains missing on " + feet.name);
                Vector3 position = feet.transform.position, scale = feet.transform.localScale;
                Quaternion rotation = feet.transform.rotation;
                feet.FinalizeFeet();
                Require(position == feet.transform.position && rotation == feet.transform.rotation && scale == feet.transform.localScale,
                    "Foot solver modified gameplay root on " + feet.name);
                float error = Mathf.Max(feet.LeftContactError, feet.RightContactError);
                Require(error < .035f, "Stance foot misses world anchor by " + error.ToString("F3") + "m on " + feet.name);
                checkedHeroes++;
                Debug.Log("DF: " + feet.name + " stance error " + error.ToString("F4") + "m, speed " + motion.PlanarSpeed.ToString("F2") + "m/s", feet);
            }
            Require(checkedHeroes > 0, "No active grounded heroes found");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
#endif
