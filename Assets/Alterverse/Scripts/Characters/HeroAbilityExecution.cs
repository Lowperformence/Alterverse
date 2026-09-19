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
        private const int ScanBufferSize=64;private readonly Collider[] scanBuffer=new Collider[ScanBufferSize];private readonly System.Collections.Generic.HashSet<Health> resolvedTargets=new();
        private int life;
        private void Awake()
        {
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            self = GetComponent<Health>(); identity = GetComponent<HeroIdentity>(); kit = GetComponent<HeroAbilityKit>();
            ready[3] = Time.time + HeroAbilityKit.UltimateInitialLock;
            self.Died += CancelPending;
        }
        private void CancelPending() { life++; HeroSkillVfx.CancelWarnings(gameObject); StopAllCoroutines(); if(locomotion!=null)locomotion.ResetMotion(); }
        private void OnDisable() => CancelPending();
        private void OnDestroy() { if (self != null) self.Died -= CancelPending; }
        public float Remaining(int slot) => Mathf.Max(0, ready[Mathf.Clamp(slot, 0, 3)] - Time.time);
        public float Cooldown(int slot) => HeroCombatRules.Interval(gameObject, kit.Spec(slot).Cooldown);
        public Health WoundedAlly(float range, bool shield = false)
        {
            TeamMember team = GetComponent<TeamMember>(); if (team == null) return null;
            Health best = null; float lowest = shield ? .98f : .95f;
            int hits=Physics.OverlapSphereNonAlloc(transform.position,range,scanBuffer);
            for(int i=0;i<hits;i++)
            {
                Collider c=scanBuffer[i];if(c==null)continue;Health h=c.GetComponentInParent<Health>();
                if(h==null||h.IsDead||!h.isActiveAndEnabled||h.GetComponent<HeroIdentity>()==null||h.Normalized>=lowest)continue;
                TeamMember member=h.GetComponent<TeamMember>();
                if(member==null||member.Team!=team.Team||(shield&&h.Shield>5f))continue;
                lowest=h.Normalized;best=h;
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
                if(!locomotion.BeginDash(end))return false;
                HeroCombatAudio.Cast(gameObject,slot);
                mover?.ResetSteering(); HeroSkillVfx.Dash(gameObject, from, locomotion.DashDestination);
            }
            else if (hero == HeroId.Mira && (slot == 1 || slot == 2))
            {
                Health ally = WoundedAlly(spec.Range, slot == 2); if (ally == null) return false;
                HeroCombatAudio.Cast(gameObject,slot);
                if (slot == 1) ally.Heal(spec.Power,false); else ally.AddShield(spec.Power, 4f,false);
                HeroSkillVfx.SupportLink(gameObject, ally);
                HeroSkillVfx.Support(gameObject,ally,slot==2,4f);
            }
            else if ((hero == HeroId.Kaelon || hero == HeroId.Rook) && slot == 1)
            {
                if (hero == HeroId.Kaelon) { if (self.Shield > 5f) return false; HeroCombatAudio.Cast(gameObject,slot);self.AddShield(spec.Power, 4f,false); }
                else { if (self.Normalized >= .95f) return false; HeroCombatAudio.Cast(gameObject,slot);self.Heal(spec.Power,false); self.AddShield(20, 3,false); }
                HeroSkillVfx.Support(gameObject,self,true,hero==HeroId.Kaelon?4f:3f);
                if(hero==HeroId.Rook)HeroSkillVfx.Support(gameObject,self,false,.45f);
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
                HeroCombatAudio.Cast(gameObject,slot);
                HeroSkillVfx.Area(gameObject, center, spec.Radius, spec.Windup, true, hero==HeroId.Mira,slot);
                if(spec.Range>0)HeroSkillVfx.Spell(gameObject,center,slot,spec.Windup);
                StartCoroutine(ResolveArea(slot, center, life));
            }
            else
            {
                if (!ValidTarget(target, spec.Range)) return false;
                HeroCombatAudio.Cast(gameObject,slot);
                if (hero == HeroId.Nyra && slot == 3) StartCoroutine(Volley(target, life));
                else if (spec.Windup > 0) StartCoroutine(DelayedSingle(slot, target, life));
                else Hit(slot, target);
            }
            HeroSkillVfx.Cast(gameObject,slot);
            if(target!=null&&!mobility)locomotion.FaceAction(target.transform.position-transform.position);
            ready[slot] = Time.time + Cooldown(slot);
            GetComponent<HeroVisualMotion>()?.PulseAbility(slot,spec.Windup);
            return true;
        }
        private bool ValidTarget(Health target, float range) => HeroCombatRules.Enemy(gameObject, target) && HeroCombatRules.Distance(transform.position, target.transform.position) <= range;
        private IEnumerator DelayedSingle(int slot, Health target, int castLife)
        {
            HeroSkillVfx.TargetWarning(gameObject,target,kit.Spec(slot).Windup);
            yield return new WaitForSeconds(kit.Spec(slot).Windup);
            if (castLife == life && !self.IsDead && HeroCombatStatus.CanAct(gameObject) && ValidTarget(target, kit.Spec(slot).Range)) Hit(slot, target);
        }
        private IEnumerator Volley(Health target, int castLife)
        {
            for (int i = 0; i < 4; i++)
            {
                if (castLife != life || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || !ValidTarget(target, kit.Ultimate.Range)) yield break;
                if(i>0)HeroCombatAudio.Cast(gameObject,3);
                float before=target.Current+target.Shield;
                HeroCombatRules.Damage(gameObject, target, kit.Ultimate.Power / 4f);
                HeroSkillVfx.Spell(gameObject,target.transform.position,3);
                if(target.Current+target.Shield<before)
                {
                    HeroSkillVfx.Impact(gameObject,target,3);
                    HeroCombatAudio.Impact(gameObject,3,target.transform.position);
                }
                yield return new WaitForSeconds(.18f);
            }
        }
        private IEnumerator ResolveArea(int slot, Vector3 center, int castLife)
        {
            AbilitySpec spec = kit.Spec(slot);
            yield return new WaitForSeconds(spec.Windup);
            if (castLife != life || self.IsDead || !HeroCombatStatus.CanAct(gameObject)) yield break;
            TeamMember own = GetComponent<TeamMember>();
            int hits=Physics.OverlapSphereNonAlloc(center,spec.Radius,scanBuffer);
            resolvedTargets.Clear();
            for(int i=0;i<hits;i++)
            {
                Collider c=scanBuffer[i];if(c==null)continue;Health h=c.GetComponentInParent<Health>();
                if(h==null||!resolvedTargets.Add(h)||h.IsDead||!h.isActiveAndEnabled)continue;
                TeamMember member = h.GetComponent<TeamMember>();
                bool ally = own != null && member != null && member.Team == own.Team && h.GetComponent<HeroIdentity>() != null;
                if (identity.Hero == HeroId.Mira)
                { if (ally) { h.Heal(spec.Power,false); h.AddShield(25, 3,false); HeroSkillVfx.Support(gameObject,h,false,.45f);HeroSkillVfx.Support(gameObject,h,true,3); } }
                else
                {
                    if (identity.Hero == HeroId.Kaelon && slot == 3 && ally) { h.AddShield(60, 3,false);HeroSkillVfx.Support(gameObject,h,true,3); }
                    if (HeroCombatRules.Enemy(gameObject, h)) Hit(slot, h, false);
                }
            }
            if (identity.Hero == HeroId.Rook && slot == 3) { self.AddShield(65, 4,false);HeroSkillVfx.Support(gameObject,self,true,4); }
            HeroCombatAudio.Impact(gameObject,slot,center);
            HeroSkillVfx.Area(gameObject, center, spec.Radius, .3f, false,identity.Hero==HeroId.Mira,slot);
        }
        private void Hit(int slot, Health target, bool visual = true)
        {
            float damage = kit.Spec(slot).Power;
            if (identity.Hero == HeroId.Veyr && slot == 3 && target.GetComponent<HeroIdentity>() != null)
                damage *= 1f + (1f - target.Normalized) * .35f;
            float before=target.Current+target.Shield;
            HeroCombatRules.Damage(gameObject, target, damage);
            bool damaged=target.Current+target.Shield<before;
            if(damaged)
            {
                HeroSkillVfx.Impact(gameObject,target,slot);
                if(visual)HeroCombatAudio.Impact(gameObject,slot,target.transform.position);
            }
            switch (identity.Hero)
            {
                case HeroId.Veyr: if (slot == 1) HeroCombatStatus.Apply(target, .8f, 1.2f); break;
                case HeroId.Kaelon: HeroCombatStatus.Apply(target, slot == 3 ? .5f : .65f, slot == 3 ? 2f : 1.5f, slot == 0 ? .55f : 0); break;
                case HeroId.Nyra: if (slot == 2) HeroCombatStatus.Apply(target, .7f, 1.8f); break;
                case HeroId.Elyra: if (slot == 1 || slot == 3) HeroCombatStatus.Apply(target, slot == 3 ? .55f : .65f, 2f); break;
                case HeroId.Mira: HeroCombatStatus.Apply(target, .75f, 1.5f); break;
                case HeroId.Rook: if (slot == 0) HeroCombatStatus.Apply(target, .8f, 1f); break;
            }
            if (visual) HeroSkillVfx.Spell(gameObject, target.transform.position, slot);
            if(damaged&&(identity.Hero==HeroId.Kaelon||identity.Hero==HeroId.Mira||identity.Hero==HeroId.Veyr&&slot==1||
                identity.Hero==HeroId.Nyra&&slot==2||identity.Hero==HeroId.Elyra&&(slot==1||slot==3)||identity.Hero==HeroId.Rook&&slot==0))
                HeroSkillVfx.Debuff(gameObject,target);
        }
    }
}

