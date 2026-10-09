using Alterverse.Characters;
using Alterverse.Combat;
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
        private readonly Vector3[] positions = new Vector3[6];
        private readonly float[] footHeight = new float[2], contacts = new float[2];
        private readonly LocomotionAnimationLibrary.Clip[] clips = new LocomotionAnimationLibrary.Clip[5];
        private readonly float[] weights = new float[5];
        private HeroVisualMotion motion;
        private Health health;
        private float legLength;
        private int sampledFrame = -1;
        public bool Ready { get; private set; }
        public bool Active => Ready && isActiveAndEnabled && motion != null && motion.isActiveAndEnabled &&
            !motion.GroundingSuspended && (health == null || !health.IsDead) && Time.timeScale > 0f &&
            motion.PlanarSpeed > .08f && motion.LocomotionWeight > .002f;
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
            string[] names = { "walk", "run", "left", "right", "back" };
            for (int i = 0; i < 5; i++) clips[i] = LocomotionAnimationLibrary.Find(names[i]);
            Ready = true;
            var identity = GetComponent<HeroIdentity>();
            Debug.Log("DF: KayKit CC0 directional locomotion active on " + (identity != null ? identity.DisplayName : name) +
                " (walk/run/strafe left/strafe right/backward; final visible leg bones).", this);
        }
        private void OnDisable() { sampledFrame = -1; }
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
            for (int i = 0; i < 5; i++) stride += clips[i].stride * weights[i];
            return legLength * stride;
        }
        public void ApplyLegs()
        {
            sampledFrame = -1;
            if (!Ready || !isActiveAndEnabled || model == null) return;
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            if (!Active) return;
            SetWeights(motion.RunBlend);
            float phase = Mathf.Repeat(motion.GaitPhase / (Mathf.PI * 2f), 1f);
            for (int side = 0; side < 2; side++) { contacts[side] = 0f; footHeight[side] = 0f; }
            float total = 0f;
            for (int clip = 0; clip < 5; clip++)
            {
                float weight = weights[clip]; if (weight <= .00001f) continue;
                float blend = weight / (total + weight);
                for (int bone = 0; bone < 6; bone++)
                {
                    Quaternion rotation = clips[clip].Rotation(bone, phase);
                    Vector3 position = clips[clip].Position(bone, phase);
                    pose[bone] = total == 0f ? rotation : Quaternion.Slerp(pose[bone], rotation, blend);
                    positions[bone] = total == 0f ? position : Vector3.Lerp(positions[bone], position, blend);
                }
                for (int side = 0; side < 2; side++)
                {
                    contacts[side] += clips[clip].Contact(side, phase) * weight;
                    footHeight[side] += clips[clip].Height(side, phase) * weight;
                }
                total += weight;
            }
            // Parent before child. No local-axis assumptions, root translation or additional Animator.
            for (int i = 0; i < 6; i++)
                bones[i].rotation = Quaternion.Slerp(bones[i].rotation, model.rotation * pose[i] * anatomicalOffset[i], motion.LocomotionWeight);
            sampledFrame = Time.frameCount;
        }
        public bool TryFootSample(bool right, out Vector3 offset, out Vector3 kneeHint, out bool contact, out Quaternion rotation)
        {
            offset = kneeHint = Vector3.zero; contact = false; rotation = Quaternion.identity;
            if (!Active || sampledFrame != Time.frameCount) return false;
            int side = right ? 1 : 0, hip = side * 3, foot = hip + 2;
            float scale = legLength / LocomotionAnimationLibrary.SourceLegLength;
            // The target pelvis keeps its own pose. Remove the source hip's sway
            // before adding this trajectory to the target foot's rest placement.
            Vector3 delta = (positions[foot] - positions[hip]) -
                (LocomotionAnimationLibrary.Rest(foot) - LocomotionAnimationLibrary.Rest(hip));
            delta.y = Mathf.Max(0f, footHeight[side]);
            offset = motion.PresentedRotation * delta * scale * motion.LocomotionWeight;
            Vector3 knee = positions[hip+1] - positions[hip];
            kneeHint = bones[hip].position + motion.PresentedRotation * knee * scale;
            contact = contacts[side] >= .5f;
            rotation = bones[foot].rotation;
            return true;
        }
    }
}
