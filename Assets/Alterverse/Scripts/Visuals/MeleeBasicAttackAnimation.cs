using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Final-pass melee basic animation for imported hero rigs.
    /// Runs after AuthoredHeroPresentation so the strike is solved from real shoulder/elbow/hand
    /// positions instead of guessed local Euler axes. This makes draw-back, contact and follow-through
    /// consistent even when imported heroes use different bone orientations.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(950)]
    public sealed class MeleeBasicAttackAnimation : MonoBehaviour
    {
        private sealed class ArmChain
        {
            public Transform Upper;
            public Transform Elbow;
            public Transform Hand;
        }

        private HeroIdentity identity;
        private BasicAttack basic;
        private Health health;
        private Transform model;
        private Transform hips;
        private Transform chest;
        private ArmChain leftArm;
        private ArmChain rightArm;

        private float previousCooldown;
        private float attackStarted = -100f;
        private float contactTime;
        private float animationDuration;
        private int attackSerial;
        private bool active;

        private void Awake()
        {
            identity = GetComponent<HeroIdentity>();
            basic = GetComponent<BasicAttack>();
            health = GetComponent<Health>();
        }

        private void Start()
        {
            if (basic == null) basic = GetComponent<BasicAttack>();
            previousCooldown = basic != null ? basic.RemainingCooldown : 0f;
            ResolveRig();
        }

        private void Update()
        {
            if (identity == null || !IsMelee(identity.Hero)) return;
            if (basic == null) basic = GetComponent<BasicAttack>();
            if (basic == null) return;

            float current = basic.RemainingCooldown;
            float cooldown = Mathf.Max(.1f, basic.AttackCooldown);

            // Only the real 0 -> full cooldown edge begins a basic strike. This also works for
            // touch input because BasicAttack owns the cooldown independently of input source.
            if (current > cooldown * .68f && previousCooldown < cooldown * .22f)
                BeginAttack();

            previousCooldown = current;
        }

        private void LateUpdate()
        {
            if (!active || identity == null || !IsMelee(identity.Hero)) return;
            if (health != null && health.IsDead)
            {
                active = false;
                return;
            }
            if (!ResolveRig()) return;

            float age = Time.time - attackStarted;
            if (age < 0f) return;
            if (age >= animationDuration)
            {
                active = false;
                return;
            }

            ApplyStrike(identity.Hero, age);
        }

        private void BeginAttack()
        {
            HeroId hero = identity.Hero;
            attackSerial++;
            attackStarted = Time.time;
            contactTime = Mathf.Max(.10f, HeroAttackDelivery.Windup(hero));
            animationDuration = contactTime + FollowSeconds(hero) + RecoverySeconds(hero);
            active = true;
        }

        private bool ResolveRig()
        {
            if (model == null)
            {
                Transform visualRoot = transform.Find("DF_VisualRoot");
                if (visualRoot != null) model = visualRoot.Find("DF_AuthoredModel");
                if (model == null)
                {
                    Transform[] all = GetComponentsInChildren<Transform>(true);
                    for (int i = 0; i < all.Length; i++)
                        if (all[i] != null && all[i].name == "DF_AuthoredModel") { model = all[i]; break; }
                }
            }
            if (model == null) return false;

            if (leftArm == null) leftArm = CaptureArm("L");
            if (rightArm == null) rightArm = CaptureArm("R");
            if (hips == null) hips = FindBone("Hips");
            if (chest == null) chest = FindBone("Chest") ?? FindBone("Spine");
            return leftArm != null && rightArm != null;
        }

        private ArmChain CaptureArm(string side)
        {
            Transform upper = FindBone("UpperArm" + side);
            Transform elbow = FindBone("Forearm" + side);
            Transform hand = FindBone("Hand" + side);
            return upper != null && elbow != null && hand != null
                ? new ArmChain { Upper = upper, Elbow = elbow, Hand = hand }
                : null;
        }

        private Transform FindBone(string boneName)
        {
            if (model == null) return null;
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == boneName) return all[i];
            return null;
        }

        private void ApplyStrike(HeroId hero, float age)
        {
            switch (hero)
            {
                case HeroId.Veyr:
                    ApplyVeyr(age);
                    break;
                case HeroId.Kaelon:
                    ApplyKaelon(age);
                    break;
                case HeroId.Rook:
                    ApplyRook(age);
                    break;
                case HeroId.Nerok:
                    ApplyNerok(age);
                    break;
                default:
                    ApplyGenericMelee(age);
                    break;
            }
        }

        private void ApplyVeyr(float age)
        {
            bool strikeRight = (attackSerial & 1) != 0;
            ArmChain strike = strikeRight ? rightArm : leftArm;
            ArmChain guard = strikeRight ? leftArm : rightArm;
            float side = strikeRight ? 1f : -1f;

            float reach = Reach(strike);
            Vector3 shoulder = strike.Upper.position;
            Vector3 ready = GuardPoint(strike, side, .34f);
            Vector3 draw = shoulder + transform.right * (side * .30f) + transform.up * -.03f - transform.forward * .16f;
            Vector3 contact = shoulder - transform.right * (side * .10f) + transform.up * -.12f + transform.forward * (reach * .88f);
            Vector3 through = shoulder - transform.right * (side * .34f) + transform.up * -.18f + transform.forward * (reach * .70f);
            Vector3 target = EvaluatePath(age, ready, draw, contact, through, HeroId.Veyr);

            SolveArm(strike, target, ElbowHint(strike, side, .48f), 1f);
            SolveArm(guard, GuardPoint(guard, -side, .31f), ElbowHint(guard, -side, .42f), 1f);
            ApplyBodyDrive(age, HeroId.Veyr, side, -17f, 10f, 22f, 4f, -5f, -7f);
        }

        private void ApplyKaelon(float age)
        {
            float side = 1f;
            float reach = Reach(rightArm);
            Vector3 shoulder = rightArm.Upper.position;
            Vector3 ready = GuardPoint(rightArm, side, .31f);

            // Heavy weapon draw is deliberately behind and outside the right shoulder. The hand
            // then travels horizontally through the target instead of lifting vertically overhead.
            Vector3 draw = shoulder + transform.right * .34f + transform.up * .05f - transform.forward * .20f;
            Vector3 contact = shoulder - transform.right * .13f + transform.up * -.15f + transform.forward * (reach * .84f);
            Vector3 through = shoulder - transform.right * .40f + transform.up * -.22f + transform.forward * (reach * .60f);
            Vector3 target = EvaluatePath(age, ready, draw, contact, through, HeroId.Kaelon);
            SolveArm(rightArm, target, ElbowHint(rightArm, side, .52f), 1f);

            // The support arm travels with the strike rather than remaining frozen while the
            // weapon hand moves across the body.
            float leftReach = Reach(leftArm);
            Vector3 leftShoulder = leftArm.Upper.position;
            Vector3 leftReady = GuardPoint(leftArm, -1f, .28f);
            Vector3 leftDraw = leftShoulder - transform.right * .15f + transform.up * -.07f + transform.forward * .06f;
            Vector3 leftContact = leftShoulder + transform.right * .08f + transform.up * -.16f + transform.forward * (leftReach * .62f);
            Vector3 leftThrough = leftShoulder + transform.right * .18f + transform.up * -.18f + transform.forward * (leftReach * .50f);
            SolveArm(leftArm, EvaluatePath(age, leftReady, leftDraw, leftContact, leftThrough, HeroId.Kaelon),
                ElbowHint(leftArm, -1f, .46f), 1f);

            ApplyBodyDrive(age, HeroId.Kaelon, side, -22f, 9f, 27f, 7f, -6f, -10f);
        }

        private void ApplyRook(float age)
        {
            bool strikeRight = (attackSerial & 1) != 0;
            ArmChain strike = strikeRight ? rightArm : leftArm;
            ArmChain guard = strikeRight ? leftArm : rightArm;
            float side = strikeRight ? 1f : -1f;
            float reach = Reach(strike);
            Vector3 shoulder = strike.Upper.position;

            // Jab/cross path: fist visibly retracts beside the ribs, then drives almost straight
            // forward and continues slightly through the target. No local bone-angle guessing.
            Vector3 ready = GuardPoint(strike, side, .30f);
            Vector3 draw = shoulder + transform.right * (side * .16f) + transform.up * -.20f + transform.forward * .01f;
            Vector3 contact = shoulder - transform.right * (side * .035f) + transform.up * -.08f + transform.forward * (reach * .94f);
            Vector3 through = shoulder - transform.right * (side * .10f) + transform.up * -.07f + transform.forward * (reach * .98f);
            SolveArm(strike, EvaluatePath(age, ready, draw, contact, through, HeroId.Rook),
                ElbowHint(strike, side, .45f), 1f);

            Vector3 guardTarget = GuardPoint(guard, -side, .34f);
            SolveArm(guard, guardTarget, ElbowHint(guard, -side, .42f), 1f);
            ApplyBodyDrive(age, HeroId.Rook, side, -24f, 13f, 20f, 8f, -6f, -8f);
        }

        private void ApplyNerok(float age)
        {
            float side = (attackSerial & 1) != 0 ? 1f : -1f;
            float reach = Reach(rightArm);
            Vector3 shoulder = rightArm.Upper.position;
            Vector3 ready = GuardPoint(rightArm, 1f, .30f);

            // Broad low-to-mid anchor sweep: large lateral travel is what sells the weight.
            Vector3 draw = shoulder + transform.right * (side * .38f) + transform.up * -.08f - transform.forward * .18f;
            Vector3 contact = shoulder - transform.right * (side * .12f) + transform.up * -.18f + transform.forward * (reach * .83f);
            Vector3 through = shoulder - transform.right * (side * .44f) + transform.up * -.24f + transform.forward * (reach * .58f);
            SolveArm(rightArm, EvaluatePath(age, ready, draw, contact, through, HeroId.Nerok),
                ElbowHint(rightArm, side, .54f), 1f);

            // Off hand braces in front of the body so the silhouette reads as a heavy two-arm swing.
            float leftReach = Reach(leftArm);
            Vector3 leftShoulder = leftArm.Upper.position;
            Vector3 leftReady = GuardPoint(leftArm, -1f, .27f);
            Vector3 leftDraw = leftShoulder - transform.right * .16f + transform.up * -.12f + transform.forward * .04f;
            Vector3 leftContact = leftShoulder + transform.right * .06f + transform.up * -.18f + transform.forward * (leftReach * .58f);
            Vector3 leftThrough = leftShoulder + transform.right * .17f + transform.up * -.20f + transform.forward * (leftReach * .46f);
            SolveArm(leftArm, EvaluatePath(age, leftReady, leftDraw, leftContact, leftThrough, HeroId.Nerok),
                ElbowHint(leftArm, -1f, .46f), 1f);

            ApplyBodyDrive(age, HeroId.Nerok, side, -26f, 11f, 31f, 8f, -6f, -11f);
        }

        private void ApplyGenericMelee(float age)
        {
            bool strikeRight = (attackSerial & 1) != 0;
            ArmChain strike = strikeRight ? rightArm : leftArm;
            ArmChain guard = strikeRight ? leftArm : rightArm;
            float side = strikeRight ? 1f : -1f;
            float reach = Reach(strike);
            Vector3 shoulder = strike.Upper.position;
            Vector3 ready = GuardPoint(strike, side, .30f);
            Vector3 draw = shoulder + transform.right * (side * .25f) + transform.up * -.08f - transform.forward * .12f;
            Vector3 contact = shoulder - transform.right * (side * .06f) + transform.up * -.12f + transform.forward * (reach * .88f);
            Vector3 through = shoulder - transform.right * (side * .25f) + transform.up * -.16f + transform.forward * (reach * .70f);
            SolveArm(strike, EvaluatePath(age, ready, draw, contact, through, identity.Hero),
                ElbowHint(strike, side, .46f), 1f);
            SolveArm(guard, GuardPoint(guard, -side, .31f), ElbowHint(guard, -side, .42f), 1f);
            ApplyBodyDrive(age, identity.Hero, side, -18f, 9f, 22f, 5f, -4f, -7f);
        }

        private Vector3 EvaluatePath(float age, Vector3 ready, Vector3 draw, Vector3 contact, Vector3 through, HeroId hero)
        {
            float windup = Mathf.Max(.10f, contactTime);
            float loadEnd = windup * .58f;
            if (age < loadEnd)
                return Vector3.Lerp(ready, draw, Ease(age / Mathf.Max(.001f, loadEnd)));
            if (age < windup)
                return Vector3.Lerp(draw, contact, Ease((age - loadEnd) / Mathf.Max(.001f, windup - loadEnd)));

            float follow = FollowSeconds(hero);
            float followEnd = windup + follow;
            if (age < followEnd)
                return Vector3.Lerp(contact, through, Ease((age - windup) / Mathf.Max(.001f, follow)));

            float recovery = RecoverySeconds(hero);
            return Vector3.Lerp(through, ready, Ease((age - followEnd) / Mathf.Max(.001f, recovery)));
        }

        private void ApplyBodyDrive(float age, HeroId hero, float side,
            float drawYaw, float contactYaw, float throughYaw,
            float drawPitch, float contactPitch, float throughPitch)
        {
            float yaw = EvaluateScalar(age, hero, drawYaw * side, contactYaw * side, throughYaw * side);
            float pitch = EvaluateScalar(age, hero, drawPitch, contactPitch, throughPitch);

            // World-space axes are stable across imported FBX bone coordinate systems.
            if (hips != null)
            {
                hips.rotation = Quaternion.AngleAxis(-yaw * .34f, transform.up) * hips.rotation;
                hips.rotation = Quaternion.AngleAxis(pitch * .18f, transform.right) * hips.rotation;
            }
            if (chest != null)
            {
                chest.rotation = Quaternion.AngleAxis(yaw, transform.up) * chest.rotation;
                chest.rotation = Quaternion.AngleAxis(pitch, transform.right) * chest.rotation;
            }
        }

        private float EvaluateScalar(float age, HeroId hero, float draw, float contact, float through)
        {
            float windup = Mathf.Max(.10f, contactTime);
            float loadEnd = windup * .58f;
            if (age < loadEnd) return Mathf.Lerp(0f, draw, Ease(age / Mathf.Max(.001f, loadEnd)));
            if (age < windup) return Mathf.Lerp(draw, contact, Ease((age - loadEnd) / Mathf.Max(.001f, windup - loadEnd)));
            float follow = FollowSeconds(hero);
            float followEnd = windup + follow;
            if (age < followEnd) return Mathf.Lerp(contact, through, Ease((age - windup) / Mathf.Max(.001f, follow)));
            return Mathf.Lerp(through, 0f, Ease((age - followEnd) / Mathf.Max(.001f, RecoverySeconds(hero))));
        }

        private Vector3 GuardPoint(ArmChain arm, float side, float forwardDistance)
        {
            return arm.Upper.position + transform.right * (side * .045f) + transform.up * -.20f + transform.forward * forwardDistance;
        }

        private Vector3 ElbowHint(ArmChain arm, float side, float outside)
        {
            return arm.Upper.position + transform.right * (side * outside) + transform.up * -.34f + transform.forward * .08f;
        }

        private static float Reach(ArmChain arm)
        {
            if (arm == null) return .65f;
            return Vector3.Distance(arm.Upper.position, arm.Elbow.position) +
                   Vector3.Distance(arm.Elbow.position, arm.Hand.position);
        }

        private static void SolveArm(ArmChain arm, Vector3 target, Vector3 elbowHint, float weight)
        {
            if (arm == null || weight <= .001f) return;
            Vector3 shoulder = arm.Upper.position;
            float upperLength = Vector3.Distance(shoulder, arm.Elbow.position);
            float lowerLength = Vector3.Distance(arm.Elbow.position, arm.Hand.position);
            if (upperLength < .001f || lowerLength < .001f) return;

            Vector3 toTarget = target - shoulder;
            if (toTarget.sqrMagnitude < .000001f) return;
            float distance = Mathf.Clamp(toTarget.magnitude,
                Mathf.Abs(upperLength - lowerLength) + .001f,
                upperLength + lowerLength - .002f);
            Vector3 direction = toTarget.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(elbowHint - shoulder, direction);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(Vector3.right, direction);
            bend.Normalize();

            float along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 elbowTarget = shoulder + direction * along + bend * height;
            Vector3 clampedTarget = shoulder + direction * distance;

            Quaternion upperBefore = arm.Upper.rotation;
            Quaternion elbowBefore = arm.Elbow.rotation;
            arm.Upper.rotation = Quaternion.FromToRotation(arm.Elbow.position - shoulder, elbowTarget - shoulder) * arm.Upper.rotation;
            arm.Elbow.rotation = Quaternion.FromToRotation(arm.Hand.position - arm.Elbow.position, clampedTarget - arm.Elbow.position) * arm.Elbow.rotation;
            arm.Upper.rotation = Quaternion.Slerp(upperBefore, arm.Upper.rotation, weight);
            arm.Elbow.rotation = Quaternion.Slerp(elbowBefore, arm.Elbow.rotation, weight);
        }

        private static float FollowSeconds(HeroId hero) => hero switch
        {
            HeroId.Veyr => .15f,
            HeroId.Kaelon => .18f,
            HeroId.Rook => .11f,
            HeroId.Nerok => .17f,
            _ => .14f
        };

        private static float RecoverySeconds(HeroId hero) => hero switch
        {
            HeroId.Veyr => .26f,
            HeroId.Kaelon => .30f,
            HeroId.Rook => .22f,
            HeroId.Nerok => .28f,
            _ => .25f
        };

        private static float Ease(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static bool IsMelee(HeroId hero) => HeroCombatProfile.StatsFor(hero).AttackRange <= 3f;
    }
}
