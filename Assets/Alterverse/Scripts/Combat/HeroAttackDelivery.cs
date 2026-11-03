using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    // Direct ranged damage uses the aiming cone. Flying effects never own collision.
    [RequireComponent(typeof(Health), typeof(HeroIdentity))]
    [DefaultExecutionOrder(120)]
    public sealed class HeroAttackDelivery : MonoBehaviour
    {
        private struct Pending
        {
            public bool Active, Launched, Basic, Volley, Hook, HasAimPoint, TargetedBasic;
            public Vector3 Direction, Point, LaunchOrigin;
            public Health Target;
            public HeroAbilityExecution Execution;
            public AimSpec Spec;
            public int Slot, VisualId;
            public float Damage, ReleaseAt, ConeUntil;
        }
        private Pending[] pending;
        private readonly RaycastHit[] blockers = new RaycastHit[32];
        private Health self;
        private HeroIdentity identity;
        private bool hasPending;
        private Health volleyTarget;
        private bool volleyTargetChosen;
        internal Vector3? VisualOrigin { get; private set; }
        internal Vector3? VisualDirection { get; private set; }
        private void Awake()
        {
            self = GetComponent<Health>(); identity = GetComponent<HeroIdentity>();
            pending = new Pending[Mathf.Clamp(AimingSettings.Current.deliveryCapacity, 8, 64)];
        }
        private void OnEnable() { if (self != null) { self.Died -= Cancel; self.Died += Cancel; } }
        public static bool IsRanged(HeroId hero)=>hero==HeroId.Bolt7||hero==HeroId.Nyra||hero==HeroId.Elyra||hero==HeroId.Mira||hero==HeroId.Traveler;
        public static float Windup(HeroId hero,int slot=-1)
        {
            if(slot<0)
            {
                // Melee basics need enough anticipation to show an actual draw-back and weapon travel.
                // Contact still happens exactly at ReleaseAt, so damage and the IK strike stay synced.
                return hero switch
                {
                    HeroId.Bolt7=>Bolt7Balance.BasicWindup,HeroId.VoidKnight=>VoidKnightBalance.BasicWindup,HeroId.Veyr=>.16f,HeroId.Kaelon=>.22f,HeroId.Nyra=>.08f,
                    HeroId.Elyra=>.14f,HeroId.Mira=>.12f,HeroId.Rook=>.18f,HeroId.Traveler=>.10f,
                    HeroId.Nerok=>.20f,_=>.16f
                };
            }

            // Ability timings stay on their established values; the longer anticipation above
            // applies only to basic melee attacks and does not rebalance skills.
            float abilityBase=hero switch
            {
                HeroId.Veyr=>.09f,HeroId.Kaelon=>.16f,HeroId.Nyra=>.08f,
                HeroId.Elyra=>.14f,HeroId.Mira=>.12f,HeroId.Rook=>.13f,HeroId.Traveler=>.10f,_=>.13f
            };
            return hero==HeroId.Nyra&&slot==3?.22f:abilityBase+.04f;
        }
        public static float AbilityWindup(HeroId hero,int slot,AbilitySpec spec)
        {
            if(spec.Windup>0)return spec.Windup;
            // Targeted damaging skills only; mobility, heal and shield remain instant utilities.
            bool targeted=spec.Power>0&&spec.Range>0&&spec.Radius<=0&&!(hero==HeroId.Mira&&(slot==1||slot==2));
            return targeted?Windup(hero,slot):0;
        }
        public static float Flight(HeroId hero,Vector3 from,Vector3 to)
        {
            if(!IsRanged(hero))return 0;
            float speed=hero==HeroId.Nyra?38:hero==HeroId.Traveler?32:hero==HeroId.Elyra?23:27;
            // A minimum readable flight even at point blank; no speed or hitbox physics changed.
            return Mathf.Clamp(Vector3.Distance(from,to)/speed,.07f,.32f);
        }
        public static HeroAttackDelivery For(GameObject actor)
            =>actor.GetComponent<HeroAttackDelivery>()??actor.AddComponent<HeroAttackDelivery>();
        public bool HasCapacity(int count = 1)
        {
            int free = 0;
            for (int i = 0; i < pending.Length; i++) if (!pending[i].Active && ++free >= count) return true;
            return false;
        }
        private bool Queue(AimPose pose, AimSpec spec, int slot, float damage, HeroAbilityExecution execution, bool volley, float windup)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay || !isActiveAndEnabled || self == null || self.IsDead ||
                !HeroCombatStatus.CanAct(gameObject) || !AimGeometry.Finite(pose.Direction) || !AimGeometry.Finite(pose.Point)) return false;
            if (spec.targetingMode == TargetingMode.Homing && (!HeroCombatRules.Enemy(gameObject, pose.Target) || CombatVisibility.HiddenFrom(gameObject,pose.Target) ||
                HeroCombatRules.TargetDistance(transform.position, pose.Target) > spec.castRange)) return false;
            if (slot < 0 && spec.targetingMode == TargetingMode.AutoBasic && pose.Target != null &&
                (!HeroCombatRules.Enemy(gameObject, pose.Target) || CombatVisibility.HiddenFrom(gameObject,pose.Target) || HeroCombatRules.TargetDistance(transform.position, pose.Target) > spec.castRange)) return false;
            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i].Active) continue;
                pending[i] = new Pending { Active = true, Basic = slot < 0, Slot = slot, Damage = damage,
                    TargetedBasic = slot < 0 && spec.targetingMode == TargetingMode.AutoBasic && pose.Target != null,
                    Execution = execution, Volley = volley, Hook = identity.Hero == HeroId.Nerok && slot == 0,
                    Direction = spec.targetingMode == TargetingMode.Cone || spec.aimType == AimType.Projectile ||
                        spec.aimType == AimType.Beam || spec.aimType == AimType.Homing ? pose.Direction :
                        spec.aimType == AimType.Melee && slot >= 0 ? pose.Direction : pose.CastDirection(transform.position),
                    Point = pose.Point, HasAimPoint = pose.HasAimPoint, Spec = spec,
                    Target = spec.targetingMode == TargetingMode.Homing || slot < 0 && spec.targetingMode == TargetingMode.AutoBasic ? pose.Target : null, ReleaseAt = Time.time + windup };
                hasPending = true;
                CosmicNebulaState.OffensiveAction(gameObject);
                if (!volley) HeroCombatAudio.Windup(gameObject, slot + 1);
                if (slot < 0)
                {
                    // Emit only after authoritative target/range/capacity acceptance.
                    using var accepted = HeroVfxEvents.Begin(HeroVfxCue.BasicWindup, gameObject,
                        pose.HasAimPoint ? pose.Point : transform.position + pose.Direction * spec.castRange,
                        duration: windup, target: pose.Target != null ? pose.Target.gameObject : null);
                    if (pose.Target != null && spec.targetingMode == TargetingMode.AutoBasic)
                        CombatMotion.Begin(gameObject, windup, pose.Target);
                    else CombatMotion.BeginAimed(gameObject, windup, pose.HasAimPoint ? pose.Point : transform.position + pose.Direction * spec.castRange);
                }
                return true;
            }
            return false;
        }
        public bool BasicAimed(AimPose pose, AimSpec spec, float amount)
            => Queue(pose, spec, -1, amount, null, false, Windup(identity.Hero));
        public bool AbilityAimed(HeroAbilityExecution execution, int slot, AimPose pose, bool volley = false, float windup = -1f)
        {
            if (execution == null || execution.gameObject != gameObject || !execution.isActiveAndEnabled || slot < 0 || slot > 3) return false;
            if (volley)
            {
                bool activeVolley = false;
                for (int i = 0; i < pending.Length; i++) activeVolley |= pending[i].Active && pending[i].Volley;
                if (!activeVolley) { volleyTarget = null; volleyTargetChosen = false; }
            }
            AimSpec spec = AimingSettings.Current.For(identity.Hero, slot);
            return Queue(pose, spec, slot, 0f, execution, volley,
                windup >= 0f ? windup : AbilityWindup(identity.Hero, slot, GetComponent<HeroAbilityKit>().Spec(slot)));
        }
        public void Basic(Health target, float amount, float range)
        {
            if (!HeroCombatRules.Enemy(gameObject, target) || HeroCombatRules.TargetDistance(transform.position, target) > range) return;
            AimSpec spec = AimingSettings.Current.For(identity.Hero, -1, range);
            BasicAimed(new AimPose(target.transform.position - transform.position, target.transform.position, target, true), spec, amount);
        }
        public void Ability(HeroAbilityExecution execution, int slot, Health target, bool volley = false, float windup = -1f)
        {
            if (slot < 0 || slot > 3 || !HeroCombatRules.Enemy(gameObject, target) ||
                HeroCombatRules.TargetDistance(transform.position, target) > GetComponent<HeroAbilityKit>().Spec(slot).Range) return;
            AbilityAimed(execution, slot, new AimPose(target.transform.position - transform.position, target.transform.position, target, true), volley, windup);
        }
        public void CancelWindups()
        {
            if (!hasPending) return;
            for (int i = 0; i < pending.Length; i++) if (!pending[i].Launched) pending[i] = default;
            Recount();
        }
        public void Cancel()
        {
            if (pending != null) for (int i = 0; i < pending.Length; i++) pending[i] = default;
            volleyTarget = null; volleyTargetChosen = false;
            hasPending = false; HeroSkillVfx.CancelProjectiles(gameObject);
        }
        private void OnDisable() { if (self != null) self.Died -= Cancel; Cancel(); }
        private void Recount()
        { hasPending = false; for (int i = 0; i < pending.Length; i++) hasPending |= pending[i].Active; }
        private float BlockDistance(Vector3 origin, Vector3 direction, float distance, float width)
        {
            if (distance <= .0001f) return distance;
            int count = Physics.SphereCastNonAlloc(origin + Vector3.up * .5f, width * .5f, direction, blockers, distance,
                GameplayCollision.SightMask, QueryTriggerInteraction.Ignore);
            RaycastHit[] hits = blockers;
            if (count == blockers.Length)
            {
                // A full buffer can contain only ignored actor colliders, or omit the
                // nearest wall. Like locomotion, obtain all hits only on this rare path.
                hits = Physics.SphereCastAll(origin + Vector3.up * .5f, width * .5f, direction, distance,
                    GameplayCollision.SightMask, QueryTriggerInteraction.Ignore);
                count = hits.Length;
            }
            float closest = distance;
            for (int i = 0; i < count; i++)
            {
                Health owner = hits[i].collider.GetComponentInParent<Health>();
                if (owner == self || owner != null && (owner.GetComponent<HeroIdentity>() != null ||
                    owner.GetComponent<LaneMinion>() != null || HeroCombatRules.Enemy(gameObject, owner))) continue;
                closest = Mathf.Min(closest, hits[i].distance);
            }
            return closest;
        }
        private Health Contact(Pending p, Vector3 from, Vector3 to, bool moving, out float fraction, float motionFraction = 1f)
        {
            if (AutomaticBasic(p))
            {
                fraction = 0f;
                if (!HeroCombatRules.Enemy(gameObject, p.Target)) return null;
                if (p.Spec.aimType == AimType.Melee)
                {
                    // Range was validated when the attack was accepted. Walking away during
                    // anticipation must not turn a valid standard strike into an empty swing.
                    Vector3 delta = AimGeometry.Flat(p.Target.transform.position - from);
                    return BlockDistance(from, AimGeometry.Direction(delta), delta.magnitude, .01f) >= delta.magnitude - .01f
                        ? p.Target : null;
                }
                return AimGeometry.HitBody(p.Target, from, to, p.Spec.projectileWidth, moving, out fraction, motionFraction)
                    ? p.Target : null;
            }
            Health nearest = null; fraction = 2f; float bestScore = float.MaxValue;
            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health target = Health.Active[i];
                if (!HeroCombatRules.Enemy(gameObject, target)) continue;
                float t;
                bool contact;
                if (p.Spec.aimType == AimType.Melee)
                {
                    Vector3 delta = AimGeometry.Flat(target.transform.position - from);
                    contact = HeroCombatRules.TargetDistance(from, target) <= p.Spec.castRange &&
                        (delta.sqrMagnitude < .0001f || Vector3.Angle(p.Direction, delta) <= p.Spec.coneAngle * .5f);
                    t = HeroCombatRules.TargetDistance(from, target) / Mathf.Max(.01f, p.Spec.castRange);
                    if (contact) contact = BlockDistance(from, AimGeometry.Direction(delta), delta.magnitude, .01f) >= delta.magnitude - .01f;
                }
                else contact = AimGeometry.HitBody(target, from, to, p.Spec.projectileWidth, moving, out t, motionFraction);
                float score = p.Spec.aimType == AimType.Melee && p.HasAimPoint
                    ? AimGeometry.Flat(target.transform.position - p.Point).sqrMagnitude : t;
                if (contact && score < bestScore) { nearest = target; fraction = t; bestScore = score; }
            }
            return nearest;
        }
        private static bool AutomaticBasic(Pending p)
            => p.TargetedBasic;

        private static bool LiveCone(Pending p)
            => (p.Spec.targetingMode == TargetingMode.Cone &&
                p.Spec.aimType == AimType.Melee) || RangedCone(p);
        private static bool RangedCone(Pending p)
            => p.Spec.aimType == AimType.Projectile || p.Spec.aimType == AimType.Beam ||
               p.Spec.aimType == AimType.Homing;

        // Every server update uses the same wedge as the player's visible aiming preview.
        // Select a SINGLE body per direct attack; a hero always beats a closer minion
        // or Hot Zone creature while both occupy the active cone.
        private Health ConeContact(Pending p)
        {
            Health best = null;
            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health target = Health.Active[i];
                if (p.Volley && volleyTargetChosen && target != volleyTarget) continue;
                if (!HeroCombatRules.Enemy(gameObject, target) ||
                    !AimGeometry.ConeBody(target, p.LaunchOrigin, p.Direction,
                        p.Spec.castRange, p.Spec.coneAngle, true)) continue;

                Vector3 delta = AimGeometry.Flat(target.transform.position - p.LaunchOrigin);
                float distance = delta.magnitude;
                float radius = AimGeometry.BodyRadius(target);
                // The cone is not permission to strike through scenery. The body surface
                // must be visible along this ray, even if its centre is behind cover.
                if (distance > .001f && BlockDistance(p.LaunchOrigin, AimGeometry.Direction(delta),
                    distance, .01f) < Mathf.Max(0f, distance - radius) - .01f) continue;

                if (HeroCombatRules.PreferConeTarget(target,best,p.LaunchOrigin)) best=target;
            }
            return best;
        }

        // Kept visible until the SAME authoritative active hit interval closes.
        public bool TryActiveCone(out Vector3 origin, out Vector3 direction, out AimSpec spec)
        {
            if (pending != null)
                for (int i = 0; i < pending.Length; i++)
                {
                    Pending p = pending[i];
                    if (!p.Active || !p.Launched || !LiveCone(p) || Time.time > p.ConeUntil) continue;
                    origin = p.LaunchOrigin; direction = p.Direction; spec = p.Spec;
                    return true;
                }
            origin = Vector3.zero; direction = Vector3.forward; spec = default;
            return false;
        }

        // Use the same stationary body and wall queries as delivery for the held hook preview.
        // This is a current-position preview; targets can still dodge after the cast commits.
        internal Vector3 PreviewProjectileEnd(AimPose pose, AimSpec spec)
        {
            Vector3 origin = transform.position, direction = pose.CastDirection(origin);
            float length = BlockDistance(origin, direction, spec.castRange, spec.projectileWidth);
            Vector3 end = origin + direction * length;
            Pending preview = new Pending { Direction = direction, Point = pose.Point,
                HasAimPoint = pose.HasAimPoint, Spec = spec };
            float fraction;
            return Contact(preview, origin, end, false, out fraction) != null
                ? Vector3.Lerp(origin, end, Mathf.Clamp01(fraction)) : end;
        }

        private void FaceBasicTarget(Pending p, bool release = false)
        {
            Vector3 direction = AimGeometry.Flat(p.Target.transform.position - transform.position);
            GetComponent<HeroLocomotion>()?.FaceAction(direction, .18f);
            GetComponent<HeroMotor>()?.TrackBasicTarget(p.Target, .18f);
            // Complete the turn by the contact/release frame, including attacks begun with
            // the target directly behind the hero. Movement is still handled by the motor.
            if (release && direction.sqrMagnitude > .0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
        private void Impact(Pending p, Health target)
        {
            // The three shots of one non-AOE volley share one victim. A killed
            // victim cannot turn the remaining shots into multi-target damage.
            if (p.Volley && !volleyTargetChosen) { volleyTarget = target; volleyTargetChosen = true; }
            if (p.Hook) p.Execution.ResolveSharkHookHit(target);
            else if (p.Basic) HeroCombatRules.ResolveBasicHit(gameObject, target, p.Damage);
            else p.Execution.ResolvePresentedHit(p.Slot, target, p.Volley);
        }
        private int Present(Pending p, Vector3 endpoint, float flight)
        {
            int id = HeroVfxPool.BeginDelivery(); VisualOrigin = p.LaunchOrigin; VisualDirection = p.Direction;
            Transform follow = (p.Spec.targetingMode == TargetingMode.Homing || AutomaticBasic(p)) && p.Target != null ? p.Target.transform : null;
            try
            {
            if (p.Basic)
            {
                HeroCombatAudio.Play(gameObject, 0, false, p.LaunchOrigin);
                HeroSkillVfx.Basic(gameObject, endpoint, flight, follow, false);
            }
            else
            {
                HeroCombatAudio.Cast(gameObject, p.Slot);
                if (p.Hook)
                {
                    HeroSkillVfx.SharkAnchor(gameObject, endpoint, flight, follow);
                    Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(gameObject, 0, endpoint, 0, flight);
                }
                else HeroSkillVfx.Spell(gameObject, endpoint, p.Slot, flight, follow);
            }
            }
            finally { VisualOrigin = null; VisualDirection = null; HeroVfxPool.EndDelivery(); }
            return id;
        }
        private void Update()
        {
            if (!hasPending || Time.timeScale <= 0f) return;
            if (!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay || self == null || self.IsDead) { Cancel(); return; }
            if (!HeroCombatStatus.CanAct(gameObject)) CancelWindups();
            for (int i = 0; i < pending.Length; i++)
            {
                Pending p = pending[i];
                if (!p.Active) continue;
                if (!p.Basic && (p.Execution == null || !p.Execution.isActiveAndEnabled)) { pending[i] = default; continue; }
                bool automatic = AutomaticBasic(p);
                if (!p.Launched)
                {
                    if (automatic && (!HeroCombatRules.Enemy(gameObject, p.Target)||CombatVisibility.HiddenFrom(gameObject,p.Target)))
                    { pending[i] = default; continue; }
                    if (automatic) FaceBasicTarget(p);
                    if (Time.time < p.ReleaseAt) continue;
                    p.Launched = true;
                    p.LaunchOrigin = transform.position;
                    // Trigger once at the real swing, including a miss; late cone entrants cannot restart it.
                    if(!p.Basic&&p.Slot==0&&identity.Hero==HeroId.VoidKnight)
                        (GetComponent<VoidKnightState>()??gameObject.AddComponent<VoidKnightState>()).BeginSweepCloak();
                    if (automatic)
                    {
                        p.Point = p.Target.transform.position;
                        p.HasAimPoint = true;
                        p.Direction = AimGeometry.Direction(p.Point - p.LaunchOrigin);
                        FaceBasicTarget(p, true);
                    }
                    if (LiveCone(p))
                    {
                        // Fixed active hit interval, independent of projectile speed,
                        // central trajectory, travel distance or a visual collision.
                        p.ConeUntil = Time.time + (p.Spec.aimType == AimType.Beam ? .12f : .20f);
                        Health entered = ConeContact(p);
                        if (entered != null && automatic) p.Target = entered;
                        Vector3 endpoint = entered != null ? entered.transform.position :
                            p.LaunchOrigin + p.Direction * p.Spec.castRange;
                        float flight = p.Spec.aimType == AimType.Melee ? 0f :
                            p.Spec.aimType == AimType.Beam ? .12f :
                            Mathf.Max(.01f, HeroCombatRules.Distance(p.LaunchOrigin, endpoint) / p.Spec.projectileSpeed);
                        p.VisualId = Present(p, endpoint, flight);
                        if (entered != null)
                        {
                            pending[i] = default;
                            Impact(p, entered);
                        }
                        else pending[i] = p;
                        continue;
                    }
                    // Melee automatic basics retain their existing committed-target behavior.
                    if (p.HasAimPoint)
                        p.Direction = new AimPose(p.Direction, p.Point, null, true).CastDirection(p.LaunchOrigin);
                    float distance = automatic ? HeroCombatRules.Distance(p.LaunchOrigin, p.Point) : p.Spec.castRange;
                    float length = BlockDistance(p.LaunchOrigin, p.Direction, distance, p.Spec.projectileWidth);
                    float fraction;
                    Health target = Contact(p, p.LaunchOrigin, p.LaunchOrigin + p.Direction * length, false, out fraction);
                    Vector3 contact = target != null ? target.transform.position : p.LaunchOrigin + p.Direction * length;
                    Present(p, contact, 0f);
                    pending[i] = default;
                    if (target != null) Impact(p, target);
                    continue;
                }
                // A body can enter anywhere in the still-active wedge, including a
                // frame-spanning dash. Each delivery commits at most one hit.
                if (Time.time > p.ConeUntil)
                { pending[i] = default; continue; }
                Health entrant = ConeContact(p);
                if (entrant != null)
                {
                    pending[i] = default;
                    HeroVfxPool.CancelDelivery(p.VisualId);
                    if (automatic) p.Target = entrant;
                    // Retarget the cosmetic effect; gameplay is already decided by the cone.
                    Present(p, entrant.transform.position, p.Spec.aimType == AimType.Melee ? 0f : .12f);
                    Impact(p, entrant);
                }
            }
            Recount();
        }
    }
}



