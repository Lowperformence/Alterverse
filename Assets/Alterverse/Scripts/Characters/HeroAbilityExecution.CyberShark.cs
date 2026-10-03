using System.Collections;
using Alterverse.Audio;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Characters
{
    public sealed partial class HeroAbilityExecution
    {
        private bool TryCastShark(int slot, Health target, ref float windup)
        {
            AbilitySpec spec=kit.Spec(slot);
            if(slot==0)
            {
                if(!ValidTarget(target,spec.Range)||!SharkSight(target))return false;
                HeroCombatAudio.Windup(gameObject,1);
                HeroSkillVfx.TargetWarning(gameObject,target,spec.Windup);
                StartCoroutine(SharkHook(target,life));
            }
            else if(slot==1)
            {
                if(self.Shield>5f)return false;
                HeroCombatAudio.Cast(gameObject,slot);
                self.AddShield(spec.Power,3f,false);
                (GetComponent<HeroCombatStatus>()??gameObject.AddComponent<HeroCombatStatus>()).BoostSpeed(1.15f,2f);
                HeroSkillVfx.Support(gameObject,self,true,3f);
            }
            else
            {
                Vector3 center=transform.position;
                if(slot==3)
                {
                    if(!ValidTarget(target,spec.Range)||!SharkSight(target))return false;
                    center=PredictedAreaCenter(target,spec);
                }
                HeroCombatAudio.Windup(gameObject,slot+1);
                HeroSkillVfx.Area(gameObject,center,spec.Radius,spec.Windup,true,false,slot);
                Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(gameObject,slot==3?2:4,center,spec.Radius,spec.Windup);
                if(slot==3)StartCoroutine(SharkVortex(center,life));
                else StartCoroutine(ResolveArea(slot,center,life));
            }
            windup=spec.Windup;
            return true;
        }

        private bool SharkSight(Health target)=>SharkSightFrom(transform.position,target);
        private bool SharkSightFrom(Vector3 origin,Health target)
        {
            if(target==null)return false;
            Vector3 from=origin+Vector3.up*.4f;
            Vector3 ray=target.transform.position+Vector3.up*.4f-from;
            if(ray.sqrMagnitude<.0001f)return true;
            int count=Physics.RaycastNonAlloc(from,ray.normalized,sightHits,ray.magnitude,
                GameplayCollision.SightMask,QueryTriggerInteraction.Ignore);
            if(count==sightHits.Length)return false;
            for(int i=0;i<count;i++)
            {
                Health owner=sightHits[i].collider.GetComponentInParent<Health>();
                if(owner!=self&&owner!=target&&owner?.GetComponent<HeroIdentity>()==null&&owner?.GetComponent<LaneMinion>()==null)return false;
            }
            return true;
        }

        private IEnumerator SharkHook(Health target,int castLife)
        {
            yield return new WaitForSeconds(kit.Q.Windup);
            if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject)||
                !ValidTarget(target,kit.Q.Range)||!SharkSight(target))yield break;
            float flight=Mathf.Clamp(HeroCombatRules.Distance(transform.position,target.transform.position)/18f,.08f,.35f);
            HeroCombatAudio.Cast(gameObject,0);
            HeroSkillVfx.SharkAnchor(gameObject,target.transform.position,flight);
            Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(gameObject,0,target.transform.position,0,flight);
            yield return new WaitForSeconds(flight);
            if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject)||
                !ValidTarget(target,kit.Q.Range+.25f)||!SharkSight(target))yield break;
            float before=target.Current+target.Shield;
            HeroCombatRules.Damage(gameObject,target,kit.Q.Power);
            if(target.Current+target.Shield>=before)yield break;
            HeroSkillVfx.Impact(gameObject,target,0);
            HeroCombatAudio.Impact(gameObject,0,target.transform.position);
            if(target.GetComponent<HeroIdentity>()!=null)
            {
                self.AddShield(kit.Passive.Power,2f,false);
                HeroSkillVfx.Support(gameObject,self,true,2f);
            }
            if(target.IsDead||target.GetComponent<HeroIdentity>()==null&&target.GetComponent<LaneMinion>()==null)yield break;
            Vector3 delta=transform.position-target.transform.position;delta.y=0;
            float distance=Mathf.Min(4.2f,Mathf.Max(0,delta.magnitude-1.3f));
            if(distance<.1f)yield break;
            HeroLocomotion body=target.GetComponent<HeroLocomotion>()??target.gameObject.AddComponent<HeroLocomotion>();
            if(body.BeginForcedMove(target.transform.position+delta.normalized*distance,.35f))
                HeroCombatStatus.Apply(target,.65f,.7f);
        }

        private IEnumerator SharkVortex(Vector3 center,int castLife)
        {
            AbilitySpec spec=kit.Ultimate;
            yield return new WaitForSeconds(spec.Windup);
            if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))yield break;
            HeroCombatAudio.Cast(gameObject,3);
            HeroSkillVfx.SharkVortex(gameObject,center,spec.Radius,3f);
            Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(gameObject,1,center,spec.Radius,3f);
            // Six half-second ticks: total damage is exactly the catalog power, never per-tick power.
            for(int tick=0;tick<6;tick++)
            {
                yield return new WaitForSeconds(.5f);
                if(castLife!=life||self.IsDead)yield break;
                bool confirmed=false;
                for(int i=Health.Active.Count-1;i>=0;i--)
                {
                    Health h=Health.Active[i];
                    if(!HeroCombatRules.Enemy(gameObject,h))continue;
                    Vector3 delta=center-h.transform.position;delta.y=0;
                    if(delta.sqrMagnitude>spec.Radius*spec.Radius)continue;
                    // The same sight rule prevents drawing or damaging opponents through a building.
                    if(!SharkSightFrom(center,h))continue;
                    float before=h.Current+h.Shield;
                    HeroCombatRules.Damage(gameObject,h,spec.Power/6f);
                    confirmed|=h.Current+h.Shield<before;
                    HeroCombatStatus.Apply(h,.75f,.6f);
                    if(!h.IsDead&&(h.GetComponent<HeroIdentity>()!=null||h.GetComponent<LaneMinion>()!=null)&&delta.magnitude>.65f)
                    {
                        HeroLocomotion body=h.GetComponent<HeroLocomotion>()??h.gameObject.AddComponent<HeroLocomotion>();
                        body.BeginForcedMove(h.transform.position+delta.normalized*Mathf.Min(.24f,delta.magnitude-.65f),.10f);
                    }
                }
                if(confirmed)HeroCombatAudio.Impact(gameObject,3,center);
            }
        }
    }
}
