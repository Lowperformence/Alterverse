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

        // The visible ground cone is the gameplay region. A target's physical body can
        // overlap its edge even when its pivot lies just outside the preview.
        public static float BodyRadius(Health target)
        {
            HeroIdentity hero = target.GetComponent<HeroIdentity>();
            if (hero != null)
                return hero.Hero == HeroId.Timmy ? TimmyBalance.CollisionRadius : HeroIdentity.HeroCollisionRadius;
            Collider collider = target.GetComponent<Collider>() ?? target.GetComponentInChildren<Collider>();
            if (collider != null && collider.enabled)
                return Mathf.Max(.25f, Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.z));
            return target.GetComponent<LaneMinion>() != null ? .35f : .4f;
        }

        public static bool ConeCircle(Vector3 origin, Vector3 direction, Vector3 point,
            float range, float angle, float bodyRadius)
        {
            Vector3 delta = Flat(point - origin);
            float distance = delta.magnitude, radius = Mathf.Max(0f, bodyRadius);
            if (distance <= radius) return true;
            if (distance > range + radius) return false;

            Vector3 forward = Direction(direction);
            float half = Mathf.Clamp(angle * .5f, 0f, 90f) * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(half), sine = Mathf.Sin(half);
            if (Vector3.Dot(forward, delta) >= cosine * distance)
                return true;

            Vector3 side = new Vector3(-forward.z, 0f, forward.x);
            Vector3 left = forward * cosine + side * sine;
            Vector3 right = forward * cosine - side * sine;
            Vector3 l = delta - left * Mathf.Clamp(Vector3.Dot(delta, left), 0f, range);
            Vector3 r = delta - right * Mathf.Clamp(Vector3.Dot(delta, right), 0f, range);
            return Mathf.Min(l.sqrMagnitude, r.sqrMagnitude) <= radius * radius;
        }

        // Movement is swept through the wedge, not sampled at the release instant.
        // Adaptive substeps are normally one per frame; faster dashes add samples so
        // a body cannot cross the narrow cone without occupying its hit region.
        public static bool SweptConeCircle(Vector3 origin, Vector3 direction,
            Vector3 previous, Vector3 current, float range, float angle, float radius)
        {
            if (ConeCircle(origin, direction, previous, range, angle, radius) ||
                ConeCircle(origin, direction, current, range, angle, radius)) return true;
            Vector3 displacement = Flat(current - previous);
            int steps = Mathf.Clamp(Mathf.CeilToInt(displacement.magnitude /
                Mathf.Max(.12f, radius * .5f)), 1, 64);
            for (int i = 1; i < steps; i++)
                if (ConeCircle(origin, direction, previous + displacement * (i / (float)steps),
                        range, angle, radius)) return true;
            return false;
        }

        public static bool ConeBody(Health target, Vector3 origin, Vector3 direction,
            float range, float angle, bool moving = false)
        {
            if (target == null || target.IsDead || !target.isActiveAndEnabled) return false;
            Vector3 current = target.transform.position;
            float radius = BodyRadius(target);
            if (!moving) return ConeCircle(origin, direction, current, range, angle, radius);
            HeroLocomotion locomotion = target.GetComponent<HeroLocomotion>();
            Vector3 previous = locomotion != null ? locomotion.FrameStartPosition : current;
            // Ignore non-physical teleports/respawns when comparing movement within a frame.
            if (Flat(current - previous).sqrMagnitude > 64f * 64f) previous = current;
            return SweptConeCircle(origin, direction, previous, current, range, angle, radius);
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
            =>Select(actor,actor.transform.position,direction,range,angle,ally,heroesOnly);

        public static Health Select(GameObject actor, Vector3 origin, Vector3 direction, float range, float angle, bool ally = false, bool heroesOnly = false)
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
                float distance = HeroCombatRules.TargetDistance(origin, h);
                Vector3 delta = Flat(h.transform.position - origin);
                float degrees = delta.sqrMagnitude > .0001f ? Vector3.Angle(Direction(direction), delta) : 0f;
                if (ally)
                {
                    if (distance > range || degrees > angle) continue;
                }
                else if (!ConeBody(h, origin, direction, range, angle * 2f)) continue;
                // Hero class wins before any distance/angle tie-break. Minions, monsters,
                // structures and objectives are considered only when no hero is eligible.
                if (!ally)
                {
                    if (HeroCombatRules.PreferConeTarget(h,best,origin)) best=h;
                }
                else
                {
                    float score = degrees * 4f + distance + h.Normalized * 2f;
                    if (score < bestScore) { bestScore = score; best = h; }
                }
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
                spec.aimType==AimType.Dash&&spec.targetingMode==TargetingMode.Cone ? target : null,
                captured || spec.aimType == AimType.Area);
        }
    }
}

