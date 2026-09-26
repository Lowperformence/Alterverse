using Alterverse.Audio;
using Alterverse.Characters;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Shared player/bot delivery for formerly instant targeted attacks. Balancing values still
    /// come from their existing catalogs; only windup and flight timing are introduced here.
    /// Fixed slots avoid an allocation/coroutine on each basic attack. No collision simulation.
    /// </summary>
    [RequireComponent(typeof(Health),typeof(HeroIdentity))]
    public sealed class HeroAttackDelivery : MonoBehaviour
    {
        private struct Pending
        {
            public bool Active,Launched,Basic,Volley;
            public Health Target;
            public HeroAbilityExecution Execution;
            public int Slot;
            public float Damage,Range,Grace,ReleaseAt,HitAt;
        }
        // Four volley shots + Q/W/E + one basic fit without growing the pool.
        private readonly Pending[] pending=new Pending[8];
        private Health self;
        private HeroIdentity identity;
        private bool hasPending;
        private void Awake(){self=GetComponent<Health>();identity=GetComponent<HeroIdentity>();}
        private void OnEnable(){self.Died-=Cancel;self.Died+=Cancel;}
        public static bool IsRanged(HeroId hero)=>hero==HeroId.Nyra||hero==HeroId.Elyra||hero==HeroId.Mira||hero==HeroId.Traveler;
        public static float Windup(HeroId hero,int slot=-1)
        {
            float basic=hero switch
            {
                HeroId.Veyr=>.09f,HeroId.Kaelon=>.16f,HeroId.Nyra=>.08f,
                HeroId.Elyra=>.14f,HeroId.Mira=>.12f,HeroId.Rook=>.13f,HeroId.Traveler=>.10f,_=>.13f
            };
            return slot<0?basic:hero==HeroId.Nyra&&slot==3?.22f:basic+.04f;
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
        private int FreeSlot()
        {
            for(int i=0;i<pending.Length;i++)if(!pending[i].Active)return i;
            Debug.LogWarning("DIMENSIONS FRONT: attack delivery capacity exceeded.");return -1;
        }
        public void Basic(Health target,float amount,float range)
        {
            if(target==null||HeroCombatRules.TargetDistance(transform.position,target)>range)return;
            int i=FreeSlot();if(i<0)return;
            float windup=Windup(identity.Hero);
            hasPending=true;
            pending[i]=new Pending{Active=true,Basic=true,Slot=-1,Target=target,Damage=amount,Range=range,Grace=IsRanged(identity.Hero)?0:.25f,ReleaseAt=Time.time+windup};
            GetComponent<HeroVisualMotion>()?.PulseBasic(windup);
        }
        public void Ability(HeroAbilityExecution execution,int slot,Health target,bool volley=false,float windup=-1)
        {
            int i=FreeSlot();if(i<0||target==null)return;
            float range=GetComponent<HeroAbilityKit>().Spec(slot).Range;
            if(HeroCombatRules.TargetDistance(transform.position,target)>range)return;
            hasPending=true;
            pending[i]=new Pending{Active=true,Slot=slot,Target=target,Execution=execution,Volley=volley,
                Range=range,Grace=IsRanged(identity.Hero)?0:.25f,ReleaseAt=Time.time+(windup>=0?windup:Windup(identity.Hero,slot))};
        }
        public void Cancel()
        {
            if(!hasPending)return;
            for(int i=0;i<pending.Length;i++)pending[i]=default;
            hasPending=false;
            HeroSkillVfx.CancelProjectiles(gameObject);
        }
        private void OnDisable(){if(self!=null)self.Died-=Cancel;Cancel();}
        private void Update()
        {
            if(!hasPending||Time.timeScale<=0)return;
            if(self==null||self.IsDead||!HeroCombatStatus.CanAct(gameObject)){Cancel();return;}
            for(int i=0;i<pending.Length;i++)
            {
                Pending p=pending[i];if(!p.Active)continue;
                if(!HeroCombatRules.Enemy(gameObject,p.Target)||
                    !p.Basic&&(p.Execution==null||!p.Execution.isActiveAndEnabled))
                {pending[i]=default;continue;}
                if(!p.Launched)
                {
                    if(Time.time<p.ReleaseAt)continue;
                    // Keep the originally selected target. Leaving the existing range can avoid contact.
                    if(HeroCombatRules.TargetDistance(transform.position,p.Target)>p.Range+p.Grace)
                    {pending[i]=default;continue;}
                    Vector3 origin=GetComponent<HeroVisualMotion>()?.ReleasePoint??transform.position;
                    float flight=Flight(identity.Hero,origin,p.Target.transform.position+Vector3.up*.25f);
                    p.Launched=true;p.HitAt=Time.time+flight;
                    if(p.Basic)
                    {
                        HeroCombatAudio.Play(gameObject,0,false,origin);
                        HeroSkillVfx.Basic(gameObject,p.Target.transform.position,flight,p.Target.transform,false);
                    }
                    else
                    {
                        HeroCombatAudio.Cast(gameObject,p.Slot);
                        HeroSkillVfx.Spell(gameObject,p.Target.transform.position,p.Slot,flight,p.Target.transform);
                    }
                    pending[i]=p;
                }
                if(Time.time<p.HitAt)continue;
                // Clear before callbacks (a hit can disable/destroy a component or trigger death).
                pending[i]=default;
                if(HeroCombatRules.TargetDistance(transform.position,p.Target)>p.Range+p.Grace)continue;
                if(p.Basic)HeroCombatRules.ResolveBasicHit(gameObject,p.Target,p.Damage);
                else p.Execution.ResolvePresentedHit(p.Slot,p.Target,p.Volley);
            }
            hasPending=false;
            for(int i=0;i<pending.Length;i++)hasPending|=pending[i].Active;
        }
    }
}
