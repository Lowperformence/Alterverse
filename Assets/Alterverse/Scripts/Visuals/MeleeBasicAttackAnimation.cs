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
        private Transform armL, armR, elbowL, elbowR, wristL, wristR, weaponL, weaponR, spine, pelvis;
        private Quaternion baseArmL, baseArmR, baseElbowL, baseElbowR, baseWristL, baseWristR;
        private Quaternion baseWeaponL, baseWeaponR, baseSpine, basePelvis;
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

            // Only a real 0 -> full cooldown jump starts a strike. Network corrections cannot
            // repeatedly restart the arm pose while an attack is already being presented.
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
            if (elbowL == null) elbowL = FindControl("ElbowJointL");
            if (elbowR == null) elbowR = FindControl("ElbowJointR");
            if (wristL == null) wristL = FindControl("WristJointL");
            if (wristR == null) wristR = FindControl("WristJointR");
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
            baseElbowL = elbowL != null ? elbowL.localRotation : Quaternion.identity;
            baseElbowR = elbowR != null ? elbowR.localRotation : Quaternion.identity;
            baseWristL = wristL != null ? wristL.localRotation : Quaternion.identity;
            baseWristR = wristR != null ? wristR.localRotation : Quaternion.identity;
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
                case HeroId.Veyr: ApplyVeyr(age); break;
                case HeroId.Kaelon: ApplyKaelon(age); break;
                case HeroId.Rook: ApplyRook(age); break;
                case HeroId.Nerok: ApplyNerok(age); break;
            }
        }

        private void ApplyVeyr(float age)
        {
            bool right = (attackSerial & 1) != 0;
            float side = right ? 1f : -1f;

            // Alternate the blades. The striking hand first opens outside the shoulder, then
            // cuts through centre mass and continues beyond the target instead of simply lifting.
            Quaternion armPose = StrikeOffset(age,
                new Vector3(-34f, -44f * side, 58f * side),
                new Vector3(-82f, 20f * side, -18f * side),
                new Vector3(-58f, 56f * side, -44f * side), .11f, .28f);
            Quaternion elbowPose = StrikeOffset(age,
                new Vector3(-48f, 0f, 0f), new Vector3(-18f, 0f, 0f),
                new Vector3(-12f, 0f, 0f), .11f, .28f);
            Quaternion wristPose = StrikeOffset(age,
                new Vector3(-8f, -10f * side, 12f * side),
                new Vector3(8f, 8f * side, -6f * side),
                new Vector3(15f, 16f * side, -12f * side), .11f, .28f);
            Quaternion weaponPose = StrikeOffset(age,
                new Vector3(-8f, -28f * side, 34f * side),
                new Vector3(18f, 16f * side, -12f * side),
                new Vector3(32f, 48f * side, -30f * side), .11f, .28f);

            if (right)
            {
                armR.localRotation = baseArmR * armPose;
                if (elbowR != null) elbowR.localRotation = baseElbowR * elbowPose;
                if (wristR != null) wristR.localRotation = baseWristR * wristPose;
                if (weaponR != null) weaponR.localRotation = baseWeaponR * weaponPose;
                armL.localRotation = baseArmL;
                if (elbowL != null) elbowL.localRotation = baseElbowL;
                if (wristL != null) wristL.localRotation = baseWristL;
                if (weaponL != null) weaponL.localRotation = baseWeaponL;
            }
            else
            {
                armL.localRotation = baseArmL * armPose;
                if (elbowL != null) elbowL.localRotation = baseElbowL * elbowPose;
                if (wristL != null) wristL.localRotation = baseWristL * wristPose;
                if (weaponL != null) weaponL.localRotation = baseWeaponL * weaponPose;
                armR.localRotation = baseArmR;
                if (elbowR != null) elbowR.localRotation = baseElbowR;
                if (wristR != null) wristR.localRotation = baseWristR;
                if (weaponR != null) weaponR.localRotation = baseWeaponR;
            }

            ApplyBodyTwist(age, side * -13f, side * 9f, side * 18f, 5f, -4f, -7f);
        }

        private void ApplyKaelon(float age)
        {
            // Auren loads the weapon beside/behind his right shoulder. The old near-vertical
            // -150 degree arm lift is deliberately absent: this is a horizontal heavy swing.
            armR.localRotation = baseArmR * StrikeOffset(age,
                new Vector3(-40f, -56f, -30f), new Vector3(-88f, 10f, -5f),
                new Vector3(-62f, 52f, 22f), .13f, .22f);
            armL.localRotation = baseArmL * StrikeOffset(age,
                new Vector3(-28f, -22f, 14f), new Vector3(-48f, 7f, 7f),
                new Vector3(-38f, 25f, 12f), .13f, .22f);
            if (elbowR != null) elbowR.localRotation = baseElbowR * StrikeOffset(age,
                new Vector3(-56f, 0f, 0f), new Vector3(-24f, 0f, 0f),
                new Vector3(-16f, 0f, 0f), .13f, .22f);
            if (elbowL != null) elbowL.localRotation = baseElbowL * StrikeOffset(age,
                new Vector3(-42f, 0f, 0f), new Vector3(-32f, 0f, 0f),
                new Vector3(-28f, 0f, 0f), .13f, .22f);
            if (wristR != null) wristR.localRotation = baseWristR * StrikeOffset(age,
                new Vector3(-10f, -8f, -5f), new Vector3(5f, 3f, 2f),
                new Vector3(12f, 9f, 5f), .13f, .22f);
            if (weaponR != null) weaponR.localRotation = baseWeaponR * StrikeOffset(age,
                new Vector3(-12f, -38f, -26f), new Vector3(16f, 8f, 5f),
                new Vector3(32f, 46f, 24f), .13f, .22f);
            if (weaponL != null) weaponL.localRotation = baseWeaponL;
            ApplyBodyTwist(age, -18f, 7f, 21f, 7f, -5f, -9f);
        }

        private void ApplyRook(float age)
        {
            bool right = (attackSerial & 1) != 0;
            float side = right ? 1f : -1f;

            // Brakk alternates a real jab/cross. The striking elbow folds back beside the ribs,
            // the chest coils away, then elbow and shoulder extend together through the target.
            Quaternion punch = StrikeOffset(age,
                new Vector3(-18f, -58f * side, -32f * side),
                new Vector3(-100f, 8f * side, -2f * side),
                new Vector3(-108f, 22f * side, 8f * side), .10f, .16f);
            Quaternion punchElbow = StrikeOffset(age,
                new Vector3(-74f, 0f, 0f), new Vector3(-10f, 0f, 0f),
                new Vector3(-5f, 0f, 0f), .10f, .16f);
            Quaternion brace = StrikeOffset(age,
                new Vector3(-12f, 12f * side, 12f * side),
                new Vector3(-26f, -8f * side, -10f * side),
                new Vector3(-30f, -10f * side, -12f * side), .10f, .16f);
            Quaternion braceElbow = StrikeOffset(age,
                new Vector3(-48f, 0f, 0f), new Vector3(-42f, 0f, 0f),
                new Vector3(-38f, 0f, 0f), .10f, .16f);

            if (right)
            {
                armR.localRotation = baseArmR * punch;
                armL.localRotation = baseArmL * brace;
                if (elbowR != null) elbowR.localRotation = baseElbowR * punchElbow;
                if (elbowL != null) elbowL.localRotation = baseElbowL * braceElbow;
                if (wristR != null) wristR.localRotation = baseWristR;
                if (wristL != null) wristL.localRotation = baseWristL;
            }
            else
            {
                armL.localRotation = baseArmL * punch;
                armR.localRotation = baseArmR * brace;
                if (elbowL != null) elbowL.localRotation = baseElbowL * punchElbow;
                if (elbowR != null) elbowR.localRotation = baseElbowR * braceElbow;
                if (wristL != null) wristL.localRotation = baseWristL;
                if (wristR != null) wristR.localRotation = baseWristR;
            }

            ApplyBodyTwist(age, side * -20f, side * 12f, side * 19f, 8f, -6f, -8f);
        }

        private void ApplyNerok(float age)
        {
            float side = (attackSerial & 1) != 0 ? 1f : -1f;

            // Nerok drives the anchor in a broad low-to-mid body arc. The shoulder, elbow,
            // wrist and anchor all load together, so the weapon visibly has mass and follow-through.
            armR.localRotation = baseArmR * StrikeOffset(age,
                new Vector3(-30f, -62f * side, -36f * side),
                new Vector3(-84f, 16f * side, -10f * side),
                new Vector3(-56f, 68f * side, 30f * side), .12f, .24f);
            armL.localRotation = baseArmL * StrikeOffset(age,
                new Vector3(-26f, -28f * side, 16f * side),
                new Vector3(-46f, 8f * side, 4f * side),
                new Vector3(-38f, 28f * side, 14f * side), .12f, .24f);
            if (elbowR != null) elbowR.localRotation = baseElbowR * StrikeOffset(age,
                new Vector3(-60f, 0f, 0f), new Vector3(-22f, 0f, 0f),
                new Vector3(-14f, 0f, 0f), .12f, .24f);
            if (elbowL != null) elbowL.localRotation = baseElbowL * StrikeOffset(age,
                new Vector3(-46f, 0f, 0f), new Vector3(-34f, 0f, 0f),
                new Vector3(-28f, 0f, 0f), .12f, .24f);
            if (wristR != null) wristR.localRotation = baseWristR * StrikeOffset(age,
                new Vector3(-12f, -10f * side, -7f * side),
                new Vector3(6f, 4f * side, 2f * side),
                new Vector3(14f, 12f * side, 8f * side), .12f, .24f);
            if (weaponR != null) weaponR.localRotation = baseWeaponR * StrikeOffset(age,
                new Vector3(-16f, -46f * side, -30f * side),
                new Vector3(10f, 14f * side, 5f * side),
                new Vector3(28f, 64f * side, 32f * side), .12f, .24f);
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
