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
            public Quaternion AnatomicalOffset, RestModel, RestLocal, GuardWorld, LastLocal;
            public Vector3 RestElbowOffsetModel, GuardPosition, RestPosition;
        }
        private HeroIdentity identity;
        private BasicAttack basic;
        private Health health, target;
        private HeroVisualMotion motion;
        private HeroLocomotion locomotion;
        private AuthoredHeroPresentation presentation;
        private Transform model;
        private Bone[] bones;
        private CombatAnimationLibrary.Clip clip;
        private float started = -100f, contact, duration, cancelStarted = -100f;
        private int serial;
        private bool active, mirror, channelStrike;
        private bool reportedStandingPose;
        private float armGaitWeight = 1f;
        private Vector3 aim;
        private readonly Quaternion[] previousPose = new Quaternion[16];
        public bool OwnsPose => isActiveAndEnabled && (health == null || !health.IsDead) &&
            bones != null && CombatAnimationLibrary.Bones != null;
        public bool StrikeActive => active && Time.time < started + duration;
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
            armGaitWeight = 1f;
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
                    RestLocal = bone.localRotation, RestPosition = bone.localPosition, LastLocal = bone.localRotation };
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
            // Scratch ticks have already made contact: 1ms windup is a presentation-only channel pulse.
            channelStrike = identity.Hero == HeroId.Timmy && windup <= .00101f;
            target = attackTarget; aim = target != null ? target.transform.position : transform.position + transform.forward;
            serial++; mirror = false;
            switch (identity.Hero)
            {
                case HeroId.Veyr: case HeroId.Timmy: clip = CombatAnimationLibrary.Find("slash_a"); mirror = serial % 2 == 0; break;
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
            active = false; channelStrike = false; target = null;
        }
        private void OnDisable() { CancelStrike(); }
        private void Update()
        {
            if (!StrikeActive) return;
            bool channelPose = channelStrike && (GetComponent<HeroCombatStatus>()?.Stunned != true || !Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay);
            if ((health != null && health.IsDead) || !HeroCombatStatus.CanAct(gameObject) && !channelPose) { CancelStrike(); return; }
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
                armGaitWeight = 0f;
                // Ability presentation owns the rig until its action ends. Blend its actual last pose
                // back to guard instead of dropping raised arms in a single frame.
                active = false; cancelStarted = Time.time;
                for (int i = 0; i < bones.Length; i++) previousPose[i] = bones[i].Transform.localRotation;
                RememberPose(); return;
            }
            ApplyGuard();
            ApplyLocomotionArms();
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
            presentation?.FinalizeCombatGrip(false);
            RememberPose();
            if (!reportedStandingPose && identity.Hero != HeroId.Nyra)
            {
                reportedStandingPose = true;
                HeroVisualDiagnostics.Trace("DF: imported rest pose RESET v5 active on " + identity.DisplayName +
                    (identity.Hero == HeroId.Kaelon ? " (left shield arm preserved)" : "") + ".", this);
            }
        }
        public string StandingPoseStatus()
        {
            if (identity == null) return "Hero identity missing";
            if (identity.Hero == HeroId.Nyra) return "Sivra excluded as requested";
            if (bones == null) return "Rig has not initialized";
            if (!OwnsPose) return "Pose disabled/dead";
            if (StrikeActive || motion != null && motion.AbilityActionActive) return "Combat action currently owns pose";
            float largest = 0f;
            foreach (int i in new[] { 4, 5, 7, 8 })
            {
                if (!UsesStandingPose(identity.Hero, i)) continue;
                Quaternion desired = bones[i].GuardWorld;
                largest = Mathf.Max(largest, Quaternion.Angle(bones[i].Transform.rotation, desired));
            }
            return "Imported rest pose RESET v5; final arm deviation " + largest.ToString("F2") + " degrees";
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
                // Bent shield arm in front of the chest; hammer hand beside the torso.
                case HeroId.Kaelon: offset = upper == 4 ? new Vector3(-.16f, -.44f, .44f) : new Vector3(.34f, -.53f, .42f); break;
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
            // Reset to the imported local rest transforms, before any retargeting or IK.
            // Never reuse LastLocal: that is the previous rendered (possibly corrupted) pose.
            float speed = motion != null ? motion.PlanarSpeed : 0f;
            bool stationary = speed < .05f && (motion == null || !motion.Dashing);
            for (int i = 0; i < bones.Length; i++)
            {
                if (!UsesStandingPose(identity.Hero, i)) continue;
                // The authored retarget layer already supplied a fresh body pose this frame.
                // Resetting it here erased breathing, head balance and turn anticipation.
                if (i < 4 && motion != null && motion.isActiveAndEnabled) continue;
                if ((i < 4 || i >= 10) && !stationary) continue;
                bones[i].Transform.localRotation = bones[i].RestLocal;
                bones[i].Transform.localPosition = bones[i].RestPosition;
            }
            if (identity.Hero == HeroId.Kaelon)
            {
                // Bend the shield arm forward while keeping its independent wrist orientation.
                SolveArm(4, GuardPoint(4));
                bones[6].Transform.rotation = model.rotation * bones[6].RestModel;
                // Bring Auren's hammer hand forward with the whole arm chain. A wrist-only
                // correction leaves the shaft behind his shoulder in the imported rest pose.
                SolveArm(7, GuardPoint(7));
                // Establish the hammer's grip orientation before GuardWorld is captured;
                // attack/recovery then inherit it instead of a second LateUpdate fighting them.
                model.GetComponent<SiegeTankAppearance>()?.OrientHammerGuard(transform, bones[9].Transform);
            }
        }
        private void ApplyLocomotionArms()
        {
            if (motion == null || !motion.isActiveAndEnabled || model == null) return;
            bool action = StrikeActive || motion.ArmActionActive || Time.time - cancelStarted < .16f;
            armGaitWeight = Mathf.MoveTowards(armGaitWeight, action ? 0f : 1f, Time.deltaTime * 12f);
            float weight = motion.GroundingSuspended ? 0f : motion.LocomotionWeight * armGaitWeight;
            Vector3 direction = Quaternion.Inverse(motion.PresentedRotation) * motion.GaitDirection;
            HeroUpperBodyGait.Pose pose = HeroUpperBodyGait.Sample(identity.Hero, motion.GaitPhase,
                weight, motion.RunBlend, direction.z, direction.x);
            Vector3 axis = model.right;
            // Preserve both authored rifle contacts and the established left shield guard.
            if (identity.Hero == HeroId.Nyra) return;
            if (identity.Hero != HeroId.Kaelon) ApplyArmGait(4, pose.leftShoulder, pose.leftElbow, axis);
            ApplyArmGait(7, pose.rightShoulder, pose.rightElbow, axis);
        }
        private void ApplyArmGait(int upper, float shoulder, float elbow, Vector3 axis)
        {
            // World axes avoid assumptions about the imported FBX's local bone frames.
            bones[upper].Transform.rotation = Quaternion.AngleAxis(shoulder, axis) * bones[upper].Transform.rotation;
            bones[upper + 1].Transform.rotation = Quaternion.AngleAxis(-elbow, axis) * bones[upper + 1].Transform.rotation;
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
            Vector3 restAxis = identity.Hero == HeroId.Kaelon ? SiegeTankAppearance.HammerGuardShaft(transform) : -transform.up;
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
