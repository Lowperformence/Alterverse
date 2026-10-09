using Alterverse.Characters;
using Alterverse.Combat;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Final visible leg pose from free authored directional clips, followed by contact IK.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroLocomotionAnimation : MonoBehaviour
    {
        private Transform model;
        private readonly Transform[] bones = new Transform[6];
        private readonly Quaternion[] anatomicalOffset = new Quaternion[6];
        private readonly Quaternion[] pose = new Quaternion[6];
        private readonly Vector3[] restFeet = new Vector3[2];
        private readonly float[] contacts = new float[2];
        private readonly bool[] contactState = new bool[2];
        private bool contactStateReady;
        private readonly LocomotionAnimationLibrary.Clip[] clips = new LocomotionAnimationLibrary.Clip[5];
        private readonly float[] weights = new float[5];
        private readonly float[] strides = new float[5];
        private HeroVisualMotion motion;
        private Health health;
        private float legLength;
        private float pelvisHeight;
        private int sampledFrame = -1;
        private int appliedLegFrame = -1;
        public bool Ready { get; private set; }
        public bool Active => Ready && isActiveAndEnabled && motion != null && motion.isActiveAndEnabled &&
            !motion.GroundingSuspended && (health == null || !health.IsDead) && Time.timeScale > 0f &&
            motion.LocomotionWeight > .002f;
        public static HeroLocomotionAnimation For(GameObject actor, Transform imported)
        {
            var component = actor.GetComponent<HeroLocomotionAnimation>() ?? actor.AddComponent<HeroLocomotionAnimation>();
            component.InitializeRig(imported);
            return component;
        }
        public void InitializeRig(Transform imported)
        {
            if (Ready || imported == null || !LocomotionAnimationLibrary.Ready) return;
            model = imported; health = GetComponent<Health>(); motion = GetComponent<HeroVisualMotion>();
            var all = imported.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < 6; i++)
            {
                foreach (var candidate in all) if (candidate.name == LocomotionAnimationLibrary.Bones[i]) { bones[i] = candidate; break; }
                if (bones[i] == null) { Debug.LogError("DF: authored locomotion bone missing: " + LocomotionAnimationLibrary.Bones[i], this); return; }
            }
            legLength = (Vector3.Distance(bones[0].position, bones[1].position) + Vector3.Distance(bones[1].position, bones[2].position) +
                Vector3.Distance(bones[3].position, bones[4].position) + Vector3.Distance(bones[4].position, bones[5].position)) * .5f;
            if (legLength < .05f) { Debug.LogError("DF: authored locomotion leg chain has invalid scale.", this); return; }
            for (int i = 0; i < 6; i++)
            {
                Vector3 along = i == 2 || i == 5 ? model.forward - model.up * .3f : bones[i+1].position - bones[i].position;
                Quaternion frame = CombatAnimationLibrary.Frame(model.InverseTransformDirection(along), Vector3.forward);
                anatomicalOffset[i] = Quaternion.Inverse(frame) * Quaternion.Inverse(model.rotation) * bones[i].rotation;
            }
            for (int side = 0; side < 2; side++) restFeet[side] = transform.InverseTransformPoint(bones[side * 3 + 2].position);
            string[] names = { "walk", "run", "left", "right", "back" };
            for (int i = 0; i < 5; i++) clips[i] = LocomotionAnimationLibrary.Find(names[i]);
            CalibrateStrides();
            Ready = true;
            var identity = GetComponent<HeroIdentity>();
            Debug.Log("DF: locomotion v2 coherent native FK active on " + (identity != null ? identity.DisplayName : name) +
                " (Quaternius walk/sprint, directional CC0 steps; native stride/sole/pelvis).", this);
        }
        private void OnDisable() { sampledFrame = appliedLegFrame = -1; contactStateReady = false; }
        private void CalibrateStrides()
        {
            // Measure the stance velocity on THIS skeleton. Source hips/leg proportions
            // cannot supply a matching travel distance for every imported hero.
            var rest = new Quaternion[6];
            for (int bone = 0; bone < 6; bone++) rest[bone] = bones[bone].localRotation;
            Vector3[] directions = { Vector3.forward, Vector3.forward, -Vector3.right, Vector3.right, -Vector3.forward };
            try
            {
                for (int clip = 0; clip < 5; clip++)
                {
                    int count = clips[clip].frames - 1;
                    var samples = new float[count * 2];
                    Vector3 along = model.rotation * directions[clip];
                    for (int frame = 0; frame < count; frame++)
                    {
                        for (int bone = 0; bone < 6; bone++)
                            bones[bone].rotation = model.rotation * clips[clip].Rotation(bone, (float)frame / count) * anatomicalOffset[bone];
                        for (int side = 0; side < 2; side++)
                            samples[frame * 2 + side] = Vector3.Dot(bones[side * 3 + 2].position - bones[side * 3].position, along);
                    }
                    var distances = new List<float>();
                    for (int frame = 0; frame < count; frame++)
                    {
                        int next = (frame + 1) % count;
                        for (int side = 0; side < 2; side++)
                            if (clips[clip].Contact(side, (float)frame / count) >= .5f &&
                                clips[clip].Contact(side, (float)next / count) >= .5f)
                            {
                                float distance = -(samples[next * 2 + side] - samples[frame * 2 + side]) * count;
                                if (distance > legLength * .1f) distances.Add(distance);
                            }
                    }
                    distances.Sort();
                    strides[clip] = distances.Count > 0 ? distances[distances.Count / 2] : legLength * clips[clip].stride;
                }
            }
            finally { for (int bone = 0; bone < 6; bone++) bones[bone].localRotation = rest[bone]; }
        }
        private void SetWeights(float run)
        {
            // The existing presentation direction is smooth and remains stable while stopping.
            Vector3 direction = motion != null ? Quaternion.Inverse(motion.PresentedRotation) * motion.GaitDirection : Vector3.forward;
            float forward = Mathf.Max(0f, direction.z), back = Mathf.Max(0f, -direction.z);
            float left = Mathf.Max(0f, -direction.x), right = Mathf.Max(0f, direction.x);
            float sum = forward + back + left + right;
            if (sum < .001f) { forward = sum = 1f; back = left = right = 0f; }
            weights[0] = forward / sum * (1f - Mathf.Clamp01(run));
            weights[1] = forward / sum * Mathf.Clamp01(run);
            weights[2] = left / sum; weights[3] = right / sum; weights[4] = back / sum;
        }
        public float CycleStride(float run)
        {
            if (!Ready) return legLength * Mathf.Lerp(1.05f, 1.65f, run);
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            SetWeights(run);
            float stride = 0f;
            for (int i = 0; i < 5; i++) stride += strides[i] * weights[i];
            return stride;
        }
        private bool SamplePose()
        {
            if (!Ready || !isActiveAndEnabled || model == null) return false;
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            if (!Active) { sampledFrame = -1; contactStateReady = false; return false; }
            if (sampledFrame == Time.frameCount) return true;
            SetWeights(motion.RunBlend);
            float phase = Mathf.Repeat(motion.GaitPhase / (Mathf.PI * 2f), 1f);
            for (int side = 0; side < 2; side++) contacts[side] = 0f;
            pelvisHeight = 0f;
            float total = 0f;
            for (int clip = 0; clip < 5; clip++)
            {
                float weight = weights[clip]; if (weight <= .00001f) continue;
                float blend = weight / (total + weight);
                for (int bone = 0; bone < 6; bone++)
                {
                    Quaternion rotation = clips[clip].Rotation(bone, phase);
                    pose[bone] = total == 0f ? rotation : Quaternion.Slerp(pose[bone], rotation, blend);
                }
                for (int side = 0; side < 2; side++)
                {
                    contacts[side] += clips[clip].Contact(side, phase) * weight;
                }
                float sourceLength = clips[clip].sourceLegLength;
                float mean = clips[clip].pelvisMean;
                // Retain the source's crouched posture, with a bounded body bob
                // suitable for the larger hero silhouettes and their weapon grips.
                float height = mean + Mathf.Clamp(clips[clip].PelvisHeight(phase) - mean, -sourceLength * .04f, sourceLength * .04f);
                pelvisHeight += height / sourceLength * weight;
                total += weight;
            }
            for (int side = 0; side < 2; side++)
            {
                // Near-identical diagonal blend weights must not toggle a world
                // foot anchor. Leave a dead band around the ambiguous 50/50 pose.
                if (!contactStateReady) contactState[side] = contacts[side] >= .5f;
                else if (contacts[side] >= .65f) contactState[side] = true;
                else if (contacts[side] <= .35f) contactState[side] = false;
            }
            contactStateReady = true;
            sampledFrame = Time.frameCount;
            return true;
        }
        public bool TryPelvisOffset(out float height)
        {
            height = 0f;
            if (!SamplePose()) return false;
            height = pelvisHeight * legLength * motion.LocomotionWeight;
            return true;
        }
        public void ApplyLegs()
        {
            if (!SamplePose()) return;
            // Parent before child. No local-axis assumptions, root translation or additional Animator.
            for (int i = 0; i < 6; i++)
                bones[i].rotation = Quaternion.Slerp(bones[i].rotation, model.rotation * pose[i] * anatomicalOffset[i], motion.LocomotionWeight);
            appliedLegFrame = Time.frameCount;
        }
        public bool TryFootSample(bool right, out Vector3 offset, out Vector3 kneeHint, out bool contact, out Quaternion rotation)
        {
            offset = kneeHint = Vector3.zero; contact = false; rotation = Quaternion.identity;
            if (!Active || sampledFrame != Time.frameCount || appliedLegFrame != Time.frameCount) return false;
            int side = right ? 1 : 0, hip = side * 3, foot = hip + 2;
            Vector3 nominal = motion.PresentedPosition + motion.PresentedRotation * Vector3.Scale(restFeet[side], transform.lossyScale);
            // Retarget rotations first, then use the resulting native FK pose.
            // A separately scaled source trajectory can be 30cm away on these rigs.
            offset = bones[foot].position - nominal;
            kneeHint = bones[hip+1].position;
            contact = contactState[side];
            rotation = bones[foot].rotation;
            return true;
        }
    }
}
