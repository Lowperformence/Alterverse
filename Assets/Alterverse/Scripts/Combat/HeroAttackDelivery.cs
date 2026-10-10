using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    // One geometric delivery path for player and bot attacks; target adapters freeze direction at cast.
    [RequireComponent(typeof(Health), typeof(HeroIdentity))]
    [DefaultExecutionOrder(120)]
    public sealed class HeroAttackDelivery : MonoBehaviour
    {
        private struct Pending
        {
            public bool Active, Launched, Basic, Volley, Hook;
            public Vector3 Direction, Point, LaunchOrigin, Position;
            public Health Target;
            public HeroAbilityExecution Execution;
            public AimSpec Spec;
            public int Slot, VisualId;
            public float Damage, ReleaseAt, SampleTime, Traveled;
        }
        private Pending[] pending;
        private readonly RaycastHit[] blockers = new RaycastHit[32];
        private Health self;
        private HeroIdentity identity;
        private bool hasPending;
        internal Vector3? VisualOrigin { get; private set; }
        private void Awake()
        {
            self = GetComponent<Health>(); identity = GetComponent<HeroIdentity>();
            pending = new Pending[Mathf.Clamp(AimingSettings.Current.deliveryCapacity, 8, 64)];
        }
        private void OnEnable() { if (self != null) { self.Died -= Cancel; self.Died += Cancel; } }
        public static bool IsRanged(HeroId hero)=>hero==HeroId.Nyra||hero==HeroId.Elyra||hero==HeroId.Mira||hero==HeroId.Traveler;
        public static float Windup(HeroId hero,int slot=-1)
        {
            if(slot<0)
            {
                // Melee basics need enough anticipation to show an actual draw-back and weapon travel.
                // Contact still happens exactly at ReleaseAt, so damage and the IK strike stay synced.
                return hero switch
                {
                    HeroId.Veyr=>.16f,HeroId.Kaelon=>.22f,HeroId.Nyra=>.08f,
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
            if (spec.targetingMode == TargetingMode.Homing && (!HeroCombatRules.Enemy(gameObject, pose.Target) ||
                HeroCombatRules.TargetDistance(transform.position, pose.Target) > spec.castRange)) return false;
            for (int i = 0; i < pending.Length; i++)
            {
                if (pending[i].Active) continue;
                pending[i] = new Pending { Active = true, Basic = slot < 0, Slot = slot, Damage = damage,
                    Execution = execution, Volley = volley, Hook = identity.Hero == HeroId.Nerok && slot == 0,
                    Direction = pose.Direction, Point = pose.Point, Spec = spec,
                    Target = spec.targetingMode == TargetingMode.Homing ? pose.Target : null, ReleaseAt = Time.time + windup };
                hasPending = true;
                if (!volley) HeroCombatAudio.Windup(gameObject, slot + 1);
                if (slot < 0) CombatMotion.BeginAimed(gameObject, windup, transform.position + pose.Direction * spec.castRange);
                return true;
            }
            return false;
        }
        public bool BasicAimed(AimPose pose, AimSpec spec, float amount)
            => Queue(pose, spec, -1, amount, null, false, Windup(identity.Hero));
        public bool AbilityAimed(HeroAbilityExecution execution, int slot, AimPose pose, bool volley = false, float windup = -1f)
        {
            if (execution == null || execution.gameObject != gameObject || !execution.isActiveAndEnabled || slot < 0 || slot > 3) return false;
            AimSpec spec = AimingSettings.Current.For(identity.Hero, slot);
            return Queue(pose, spec, slot, 0f, execution, volley,
                windup >= 0f ? windup : AbilityWindup(identity.Hero, slot, GetComponent<HeroAbilityKit>().Spec(slot)));
        }
        public void Basic(Health target, float amount, float range)
        {
            if (!HeroCombatRules.Enemy(gameObject, target) || HeroCombatRules.TargetDistance(transform.position, target) > range) return;
            AimSpec spec = AimingSettings.Current.For(identity.Hero, -1, range);
            BasicAimed(new AimPose(target.transform.position - transform.position, target.transform.position, target), spec, amount);
        }
        public void Ability(HeroAbilityExecution execution, int slot, Health target, bool volley = false, float windup = -1f)
        {
            if (slot < 0 || slot > 3 || !HeroCombatRules.Enemy(gameObject, target) ||
                HeroCombatRules.TargetDistance(transform.position, target) > GetComponent<HeroAbilityKit>().Spec(slot).Range) return;
            AbilityAimed(execution, slot, new AimPose(target.transform.position - transform.position, target.transform.position, target), volley, windup);
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
            if (count == blockers.Length) return 0f; // Conservatively stop on a saturated query.
            float closest = distance;
            for (int i = 0; i < count; i++)
            {
                Health owner = blockers[i].collider.GetComponentInParent<Health>();
                if (owner == self || owner != null && (owner.GetComponent<HeroIdentity>() != null ||
                    owner.GetComponent<LaneMinion>() != null || HeroCombatRules.Enemy(gameObject, owner))) continue;
                closest = Mathf.Min(closest, blockers[i].distance);
            }
            return closest;
        }
        private Health Contact(Pending p, Vector3 from, Vector3 to, bool moving, out float fraction, float motionFraction = 1f)
        {
            Health nearest = null; fraction = 2f;
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
                if (contact && t < fraction) { nearest = target; fraction = t; }
            }
            return nearest;
        }
        private void Impact(Pending p, Health target)
        {
            if (p.Hook) p.Execution.ResolveSharkHookHit(target, p.LaunchOrigin);
            else if (p.Basic) HeroCombatRules.ResolveBasicHit(gameObject, target, p.Damage);
            else p.Execution.ResolvePresentedHit(p.Slot, target, p.Volley);
        }
        private int Present(Pending p, Vector3 endpoint, float flight)
        {
            int id = HeroVfxPool.BeginDelivery(); VisualOrigin = p.LaunchOrigin;
            Transform follow = p.Spec.targetingMode == TargetingMode.Homing && p.Target != null ? p.Target.transform : null;
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
            finally { VisualOrigin = null; HeroVfxPool.EndDelivery(); }
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
                if (!p.Launched)
                {
                    if (Time.time < p.ReleaseAt) continue;
                    p.Launched = true; p.LaunchOrigin = transform.position; p.Position = p.LaunchOrigin; p.SampleTime = Time.time;
                    float length = BlockDistance(p.LaunchOrigin, p.Direction, p.Spec.castRange, p.Spec.projectileWidth);
                    Vector3 endpoint = p.LaunchOrigin + p.Direction * length;
                    if (p.Spec.aimType == AimType.Melee || p.Spec.aimType == AimType.Beam)
                    {
                        float fraction; Health target = Contact(p, p.LaunchOrigin, endpoint, false, out fraction);
                        if (target != null) endpoint = Vector3.Lerp(p.LaunchOrigin, endpoint, Mathf.Clamp01(fraction));
                        Present(p, endpoint, p.Spec.aimType == AimType.Beam ? .12f : 0f);
                        pending[i] = default;
                        if (target != null) Impact(p, target);
                        continue;
                    }
                    // A fixed visual endpoint, never the selected enemy's transform.
                    p.VisualId = Present(p, endpoint, Mathf.Max(.01f, length / p.Spec.projectileSpeed));
                }
                float dt = Mathf.Max(0f, Time.time - p.SampleTime); p.SampleTime = Time.time;
                if (dt <= 0f) { pending[i] = p; continue; }
                if (p.Spec.targetingMode == TargetingMode.Homing && HeroCombatRules.Enemy(gameObject, p.Target))
                    p.Direction = AimGeometry.Direction(p.Target.transform.position - p.Position);
                float step = Mathf.Min(p.Spec.castRange - p.Traveled, p.Spec.projectileSpeed * dt);
                float allowed = BlockDistance(p.Position, p.Direction, step, p.Spec.projectileWidth);
                Vector3 end = p.Position + p.Direction * allowed;
                // A wall/range endpoint can end this frame's flight early. Do not sample later target movement.
                float contactFraction; Health hit = Contact(p, p.Position, end, true, out contactFraction,
                    allowed / Mathf.Max(.0001f, p.Spec.projectileSpeed * dt));
                p.Traveled += allowed; p.Position = end;
                if (hit != null || allowed < step - .0001f || p.Traveled >= p.Spec.castRange - .0001f)
                {
                    pending[i] = default;
                    HeroVfxPool.CancelDelivery(p.VisualId);
                    if (hit != null) Impact(p, hit);
                }
                else pending[i] = p;
            }
            Recount();
        }
    }
}
