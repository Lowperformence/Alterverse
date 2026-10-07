#if UNITY_EDITOR
using System;
using System.Reflection;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class GaitPresentationRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Gait contact continuity")]
        public static void RunChecks()
        {
            MethodInfo evaluate = typeof(HeroVisualMotion).GetMethod("EvaluateRunLeg", BindingFlags.NonPublic | BindingFlags.Static);
            if (evaluate == null) throw new InvalidOperationException("Gait evaluator missing.");
            int samples = 0;
            foreach (bool imported in new[] { false, true })
            foreach (float weight in new[] { 0f, .1f, .35f, .7f, 1f })
            foreach (float stance in new[] { .56f, .61f, .66f })
            foreach (float run in new[] { 0f, .5f, 1f })
            {
                float hipRange = Mathf.Lerp(22f, 38f, run) * weight;
                float kneeRange = Mathf.Lerp(30f, 52f, run) * weight;
                Vector3 Pose(float cycle)
                {
                    object[] args = { cycle, hipRange, kneeRange, weight, stance, 0f, 0f, 0f, imported };
                    evaluate.Invoke(null, args);
                    return new Vector3((float)args[5], (float)args[6], (float)args[7]);
                }
                // Include every piecewise seam, not only the stride wrap.
                foreach (float seam in new[] { 0f, stance * .12f, stance * .14f, stance * .76f,
                    stance * .78f, stance, stance + (1f - stance) * .22f, stance + (1f - stance) * .78f })
                {
                    Vector3 before = Pose(seam - .000001f), after = Pose(seam + .000001f);
                    Require((before - after).magnitude < .02f, "A gait joint jumps at contact/recovery.");
                }
                for (int i = 0; i < 1000; i++)
                {
                    float cycle = i / 1000f;
                    Vector3 pose = Pose(cycle);
                    Require(!float.IsNaN(pose.x) && !float.IsNaN(pose.y) && !float.IsNaN(pose.z), "Invalid gait pose.");
                    Require(Mathf.Abs(pose.x) <= hipRange + .001f && pose.y >= -.001f &&
                        pose.y <= Mathf.Max(8f * weight, kneeRange * weight + 2f * weight) + .001f && Mathf.Abs(pose.z) <= 60.001f, "Joint range exceeded.");
                    if (weight == 0f) Require(pose.sqrMagnitude < .000001f, "Idle legs did not settle.");
                    float u = cycle / stance;
                    if (u >= .12f && u <= .76f)
                        Require(Mathf.Abs(pose.x + pose.y + pose.z) < .001f, "Loaded sole tilts during a blended start/stop.");
                    samples++;
                }
            }
            Debug.Log("DF: gait contact continuity passed (" + samples + " samples). Visual, physics, multiplayer and Android acceptance remain separate checks.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
