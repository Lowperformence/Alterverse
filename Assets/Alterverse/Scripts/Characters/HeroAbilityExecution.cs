using System.Collections;
using Alterverse.Audio;
using Alterverse.AI;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Characters
{
    [RequireComponent(typeof(Health), typeof(HeroIdentity), typeof(HeroAbilityKit))]
    public sealed class HeroAbilityExecution : MonoBehaviour
    {
        private Health self;
        private HeroLocomotion locomotion;
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private readonly float[] ready = new float[4];
        private readonly System.Collections.Generic.HashSet<Health> resolvedTargets = new();
        private int life;
        private readonly RaycastHit[] sightHits = new RaycastHit[16];

        private void Awake()
        {
            locomotion = GetComponent<HeroLocomotion>() ?? gameObject.AddComponent<HeroLocomotion>();
            self = GetComponent<Health>();
            identity = GetComponent<HeroIdentity>();
            kit = GetComponent<HeroAbilityKit>();
            ready[3] = Time.time + HeroAbilityKit.UltimateInitialLock;
            self.Died += CancelPending;
        }

        private void CancelPending()
        {
            GetComponent<HeroAttackDelivery>()?.Cancel();
            life++;
            HeroSkillVfx.CancelWarnings(gameObject);
            StopAllCoroutines();
            if (locomotion != null) locomotion.ResetMotion();
        }

        private void OnDisable() => CancelPending();

        private void OnDestroy()
        {
            if (self != null) self.Died -= CancelPending;
        }

        public float Remaining(int slot) => Mathf.Max(0, ready[Mathf.Clamp(slot, 0, 3)] - Time.time);
        public float Cooldown(int slot) => HeroCombatRules.Interval(gameObject, kit.Spec(slot).Cooldown);

        public void MarkNetworkRequestCooldown(int slot)
        {
            slot = Mathf.Clamp(slot, 0, 3);
            ready[slot] = Time.time + Cooldown(slot);
            GetComponent<HeroVisualMotion>()?.PulseAbility(slot, HeroAttackDelivery.AbilityWindup(identity.Hero, slot, kit.Spec(slot)));
        }

        public void ApplyNetworkCooldown(int slot, float remaining)
        {
            if (!Alterverse.Multiplayer.CoopAuthority.Active || Alterverse.Multiplayer.CoopAuthority.LocalIsHost) return;
            slot = Mathf.Clamp(slot, 0, 3);
            ready[slot] = Time.time + Mathf.Max(0f, remaining);
        }

        private static float TemporaryShield(Health health)
        {
            return health == null ? 0f : Mathf.Max(0f, health.Shield - health.HeroBaseShield);
        }

        public Health WoundedAlly(float range, bool shield = false)
        {
            TeamMember team = GetComponent<TeamMember>();
            if (team == null) return null;

            Health best = null;
            float lowest = shield ? .98f : .95f;
            float rangeSqr = range * range;

            // Support targeting must not depend on a physics collider. Heroes are valid combat
            // targets through Health/TeamMember even while a collider is absent or temporarily disabled.
            for (int i = 0; i < Health.Active.Count; i++)
            {
                Health h = Health.Active[i];
                if (h == null || h.IsDead || !h.isActiveAndEnabled || h.GetComponent<HeroIdentity>() == null || h.Normalized >= lowest) continue;
                TeamMember member = h.GetComponent<TeamMember>();
                if (member == null || member.Team != team.Team || (shield && TemporaryShield(h) > 5f)) continue;
                Vector3 delta = h.transform.position - transform.position;
                delta.y = 0;
                if (delta.sqrMagnitude > rangeSqr) continue;
                lowest = h.Normalized;
                best = h;
            }
            return best;
        }

        public bool TryCast(int slot, Health target, Vector3 movement)
        {
            if (slot < 0 || slot > 3 || Time.timeScale <= 0 || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || Remaining(slot) > 0) return false;

            AbilitySpec spec = kit.Spec(slot);
            HeroId hero = identity.Hero;
            float presentationWindup = spec.Windup;
            bool mobility = (hero == HeroId.Veyr && slot == 2) ||
                            (hero == HeroId.Nyra && slot == 1) ||
                            (hero == HeroId.Elyra && slot == 2) ||
                            (hero == HeroId.Traveler && slot == 1);

            if (mobility)
            {
                movement.y = 0;
                if (movement.sqrMagnitude < .01f) movement = transform.forward;
                Vector3 from = transform.position;
                Vector3 end = from + movement.normalized * spec.Range;

                // An assassin gap-close should not jump beyond its intended opponent.
                MobaBotMover mover = GetComponent<MobaBotMover>();
                if (mover != null && target != null && Vector3.Dot(movement, target.transform.position - from) > 0)
                    end = from + movement.normalized * Mathf.Min(spec.Range, Mathf.Max(0, HeroCombatRules.Distance(from, target.transform.position) - 1.5f));

                end = GameplayCollision.ClampToRoundedArena(end, 40f, 33f);
                if ((end - from).sqrMagnitude < .01f || (mover != null && !mover.CanRepositionTo(end))) return false;
                if (!locomotion.BeginDash(end)) return false;

                HeroCombatAudio.Cast(gameObject, slot);
                mover?.ResetSteering();
                HeroSkillVfx.Dash(gameObject, from, locomotion.DashDestination);
            }
            else if (hero == HeroId.Mira && (slot == 1 || slot == 2))
            {
                Health ally = WoundedAlly(spec.Range, slot == 2);
                if (ally == null) return false;
                HeroCombatAudio.Cast(gameObject, slot);
                if (slot == 1) ally.Heal(spec.Power, false);
                else ally.AddShield(spec.Power, 4f, false);
                HeroSkillVfx.SupportLink(gameObject, ally);
                HeroSkillVfx.Support(gameObject, ally, slot == 2, 4f);
            }
            else if ((hero == HeroId.Kaelon || hero == HeroId.Rook) && slot == 1)
            {
                if (hero == HeroId.Kaelon)
                {
                    if (TemporaryShield(self) > 5f) return false;
                    HeroCombatAudio.Cast(gameObject, slot);
                    self.AddShield(spec.Power, 4f, false);
                }
                else
                {
                    if (self.Normalized >= .95f) return false;
                    HeroCombatAudio.Cast(gameObject, slot);
                    self.Heal(spec.Power, false);
                    self.AddShield(20, 3, false);
                }

                HeroSkillVfx.Support(gameObject, self, true, hero == HeroId.Kaelon ? 4f : 3f);
                if (hero == HeroId.Rook) HeroSkillVfx.Support(gameObject, self, false, .45f);
            }
            else if (spec.Radius > 0)
            {
                Vector3 center = transform.position;
                if (spec.Range > 0)
                {
                    if (!ValidTarget(target, spec.Range)) return false;
                    center = PredictedAreaCenter(target, spec);
                }
                if (hero == HeroId.Mira && WoundedAlly(spec.Radius) == null) return false;

                HeroCombatAudio.Cast(gameObject, slot);
                HeroSkillVfx.Area(gameObject, center, spec.Radius, spec.Windup, true, hero == HeroId.Mira, slot);
                if (spec.Range > 0) HeroSkillVfx.Spell(gameObject, center, slot, spec.Windup);
                StartCoroutine(ResolveArea(slot, center, life));
            }
            else
            {
                if (!ValidTarget(target, spec.Range)) return false;
                if (spec.Windup > 0) HeroCombatAudio.Cast(gameObject, slot);

                if (hero == HeroId.Nyra && slot == 3)
                {
                    presentationWindup = HeroAttackDelivery.Windup(hero, slot);
                    StartCoroutine(Volley(target, life));
                }
                else if (spec.Windup > 0)
                {
                    StartCoroutine(DelayedSingle(slot, target, life));
                }
                else
                {
                    presentationWindup = HeroAttackDelivery.Windup(hero, slot);
                    HeroAttackDelivery.For(gameObject).Ability(this, slot, target);
                }
            }

            HeroSkillVfx.Cast(gameObject, slot);
            if (target != null && !mobility) locomotion.FaceAction(target.transform.position - transform.position);
            ready[slot] = Time.time + Cooldown(slot);
            GetComponent<HeroVisualMotion>()?.PulseAbility(slot, presentationWindup);
            return true;
        }

        public Vector3 PredictedAreaCenter(Health target, AbilitySpec spec)
        {
            Vector3 position = target.transform.position;
            Vector3 velocity = Vector3.zero;
            float quality = .55f;
            BotTargetSensor sensor = GetComponent<BotTargetSensor>();

            if (sensor != null)
            {
                if (!sensor.CanObserve(target) || !sensor.TryObservedVelocity(target, out velocity)) return position;
                position = sensor.ObservedTargetPosition;
                MobaBotBrain brain = GetComponent<MobaBotBrain>();
                quality = brain == null ? .35f : brain.DifficultyLevel switch
                {
                    BotDifficulty.Easy => .05f,
                    BotDifficulty.Normal => .35f,
                    BotDifficulty.Hard => .65f,
                    _ => .8f
                };
            }
            else
            {
                // No player input or hidden movement: verify the current line of sight first.
                Vector3 from = transform.position + Vector3.up * .4f;
                Vector3 to = position + Vector3.up * .4f;
                Vector3 ray = to - from;
                int count = Physics.RaycastNonAlloc(from, ray.normalized, sightHits, ray.magnitude,
                    GameplayCollision.SightMask, QueryTriggerInteraction.Ignore);
                if (count == sightHits.Length) return position;

                for (int i = 0; i < count; i++)
                {
                    Health owner = sightHits[i].collider.GetComponentInParent<Health>();
                    if (owner != self && owner != target) return position;
                }

                HeroLocomotion body = target.GetComponent<HeroLocomotion>();
                if (body != null) velocity = body.Velocity;
            }

            return HeroAimPrediction.Point(transform.position, position, velocity, spec.Windup, quality, spec.Range);
        }

        private bool ValidTarget(Health target, float range)
        {
            return HeroCombatRules.Enemy(gameObject, target) &&
                   HeroCombatRules.BasicDistance(transform.position, target) <= range;
        }

        private IEnumerator DelayedSingle(int slot, Health target, int castLife)
        {
            HeroSkillVfx.TargetWarning(gameObject, target, kit.Spec(slot).Windup);
            yield return new WaitForSeconds(kit.Spec(slot).Windup);
            if (castLife == life && !self.IsDead && HeroCombatStatus.CanAct(gameObject) && ValidTarget(target, kit.Spec(slot).Range + .30f))
                Hit(slot, target);
        }

        private IEnumerator Volley(Health target, int castLife)
        {
            // Existing four-shot cadence remains .18s; the first shot now has readable preparation.
            yield return new WaitForSeconds(HeroAttackDelivery.Windup(identity.Hero, 3));
            for (int i = 0; i < 4; i++)
            {
                if (castLife != life || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || !ValidTarget(target, kit.Ultimate.Range)) yield break;
                HeroAttackDelivery.For(gameObject).Ability(this, 3, target, true, 0);
                yield return new WaitForSeconds(.18f);
            }
        }

        internal void ResolvePresentedHit(int slot, Health target, bool volley)
        {
            if (!volley)
            {
                Hit(slot, target, true, true);
                return;
            }

            float before = target.Current + target.Shield;
            HeroCombatRules.Damage(gameObject, target, kit.Ultimate.Power / 4f);
            if (target.Current + target.Shield < before)
            {
                HeroSkillVfx.Impact(gameObject, target, 3);
                HeroCombatAudio.Impact(gameObject, 3, target.transform.position);
            }
        }

        private IEnumerator ResolveArea(int slot, Vector3 center, int castLife)
        {
            AbilitySpec spec = kit.Spec(slot);
            yield return new WaitForSeconds(spec.Windup);
            if (castLife != life || self.IsDead || !HeroCombatStatus.CanAct(gameObject)) yield break;

            TeamMember own = GetComponent<TeamMember>();
            Collider[] areaHits = Physics.OverlapSphere(center, spec.Radius, GameplayCollision.TargetMask, QueryTriggerInteraction.Collide);
            resolvedTargets.Clear();

            for (int i = 0; i < areaHits.Length; i++)
            {
                Collider c = areaHits[i];
                if (c == null) continue;
                Health h = c.GetComponentInParent<Health>();
                if (h == null || !resolvedTargets.Add(h) || h.IsDead || !h.isActiveAndEnabled) continue;

                TeamMember member = h.GetComponent<TeamMember>();
                bool ally = own != null && member != null && member.Team == own.Team && h.GetComponent<HeroIdentity>() != null;

                if (identity.Hero == HeroId.Mira)
                {
                    if (ally)
                    {
                        h.Heal(spec.Power, false);
                        h.AddShield(25, 3, false);
                        HeroSkillVfx.Support(gameObject, h, false, .45f);
                        HeroSkillVfx.Support(gameObject, h, true, 3);
                    }
                }
                else
                {
                    if (identity.Hero == HeroId.Kaelon && slot == 3 && ally)
                    {
                        h.AddShield(60, 3, false);
                        HeroSkillVfx.Support(gameObject, h, true, 3);
                    }
                    if (HeroCombatRules.Enemy(gameObject, h)) Hit(slot, h, false);
                }
            }

            if (identity.Hero == HeroId.Rook && slot == 3)
            {
                self.AddShield(65, 4, false);
                HeroSkillVfx.Support(gameObject, self, true, 4);
            }

            HeroCombatAudio.Impact(gameObject, slot, center);
            HeroSkillVfx.Area(gameObject, center, spec.Radius, .3f, false, identity.Hero == HeroId.Mira, slot);
        }

        private void Hit(int slot, Health target, bool visual = true, bool launched = false)
        {
            float damage = kit.Spec(slot).Power;
            if (identity.Hero == HeroId.Veyr && slot == 3 && target.GetComponent<HeroIdentity>() != null)
                damage *= 1f + (1f - target.Normalized) * .35f;

            float before = target.Current + target.Shield;
            HeroCombatRules.Damage(gameObject, target, damage);
            bool damaged = target.Current + target.Shield < before;

            if (damaged)
            {
                HeroSkillVfx.Impact(gameObject, target, slot);
                if (visual) HeroCombatAudio.Impact(gameObject, slot, target.transform.position);

                switch (identity.Hero)
                {
                    case HeroId.Veyr:
                        if (slot == 1) HeroCombatStatus.Apply(target, .8f, 1.2f);
                        break;
                    case HeroId.Kaelon:
                        HeroCombatStatus.Apply(target, slot == 3 ? .5f : .65f, slot == 3 ? 2f : 1.5f, slot == 0 ? .55f : 0);
                        break;
                    case HeroId.Nyra:
                        if (slot == 2) HeroCombatStatus.Apply(target, .7f, 1.8f);
                        break;
                    case HeroId.Elyra:
                        if (slot == 1 || slot == 3) HeroCombatStatus.Apply(target, slot == 3 ? .55f : .65f, 2f);
                        break;
                    case HeroId.Mira:
                        HeroCombatStatus.Apply(target, .75f, 1.5f);
                        break;
                    case HeroId.Rook:
                        if (slot == 0) HeroCombatStatus.Apply(target, .8f, 1f);
                        break;
                    case HeroId.Traveler:
                        if (slot == 2 || slot == 3) HeroCombatStatus.Apply(target, slot == 3 ? .62f : .72f, slot == 3 ? 1.8f : 1.5f);
                        break;
                }
            }

            if (visual && !launched) HeroSkillVfx.Spell(gameObject, target.transform.position, slot);

            if (damaged &&
                (identity.Hero == HeroId.Kaelon ||
                 identity.Hero == HeroId.Mira ||
                 identity.Hero == HeroId.Veyr && slot == 1 ||
                 identity.Hero == HeroId.Nyra && slot == 2 ||
                 identity.Hero == HeroId.Elyra && (slot == 1 || slot == 3) ||
                 identity.Hero == HeroId.Rook && slot == 0 ||
                 identity.Hero == HeroId.Traveler && (slot == 2 || slot == 3)))
                HeroSkillVfx.Debuff(gameObject, target);
        }
    }
}
