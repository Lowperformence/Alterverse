using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Final combat pose owner for ALL authored heroes (legacy component name retained for scene/network compatibility).
    /// Blender clips supply anatomical world frames; fitted grips and heavy weapon paths are solved afterwards.
    /// HeroAttackDelivery still exclusively owns damage, cooldowns and the contact clock.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(950)]
    public sealed class MeleeBasicAttackAnimation : MonoBehaviour
    {
        private sealed class Bone
        {
            public Transform Transform;
            public Quaternion AnatomicalOffset, RestModel, GuardWorld, LastLocal;
            public Vector3 RestElbowOffsetModel, GuardPosition;
        }
        private HeroIdentity identity;
        private BasicAttack basic;
        private Health health, target;
        private HeroVisualMotion motion;
        private HeroLocomotion locomotion;
        private AuthoredHeroPresentation presentation;
        private Transform model;
        private Bone[] bones;
        private CombatAnimationLibrary.Clip clip, relaxedIdle, standingIdle;
        private float started = -100f, contact, duration, cancelStarted = -100f;
        private int serial;
        private bool active, mirror;
        private bool standingOffsetApplied;
        private bool reportedStandingPose;
        private Vector3 standingHipsLocal;
        private Vector3 aim;
        private readonly Quaternion[] previousPose = new Quaternion[16];
        public bool OwnsPose => isActiveAndEnabled && (health == null || !health.IsDead) &&
            bones != null && CombatAnimationLibrary.Bones != null;
        public bool StrikeActive => active && Time.time < started + duration;
        public const float OutwardGuardDegrees = 50f;
        public static bool UsesStandingPose(HeroId hero, int bone) => hero != HeroId.Nyra &&
            !(hero == HeroId.Kaelon && bone >= 4 && bone <= 6);
        private void Awake()
        {
            identity = GetComponent<HeroIdentity>(); basic = GetComponent<BasicAttack>();
            health = GetComponent<Health>(); motion = GetComponent<HeroVisualMotion>();
            locomotion = GetComponent<HeroLocomotion>(); presentation = GetComponent<AuthoredHeroPresentation>();
        }
        private void OnEnable()
        {
            reportedStandingPose = false;
            if (bones == null) return;
            relaxedIdle = CombatAnimationLibrary.Find("idle_relaxed");
            standingIdle = CombatAnimationLibrary.Find("mixamo_standing_idle");
        }
        public static MeleeBasicAttackAnimation For(GameObject actor)
        {
            if (actor.GetComponent<HeroIdentity>() == null) return null;
            return actor.GetComponent<MeleeBasicAttackAnimation>() ?? actor.AddComponent<MeleeBasicAttackAnimation>();
        }
        public void InitializeRig(Transform imported)
        {
            if (bones != null || imported == null || CombatAnimationLibrary.Bones == null) return;
            // Also initialize when a preview actor was assembled while inactive.
            identity = GetComponent<HeroIdentity>();
            presentation = GetComponent<AuthoredHeroPresentation>();
            model = imported;
            string[] names = CombatAnimationLibrary.Bones;
            var captured = new Bone[names.Length];
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < names.Length; i++)
            {
                Transform bone = null;
                foreach (Transform part in all) if (part.name == names[i]) { bone = part; break; }
                if (bone == null) { Debug.LogError("DF: combat retarget bone missing: " + names[i], this); return; }
                captured[i] = new Bone { Transform = bone, RestModel = Quaternion.Inverse(model.rotation) * bone.rotation,
                    LastLocal = bone.localRotation };
            }
            // Capture before any pose is written. Anatomical offsets compensate each model's A/T-pose
            // and FBX axes. Hand direction comes from the forearm, not an assumed local bone axis.
            for (int i = 0; i < names.Length; i++)
            {
                int next = i == 0 ? 1 : i == 1 ? 2 : i == 2 ? 3 : i == 4 || i == 5 || i == 7 || i == 8 ||
                    i == 10 || i == 11 || i == 13 || i == 14 ? i + 1 : -1;
                Vector3 along = next >= 0 ? captured[next].Transform.position - captured[i].Transform.position :
                    i == 6 || i == 9 ? captured[i].Transform.position - captured[i-1].Transform.position : model.up;
                if (i == 12 || i == 15) along = model.forward - model.up * .3f;
                Quaternion frame = CombatAnimationLibrary.Frame(model.InverseTransformDirection(along), Vector3.forward);
                captured[i].AnatomicalOffset = Quaternion.Inverse(frame) * captured[i].RestModel;
            }
            // Follow the imported arm's natural elbow bend instead of an exaggerated outside hint.
            for (int upper = 4; upper <= 7; upper += 3)
                captured[upper].RestElbowOffsetModel = model.InverseTransformVector(
                    captured[upper+1].Transform.position - captured[upper].Transform.position);
            bones = captured;
            relaxedIdle = CombatAnimationLibrary.Find("idle_relaxed");
            standingIdle = CombatAnimationLibrary.Find("mixamo_standing_idle");
        }
        private void Start()
        {
            if (bones != null) return;
            Transform root = transform.Find("DF_VisualRoot");
            if (root != null) InitializeRig(root.Find("DF_AuthoredModel"));
        }
        public void SetAim(Vector3 point) { aim = point; }
        public void Begin(float windup, Health attackTarget = null)
        {
            if (identity == null) return;
            target = attackTarget; aim = target != null ? target.transform.position : transform.position + transform.forward;
            serial++; mirror = false;
            switch (identity.Hero)
            {
                case HeroId.Veyr: clip = CombatAnimationLibrary.Find("slash_a"); mirror = serial % 2 == 0; break;
                case HeroId.Kaelon: case HeroId.Nerok: clip = CombatAnimationLibrary.Find("heavy_sweep"); break;
                case HeroId.Rook: clip = CombatAnimationLibrary.Find(serial % 2 == 1 ? "jab" : "cross"); break;
                case HeroId.Nyra: clip = null; break;
                default: clip = CombatAnimationLibrary.Find("spell"); mirror = identity.Hero != HeroId.Mira; break;
            }
            contact = Mathf.Max(.001f, windup);
            float recovery = identity.Hero == HeroId.Veyr ? .40f : identity.Hero == HeroId.Rook ? .34f :
                identity.Hero == HeroId.Nerok ? .49f : identity.Hero == HeroId.Kaelon ? .46f : .36f;
            float budget = basic != null ? basic.AttackCooldown : HeroCombatProfile.StatsFor(identity.Hero).AttackInterval;
            duration = Mathf.Min(contact + recovery, Mathf.Max(contact + .06f, budget * .95f));
            started = Time.time; active = identity.Hero == HeroId.Nyra || clip != null; cancelStarted = -100f;
            // Facing is presentation-only here; BasicAttack keeps the original movement slowdown window.
            if (locomotion == null) locomotion = GetComponent<HeroLocomotion>();
            locomotion?.FaceAction(aim - transform.position, duration);
        }
        public void Seek(float elapsed) { started -= Mathf.Max(0, elapsed); }
        public void CancelStrike()
        {
            if (active && bones != null)
            {
                cancelStarted = Time.time;
                for (int i = 0; i < bones.Length; i++) previousPose[i] = bones[i].LastLocal;
            }
            active = false; target = null;
        }
        private void OnDisable() { CancelStrike(); ResetStandingOffset(); }
        // Called before the upstream pose writer; never accumulate presentation-only hip offsets.
        public void ResetStandingOffset()
        {
            if (standingOffsetApplied && bones != null && bones[0].Transform != null)
                bones[0].Transform.localPosition = standingHipsLocal;
            standingOffsetApplied = false;
        }
        private void Update()
        {
            if (!StrikeActive) return;
            if ((health != null && health.IsDead) || !HeroCombatStatus.CanAct(gameObject)) { CancelStrike(); return; }
            Vector3 facing = target != null ? target.transform.position - transform.position : aim - transform.position;
            if (locomotion == null) locomotion = GetComponent<HeroLocomotion>();
            locomotion?.FaceAction(facing, Mathf.Max(.02f, started + duration - Time.time));
        }
        private void LateUpdate()
        {
            // AuthoredHeroPresentation calls us synchronously AFTER its base-bone writes.
            // This remains correct with local Script Execution Order overrides in Unity.
            if (presentation == null) EvaluatePose();
        }
        public void EvaluatePose()
        {
            if (!OwnsPose || identity == null || (health != null && health.IsDead)) return;
            if (motion == null) motion = GetComponent<HeroVisualMotion>();
            if (presentation == null) presentation = GetComponent<AuthoredHeroPresentation>();
            if (motion != null && motion.AbilityActionActive)
            {
                // Ability presentation owns the rig until its action ends. Blend its actual last pose
                // back to guard instead of dropping raised arms in a single frame.
                active = false; cancelStarted = Time.time;
                for (int i = 0; i < bones.Length; i++) previousPose[i] = bones[i].Transform.localRotation;
                RememberPose(); return;
            }
            ApplyGuard();
            ApplyOutwardGuard();
            StraightenGuardArms();
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].GuardWorld = bones[i].Transform.rotation;
                bones[i].GuardPosition = bones[i].Transform.position;
            }
            float age = Time.time - started;
            if (StrikeActive && age >= 0f)
            {
                if (identity.Hero == HeroId.Nyra) ApplyRifleRecoil(age);
                else ApplyClip(age);
            }
            else active = false;
            if (!active && Time.time - cancelStarted < .16f)
            {
                float weight = CombatMotion.Ease((Time.time - cancelStarted) / .16f);
                // Parent-to-child local blend preserves a connected rig throughout interruptions.
                for (int i = 0; i < 10; i++) bones[i].Transform.localRotation =
                    Quaternion.Slerp(previousPose[i], bones[i].Transform.localRotation, weight);
            }
            presentation?.FinalizeCombatGrip();
            RememberPose();
            if (!reportedStandingPose && standingIdle != null && identity.Hero != HeroId.Nyra)
            {
                reportedStandingPose = true;
                Debug.Log("DF: Standing Idle v4 + straight elbows + outward arms ACTIVE on " + identity.DisplayName +
                    (identity.Hero == HeroId.Kaelon ? " (left shield arm preserved)" : "") + ".", this);
            }
        }
        public string StandingPoseStatus()
        {
            if (identity == null) return "Hero identity missing";
            if (identity.Hero == HeroId.Nyra) return "Sivra excluded as requested";
            if (bones == null) return "Rig has not initialized";
            if (standingIdle == null) return "Mixamo resource missing or invalid";
            if (!OwnsPose) return "Pose disabled/dead";
            if (StrikeActive || motion != null && motion.AbilityActionActive) return "Combat action currently owns pose";
            float largest = 0f;
            foreach (int i in new[] { 4, 5, 7, 8 })
            {
                if (!UsesStandingPose(identity.Hero, i)) continue;
                Quaternion desired = bones[i].GuardWorld;
                largest = Mathf.Max(largest, Quaternion.Angle(bones[i].Transform.rotation, desired));
            }
            float bend = 0f;
            for (int upper = 4; upper <= 7; upper += 3)
            {
                if (!UsesStandingPose(identity.Hero, upper)) continue;
                bend = Mathf.Max(bend, Vector3.Angle(
                    bones[upper+1].Transform.position-bones[upper].Transform.position,
                    bones[upper+2].Transform.position-bones[upper+1].Transform.position));
            }
            return "Straight outward guard; elbow bend " + bend.ToString("F2") +
                " degrees; pose deviation " + largest.ToString("F2") + " degrees";
        }
        public static void StraightenArm(Transform shoulder, Transform elbow, Transform hand)
        {
            if (shoulder == null || elbow == null || hand == null) return;
            Vector3 upper = elbow.position - shoulder.position;
            Vector3 forearm = hand.position - elbow.position;
            // Work in the elbow parent's space to account for the hero's nonuniform scale.
            if (elbow.parent != null)
            {
                upper = elbow.parent.InverseTransformVector(upper);
                forearm = elbow.parent.InverseTransformVector(forearm);
            }
            if (upper.sqrMagnitude < .000001f || forearm.sqrMagnitude < .000001f) return;
            elbow.localRotation = Quaternion.FromToRotation(forearm, upper) * elbow.localRotation;
        }
        private void StraightenGuardArms()
        {
            for (int upper = 4; upper <= 7; upper += 3)
                if (UsesStandingPose(identity.Hero, upper))
                    StraightenArm(bones[upper].Transform, bones[upper+1].Transform, bones[upper+2].Transform);
        }
        private Quaternion outwardLeft = Quaternion.identity, outwardRight = Quaternion.identity;
        public static Quaternion OutwardRotation(Vector3 shoulderToHand, Vector3 outward)
        {
            if (shoulderToHand.sqrMagnitude < .000001f || outward.sqrMagnitude < .000001f)
                return Quaternion.identity;
            // Work from the actual chain direction, not imported FBX local axes. This always
            // moves toward the hero's outer side, even on differently oriented skeletons.
            // Stop at fully outward if less than 50 degrees remain, rather than swinging back in.
            return Quaternion.RotateTowards(Quaternion.identity,
                Quaternion.FromToRotation(shoulderToHand, outward), OutwardGuardDegrees);
        }
        private void ApplyOutwardGuard()
        {
            outwardLeft = outwardRight = Quaternion.identity;
            if (identity.Hero == HeroId.Nyra) return;
            for (int upper = 4; upper <= 7; upper += 3)
            {
                if (!UsesStandingPose(identity.Hero, upper)) continue;
                Vector3 direction = bones[upper+2].Transform.position - bones[upper].Transform.position;
                Quaternion turn = OutwardRotation(direction, model.right * (upper == 4 ? -1f : 1f));
                // Rotate ONLY the shoulder. Its elbow, wrist and weapon children follow once;
                // rotating all three joints separately would accumulate up to 150 degrees.
                bones[upper].Transform.rotation = turn * bones[upper].Transform.rotation;
                if (upper == 4) outwardLeft = turn; else outwardRight = turn;
            }
        }
        private void RememberPose() { for (int i = 0; i < bones.Length; i++) bones[i].LastLocal = bones[i].Transform.localRotation; }
        private float Reach(int upper) => Vector3.Distance(bones[upper].Transform.position, bones[upper+1].Transform.position) +
            Vector3.Distance(bones[upper+1].Transform.position, bones[upper+2].Transform.position);
        private Vector3 GuardPoint(int upper)
        {
            float side = upper == 4 ? -1f : 1f, length = Reach(upper);
            Vector3 offset;
            switch (identity.Hero)
            {
                case HeroId.Rook: offset = new Vector3(side * .34f, -.76f, .04f); break;
                case HeroId.Veyr: offset = new Vector3(side * .40f, -.73f, .06f); break;
                case HeroId.Kaelon: offset = upper == 4 ? new Vector3(-.34f, -.67f, .12f) : new Vector3(.43f, -.72f, .07f); break;
                case HeroId.Nerok: offset = new Vector3(side * .44f, -.73f, .06f); break;
                case HeroId.Elyra: offset = new Vector3(side * .36f, -.76f, .05f); break;
                case HeroId.Mira: offset = new Vector3(side * .35f, -.77f, .04f); break;
                case HeroId.Traveler: offset = new Vector3(side * .37f, -.77f, .04f); break;
                default: offset = new Vector3(side * .37f, -.74f, .05f); break;
            }
            return bones[upper].Transform.position + model.TransformDirection(offset) * length;
        }
        private Vector3 Hint(int upper) => bones[upper].Transform.position +
            model.TransformVector(bones[upper].RestElbowOffsetModel) + transform.forward * (Reach(upper) * .035f);
        private void SolveArm(int upper, Vector3 point)
        {
            CombatMotion.Solve(bones[upper].Transform, bones[upper+1].Transform, bones[upper+2].Transform, point, Hint(upper));
        }
        private void ApplyGuard()
        {
            if (identity.Hero == HeroId.Nyra) return; // Its authored stock/handle sockets are authoritative.
            if (standingIdle != null)
            {
                ApplyStandingBody();
                for (int i = 4; i < 10; i++)
                {
                    // Auren's left upper arm, elbow and wrist keep the existing shield guard.
                    if (!UsesStandingPose(identity.Hero, i)) continue;
                    bones[i].Transform.rotation = model.rotation * standingIdle.Sample(i, 0f, bones.Length) *
                        bones[i].AnatomicalOffset;
                }
                if (identity.Hero == HeroId.Kaelon)
                {
                    SolveArm(4, GuardPoint(4));
                    bones[6].Transform.rotation = model.rotation * bones[6].RestModel;
                }
                presentation?.FinalizeGuardGrip();
                return;
            }
            if (relaxedIdle != null && (identity.Hero == HeroId.Mira || identity.Hero == HeroId.Rook ||
                identity.Hero == HeroId.Veyr || identity.Hero == HeroId.Traveler))
            {
                // Use the downloaded CC0 Idle_Loop, including its actual elbow and wrist
                // directions. Anatomical offsets retarget it without assuming FBX local axes.
                float frame = Mathf.Repeat(Time.time * relaxedIdle.fps, relaxedIdle.frames - 1);
                for (int i = 4; i < 10; i++)
                    bones[i].Transform.rotation = model.rotation * relaxedIdle.Sample(i, frame, bones.Length) *
                        bones[i].AnatomicalOffset;
                // Capture the SAME fitted grip pose for idle and the attack's entry/recovery.
                presentation?.FinalizeGuardGrip();
                return;
            }
            for (int upper = 4; upper <= 7; upper += 3)
            {
                SolveArm(upper, GuardPoint(upper));
                bones[upper+2].Transform.rotation = model.rotation * bones[upper+2].RestModel;
            }
        }
        private void ApplyStandingBody()
        {
            // Fade the standing body out as locomotion takes over; the arms retain the new
            // starting guard. No source translation, root motion or collider change is applied.
            float speed = motion != null ? motion.PlanarSpeed : 0f;
            float weight = motion != null && motion.Dashing ? 0f :
                1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.05f, .8f, speed));
            if (weight <= 0f) return;
            Vector3 footL = bones[12].Transform.position, footR = bones[15].Transform.position;
            Quaternion rotationL = bones[12].Transform.rotation, rotationR = bones[15].Transform.rotation;
            for (int i = 0; i < bones.Length; i++)
            {
                if (i >= 4 && i < 10) continue;
                Quaternion desired = model.rotation * standingIdle.Sample(i, 0f, bones.Length) * bones[i].AnatomicalOffset;
                bones[i].Transform.rotation = Quaternion.Slerp(bones[i].Transform.rotation, desired, weight);
            }
            // Keep the existing ground height and sole orientation on each hero's proportions.
            Vector3 left = bones[12].Transform.position, right = bones[15].Transform.position;
            left.y = footL.y; right.y = footR.y;
            float extension = Mathf.Lerp(.99999f, .975f, weight);
            float drop = Mathf.Max(StandingHipDrop(10, left, extension), StandingHipDrop(13, right, extension));
            standingHipsLocal = bones[0].Transform.localPosition;
            standingOffsetApplied = true;
            bones[0].Transform.position -= Vector3.up * drop;
            CombatMotion.Solve(bones[10].Transform, bones[11].Transform, bones[12].Transform, left, bones[11].Transform.position, extension);
            CombatMotion.Solve(bones[13].Transform, bones[14].Transform, bones[15].Transform, right, bones[14].Transform.position, extension);
            bones[12].Transform.rotation = rotationL; bones[15].Transform.rotation = rotationR;
        }
        private float StandingHipDrop(int thigh, Vector3 foot, float extension)
        {
            float reach = Reach(thigh) * extension;
            Vector3 delta = bones[thigh].Transform.position - foot;
            float verticalReach = Mathf.Sqrt(Mathf.Max(0f, reach * reach - delta.x * delta.x - delta.z * delta.z));
            return Mathf.Max(0f, delta.y - verticalReach);
        }
        private Quaternion SourceRotation(int index, float frame)
        {
            Quaternion q = clip.Sample(mirror ? CombatAnimationLibrary.MirrorBone(index) : index, frame, bones.Length);
            return mirror ? CombatAnimationLibrary.Mirror(q) : q;
        }
        private void ApplyClip(float age)
        {
            Vector3 footL = bones[12].Transform.position, footR = bones[15].Transform.position;
            Vector3 kneeL = bones[11].Transform.position, kneeR = bones[14].Transform.position;
            Quaternion footRotationL = bones[12].Transform.rotation, footRotationR = bones[15].Transform.rotation;
            bool heavy = identity.Hero == HeroId.Kaelon || identity.Hero == HeroId.Nerok;
            float tail = duration - contact;
            // Heavy source contains another windup after frame23: our own recovery begins BEFORE that.
            float sourceEnd = heavy ? contact + tail * .48f : duration;
            float frame = CombatAnimationLibrary.SourceFrame(clip, age, contact, sourceEnd);
            float entry = CombatMotion.Ease(age / Mathf.Max(.015f, contact * .42f));
            float exit = 1f - CombatMotion.Ease((age - (heavy ? sourceEnd : duration - tail * .30f)) /
                Mathf.Max(.01f, heavy ? duration-sourceEnd : tail*.30f));
            float weight = entry * exit;
            for (int i = 0; i < 10; i++)
            {
                if (identity.Hero == HeroId.Mira && i >= 7 || (identity.Hero == HeroId.Elyra || identity.Hero == HeroId.Traveler) && i >= 4 && i <= 6) continue;
                if (heavy && i >= 4 && i <= 6) continue;
                Quaternion desired = model.rotation * SourceRotation(i, frame) * bones[i].AnatomicalOffset;
                if (i < 4)
                {
                    // Keep a forward-facing torso and planted gait; source combo has a full body spin.
                    float limit = i == 0 ? 10f : i == 3 ? 7f : heavy ? 28f : 22f;
                    desired = Quaternion.RotateTowards(bones[i].GuardWorld, desired, limit);
                }
                if (i == 6 || i == 9)
                {
                    // Wrist motion is relative to the source start pose, calibrated onto the real fitted grip.
                    Quaternion delta = SourceRotation(i, frame) * Quaternion.Inverse(SourceRotation(i, 0));
                    Quaternion wristGuard = bones[i].GuardWorld;
                    if (identity.Hero == HeroId.Rook && (clip.name == "jab" ? i == 6 : i == 9))
                    {
                        // The resting fist points down; only the striking fist rotates toward contact.
                        Vector3 anatomical = model.rotation * (bones[i].RestModel *
                            Quaternion.Inverse(bones[i].AnatomicalOffset) * Vector3.forward);
                        wristGuard = Quaternion.FromToRotation(anatomical, transform.forward) * wristGuard;
                    }
                    desired = model.rotation * delta * Quaternion.Inverse(model.rotation) * wristGuard;
                }
                bones[i].Transform.rotation = Quaternion.Slerp(bones[i].GuardWorld, desired, weight);
            }
            // Preserve the current locomotion foot targets, including strafing/backpedaling.
            CombatMotion.Solve(bones[10].Transform, bones[11].Transform, bones[12].Transform, footL, kneeL);
            CombatMotion.Solve(bones[13].Transform, bones[14].Transform, bones[15].Transform, footR, kneeR);
            bones[12].Transform.rotation = footRotationL; bones[15].Transform.rotation = footRotationR;
            if (heavy) ApplyHeavySweep(age);
            else if (identity.Hero == HeroId.Veyr)
            {
                int off = mirror ? 7 : 4;
                SolveArm(off, bones[off+2].GuardPosition); bones[off+2].Transform.rotation = bones[off+2].GuardWorld;
            }
        }
        private void ApplyHeavySweep(float age)
        {
            float length = Reach(7);
            Vector3 shoulder = bones[7].Transform.position, ready = bones[9].GuardPosition;
            Vector3 draw = shoulder + transform.TransformDirection(new Vector3(.62f, -.26f, -.36f)) * length;
            Vector3 hit = shoulder + transform.TransformDirection(new Vector3(.02f, -.27f, .82f)) * length;
            Vector3 through = shoulder + transform.TransformDirection(new Vector3(-.17f, -.27f, .64f)) * length;
            SolveArm(7, SmoothSweep(age, ready, draw, hit, through));
            SolveArm(4, bones[6].GuardPosition);
            bones[6].Transform.rotation = bones[6].GuardWorld;
            // Shaft direction follows its own horizontal arc. The handle remains rigid in the palm.
            Vector3 restAxis = identity.Hero == HeroId.Kaelon ? transform.up : -transform.up;
            Vector3 axis = SmoothSweep(age, restAxis, transform.right*.86f-transform.forward*.50f,
                transform.forward, -transform.right*.84f+transform.forward*.54f).normalized;
            bones[9].Transform.rotation = Quaternion.FromToRotation(restAxis, axis) * bones[9].GuardWorld;
        }
        private Vector3 SmoothSweep(float age, Vector3 guard, Vector3 draw, Vector3 hit, Vector3 through)
        {
            float load = contact*.58f, follow = contact+(duration-contact)*.34f;
            if (age < load) return Vector3.Lerp(guard, draw, CombatMotion.Ease(age/Mathf.Max(.001f,load)));
            // Cubic Hermite with a shared contact velocity: no stop or kink at the damage frame.
            Vector3 velocity = (through-draw) / Mathf.Max(.001f, follow-load);
            if (age < contact) return Hermite(draw, hit, Vector3.zero, velocity*(contact-load), (age-load)/(contact-load));
            if (age < follow) return Hermite(hit, through, velocity*(follow-contact), Vector3.zero, (age-contact)/(follow-contact));
            return Vector3.Lerp(through, guard, CombatMotion.Ease((age-follow)/Mathf.Max(.001f,duration-follow)));
        }
        private static Vector3 Hermite(Vector3 a, Vector3 b, Vector3 va, Vector3 vb, float t)
        {
            t = Mathf.Clamp01(t); float t2=t*t, t3=t2*t;
            return (2*t3-3*t2+1)*a+(t3-2*t2+t)*va+(-2*t3+3*t2)*b+(t3-t2)*vb;
        }
        private void ApplyRifleRecoil(float age)
        {
            // Brace before projectile release, recoil afterwards, and recover with both hands on the rifle.
            float shift = age < contact ? -.018f * CombatMotion.Ease(age/contact) :
                age < contact+.055f ? Mathf.Lerp(-.018f,-.055f,CombatMotion.Ease((age-contact)/.055f)) :
                Mathf.Lerp(-.055f,0,CombatMotion.Ease((age-contact-.055f)/Mathf.Max(.01f,duration-contact-.055f)));
            SolveArm(7, bones[9].Transform.position + transform.forward*shift);
        }
    }
}
