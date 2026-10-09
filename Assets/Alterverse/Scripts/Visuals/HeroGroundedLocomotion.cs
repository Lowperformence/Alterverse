using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Final visual-only foot placement on the actual visible skeleton.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(945)]
    public sealed class HeroGroundedLocomotion : MonoBehaviour
    {
        private sealed class Leg
        {
            public Transform hip, knee, foot;
            public Vector3 rest, plant, swingStart, target, normal = Vector3.up;
            public Quaternion restRotation;
            public Quaternion rotation;
            public Vector3[] solePoints;
            public float length, sole, cycle, soleAdjustment;
            public bool contact, initialized, authoredSample;
            public Vector3 authoredKneeHint, authoredTarget;
            public Quaternion authoredRotation;
            public float authoredLift;
        }
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private HeroVisualMotion motion;
        private AuthoredHeroPresentation presentation;
        private HeroLocomotionAnimation clips;
        private Leg left, right;
        private Transform pelvis;
        private Vector3 pelvisBefore, pelvisAfter;
        private bool correctedPelvis;
        private int appliedFrame = -1;
        private float floorOffset;
        private bool reportedAuthoredContacts;
        public bool Ready => left != null && right != null;
        public float LegLength => Ready ? (left.length + right.length) * .5f : 1f;
        public Vector3 LeftTarget => Ready ? left.target : transform.position;
        public Vector3 RightTarget => Ready ? right.target : transform.position;
        public bool LeftContact => Ready && left.contact;
        public bool RightContact => Ready && right.contact;
        public float LeftContactError => Ready && left.initialized && left.contact ? Vector3.Distance(left.foot.position, left.target) : 0f;
        public float RightContactError => Ready && right.initialized && right.contact ? Vector3.Distance(right.foot.position, right.target) : 0f;
        public float CycleStride(float run)
        {
            if (clips == null) clips = GetComponent<HeroLocomotionAnimation>();
            return clips != null && clips.isActiveAndEnabled && clips.Ready ? clips.CycleStride(run) :
                LegLength * Mathf.Lerp(1.05f, 1.65f, Mathf.Clamp01(run));
        }

        public void Initialize(Transform visualRoot, HeroProceduralRig rig)
        {
            if (Ready || visualRoot == null || rig == null || !rig.Ready) return;
            motion = GetComponent<HeroVisualMotion>();
            presentation = GetComponent<AuthoredHeroPresentation>();
            clips = GetComponent<HeroLocomotionAnimation>();
            Transform imported = visualRoot.Find("DF_AuthoredModel");
            if (imported != null)
            {
                Transform Find(string name)
                {
                    foreach (Transform bone in imported.GetComponentsInChildren<Transform>(true))
                        if (bone.name == name) return bone;
                    return null;
                }
                left = Capture(Find("ThighL"), Find("ShinL"), Find("FootL"));
                right = Capture(Find("ThighR"), Find("ShinR"), Find("FootR"));
                pelvis = Find("Hips");
                if (Ready) CaptureSoles(imported);
            }
            else
            {
                rig.GetLeg(false, out Transform lh, out Transform lk, out Transform lf);
                rig.GetLeg(true, out Transform rh, out Transform rk, out Transform rf);
                left = Capture(lh, lk, lf); right = Capture(rh, rk, rf);
                pelvis = rig.Pelvis;
            }
            if (!Ready) { Debug.LogError("DF: grounded locomotion cannot find both visible leg chains on " + name, this); return; }
            floorOffset = Mathf.Min(left.foot.position.y - left.sole, right.foot.position.y - right.sole) - transform.position.y;
            Debug.Log("DF: grounded locomotion v1 active on " + name + "; real leg length " + LegLength.ToString("F2") + "m", this);
        }
        private Leg Capture(Transform hip, Transform knee, Transform foot)
        {
            if (hip == null || knee == null || foot == null) return null;
            float length = Vector3.Distance(hip.position, knee.position) + Vector3.Distance(knee.position, foot.position);
            if (length < .05f) return null;
            return new Leg { hip = hip, knee = knee, foot = foot, length = length,
                rest = transform.InverseTransformPoint(foot.position),
                restRotation = Quaternion.Inverse(transform.rotation) * foot.rotation,
                sole = length * .075f };
        }
        private void CaptureSoles(Transform imported)
        {
            // Once per model: bake the corrected bind pose and inspect only foot-weighted vertices.
            var baked = new Mesh();
            float lowL = float.PositiveInfinity, lowR = float.PositiveInfinity;
            var pointsL = new System.Collections.Generic.HashSet<Vector3>();
            var pointsR = new System.Collections.Generic.HashSet<Vector3>();
            try
            {
                foreach (SkinnedMeshRenderer skin in imported.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (skin.sharedMesh == null) continue;
                    int li = System.Array.IndexOf(skin.bones, left.foot), ri = System.Array.IndexOf(skin.bones, right.foot);
                    if (li < 0 && ri < 0) continue;
                    skin.BakeMesh(baked); Vector3[] vertices = baked.vertices; BoneWeight[] weights = skin.sharedMesh.boneWeights;
                    for (int i = 0; i < vertices.Length && i < weights.Length; i++)
                    {
                        BoneWeight w = weights[i];
                        Vector3 point = skin.transform.TransformPoint(vertices[i]); float y = point.y;
                        bool On(int index) => index >= 0 && ((w.boneIndex0 == index && w.weight0 > .35f) ||
                            (w.boneIndex1 == index && w.weight1 > .35f) || (w.boneIndex2 == index && w.weight2 > .35f) ||
                            (w.boneIndex3 == index && w.weight3 > .35f));
                        if (On(li)) { lowL = Mathf.Min(lowL, y); pointsL.Add(point); }
                        if (On(ri)) { lowR = Mathf.Min(lowR, y); pointsR.Add(point); }
                    }
                }
                if (!float.IsInfinity(lowL)) left.sole = Mathf.Clamp(left.foot.position.y - lowL, .01f, left.length * .22f);
                if (!float.IsInfinity(lowR)) right.sole = Mathf.Clamp(right.foot.position.y - lowR, .01f, right.length * .22f);
                void CapturePoints(Leg leg, System.Collections.Generic.HashSet<Vector3> points)
                {
                    leg.solePoints = new Vector3[points.Count]; int index = 0;
                    foreach (Vector3 point in points) leg.solePoints[index++] = leg.foot.InverseTransformPoint(point);
                }
                CapturePoints(left, pointsL); CapturePoints(right, pointsR);
            }
            finally { if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked); }
        }
        private void Update() => RestorePelvis();
        private void OnDisable() { RestorePelvis(); ResetContacts(); }
        private void RestorePelvis()
        {
            // Do not subtract our old correction if another pose owner already replaced it.
            if (correctedPelvis && pelvis != null && (pelvis.localPosition - pelvisAfter).sqrMagnitude < .00000001f)
                pelvis.localPosition = pelvisBefore;
            correctedPelvis = false;
        }
        public void ResetContacts()
        {
            RestorePelvis(); appliedFrame = -1;
            if (left != null) { left.initialized = false; left.authoredSample = false; }
            if (right != null) { right.initialized = false; right.authoredSample = false; }
        }
        private void LateUpdate()
        {
            // Imported heroes call us synchronously after the final combat pose.
            if (presentation == null) FinalizeFeet();
        }
        public void FinalizeFeet()
        {
            if (!isActiveAndEnabled || !Ready || motion == null || !motion.isActiveAndEnabled ||
                Time.deltaTime <= 0f || appliedFrame == Time.frameCount) return;
            appliedFrame = Time.frameCount;
            Health health = GetComponent<Health>();
            if (motion.GroundingSuspended || health != null && health.IsDead) { ResetContacts(); appliedFrame = Time.frameCount; return; }
            float dt = Mathf.Min(Time.deltaTime, .1f), run = motion.RunBlend;
            bool moving = motion.PlanarSpeed > .08f;
            float cycle = Mathf.Repeat(motion.GaitPhase / (Mathf.PI * 2f), 1f);
            float stance = Mathf.Lerp(.62f, .46f, run);
            Vector3 direction = motion.PresentedVelocity; direction.y = 0f;
            direction = direction.sqrMagnitude > .0001f ? direction.normalized : motion.PresentedRotation * Vector3.forward;
            if (clips == null) clips = GetComponent<HeroLocomotionAnimation>();
            Vector3 leftOffset = Vector3.zero, rightOffset = Vector3.zero, leftHint = Vector3.zero, rightHint = Vector3.zero;
            Quaternion leftRotation = Quaternion.identity, rightRotation = Quaternion.identity;
            bool leftContact = false, rightContact = false;
            bool sampled = moving && clips != null && clips.isActiveAndEnabled && clips.Ready && clips.Active &&
                clips.TryFootSample(false, out leftOffset, out leftHint, out leftContact, out leftRotation) &&
                clips.TryFootSample(true, out rightOffset, out rightHint, out rightContact, out rightRotation);
            if (sampled)
            {
                // Read the source trajectory/contact metadata sampled by ApplyLegs this frame.
                if (!reportedAuthoredContacts)
                {
                    reportedAuthoredContacts = true;
                    Debug.Log("DF: authored locomotion foot trajectories and contacts active on " + name, this);
                }
                PlanAuthored(left, leftOffset, leftHint, leftContact, leftRotation, cycle);
                PlanAuthored(right, rightOffset, rightHint, rightContact, rightRotation, Mathf.Repeat(cycle + .5f, 1f));
            }
            else
            {
                Plan(left, cycle, stance, run, direction, moving, dt);
                Plan(right, Mathf.Repeat(cycle + .5f, 1f), stance, run, direction, moving, dt);
            }
            PrepareFoot(left, stance, moving); PrepareFoot(right, stance, moving);
            RestorePelvis();
            float maxDrop = pelvis != null ? LegLength * .19f : 0f;
            FitAuthoredTarget(left, maxDrop, stance, moving);
            FitAuthoredTarget(right, maxDrop, stance, moving);
            if (pelvis != null)
            {
                float drop = Mathf.Min(Mathf.Max(RequiredDrop(left), RequiredDrop(right)), maxDrop);
                if (drop > .00001f)
                {
                    pelvisBefore = pelvis.localPosition; pelvis.position -= Vector3.up * drop;
                    pelvisAfter = pelvis.localPosition; correctedPelvis = true;
                }
            }
            Solve(left, stance, moving); Solve(right, stance, moving);
        }
        private Vector3 Nominal(Leg leg) => motion.PresentedPosition + motion.PresentedRotation * Vector3.Scale(leg.rest, transform.lossyScale);
        private Vector3 Ground(Vector3 point, Leg leg, out Vector3 normal)
        {
            normal = Vector3.up;
            float floor = motion.PresentedPosition.y + floorOffset;
            Vector3 origin = new Vector3(point.x, motion.PresentedPosition.y + LegLength * .7f, point.z);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, LegLength * 2f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = hits[i]; Collider other = hit.collider;
                if (other == null || other.transform.IsChildOf(transform) || hit.normal.y < .65f ||
                    hit.point.y > motion.PresentedPosition.y + LegLength * .25f ||
                    other.GetComponentInParent<HeroIdentity>() != null || other.GetComponentInParent<CharacterController>() != null) continue;
                if (hit.distance < nearest) { nearest = hit.distance; floor = hit.point.y; normal = hit.normal; }
            }
            point.y = floor + leg.sole; return point;
        }
        private void PlanAuthored(Leg leg, Vector3 offset, Vector3 kneeHint, bool contact, Quaternion rotation, float cycle)
        {
            leg.target.y -= leg.soleAdjustment; leg.soleAdjustment = 0f;
            Vector3 planarOffset = new Vector3(offset.x, 0f, offset.z);
            Vector3 sourceGround = Ground(Nominal(leg) + planarOffset, leg, out Vector3 normal);
            if (contact)
            {
                if (!leg.initialized || !leg.contact || !leg.authoredSample)
                {
                    leg.plant = sourceGround; leg.normal = normal;
                }
                leg.target = leg.plant;
            }
            else
            {
                leg.target = sourceGround + Vector3.up * Mathf.Max(0f, offset.y);
                leg.normal = normal;
            }
            leg.authoredSample = true; leg.authoredKneeHint = kneeHint;
            leg.authoredRotation = rotation;
            leg.authoredLift = contact ? 0f : Mathf.Max(0f, offset.y);
            leg.authoredTarget = sourceGround + Vector3.up * leg.authoredLift;
            leg.cycle = cycle; leg.contact = contact; leg.initialized = true;
            // An abrupt turn cannot preserve a contact beyond the actual leg's reach.
            Vector3 reachOffset = leg.target - leg.hip.position; reachOffset.y = 0f;
            if (reachOffset.magnitude > leg.length * .68f)
            {
                leg.target = sourceGround + (contact ? Vector3.zero : Vector3.up * Mathf.Max(0f, offset.y));
                leg.plant = sourceGround; leg.normal = normal;
            }
        }
        private void FitAuthoredTarget(Leg leg, float maxDrop, float stance, bool moving)
        {
            if (!leg.authoredSample) return;
            // Source pelvis motion is not copied to the target body. A valid source
            // swing can therefore exceed the target's reach even after its allowed
            // pelvis drop. Keep the sole on terrain and fit only the horizontal step.
            for (int pass = 0; pass < 3; pass++)
            {
                float reach = (Vector3.Distance(leg.hip.position, leg.knee.position) +
                    Vector3.Distance(leg.knee.position, leg.foot.position)) * .99f;
                float height = Mathf.Max(0f, leg.hip.position.y - leg.target.y - maxDrop);
                float budget = Mathf.Sqrt(Mathf.Max(0f, reach * reach - height * height));
                Vector3 offset = leg.target - leg.hip.position; offset.y = 0f;
                if (offset.sqrMagnitude <= budget * budget + .0000001f) return;
                // Preserve a planted world anchor until it becomes unreachable;
                // then acquire a reachable contact from this frame's source pose.
                Vector3 candidate = pass == 0 && leg.contact ?
                    leg.authoredTarget + Vector3.up * leg.soleAdjustment : leg.target;
                height = Mathf.Max(0f, leg.hip.position.y - candidate.y - maxDrop);
                budget = Mathf.Sqrt(Mathf.Max(0f, reach * reach - height * height));
                offset = candidate - leg.hip.position; offset.y = 0f;
                if (offset.sqrMagnitude > budget * budget && offset.sqrMagnitude > .0000001f)
                {
                    Vector3 fitted = leg.hip.position + offset.normalized * budget;
                    candidate.x = fitted.x; candidate.z = fitted.z;
                }
                leg.target = Ground(candidate, leg, out leg.normal) + Vector3.up * leg.authoredLift;
                leg.soleAdjustment = 0f;
                PrepareFoot(leg, stance, moving);
                if (leg.contact) leg.plant = leg.target - Vector3.up * leg.soleAdjustment;
            }
        }
        private void Plan(Leg leg, float cycle, float stance, float run, Vector3 direction, bool moving, float dt)
        {
            // A missing clip retains the established fallback; stop uses its smooth settling.
            if (leg.authoredSample && moving) leg.initialized = false;
            leg.authoredSample = false;
            leg.target.y -= leg.soleAdjustment; leg.soleAdjustment = 0f;
            Vector3 nominal = Ground(Nominal(leg), leg, out Vector3 normal);
            float lead = CycleStride(run) * stance * .5f * motion.LocomotionWeight;
            bool contact = !moving || cycle < stance;
            if (!moving)
            {
                leg.target = leg.initialized ? Vector3.Lerp(leg.target, nominal, 1f - Mathf.Exp(-16f * dt)) : nominal;
                leg.plant = leg.target; leg.normal = normal;
            }
            else if (contact)
            {
                if (!leg.initialized || !leg.contact || cycle < leg.cycle)
                    leg.plant = Ground(nominal + direction * lead * (1f - 2f * cycle / stance), leg, out leg.normal);
                // Actual world-space anchor: translating/turning the actor cannot drag this foot.
                leg.target = leg.plant;
            }
            else
            {
                if (!leg.initialized || leg.contact) leg.swingStart = leg.initialized ? leg.plant : leg.foot.position;
                float u = Mathf.Clamp01((cycle - stance) / (1f - stance));
                Vector3 landing = Ground(nominal + direction * lead, leg, out leg.normal);
                leg.target = Vector3.Lerp(leg.swingStart, landing, CombatMotion.Ease(u)) +
                    Vector3.up * (Mathf.Sin(Mathf.PI * u) * leg.length * Mathf.Lerp(.14f, .26f, run) * motion.LocomotionWeight);
            }
            leg.cycle = cycle; leg.contact = contact; leg.initialized = true;
            // Sharp turns release an unreachable old anchor instead of stretching the skeleton.
            Vector3 offset = leg.target - leg.hip.position; offset.y = 0;
            if (offset.magnitude > leg.length * .68f)
            {
                leg.target = Ground(nominal, leg, out leg.normal); leg.plant = leg.target;
                leg.swingStart = leg.target;
            }
        }
        private static float RequiredDrop(Leg leg)
        {
            Vector3 d = leg.hip.position - leg.target;
            float reach = (Vector3.Distance(leg.hip.position, leg.knee.position) +
                Vector3.Distance(leg.knee.position, leg.foot.position)) * .998f;
            float height = Mathf.Sqrt(Mathf.Max(.0001f, reach * reach - d.x * d.x - d.z * d.z));
            return Mathf.Max(0f, d.y - height);
        }
        private void PrepareFoot(Leg leg, float stance, bool moving)
        {
            float pitch = 0f;
            if (!leg.authoredSample && moving && leg.contact)
            {
                float u = leg.cycle / stance;
                if (u < .12f) pitch = Mathf.Lerp(-7f, 0f, CombatMotion.Ease(u / .12f));
                else if (u > .80f) pitch = Mathf.Lerp(0f, 17f, CombatMotion.Ease((u - .80f) / .20f));
            }
            else if (!leg.authoredSample && moving)
            {
                float u = Mathf.Clamp01((leg.cycle - stance) / (1f - stance));
                pitch = u < .30f ? Mathf.Lerp(17f, -10f, CombatMotion.Ease(u / .30f)) :
                    u > .80f ? Mathf.Lerp(-10f, -7f, CombatMotion.Ease((u - .80f) / .20f)) : -10f;
            }
            pitch *= motion.LocomotionWeight;
            Quaternion flat = motion.PresentedRotation * leg.restRotation;
            leg.rotation = leg.authoredSample ?
                Quaternion.FromToRotation(Vector3.up, leg.normal) * leg.authoredRotation :
                Quaternion.AngleAxis(pitch, motion.PresentedRotation * Vector3.right) *
                    Quaternion.FromToRotation(Vector3.up, leg.normal) * flat;
            leg.foot.rotation = leg.rotation;
            // Toe-off rotates around the ankle. Raise the ankle by the actual rotated
            // sole extent so the front of the boot cannot rotate through the floor.
            if (leg.solePoints != null && leg.solePoints.Length > 0)
            {
                Matrix4x4 matrix = leg.foot.localToWorldMatrix; float low = float.PositiveInfinity;
                foreach (Vector3 point in leg.solePoints) low = Mathf.Min(low, matrix.MultiplyVector(point).y);
                leg.soleAdjustment = -low - leg.sole; leg.target.y += leg.soleAdjustment;
            }
        }
        private void Solve(Leg leg, float stance, bool moving)
        {
            Vector3 hint = leg.authoredSample ? leg.authoredKneeHint :
                leg.hip.position + motion.PresentedRotation * Vector3.forward * leg.length + Vector3.up * .10f;
            // Repeat once for the existing nonuniform model scale; never translate the ankle bone.
            for (int i = 0; i < 2; i++) CombatMotion.Solve(leg.hip, leg.knee, leg.foot, leg.target, hint, .9999f);
            leg.foot.rotation = leg.rotation;
        }
    }
}

