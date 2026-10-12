using System.Collections;
using Alterverse.Audio;
using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Characters
{
    public sealed partial class HeroAbilityExecution
    {
        private bool TryCastShark(int slot, Health target, ref float windup, AimPose? aimed = null)
        {
            AbilitySpec spec=kit.Spec(slot);
            if(slot==0)
            {
                if (!aimed.HasValue && (!ValidTarget(target,spec.Range)||!SharkSight(target))) return false;
                AimPose pose = aimed ?? new AimPose(target.transform.position - transform.position, target.transform.position, null, true);
                if (!HeroAttackDelivery.For(gameObject).AbilityAimed(this,slot,pose,windup:spec.Windup)) return false;
            }
            else if(slot==1)
            {
                if(self.TemporaryShield>5f)return false;
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
                    if (aimed.HasValue)
                    {
                        center = aimed.Value.Point;
                        if (!SharkSightPoint(transform.position, center)) return false;
                    }
                    else
                    {
                        if(!ValidTarget(target,spec.Range)||!SharkSight(target))return false;
                        center=PredictedAreaCenter(target,spec);
                    }
                }
                HeroCombatAudio.Windup(gameObject,slot+1);
                HeroSkillVfx.Area(gameObject,center,AimingSettings.Current.For(identity.Hero,slot).aoeRadius,spec.Windup,true,false,slot);
                Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(gameObject,slot==3?2:4,center,AimingSettings.Current.For(identity.Hero,slot).aoeRadius,spec.Windup);
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
            return SharkSightPoint(origin, target.transform.position, target);
        }
        private bool SharkSightPoint(Vector3 origin, Vector3 point, Health target = null)
        {
            Vector3 from=origin+Vector3.up*.4f;
            Vector3 ray=point+Vector3.up*.4f-from;
            if(ray.sqrMagnitude<.0001f)return true;
            int count=Physics.RaycastNonAlloc(from,ray.normalized,sightHits,ray.magnitude,
                GameplayCollision.SightMask,QueryTriggerInteraction.Ignore);
            RaycastHit[] hits=sightHits;
            if(count==sightHits.Length)
            {
                hits=Physics.RaycastAll(from,ray.normalized,ray.magnitude,
                    GameplayCollision.SightMask,QueryTriggerInteraction.Ignore);
                count=hits.Length;
            }
            for(int i=0;i<count;i++)
            {
                Health owner=hits[i].collider.GetComponentInParent<Health>();
                if(owner!=self&&(target==null||owner!=target)&&owner?.GetComponent<HeroIdentity>()==null&&owner?.GetComponent<LaneMinion>()==null)return false;
            }
            return true;
        }

        internal bool CanReleasePresentedHit(int slot,Health target)
            =>identity.Hero!=HeroId.Nerok||slot!=0||SharkSight(target);

        internal void ResolveSharkHookHit(Health target)
        {
            // The delivery sweep already confirmed projectile contact after wall clipping.
            // A second ray from the old launch point to the target's newer position
            // incorrectly invalidates valid hits. The pull still checks current LOS.
            if(!HeroCombatRules.Enemy(gameObject,target))return;
            float before=target.Current+target.Shield;
            HeroCombatRules.Damage(gameObject,target,kit.Q.Power);
            if(target.Current+target.Shield>=before)return;
            HeroSkillVfx.Impact(gameObject,target,0);
            HeroCombatAudio.Impact(gameObject,0,target.transform.position);
            if(target.GetComponent<HeroIdentity>()!=null)
            {
                self.AddShield(kit.Passive.Power,2f,false);
                HeroSkillVfx.Support(gameObject,self,true,2f);
            }
            if(target.IsDead||target.GetComponent<HeroIdentity>()==null&&target.GetComponent<LaneMinion>()==null)return;
            Vector3 delta=transform.position-target.transform.position;delta.y=0;
            float distance=Mathf.Min(4.2f,Mathf.Max(0,delta.magnitude-1.3f));
            if(distance<.1f||!SharkSight(target))return;
            HeroLocomotion body=target.GetComponent<HeroLocomotion>()??target.gameObject.AddComponent<HeroLocomotion>();
            if(body.BeginForcedMove(target.transform.position+delta.normalized*distance,.35f))
            {
                HeroSkillVfx.SharkPullChain(gameObject,target,.35f);
                Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(gameObject,6,target.transform.position,0,.35f,target);
                HeroCombatStatus.Apply(target,.65f,.7f);
            }
        }

        private IEnumerator SharkVortex(Vector3 center,int castLife)
        {
            AbilitySpec spec=kit.Ultimate;
            AimSpec aim = AimingSettings.Current.For(identity.Hero, 3);
            yield return new WaitForSeconds(spec.Windup);
            if(castLife!=life||self.IsDead||!HeroCombatStatus.CanAct(gameObject))yield break;
            HeroCombatAudio.Cast(gameObject,3);
            HeroSkillVfx.SharkVortex(gameObject,center,aim.aoeRadius,3f);
            Alterverse.Multiplayer.CoopHeroStateSync.BroadcastSharkEffect(gameObject,1,center,aim.aoeRadius,3f);
            // Register the current continuously; damage retains exactly six half-second ticks.
            float started=Time.time;
            int ticks=0;
            while(ticks<6)
            {
                if(castLife!=life||self.IsDead||!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)yield break;
                float elapsed=Time.time-started;
                int due=Mathf.Min(6,Mathf.FloorToInt((elapsed+.00001f)/.5f));
                bool confirmed=false;
                for(int i=Health.Active.Count-1;i>=0;i--)
                {
                    Health h=Health.Active[i];
                    if(!HeroCombatRules.Enemy(gameObject,h)||(h.DamageGate!=null&&!h.DamageGate()))continue;
                    HeroLocomotion body=h.GetComponent<HeroLocomotion>();
                    Vector3 position=body!=null?body.FrameStartPosition:h.transform.position;
                    Vector3 delta=center-position;delta.y=0;
                    if(!AimGeometry.Circle(center, position, aim.aoeRadius)||!SharkSightFrom(center,h))continue;
                    float before=h.Current+h.Shield;
                    for(int tick=ticks;tick<due;tick++)HeroCombatRules.Damage(gameObject,h,spec.Power/6f);
                    confirmed|=h.Current+h.Shield<before;
                    if(!h.IsDead&&(h.GetComponent<HeroIdentity>()!=null||h.GetComponent<LaneMinion>()!=null))
                    {
                        body=body??h.gameObject.AddComponent<HeroLocomotion>();
                        body.SetVortexPull(center,aim.aoeRadius,Mathf.Min(Time.deltaTime*1.5f,Mathf.Max(0f,3f-elapsed)));
                    }
                }
                if(confirmed)HeroCombatAudio.Impact(gameObject,3,center);
                ticks=due;
                if(ticks<6)yield return null;
            }
        }
    }
}

