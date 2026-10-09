using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Bounded, distance-phase locomotion offsets relative to each hero's fitted rest pose.</summary>
    public static class HeroUpperBodyGait
    {
        public struct Pose
        {
            public float hipYaw, chestYaw, chestPitch, chestRoll, neckYaw;
            public float leftShoulder, rightShoulder, leftElbow, rightElbow;
        }
        public static Pose Sample(HeroId hero, float phase, float weight, float run, float forward, float side)
        {
            weight = Mathf.Clamp01(weight); run = Mathf.Clamp01(run);
            float direction = Mathf.Clamp(Mathf.Clamp(forward, -1f, 1f) + Mathf.Clamp(side, -1f, 1f) * .65f, -1f, 1f);
            float step = Mathf.Cos(phase) * weight * direction;
            float heavy = hero == HeroId.Kaelon || hero == HeroId.Nerok ? .80f : 1f;
            float body = hero == HeroId.Nyra ? .35f : heavy;
            float left = Mathf.Lerp(11f, 20f, run), right = left;
            float elbowL = Mathf.Lerp(5f, 10f, run), elbowR = elbowL;
            switch (hero)
            {
                case HeroId.Nyra: left = right = elbowL = elbowR = 0f; break;
                case HeroId.Kaelon: left = elbowL = 0f; right = Mathf.Lerp(6f, 11f, run); elbowR = 4f; break;
                case HeroId.Nerok: right = Mathf.Lerp(6f, 10f, run); elbowR = 4f; break;
                case HeroId.Mira: right = Mathf.Lerp(5f, 9f, run); elbowR = 4f; break;
                case HeroId.Veyr: left = right = Mathf.Lerp(9f, 16f, run); break;
                case HeroId.Rook: left = right = Mathf.Lerp(13f, 24f, run); break;
            }
            var pose = new Pose();
            pose.hipYaw = step * Mathf.Lerp(2.5f, 5f, run) * body;
            pose.chestYaw = -step * Mathf.Lerp(4f, 8f, run) * body;
            pose.chestPitch = weight * body * (run * 2.5f + Mathf.Cos(phase * 2f) * Mathf.Lerp(.45f, .85f, run));
            pose.chestRoll = Mathf.Sin(phase) * weight * body * Mathf.Lerp(.7f, 1.6f, run) +
                Mathf.Clamp(side, -1f, 1f) * weight * body * .8f;
            pose.neckYaw = -(pose.hipYaw * .5f + pose.chestYaw) * .65f;
            // Cycle zero is left-foot contact in the grounded solver: the left arm moves back.
            pose.leftShoulder = step * left; pose.rightShoulder = -step * right;
            // Extra flex is always forward, never alternating into elbow hyperextension.
            pose.leftElbow = weight * elbowL * (.5f + .5f * Mathf.Max(0f, -step));
            pose.rightElbow = weight * elbowR * (.5f + .5f * Mathf.Max(0f, step));
            return pose;
        }
    }
}
