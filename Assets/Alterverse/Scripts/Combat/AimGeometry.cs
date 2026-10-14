using Alterverse.Characters;
using UnityEngine;

namespace Alterverse.Combat
{
    public static class AimGeometry
    {
        public static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        public static Vector3 Direction(Vector3 v)
        { v = Flat(v); return Finite(v) && v.sqrMagnitude > .000001f ? v.normalized : Vector3.forward; }
        public static Vector3 ClampPoint(Vector3 origin, Vector3 point, float range)
            => origin + Vector3.ClampMagnitude(Flat(point - origin), Mathf.Max(0f, range));
        public static bool Circle(Vector3 center, Vector3 point, float radius)
            => Flat(point - center).sqrMagnitude <= radius * radius;
        public static bool Cone(Vector3 origin, Vector3 direction, Vector3 point, float range, float angle)
        {
            Vector3 delta = Flat(point - origin);
            return delta.sqrMagnitude <= range * range &&
                (delta.sqrMagnitude < .000001f || Vector3.Dot(Direction(direction), delta.normalized) >= Mathf.Cos(angle * .5f * Mathf.Deg2Rad));
        }

        // Continuous relative-motion circle sweep: moving targets cannot tunnel through a fast shot.
        public static bool Sweep(Vector3 from, Vector3 to, Vector3 targetFrom, Vector3 targetTo, float radius, out float fraction)
        {
            Vector3 relative = Flat(from - targetFrom), travel = Flat((to - from) - (targetTo - targetFrom));
            float c = relative.sqrMagnitude - radius * radius;
            fraction = 0f;
            if (c <= 0f) return true;
            float a = travel.sqrMagnitude;
            if (a < .000001f) return false;
            float b = Vector3.Dot(relative, travel), discriminant = b * b - a * c;
            if (discriminant < 0f) return false;
            fraction = (-b - Mathf.Sqrt(discriminant)) / a;
            return fraction >= 0f && fraction <= 1f;
        }

        public static bool HitBody(Health target, Vector3 from, Vector3 to, float width, bool moving, out float fraction, float motionFraction = 1f)
        {
            Vector3 end = target.transform.position;
            bool unit = target.GetComponent<HeroIdentity>() != null || target.GetComponent<LaneMinion>() != null;
            if (unit)
            {
                HeroLocomotion body = target.GetComponent<HeroLocomotion>();
                Vector3 start = moving && body != null ? body.FrameStartPosition : end;
                end = Vector3.Lerp(start, end, Mathf.Clamp01(motionFraction));
                HeroIdentity identity=target.GetComponent<HeroIdentity>();
                float radius=identity!=null&&identity.Hero==HeroId.Timmy?TimmyBalance.CollisionRadius:HeroIdentity.HeroCollisionRadius;
                return Sweep(from, to, start, end, radius + width * .5f, out fraction);
            }
            // Objective surfaces remain hittable without walking into their large visual models.
            Collider collider = target.GetComponent<Collider>() ?? target.GetComponentInChildren<Collider>();
            if (collider == null || !collider.enabled) return Sweep(from, to, end, end, width * .5f + .4f, out fraction);
            Vector3 a = from; a.y = collider.bounds.center.y;
            Vector3 delta = Flat(to - from);
            fraction = 0f;
            if ((collider.ClosestPoint(a) - a).sqrMagnitude <= width * width * .25f) return true;
            RaycastHit hit;
            if (collider.Raycast(new Ray(a, Direction(delta)), out hit, delta.magnitude))
            { fraction = hit.distance / Mathf.Max(.0001f, delta.magnitude); return true; }
            // Include projectile thickness at the closest point on the swept line.
            float t = Mathf.Clamp01(Vector3.Dot(Flat(collider.bounds.center - a), delta) / Mathf.Max(.0001f, delta.sqrMagnitude));
            Vector3 p = a + delta * t;
            fraction = t;
            return (collider.ClosestPoint(p) - p).sqrMagnitude <= width * width * .25f;
        }

