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
            public Quaternion rotation, rotationBefore;
            public Vector3[] solePoints;
            public float length, sole, cycle, soleAdjustment;
            public bool contact, initialized, authoredSample, hasBendDirection, hasFootRotation;
            public int footRotationFrame = -1;
            public Vector3 bendDirection;
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
        private int preparedPelvisFrame = -1;
        private float floorOffset;
        private float displayedDrop;
        private bool displayedDropReady;
        private float terrainPelvisOffset;
        private bool terrainOffsetReady;
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
            Debug.Log("DF: grounded locomotion v3 active on " + name + "; real leg length " + LegLength.ToString("F2") + "m", this);
        }
        private Leg Capture(Transform hip, Transform knee, Transform foot)
        {
            if (hip == null || knee == null || foot == null) return null;
            float length = Vector3.Distance(hip.position, knee.position) + Vector3.Distance(knee.position, foot.position);
            if (length < .05f) return null;
            return new Leg { hip = hip, knee = knee, foot = foot, length = length,
                rest = transform.InverseTransformPoint(foot.position),
                restRotation = Quaternion.Inverse(transform.rotation) * foot.rotation,
                rotation = foot.rotation, hasFootRotation = true,
                sole = length * .075f };
        }
        private void CaptureSoles(Transform imported)
        {
            // Mesh bind matrices map imported vertices into their bone spaces.
            // Reconstruct skinning explicitly: BakeMesh scale compensation must
            // not become a second renderer/FBX scale in the foot target.
            var pointsL = new System.Collections.Generic.HashSet<Vector3>();
            var pointsR = new System.Collections.Generic.HashSet<Vector3>();
            int rejected = 0, unreadable = 0;
            foreach (SkinnedMeshRenderer skin in imported.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = skin.sharedMesh;
                if (mesh == null) continue;
                if (!mesh.isReadable) { unreadable++; continue; }
                Transform[] bones = skin.bones;
                int li = System.Array.IndexOf(bones, left.foot), ri = System.Array.IndexOf(bones, right.foot);
                if (li < 0 && ri < 0) continue;
                Vector3[] vertices = mesh.vertices;
                BoneWeight[] weights = mesh.boneWeights;
                Matrix4x4[] bind = mesh.bindposes;
                for (int i = 0; i < vertices.Length && i < weights.Length; i++)
                {
                    BoneWeight weight = weights[i];
                    bool onLeft = FootWeighted(weight, li), onRight = FootWeighted(weight, ri);
                    if (!onLeft && !onRight) continue;
                    Vector3 point = SkinVertex(bones, bind, weight, vertices[i]);
                    if (onLeft && !CaptureSolePoint(left, point, pointsL)) rejected++;
                    if (onRight && !CaptureSolePoint(right, point, pointsR)) rejected++;
                }
            }
            left.solePoints = new System.Collections.Generic.List<Vector3>(pointsL).ToArray();
            right.solePoints = new System.Collections.Generic.List<Vector3>(pointsR).ToArray();
            left.sole = SoleExtent(left); right.sole = SoleExtent(right);
            Debug.Log("DF: grounded locomotion v3 bind-matrix soles on " + name +
                "; points L/R=" + left.solePoints.Length + "/" + right.solePoints.Length +
                "; sole L/R=" + left.sole.ToString("F3") + "/" + right.sole.ToString("F3") +
                "m; rejected=" + rejected + "; unreadable=" + unreadable, this);
        }
        private static bool FootWeighted(BoneWeight w, int index)
        {
            return index >= 0 && ((w.boneIndex0 == index && w.weight0 > .35f) ||
                (w.boneIndex1 == index && w.weight1 > .35f) ||
                (w.boneIndex2 == index && w.weight2 > .35f) ||
                (w.boneIndex3 == index && w.weight3 > .35f));
        }
        private static Vector3 SkinVertex(Transform[] bones, Matrix4x4[] bind, BoneWeight w, Vector3 point)
        {
            Vector3 result = Vector3.zero;
            float total = 0f;
            for (int influence = 0; influence < 4; influence++)
            {
                int index = influence == 0 ? w.boneIndex0 : influence == 1 ? w.boneIndex1 :
                    influence == 2 ? w.boneIndex2 : w.boneIndex3;
                float weight = influence == 0 ? w.weight0 : influence == 1 ? w.weight1 :
                    influence == 2 ? w.weight2 : w.weight3;
                if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight) ||
                    index < 0 || index >= bones.Length || index >= bind.Length || bones[index] == null) continue;
                result += bones[index].localToWorldMatrix.MultiplyPoint3x4(bind[index].MultiplyPoint3x4(point)) * weight;
                total += weight;
            }
            return total > .00001f ? result / total : new Vector3(float.NaN, float.NaN, float.NaN);
        }
        private static bool CaptureSolePoint(Leg leg, Vector3 point, System.Collections.Generic.HashSet<Vector3> points)
        {
            Vector3 offset = point - leg.foot.position;
            float radius = Mathf.Max(.1f, leg.length * .45f);
            if (float.IsNaN(offset.sqrMagnitude) || float.IsInfinity(offset.sqrMagnitude) ||
                offset.sqrMagnitude > radius * radius) return false;
            points.Add(leg.foot.InverseTransformPoint(point));
            return true;
        }
        private void Update() { RestorePelvis(); preparedPelvisFrame = -1; }
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
            RestorePelvis(); appliedFrame = preparedPelvisFrame = -1; displayedDrop = 0f; displayedDropReady = terrainOffsetReady = false;
            if (left != null) { left.initialized = false; left.authoredSample = false; left.hasBendDirection = false; left.hasFootRotation = false; left.footRotationFrame = -1; }
            if (right != null) { right.initialized = false; right.authoredSample = false; right.hasBendDirection = false; right.hasFootRotation = false; right.footRotationFrame = -1; }
        }
        private void LateUpdate()
        {
            // Imported heroes call us synchronously after the final combat pose.
            if (presentation == null) FinalizeFeet();
        }
        public void PrepareSourcePelvis()
        {
            if (!isActiveAndEnabled || !Ready || pelvis == null || preparedPelvisFrame == Time.frameCount) return;
            preparedPelvisFrame = Time.frameCount;
            RestorePelvis();
            if (motion == null || motion.GroundingSuspended || !motion.isActiveAndEnabled) return;
            Health health = GetComponent<Health>();
            if (health != null && health.IsDead) return;
            // Actor origin is one metre above the arena. Imported model offsets
            // leave its bind soles above that floor; align once before gait/IK so
            // each supporting-foot swap does not repeat this body correction.
            float terrain = Mathf.Min(Ground(Nominal(left), left, out _).y - Nominal(left).y,
                Ground(Nominal(right), right, out _).y - Nominal(right).y);
            terrainPelvisOffset = !terrainOffsetReady ? terrain :
                Mathf.MoveTowards(terrainPelvisOffset, terrain, LegLength * 3.5f * Mathf.Min(Time.deltaTime, .1f));
            terrainOffsetReady = true;
            OffsetPelvis(Vector3.up * terrainPelvisOffset);
            if (clips == null) clips = GetComponent<HeroLocomotionAnimation>();
            if (clips != null && clips.TryPelvisOffset(out float height)) OffsetPelvis(Vector3.up * height);
        }
        private void OffsetPelvis(Vector3 offset)
        {
            if (pelvis == null || offset.sqrMagnitude < .0000000001f) return;
            if (!correctedPelvis) pelvisBefore = pelvis.localPosition;
            pelvis.position += offset;
            pelvisAfter = pelvis.localPosition; correctedPelvis = true;
        }
        public void FinalizeFeet()
        {
            if (!isActiveAndEnabled || !Ready || motion == null || !motion.isActiveAndEnabled ||
                Time.deltaTime <= 0f || appliedFrame == Time.frameCount) return;
            appliedFrame = Time.frameCount;
            Health health = GetComponent<Health>();
            if (motion.GroundingSuspended || health != null && health.IsDead) { ResetContacts(); appliedFrame = Time.frameCount; return; }
            // Imported heroes prepare before the hand/weapon solvers; standalone rigs
            // prepare here. A second call in this frame must not apply the offset twice.
            PrepareSourcePelvis();
            Vector3 previousLeft = left.initialized ? left.target : left.foot.position;
            Vector3 previousRight = right.initialized ? right.target : right.foot.position;
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
            bool sampled = clips != null && clips.isActiveAndEnabled && clips.Ready && clips.Active &&
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
            float maxDrop = pelvis != null ? LegLength * (sampled ? .12f : .19f) : 0f;
            FitAuthoredTarget(left, maxDrop, stance, moving);
            FitAuthoredTarget(right, maxDrop, stance, moving);
            StabilizeAuthoredTarget(left, previousLeft, dt);
            StabilizeAuthoredTarget(right, previousRight, dt);
            FitAuthoredTarget(left, maxDrop, stance, moving);
            FitAuthoredTarget(right, maxDrop, stance, moving);
            if (pelvis != null)
            {
                float drop = Mathf.Min(Mathf.Max(RequiredDrop(left), RequiredDrop(right)), maxDrop);
                // The first frame aligns the bind pose to terrain. Later support
                // changes must not move the pelvis by its whole correction at once.
                displayedDrop = !displayedDropReady ? drop : Mathf.Max(drop, Mathf.Lerp(displayedDrop, drop, 1f - Mathf.Exp(-12f * dt)));
                displayedDropReady = true;
                if (displayedDrop > .00001f) OffsetPelvis(-Vector3.up * displayedDrop);
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
                1 << GameplayCollision.Ground, QueryTriggerInteraction.Ignore);
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
            // Preserve the animated native FK sole clearance above the terrain.
            // Source shoes have different ankle/sole proportions; applying
            // their clearance again would lift or squash the native leg pose.
            float extent = SoleExtent(leg);
            float lift = Mathf.Max(0f, Nominal(leg).y + offset.y - sourceGround.y - (extent - leg.sole));
            // Human target rigs have longer shins than the cartoon source rig.
            // Retargeting must limit clearance rather than carry its high kicks
            // through a full-strength foot solve on every hero.
            lift = Mathf.Min(lift, leg.length * Mathf.Lerp(.14f, .23f, motion.RunBlend));
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
                leg.target = sourceGround + Vector3.up * lift;
                leg.normal = normal;
            }
            leg.authoredSample = true; leg.authoredKneeHint = kneeHint;
            leg.authoredRotation = rotation;
            leg.authoredLift = contact ? 0f : lift;
            leg.authoredTarget = sourceGround + Vector3.up * leg.authoredLift;
            leg.cycle = cycle; leg.contact = contact; leg.initialized = true;
            // An unreachable plant is projected toward reach below. Never replace
            // it with the opposite end of the source stride in a single frame.
        }
        private void StabilizeAuthoredTarget(Leg leg, Vector3 previous, float dt)
        {
            if (!leg.authoredSample) return;
            float maxStep = Mathf.Max(leg.length * 6f, motion.PlanarSpeed * 1.7f) * dt;
            leg.target = Vector3.MoveTowards(previous, leg.target, maxStep);
            Vector3 floor = Ground(leg.target, leg, out Vector3 normal);
            float minimum = floor.y + leg.soleAdjustment;
            // A support anchor belongs to the terrain, never the interpolated
            // airborne ankle. Otherwise one elevated frame becomes a permanent
            // floating plant. Swing interpolation must retain its lift bound too.
            float lift = leg.length * Mathf.Lerp(.14f, .23f, motion.RunBlend);
            leg.target.y = Mathf.Clamp(leg.target.y, minimum, minimum + lift);
            leg.normal = normal;
            leg.authoredLift = Mathf.Max(0f, leg.target.y - minimum);
            if (leg.contact)
            {
                leg.plant = leg.target - Vector3.up * leg.soleAdjustment;
                leg.plant.y = floor.y;
            }
        }
        private void FitAuthoredTarget(Leg leg, float maxDrop, float stance, bool moving)
        {
            if (!leg.authoredSample) return;
            // A planted world anchor can leave reach during a sharp turn. Preserve
            // the native FK swing and fit only a contact that has become unreachable.
            for (int pass = 0; pass < 3; pass++)
            {
                float reach = (Vector3.Distance(leg.hip.position, leg.knee.position) +
                    Vector3.Distance(leg.knee.position, leg.foot.position)) * .99f;
                float height = Mathf.Max(0f, leg.hip.position.y - leg.target.y - maxDrop);
                float budget = Mathf.Sqrt(Mathf.Max(0f, reach * reach - height * height));
                Vector3 offset = leg.target - leg.hip.position; offset.y = 0f;
                if (offset.sqrMagnitude <= budget * budget + .0000001f) return;
                // Project the existing anchor to the nearest reachable position.
                // Reacquiring the source foot here caused meter-wide jumps while
                // contact remained true, especially on turns and backsteps.
                Vector3 candidate = leg.target;
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
                if (leg.contact) leg.plant = leg.target - Vector3.up * (leg.soleAdjustment + leg.authoredLift);
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
            Quaternion desiredRotation = leg.authoredSample ?
                Quaternion.FromToRotation(Vector3.up, leg.normal) * leg.authoredRotation :
                Quaternion.AngleAxis(pitch, motion.PresentedRotation * Vector3.right) *
                    Quaternion.FromToRotation(Vector3.up, leg.normal) * flat;
            // A direction reversal can switch blend quadrants in one frame. Keep
            // the final boot orientation continuous, also while clips fade out.
            // Reach fitting calls this repeatedly; use one baseline per frame.
            if (leg.footRotationFrame != Time.frameCount)
            {
                leg.rotationBefore = leg.hasFootRotation ? leg.rotation : leg.foot.rotation;
                leg.hasFootRotation = true; leg.footRotationFrame = Time.frameCount;
            }
            leg.rotation = Quaternion.RotateTowards(leg.rotationBefore, desiredRotation,
                720f * Mathf.Min(Time.deltaTime, .1f));
            leg.foot.rotation = leg.rotation;
            // Toe-off rotates around the ankle. Raise the ankle by the actual rotated
            // sole extent so the front of the boot cannot rotate through the floor.
            if (leg.solePoints != null && leg.solePoints.Length > 0)
            {
                leg.soleAdjustment = SoleExtent(leg) - leg.sole; leg.target.y += leg.soleAdjustment;
            }
        }
        private static float SoleExtent(Leg leg)
        {
            if (leg.solePoints == null || leg.solePoints.Length == 0) return leg.sole;
            Matrix4x4 matrix = leg.foot.localToWorldMatrix; float low = float.PositiveInfinity;
            foreach (Vector3 point in leg.solePoints)
            {
                float y = matrix.MultiplyVector(point).y;
                if (!float.IsNaN(y) && !float.IsInfinity(y)) low = Mathf.Min(low, y);
            }
            // An imported skin outlier must never lift a foot toward the torso.
            // This also protects against scale changes after initial calibration.
            float extent = float.IsInfinity(low) ? leg.sole : -low;
            return Mathf.Clamp(extent, .01f, leg.length * .35f);
        }
        private void Solve(Leg leg, float stance, bool moving)
        {
            Vector3 axis = (leg.target - leg.hip.position).normalized;
            Vector3 forward = motion.PresentedRotation * Vector3.forward;
            Vector3 pole = forward - axis * Vector3.Dot(forward, axis);
            if (pole.sqrMagnitude < .0001f) pole = motion.PresentedRotation * Vector3.right;
            pole.Normalize();
            if (leg.authoredSample)
            {
                Vector3 source = leg.authoredKneeHint - leg.hip.position;
                source -= axis * Vector3.Dot(source, axis);
                // A nearly straight source knee can put its hint on either side
                // of the target axis. Keep the anatomical forward bend dominant.
                if (source.sqrMagnitude > leg.length * leg.length * .0004f)
                    pole = Vector3.Lerp(pole, source.normalized, .25f).normalized;
            }
            if (leg.hasBendDirection)
            {
                Vector3 previous = leg.bendDirection - axis * Vector3.Dot(leg.bendDirection, axis);
                if (previous.sqrMagnitude > .0001f)
                    pole = Vector3.Lerp(previous.normalized, pole, 1f - Mathf.Exp(-24f * Mathf.Min(Time.deltaTime, .1f))).normalized;
            }
            leg.bendDirection = pole; leg.hasBendDirection = true;
            Vector3 hint = leg.hip.position + pole * leg.length;
            // Repeat once for the existing nonuniform model scale; never translate the ankle bone.
            for (int i = 0; i < 2; i++) CombatMotion.Solve(leg.hip, leg.knee, leg.foot, leg.target, hint, .9999f);
            leg.foot.rotation = leg.rotation;
        }
    }
}

