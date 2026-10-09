#if UNITY_EDITOR
using System;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class HeroUpperBodyRegression
    {
        [MenuItem("DIMENSIONS FRONT/Checks/Upper body gait curves")]
        public static void Curves()
        {
            foreach (HeroId hero in Enum.GetValues(typeof(HeroId)))
            {
                foreach (float run in new[] { 0f, 1f })
                {
                    for (int frame = 0; frame <= 120; frame++)
                    {
                        float phase = frame / 120f * Mathf.PI * 2f;
                        var pose = HeroUpperBodyGait.Sample(hero, phase, 1f, run, 1f, 0f);
                        Require(pose.hipYaw * pose.chestYaw <= .00001f, "Chest/hip counterphase missing on " + hero);
                        Require(pose.leftShoulder * pose.rightShoulder <= .00001f, "Arms march together on " + hero);
                        Require(Mathf.Abs(pose.leftShoulder) <= 24.01f && Mathf.Abs(pose.rightShoulder) <= 24.01f, "Unbounded shoulder swing");
                        Require(pose.leftElbow >= 0f && pose.rightElbow >= 0f, "Elbow hyperextension input");
                        if (hero == HeroId.Nyra) Require(pose.leftShoulder == 0f && pose.rightShoulder == 0f && pose.leftElbow == 0f && pose.rightElbow == 0f, "Rifle grip receives gait pump");
                        if (hero == HeroId.Kaelon) Require(pose.leftShoulder == 0f && pose.leftElbow == 0f, "Left shield arm changed");
                        var idle = HeroUpperBodyGait.Sample(hero, phase, 0f, run, 1f, 0f);
                        Require(idle.hipYaw == 0f && idle.chestYaw == 0f && idle.chestPitch == 0f && idle.chestRoll == 0f && idle.leftShoulder == 0f && idle.rightShoulder == 0f, "Idle retains a gait offset");
                    }
                }
            }
            Debug.Log("DF: all eight upper-body gait profiles passed counterphase, limits, stop and weapon constraints.");
        }

        private static HeroVisualMotion observed;
        private static Transform chest, armL, armR;
        private static Quaternion initialChest, initialL, initialR;
        private static float chestChange, leftChange, rightChange, phaseCoverage, lastPhase;
        private static double until;
        private static bool previousEligible, captured;

        [MenuItem("DIMENSIONS FRONT/Checks/Record selected hero upper body for 10 seconds")]
        public static void RecordVisibleBones()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Start Play Mode and select a hero before recording.");
            observed = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<HeroVisualMotion>() : null;
            if (observed == null) throw new InvalidOperationException("Select a gameplay hero with HeroVisualMotion.");
            Transform root = observed.transform.Find("DF_VisualRoot/DF_AuthoredModel");
            if (root == null) throw new InvalidOperationException("Selected hero has no visible authored model.");
            chest = armL = armR = null;
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "Chest") chest = bone;
                if (bone.name == "UpperArmL") armL = bone;
                if (bone.name == "UpperArmR") armR = bone;
            }
            if (chest == null || armL == null || armR == null) throw new InvalidOperationException("Visible upper-body chain is incomplete.");
            chestChange = leftChange = rightChange = phaseCoverage = 0f; previousEligible = captured = false;
            until = EditorApplication.timeSinceStartup + 10;
            EditorApplication.update -= Observe; EditorApplication.update += Observe;
            Debug.Log("DF: recording the final visible bones for 10 seconds. Run continuously without attacking.");
        }
        private static void Observe()
        {
            if (!EditorApplication.isPlaying || observed == null) { EditorApplication.update -= Observe; return; }
            if (chest == null || armL == null || armR == null) { EditorApplication.update -= Observe; Debug.LogWarning("DF: hero model changed during recording; repeat the check."); return; }
            bool eligible = observed.isActiveAndEnabled && Time.timeScale > 0f && observed.PlanarSpeed > 1.5f && observed.LocomotionWeight > .75f &&
                !observed.GroundingSuspended && !observed.ArmActionActive && !observed.AbilityActionActive;
            if (eligible)
            {
                if (!captured) { initialChest = chest.localRotation; initialL = armL.localRotation; initialR = armR.localRotation; captured = true; }
                chestChange = Mathf.Max(chestChange, Quaternion.Angle(initialChest, chest.localRotation));
                leftChange = Mathf.Max(leftChange, Quaternion.Angle(initialL, armL.localRotation));
                rightChange = Mathf.Max(rightChange, Quaternion.Angle(initialR, armR.localRotation));
                if (previousEligible) phaseCoverage += Mathf.Max(0f, Mathf.DeltaAngle(lastPhase * Mathf.Rad2Deg, observed.GaitPhase * Mathf.Rad2Deg) * Mathf.Deg2Rad);
                lastPhase = observed.GaitPhase;
            }
            previousEligible = eligible;
            if (EditorApplication.timeSinceStartup < until) return;
            EditorApplication.update -= Observe;
            Require(phaseCoverage >= Mathf.PI * 2f, "Recording did not cover a full moving gait cycle; repeat with continuous running.");
            Require(chestChange > .75f, "Final visible chest is still rigid.");
            HeroId hero = observed.GetComponent<HeroIdentity>().Hero;
            if (hero != HeroId.Nyra) Require(rightChange > 5f, "Final visible right arm gait was overwritten.");
            if (hero != HeroId.Nyra && hero != HeroId.Kaelon) Require(leftChange > 5f, "Final visible left arm gait was overwritten.");
            Debug.Log("DF: visible upper-body recording passed on " + hero + ": chest " + chestChange.ToString("F1") + "°, left " + leftChange.ToString("F1") + "°, right " + rightChange.ToString("F1") + "°.", observed);
        }
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    }
}
#endif