        public static Health Select(GameObject actor, Vector3 direction, float range, float angle, bool ally = false, bool heroesOnly = false)
        {
            Health best = null; float bestScore = float.MaxValue;
            TeamMember team = actor.GetComponent<TeamMember>();
            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health h = Health.Active[i];
                if (h == null || h.IsDead || !h.isActiveAndEnabled) continue;
                if (heroesOnly && h.GetComponent<HeroIdentity>() == null) continue;
                if (ally)
                {
                    TeamMember other = h.GetComponent<TeamMember>();
                    // Aimed support chooses another hero before considering self fallback.
                    // Full health or an existing shield must not redirect an aimed cast to self.
                    if (h.gameObject == actor || team == null || other == null || other.Team != team.Team ||
                        h.GetComponent<HeroIdentity>() == null) continue;
                }
                else if (!HeroCombatRules.Enemy(actor, h)) continue;
                float distance = HeroCombatRules.TargetDistance(actor.transform.position, h);
                if (distance > range) continue;
                Vector3 delta = Flat(h.transform.position - actor.transform.position);
                float degrees = delta.sqrMagnitude > .0001f ? Vector3.Angle(Direction(direction), delta) : 0f;
                if (degrees > angle) continue;
                float score = degrees * 4f + distance + (h.GetComponent<HeroIdentity>() == null ? 1f : 0f);
                if (ally) score += h.Normalized * 2f;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            return best;
        }
        public static Vector3 Assist(GameObject actor, Vector3 direction, AimSpec spec, out Health target)
        {
            direction = Direction(direction);
            SoftTargeting soft = actor.GetComponent<SoftTargeting>();
            target = soft != null ? soft.AcquireAssisted(spec.castRange, direction, spec.aimAssistAngle)
                : spec.aimAssistAngle > 0f ? Select(actor, direction, spec.castRange, spec.aimAssistAngle) : null;
            return target != null ? Direction(target.transform.position - actor.transform.position) : direction;
        }
        public static bool UsesConeCapture(AimSpec spec)
            => (spec.targetingMode == TargetingMode.Cone || spec.targetingMode == TargetingMode.Homing) &&
                (spec.aimType == AimType.Melee || spec.aimType == AimType.Projectile || spec.aimType == AimType.Beam || spec.aimType == AimType.Homing || spec.aimType == AimType.Dash);
        public static Health CaptureConeTarget(GameObject actor, Vector3 direction, AimSpec spec)
        {
            SoftTargeting soft = actor.GetComponent<SoftTargeting>();
            if(spec.aimType==AimType.Dash)
                return soft != null ? soft.AcquireCone(spec.castRange,direction,spec.coneAngle,true)
                    : Select(actor,direction,spec.castRange,spec.coneAngle*.5f,heroesOnly:true);
            return soft != null ? soft.AcquireCone(spec.castRange, direction, spec.coneAngle)
                : Select(actor, direction, spec.castRange, spec.coneAngle * .5f);
        }
        public static AimPose TapPose(GameObject actor, AimSpec spec)
        {
            if (spec.aimType == AimType.Dash)
            {
                HeroMotor motor = actor.GetComponent<HeroMotor>();
                Vector3 movement = motor != null ? motor.CurrentMoveDirection : Vector3.zero;
                if (Flat(movement).sqrMagnitude < .0001f)
                    movement = motor != null ? motor.LastMoveDirection : actor.transform.forward;
                Vector3 dash = Direction(movement);
                return new AimPose(dash, actor.transform.position + dash * spec.castRange,
                    allowDashTargetCapture: false);
            }
            Vector3 direction = Direction(actor.transform.forward); Health target = null;
            if (spec.aimType == AimType.Ally)
                target = Select(actor, direction, spec.castRange, spec.coneAngle * .5f, true);
            else if (UsesConeCapture(spec)) target = CaptureConeTarget(actor, direction, spec);
            else if (spec.aimType != AimType.Dash && spec.aimType != AimType.Self && spec.aimType != AimType.SelfArea)
                direction = Assist(actor, direction, spec, out target);
            Vector3 point = actor.transform.position + direction * spec.castRange * .65f;
            if (spec.aimType == AimType.Area && target != null) point = ClampPoint(actor.transform.position, target.transform.position, spec.castRange);
            bool captured = spec.targetingMode == TargetingMode.Cone && target != null;
            if (captured) point = ClampPoint(actor.transform.position, target.transform.position, spec.castRange);
            if (spec.aimType == AimType.SelfArea || spec.aimType == AimType.Self) point = actor.transform.position;
            return new AimPose(direction, point, spec.targetingMode == TargetingMode.Ally || spec.targetingMode == TargetingMode.Homing ||
                spec.aimType==AimType.Dash&&spec.targetingMode==TargetingMode.Cone ? target : null, captured);
        }
    }
}

