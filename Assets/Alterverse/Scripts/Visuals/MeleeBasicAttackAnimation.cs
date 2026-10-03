using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Presentation-only melee basic attack layer. HeroVisualMotion still owns locomotion/gait;
    /// this component replaces only the short basic-melee strike pose after HeroVisualMotion and
    /// before AuthoredHeroPresentation retargets the pose controls onto imported skeletons.
    /// Combat timing, damage, movement and cooldowns are never changed here.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(935)]
    public sealed class MeleeBasicAttackAnimation : MonoBehaviour
    {
        private HeroIdentity identity;
        private BasicAttack basic;
        private Health health;
        private AuthoredHeroPresentation authored;

        private Transform root;
        private Transform armL, armR, weaponL, weaponR, spine, pelvis;
        private Quaternion baseArmL, baseArmR, baseWeaponL, baseWeaponR, baseSpine, basePelvis;
        private bool baseCaptured;

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
            ResolveControls();
            if (ControlsReady()) CaptureBasePose();
        }

        private void Update()
        {
            if (identity == null || !IsMelee(identity.Hero)) return;
            if (basic == null) basic = GetComponent<BasicAttack>();
            if (basic == null) return;

            float current = basic.RemainingCooldown;
            float cooldown = Mathf.Max(.1f, basic.AttackCooldown);

            // Same robust edge used by the attack-readability layer: only a real 0 -> full
            // cooldown jump starts a strike, so replication corrections cannot double-trigger it.
            if (current > cooldown * .68f && previousCooldown < cooldown * .22f)
                BeginAttack();

            previousCooldown = current;
        }

        private void LateUpdate()
        {
            if (identity == null || !IsMelee(identity.Hero)) return;
            if (health != null && health.IsDead)
            {
                active = false;
                return;
            }

            if (!ResolveControls()) return;

            if (!active)
            {
                CaptureBasePose();
                return;
            }

            float age = Time.time - attackStarted;
            if (age < 0f) return;
            if (age >= animationDuration)
            {
                active = false;
                return;
            }

            if (!baseCaptured) CaptureBasePose();
            ApplyMeleePose(identity.Hero, age);
        }

        private void BeginAttack()
        {
            if (identity == null) return;
            HeroId hero = identity.Hero;
            if (!IsMelee(hero)) return;

            attackSerial++;
            attackStarted = Time.time;
            contactTime = Mathf.Max(.07f, HeroAttackDelivery.Windup(hero));
            animationDuration = hero switch
            {
                HeroId.Veyr => .72f,
                HeroId.Kaelon => .60f,
                HeroId.Rook => .52f,
                HeroId.Nerok => .50f,
                _ => .50f
            };
            active = true;
        }

        private bool ResolveControls()
        {
            if (root == null) root = transform.Find("DF_VisualRoot");
            if (root == null) return false;
            if (authored == null) authored = GetComponent<AuthoredHeroPresentation>();

            if (armL == null) armL = FindControl("ArmL");
            if (armR == null) armR = FindControl("ArmR");
            if (weaponL == null) weaponL = FindControl("WeaponL");
            if (weaponR == null) weaponR = FindControl("WeaponR");
            if (spine == null) spine = FindControl("SpineUpper") ?? FindControl("Torso");
            if (pelvis == null) pelvis = FindControl("PelvisJoint") ?? FindControl("Pelvis");

            return ControlsReady();
        }

        private bool ControlsReady() => armL != null && armR != null;

        private Transform FindControl(string shortName)
        {
            if (authored != null)
            {
                Transform control = authored.Control(shortName);
                if (control != null) return control;
            }

            if (root == null) return null;
            string wanted = "DF_" + shortName;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == wanted) return all[i];
            return null;
        }

        private void CaptureBasePose()
        {
            if (!ControlsReady()) return;
            baseArmL = armL.localRotation;
            baseArmR = armR.localRotation;
            baseWeaponL = weaponL != null ? weaponL.localRotation : Quaternion.identity;
            baseWeaponR = weaponR != null ? weaponR.localRotation : Quaternion.identity;
            baseSpine = spine != null ? spine.localRotation : Quaternion.identity;
            basePelvis = pelvis != null ? pelvis.localRotation : Quaternion.identity;
            baseCaptured = true;
        }

        private void ApplyMeleePose(HeroId hero, float age)
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
            }
        }

        private void ApplyVeyr(float age)
        {
            bool right = (attackSerial & 1) != 0;
            float side = right ? 1f : -1f;

            // Draw the striking blade outside the shoulder, cut across the centre line, then
            // continue past the target. The off hand stays in the captured crossed guard.
            Vector3 draw = new Vector3(-34f, -44f * side, 58f * side);
            Vector3 contact = new Vector3(-82f, 20f * side, -18f * side);
            Vector3 through = new Vector3(-58f, 56f * side, -44f * side);
            Quaternion armPose = StrikeOffset(age, draw, contact, through, .11f, .28f);

            Vector3 weaponDraw = new Vector3(-8f, -28f * side, 34f * side);
            Vector3 weaponContact = new Vector3(18f, 16f * side, -12f * side);
            Vector3 weaponThrough = new Vector3(32f, 48f * side, -30f * side);
            Quaternion weaponPose = StrikeOffset(age, weaponDraw, weaponContact, weaponThrough, .11f, .28f);

            if (right)
            {
                armR.localRotation = baseArmR * armPose;
                if (weaponR != null) weaponR.localRotation = baseWeaponR * weaponPose;
                armL.localRotation = baseArmL;
                if (weaponL != null) weaponL.localRotation = baseWeaponL;
            }
            else
            {
                armL.localRotation = baseArmL * armPose;
                if (weaponL != null) weaponL.localRotation = baseWeaponL * weaponPose;
                armR.localRotation = baseArmR;
                if (weaponR != null) weaponR.localRotation = baseWeaponR;
            }

            ApplyBodyTwist(age, side * -13f, side * 9f, side * 18f, 5f, -4f, -7f);
        }

        private void ApplyKaelon(float age)
        {
            // Heavy side-loaded swing. Nothing goes into the old -150 degree overhead pose:
            // the weapon visibly travels from the rear shoulder, through the target, to follow-through.
            Quaternion rightArm = StrikeOffset(age,
                new Vector3(-40f, -56f, -30f),
                new Vector3(-88f, 10f, -5f),
                new Vector3(-62f, 52f, 22f), .13f, .22f);
            Quaternion leftArm = StrikeOffset(age,
                new Vector3(-28f, -22f, 14f),
                new Vector3(-48f, 7f, 7f),
                new Vector3(-38f, 25f, 12f), .13f, .22f);
            Quaternion weapon = StrikeOffset(age,
                new Vector3(-12f, -38f, -26f),
                new Vector3(16f, 8f, 5f),
                new Vector3(32f, 46f, 24f), .13f, .22f);

            armR.localRotation = baseArmR * rightArm;
            armL.localRotation = baseArmL * leftArm;
            if (weaponR != null) weaponR.localRotation = baseWeaponR * weapon;
            if (weaponL != null) weaponL.localRotation = baseWeaponL;
            ApplyBodyTwist(age, -18f, 7f, 21f, 7f, -5f, -9f);
        }

        private void ApplyRook(float age)
        {
            bool right = (attackSerial & 1) != 0;
            float side = right ? 1f : -1f;

            // True jab/cross: fist retracts beside the ribs first, shoulder and chest rotate,
            // then the arm drives straight through the target instead of lifting upward.
            Vector3 draw = new Vector3(-18f, -58f * side, -32f * side);
            Vector3 contact = new Vector3(-100f, 8f * side, -2f * side);
            Vector3 through = new Vector3(-108f, 22f * side, 8f * side);
            Quaternion punch = StrikeOffset(age, draw, contact, through, .10f, .16f);

            Quaternion brace = StrikeOffset(age,
                new Vector3(-12f, 12f * side, 12f * side),
                new Vector3(-26f, -8f * side, -10f * side),
                new Vector3(-30f, -10f * side, -12f * side), .10f, .16f);

            if (right)
            {
                armR.localRotation = baseArmR * punch;
                armL.localRotation = baseArmL * brace;
            }
            else
            {
                armL.localRotation = baseArmL * Mirror(punch);
                armR.localRotation = baseArmR * Mirror(brace);
            }

            ApplyBodyTwist(age, side * -20f, side * 12f, side * 19f, 8f, -6f, -8f);
        }

        private void ApplyNerok(float age)
        {
            float side = (attackSerial & 1) != 0 ? 1f : -1f;

            // Nerok's anchor is a broad low-to-mid body swing. The draw stays outside and behind
            // the shoulder instead of pointing vertically above the head.
            Quaternion rightArm = StrikeOffset(age,
                new Vector3(-30f, -62f * side, -36f * side),
                new Vector3(-84f, 16f * side, -10f * side),
                new Vector3(-56f, 68f * side, 30f * side), .12f, .24f);
            Quaternion leftArm = StrikeOffset(age,
                new Vector3(-26f, -28f * side, 16f * side),
                new Vector3(-46f, 8f * side, 4f * side),
                new Vector3(-38f, 28f * side, 14f * side), .12f, .24f);
            Quaternion weapon = StrikeOffset(age,
                new Vector3(-16f, -46f * side, -30f * side),
                new Vector3(10f, 14f * side, 5f * side),
                new Vector3(28f, 64f * side, 32f * side), .12f, .24f);

            armR.localRotation = baseArmR * rightArm;
            armL.localRotation = baseArmL * leftArm;
            if (weaponR != null) weaponR.localRotation = baseWeaponR * weapon;
            if (weaponL != null) weaponL.localRotation = baseWeaponL;
            ApplyBodyTwist(age, side * -23f, side * 10f, side * 26f, 8f, -5f, -10f);
        }

        private Quaternion StrikeOffset(float age, Vector3 drawn, Vector3 contact, Vector3 through,
            float followSeconds, float recoverySeconds)
        {
            float windup = Mathf.Max(.07f, contactTime);
            float loadEnd = windup * .58f;
            Quaternion qDraw = Quaternion.Euler(drawn);
            Quaternion qContact = Quaternion.Euler(contact);
            Quaternion qThrough = Quaternion.Euler(through);

            if (age < loadEnd)
                return Quaternion.Slerp(Quaternion.identity, qDraw, Ease(age / Mathf.Max(.001f, loadEnd)));

            if (age < windup)
                return Quaternion.Slerp(qDraw, qContact,
                    Ease((age - loadEnd) / Mathf.Max(.001f, windup - loadEnd)));

            float followEnd = windup + followSeconds;
            if (age < followEnd)
                return Quaternion.Slerp(qContact, qThrough,
                    Ease((age - windup) / Mathf.Max(.001f, followSeconds)));

            float recoverEnd = followEnd + recoverySeconds;
            if (age < recoverEnd)
                return Quaternion.Slerp(qThrough, Quaternion.identity,
                    Ease((age - followEnd) / Mathf.Max(.001f, recoverySeconds)));

            return Quaternion.identity;
        }

        private void ApplyBodyTwist(float age, float drawYaw, float contactYaw, float throughYaw,
            float drawPitch, float contactPitch, float throughPitch)
        {
            float yaw = StrikeScalar(age, drawYaw, contactYaw, throughYaw, .12f, .24f);
            float pitch = StrikeScalar(age, drawPitch, contactPitch, throughPitch, .12f, .24f);

            if (spine != null)
                spine.localRotation = baseSpine * Quaternion.Euler(pitch, yaw, -yaw * .16f);
            if (pelvis != null)
                pelvis.localRotation = basePelvis * Quaternion.Euler(pitch * .24f, -yaw * .38f, yaw * .08f);
        }

        private float StrikeScalar(float age, float drawn, float contact, float through,
            float followSeconds, float recoverySeconds)
        {
            float windup = Mathf.Max(.07f, contactTime);
            float loadEnd = windup * .58f;
            if (age < loadEnd) return Mathf.Lerp(0f, drawn, Ease(age / Mathf.Max(.001f, loadEnd)));
            if (age < windup) return Mathf.Lerp(drawn, contact,
                Ease((age - loadEnd) / Mathf.Max(.001f, windup - loadEnd)));
            float followEnd = windup + followSeconds;
            if (age < followEnd) return Mathf.Lerp(contact, through,
                Ease((age - windup) / Mathf.Max(.001f, followSeconds)));
            float recoverEnd = followEnd + recoverySeconds;
            if (age < recoverEnd) return Mathf.Lerp(through, 0f,
                Ease((age - followEnd) / Mathf.Max(.001f, recoverySeconds)));
            return 0f;
        }

        private static Quaternion Mirror(Quaternion rotation)
        {
            Vector3 e = rotation.eulerAngles;
            e.y = -Mathf.DeltaAngle(0f, e.y);
            e.z = -Mathf.DeltaAngle(0f, e.z);
            e.x = Mathf.DeltaAngle(0f, e.x);
            return Quaternion.Euler(e);
        }

        private static float Ease(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static bool IsMelee(HeroId hero)
        {
            HeroCombatStats stats = HeroCombatProfile.StatsFor(hero);
            return stats.AttackRange <= 3f;
        }

        private void OnDisable()
        {
            active = false;
            baseCaptured = false;
            attackStarted = -100f;
        }
    }
}
