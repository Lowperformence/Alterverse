using System.Collections;
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
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private readonly float[] ready = new float[4];
        private int life;
        private void Awake()
        {
            self = GetComponent<Health>(); identity = GetComponent<HeroIdentity>(); kit = GetComponent<HeroAbilityKit>();
            ready[3] = Time.time + HeroAbilityKit.UltimateInitialLock;
            self.Died += CancelPending;
        }
        private void CancelPending() { life++; StopAllCoroutines(); }
        private void OnDisable() => CancelPending();
        private void OnDestroy() { if (self != null) self.Died -= CancelPending; }
        public float Remaining(int slot) => Mathf.Max(0, ready[Mathf.Clamp(slot, 0, 3)] - Time.time);
        public float Cooldown(int slot) => HeroCombatRules.Interval(gameObject, kit.Spec(slot).Cooldown);
        public Health WoundedAlly(float range, bool shield = false)
        {
            TeamMember team = GetComponent<TeamMember>(); if (team == null) return null;
            Health best = null; float lowest = shield ? .98f : .95f;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h == null || h.IsDead || !h.isActiveAndEnabled || h.GetComponent<HeroIdentity>() == null || h.Normalized >= lowest) continue;
                TeamMember member = h.GetComponent<TeamMember>();
                if (member == null || member.Team != team.Team || HeroCombatRules.Distance(transform.position, h.transform.position) > range || (shield && h.Shield > 5f)) continue;
                lowest = h.Normalized; best = h;
            }
            return best;
        }
        public bool TryCast(int slot, Health target, Vector3 movement)
        {
            if (slot < 0 || slot > 3 || Time.timeScale <= 0 || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || Remaining(slot) > 0) return false;
            AbilitySpec spec = kit.Spec(slot); HeroId hero = identity.Hero;
            bool mobility = (hero == HeroId.Veyr && slot == 2) || (hero == HeroId.Nyra && slot == 1) || (hero == HeroId.Elyra && slot == 2);
            if (mobility)
            {
                movement.y = 0; if (movement.sqrMagnitude < .01f) movement = transform.forward;
                Vector3 from = transform.position, end = from + movement.normalized * spec.Range;
                // An assassin gap-close should not jump beyond its intended opponent.
                MobaBotMover mover = GetComponent<MobaBotMover>();
                if (mover != null && target != null && Vector3.Dot(movement, target.transform.position - from) > 0)
                    end = from + movement.normalized * Mathf.Min(spec.Range, Mathf.Max(0, HeroCombatRules.Distance(from, target.transform.position) - 1.5f));
                end.x = Mathf.Clamp(end.x, -40, 40); end.z = Mathf.Clamp(end.z, -33, 33);
                if ((end-from).sqrMagnitude < .01f || (mover != null && !mover.CanRepositionTo(end))) return false;
                CharacterController cc = GetComponent<CharacterController>();
                if (cc != null && cc.enabled) { cc.enabled = false; transform.position = end; cc.enabled = true; }
                else transform.position = end;
                mover?.ResetMotion(); HeroSkillVfx.Dash(gameObject, from, end);
            }
            else if (hero == HeroId.Mira && (slot == 1 || slot == 2))
            {
                Health ally = WoundedAlly(spec.Range, slot == 2); if (ally == null) return false;
                if (slot == 1) ally.Heal(spec.Power); else ally.AddShield(spec.Power, 4f);
                HeroSkillVfx.Link(gameObject, ally.transform.position);
                HeroSkillVfx.Area(gameObject, ally.transform.position, .8f, .3f, false);
            }
            else if ((hero == HeroId.Kaelon || hero == HeroId.Rook) && slot == 1)
            {
                if (hero == HeroId.Kaelon) { if (self.Shield > 5f) return false; self.AddShield(spec.Power, 4f); }
                else { if (self.Normalized >= .95f) return false; self.Heal(spec.Power); self.AddShield(20, 3); }
                HeroSkillVfx.Area(gameObject, transform.position, 1f, .4f, false);
            }
            else if (spec.Radius > 0)
            {
                Vector3 center = transform.position;
                if (spec.Range > 0)
                {
                    if (!ValidTarget(target, spec.Range)) return false;
                    center = target.transform.position;
                }
                if (hero == HeroId.Mira && WoundedAlly(spec.Radius) == null) return false;
                HeroSkillVfx.Area(gameObject, center, spec.Radius, spec.Windup, true);
                StartCoroutine(ResolveArea(slot, center, life));
            }
            else
            {
                if (!ValidTarget(target, spec.Range)) return false;
                if (hero == HeroId.Nyra && slot == 3) StartCoroutine(Volley(target, life));
                else if (spec.Windup > 0) StartCoroutine(DelayedSingle(slot, target, life));
                else Hit(slot, target);
            }
            ready[slot] = Time.time + Cooldown(slot);
            GetComponent<HeroVisualMotion>()?.PulseAttack(slot == 3);
            return true;
        }
        private bool ValidTarget(Health target, float range) => HeroCombatRules.Enemy(gameObject, target) && HeroCombatRules.Distance(transform.position, target.transform.position) <= range;
        private IEnumerator DelayedSingle(int slot, Health target, int castLife)
        {
            HeroSkillVfx.Area(gameObject, target.transform.position, .7f, kit.Spec(slot).Windup, true);
            yield return new WaitForSeconds(kit.Spec(slot).Windup);
            if (castLife == life && !self.IsDead && HeroCombatStatus.CanAct(gameObject) && ValidTarget(target, kit.Spec(slot).Range)) Hit(slot, target);
        }
        private IEnumerator Volley(Health target, int castLife)
        {
            for (int i = 0; i < 4; i++)
            {
                if (castLife != life || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || !ValidTarget(target, kit.Ultimate.Range)) yield break;
                HeroCombatRules.Damage(gameObject, target, kit.Ultimate.Power / 4f);
                HeroSkillVfx.Link(gameObject, target.transform.position, true);
                yield return new WaitForSeconds(.18f);
            }
        }
        private IEnumerator ResolveArea(int slot, Vector3 center, int castLife)
        {
            AbilitySpec spec = kit.Spec(slot);
            yield return new WaitForSeconds(spec.Windup);
            if (castLife != life || self.IsDead || !HeroCombatStatus.CanAct(gameObject)) yield break;
            TeamMember own = GetComponent<TeamMember>();
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h == null || h.IsDead || !h.isActiveAndEnabled || HeroCombatRules.Distance(center, h.transform.position) > spec.Radius) continue;
                TeamMember member = h.GetComponent<TeamMember>();
                bool ally = own != null && member != null && member.Team == own.Team && h.GetComponent<HeroIdentity>() != null;
                if (identity.Hero == HeroId.Mira)
                { if (ally) { h.Heal(spec.Power); h.AddShield(25, 3); } }
                else
                {
                    if (identity.Hero == HeroId.Kaelon && slot == 3 && ally) h.AddShield(60, 3);
                    if (HeroCombatRules.Enemy(gameObject, h)) Hit(slot, h, false);
                }
            }
            if (identity.Hero == HeroId.Rook && slot == 3) self.AddShield(65, 4);
            HeroSkillVfx.Area(gameObject, center, spec.Radius, .3f, false);
        }
        private void Hit(int slot, Health target, bool visual = true)
        {
            float damage = kit.Spec(slot).Power;
            if (identity.Hero == HeroId.Veyr && slot == 3 && target.GetComponent<HeroIdentity>() != null)
                damage *= 1f + (1f - target.Normalized) * .35f;
            HeroCombatRules.Damage(gameObject, target, damage);
            switch (identity.Hero)
            {
                case HeroId.Veyr: if (slot == 1) HeroCombatStatus.Apply(target, .8f, 1.2f); break;
                case HeroId.Kaelon: HeroCombatStatus.Apply(target, slot == 3 ? .5f : .65f, slot == 3 ? 2f : 1.5f, slot == 0 ? .55f : 0); break;
                case HeroId.Nyra: if (slot == 2) HeroCombatStatus.Apply(target, .7f, 1.8f); break;
                case HeroId.Elyra: if (slot == 1 || slot == 3) HeroCombatStatus.Apply(target, slot == 3 ? .55f : .65f, 2f); break;
                case HeroId.Mira: HeroCombatStatus.Apply(target, .75f, 1.5f); break;
                case HeroId.Rook: if (slot == 0) HeroCombatStatus.Apply(target, .8f, 1f); break;
            }
            if (visual) HeroSkillVfx.Link(gameObject, target.transform.position, slot == 3);
        }
    }
}
