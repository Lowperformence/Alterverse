using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Combat
{
    public enum AimType { Melee, Projectile, Beam, Area, AreaCone, SelfArea, Dash, Self, Ally, Homing }
    public enum TargetingMode { Geometry, Ally, Homing, Cone }

    [System.Serializable]
    public struct AimSpec
    {
        public AimType aimType;
        public TargetingMode targetingMode;
        public float castRange, coneAngle, aimAssistAngle, projectileWidth, projectileSpeed, aoeRadius;
        public bool castOnRelease, allowTapCast;
    }

    public readonly struct AimPose
    {
        public readonly Vector3 Direction, Point;
        // A captured world point is independent of the preview direction and never follows a target.
        public readonly bool HasAimPoint;
        // Only ally support and explicitly configured homing may consume this reference.
        public readonly Health Target;
        public AimPose(Vector3 direction, Vector3 point, Health target = null, bool hasAimPoint = false)
        { Direction = AimGeometry.Direction(direction); Point = point; Target = target; HasAimPoint = hasAimPoint; }
        public Vector3 CastDirection(Vector3 origin)
        {
            Vector3 delta = AimGeometry.Flat(Point - origin);
            return HasAimPoint && delta.sqrMagnitude > .000001f ? AimGeometry.Direction(delta) : Direction;
        }
    }

    [CreateAssetMenu(menuName = "DIMENSIONS FRONT/Aiming settings")]
    public sealed class AimingSettings : ScriptableObject
    {
        [System.Serializable]
        public struct AttackOverride
        {
            public HeroId hero;
            [Range(-1, 3)] public int slot;
            public AimSpec spec;
        }

        public float meleeConeAngle = 70f, rangedConeAngle = 35f, aimAssistAngle = 8f;
        public float basicMeleeConeAngle = 80f;
        public float projectileWidth = .30f, dragRadius = 96f, dragDeadZone = 10f, tapSeconds = .20f;
        public float directionDeadZone = 2f;
        public bool rememberAimDirection;
        public int deliveryCapacity = 16;
        public Color previewColor = new Color(1f, .94f, .56f, .22f);
        [Range(0f, 1f)] public float previewOpacity = .75f;
        [Range(0f, 1f)] public float conePreviewOpacity = .75f;
        public AttackOverride[] overrides = new AttackOverride[0];
        private static AimingSettings current;
        public static AimingSettings Current
        {
            get
            {
                if (current == null)
                    current = Resources.Load<AimingSettings>("Combat/AimingSettings") ?? CreateInstance<AimingSettings>();
                return current;
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => current = null;

        // Range, radius, power, cooldown and windup continue to come from the existing catalogs.
        public AimSpec For(HeroId hero, int slot, float? basicRange = null)
        {
            bool ranged = HeroAttackDelivery.IsRanged(hero);
            AbilitySpec ability = HeroAbilityKit.LoadoutFor(hero).Spec(Mathf.Max(0, slot));
            AimType type = slot < 0 ? (ranged ? AimType.Projectile : AimType.Melee) : Classify(hero, slot, ability);
            AimSpec spec = new AimSpec
            {
                aimType = type, targetingMode = type == AimType.Ally ? TargetingMode.Ally :
                    slot >= 0 && (type == AimType.Melee || type == AimType.Projectile || type == AimType.Beam)
                        ? TargetingMode.Cone : TargetingMode.Geometry,
                castRange = slot < 0 ? basicRange ?? HeroCombatProfile.StatsFor(hero).AttackRange : ability.Range,
                aoeRadius = slot < 0 ? 0 : ability.Radius,
                coneAngle = slot < 0 && !ranged ? basicMeleeConeAngle : ranged ? rangedConeAngle : meleeConeAngle,
                aimAssistAngle = aimAssistAngle, projectileWidth = projectileWidth,
                projectileSpeed = hero == HeroId.Nyra ? 38f : hero == HeroId.Traveler ? 32f : hero == HeroId.Elyra ? 23f : 27f,
                castOnRelease = true, allowTapCast = true
            };
            if (hero == HeroId.Nerok && slot == 0) { spec.projectileSpeed = 18f; spec.projectileWidth = .45f; }
            if (hero == HeroId.Kaelon && slot == 0) { spec.projectileSpeed = 14f; spec.projectileWidth = .86f; }
            if (hero == HeroId.Veyr && slot == 1) { spec.projectileSpeed = 18f; spec.projectileWidth = .45f; }
            if (overrides != null)
                for (int i = 0; i < overrides.Length; i++)
                    if (overrides[i].hero == hero && overrides[i].slot == slot) { spec = overrides[i].spec; break; }
            spec.castRange = Mathf.Max(0f, spec.castRange);
            spec.aoeRadius = Mathf.Max(0f, spec.aoeRadius);
            spec.coneAngle = Mathf.Clamp(spec.coneAngle, 1f, 180f);
            spec.aimAssistAngle = Mathf.Clamp(spec.aimAssistAngle, 0f, spec.coneAngle * .5f);
            spec.projectileWidth = Mathf.Max(.01f, spec.projectileWidth);
            spec.projectileSpeed = Mathf.Max(.1f, spec.projectileSpeed);
            return spec;
        }

        private static AimType Classify(HeroId hero, int slot, AbilitySpec ability)
        {
            if ((hero == HeroId.Veyr && slot == 2) || (hero == HeroId.Nyra && slot == 1) ||
                (hero == HeroId.Elyra && slot == 2) || (hero == HeroId.Traveler && slot == 1)) return AimType.Dash;
            if (hero == HeroId.Mira && (slot == 1 || slot == 2)) return AimType.Ally;
            if ((hero == HeroId.Kaelon || hero == HeroId.Rook || hero == HeroId.Nerok) && slot == 1) return AimType.Self;
            if (ability.Radius > 0f) return ability.Range > 0f ? AimType.Area : AimType.SelfArea;
            if (hero == HeroId.Nyra && slot == 0) return AimType.Beam;
            if ((hero == HeroId.Nerok && slot == 0) || (hero == HeroId.Kaelon && slot == 0) ||
                (hero == HeroId.Veyr && slot == 1) || HeroAttackDelivery.IsRanged(hero)) return AimType.Projectile;
            return AimType.Melee;
        }
    }
}
