using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
namespace Alterverse.AI
{
    [RequireComponent(typeof(Health), typeof(BotTargetSensor), typeof(HeroIdentity))]
    [RequireComponent(typeof(HeroAbilityKit))]
    public sealed class HeroBotAbilities : MonoBehaviour
    {
        private Health self;
        private BotTargetSensor sensor;
        private HeroIdentity identity;
        private HeroAbilityKit kit;
        private HeroAbilityExecution execution;
        private MobaBotBrain brain;
        private float nextDecision, controlReadyAt;
        private Health controlledTarget;
        private void Awake()
        {
            self = GetComponent<Health>(); sensor = GetComponent<BotTargetSensor>();
            identity = GetComponent<HeroIdentity>(); kit = GetComponent<HeroAbilityKit>(); brain = GetComponent<MobaBotBrain>();
            execution = GetComponent<HeroAbilityExecution>() ?? gameObject.AddComponent<HeroAbilityExecution>();
            nextDecision = Time.time + Random.Range(.7f,1.8f);
        }
        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if (Time.timeScale <= 0 || self.IsDead || !HeroCombatStatus.CanAct(gameObject) || Time.time < nextDecision) return;
            if(brain==null)brain=GetComponent<MobaBotBrain>();
            var difficulty=brain!=null?brain.Difficulty:BotDifficultyProfile.For(BotDifficulty.Normal);
            float rhythm=brain!=null?brain.DecisionRhythm:1f;
            nextDecision = Time.time + Random.Range(.28f,.48f)*difficulty.DecisionScale*rhythm;
            Health target = sensor.CurrentTarget;
            if(target!=null&&!sensor.CanObserve(target))target=null;
            HeroId h = identity.Hero;
            bool localBaseDefense=brain!=null&&brain.IsLocalBaseThreat(target);
            bool fighting = brain == null || brain.State == MobaBotBrain.BotState.Fight || localBaseDefense;
            float distance = target != null ? HeroCombatRules.Distance(transform.position,target.transform.position) : float.MaxValue;
            float discipline=difficulty.AbilityDiscipline;
            bool coordinated=brain!=null&&difficulty.Coordinated;
            bool followUp=false;
            if(coordinated&&target!=null)
            {
                // Only after this bot has actually seen the stun/slow, never from an input or cooldown.
                HeroCombatStatus status=target.GetComponent<HeroCombatStatus>();
                bool controlled=status!=null&&(status.Stunned||status.MoveMultiplier<.8f);
                if(controlled&&controlledTarget!=target)
                {controlledTarget=target;controlReadyAt=Time.time+difficulty.FollowUpDelay*rhythm;}
                else if(!controlled)controlledTarget=null;
                followUp=controlled&&controlledTarget==target&&Time.time>=controlReadyAt;
            }
            else controlledTarget=null;
            if (h == HeroId.Mira)
            {
                Health ally = execution.WoundedAlly(kit.Ultimate.Radius);
                float supportUltThreshold=Mathf.Lerp(.30f,.62f,discipline);
                if (ally != null && ally.Normalized < supportUltThreshold && TryCast(3,target)) return;
                Health healAlly=execution.WoundedAlly(kit.Spec(1).Range);
                if(healAlly!=null&&healAlly.Normalized<Mathf.Lerp(.70f,.90f,discipline)&&TryCast(1,target))return;
                if(sensor.NearbyEnemies>0&&TryCast(2,target))return;
            }
            float defensiveThreshold=Mathf.Lerp(.55f,.82f,discipline);
            if ((h == HeroId.Rook || h == HeroId.Kaelon && sensor.NearbyEnemies>0) && self.Normalized < defensiveThreshold && TryCast(1,target)) return;
            if (target == null) return;
            if (distance < 3.5f && ((h == HeroId.Nyra && TryCast(1,target)) || (h == HeroId.Elyra && TryCast(2,target)) || (h == HeroId.Traveler && TryCast(1,target)))) return;
            bool safeEngage=!coordinated||!brain.NeedsCombatReset&&target!=null&&!brain.Tactics.UnsafeDefense(target.transform.position)&&
                (brain.Tactics.CanFinish(target,transform.position)||
                 brain.Tactics.FightRatio(transform.position)>=1f&&(sensor.VisibleHeroesNear(target.transform.position,6f)<=1||followUp));
            if (h == HeroId.Veyr && (!fighting||safeEngage) && ((!fighting && self.Normalized < .5f) || (fighting && distance > 3.1f && distance < 7f)) && TryCast(2,target)) return;
            if (!fighting || coordinated&&h==HeroId.Veyr&&brain.NeedsCombatReset) return;
            // Missed opportunities delay a decision, never reset or shorten real cooldowns.
            if(brain!=null&&brain.Chance(difficulty.AbilityMistake))return;
            bool isHero=target.GetComponent<HeroIdentity>()!=null;
            bool ultimateWindow;
            if(h==HeroId.Veyr)
                ultimateWindow=isHero&&target.Normalized<Mathf.Lerp(.35f,.62f,discipline);
            else if(h==HeroId.Kaelon)
                ultimateWindow=isHero&&distance<=kit.Ultimate.Radius&&
                    (discipline<.70f||sensor.NearbyEnemies>=2||target.Normalized<.45f);
            else
                ultimateWindow=isHero&&target.Normalized<Mathf.Lerp(.45f,.78f,discipline);

            if(discipline>=.82f&&isHero&&sensor.NearbyEnemies>=2&&h!=HeroId.Veyr&&h!=HeroId.Kaelon)
                ultimateWindow=true;
            if(h==HeroId.Rook)ultimateWindow=ultimateWindow&&distance<=kit.Ultimate.Radius;
            if(coordinated&&isHero)
            {
                Vector3 center=kit.Ultimate.Range>0?target.transform.position:transform.position;
                int grouped=sensor.VisibleHeroesNear(center,Mathf.Max(1f,kit.Ultimate.Radius));
                bool execute=target.Current<=kit.Ultimate.Power*.9f;
                bool teamfight=sensor.NearbyEnemies>=2&&sensor.NearbyAllies>=1;
                bool ordinaryFinish=execution.Remaining(0)<=0&&target.Current<=kit.Q.Power&&distance<=kit.Q.Range;
                // No solo high-HP ultimate merely because it is off cooldown; preserve it for a real window.
                ultimateWindow=!ordinaryFinish&&(execute||grouped>=2||teamfight&&followUp);
                if(h==HeroId.Veyr)ultimateWindow&=safeEngage;
                if((h==HeroId.Kaelon||h==HeroId.Rook)&&distance>kit.Ultimate.Radius)ultimateWindow=false;
                if(controlledTarget==target&&!followUp&&!execute)ultimateWindow=false;
            }
            if (h != HeroId.Mira && ultimateWindow && TryCast(3,target)) return;
            // Mage follows visible CC after a human delay. Ordinary uncoordinated casts still work.
            if(coordinated&&h==HeroId.Elyra&&controlledTarget==target&&!followUp)return;
            if (TryCast(0,target)) return;
            if ((h == HeroId.Veyr || h == HeroId.Elyra) && TryCast(1,target)) return;
            if (h==HeroId.Traveler && distance<=kit.E.Range && TryCast(2,target)) return;
            if ((h == HeroId.Kaelon || h == HeroId.Nyra || h == HeroId.Rook) && distance <= Mathf.Max(kit.E.Range,kit.E.Radius)) TryCast(2,target);
        }
        private bool TryCast(int slot, Health target)
        {
            if(execution.Remaining(slot)>0)return false;
            AbilitySpec spec=kit.Spec(slot);
            bool support=identity.Hero==HeroId.Mira&&(slot==1||slot==2||slot==3);
            bool defensive=(identity.Hero==HeroId.Kaelon||identity.Hero==HeroId.Rook)&&slot==1;
            if(!support&&!defensive&&spec.Radius>0&&spec.Range<=0)
            {
                // The execution layer permits ground casts; AI must avoid empty circles.
                if(target==null||HeroCombatRules.Distance(transform.position,target.transform.position)>spec.Radius*.92f)return false;
            }
            if(brain!=null&&brain.Difficulty.Coordinated&&(identity.Hero==HeroId.Elyra||identity.Hero==HeroId.Traveler)&&spec.Radius>0&&spec.Range>0)
                target=SelectAreaTarget(target,spec);
            if(slot==3&&!support&&spec.Radius>0&&spec.Windup>0&&brain!=null&&brain.Difficulty.Coordinated&&target!=null)
            {
                HeroCombatStatus status=target.GetComponent<HeroCombatStatus>();
                bool controlled=status!=null&&(status.Stunned||status.MoveMultiplier<.8f);
                Vector3 center=spec.Range>0?execution.PredictedAreaCenter(target,spec):transform.position;
                bool group=sensor.VisibleHeroesNear(center,spec.Radius)>=2;
                if(!controlled&&!group&&!sensor.HasSteadyMotion&&target.Current>spec.Power)return false;
            }
            Vector3 direction = target != null ? transform.position-target.transform.position : transform.forward;
            if (identity.Hero == HeroId.Veyr && (brain == null || brain.State == MobaBotBrain.BotState.Fight&&!brain.NeedsCombatReset)) direction = -direction;
            bool cast=execution.TryCast(slot,target,direction);
            if(cast&&slot==1&&identity.Hero==HeroId.Veyr&&brain!=null&&brain.Difficulty.Coordinated&&
                execution.Remaining(0)>0&&!brain.Tactics.CanFinish(target,transform.position))brain.RequestCombatReset();
            return cast;
        }
        private Health SelectAreaTarget(Health fallback,AbilitySpec spec)
        {
            Health best=fallback;
            int bestCount=fallback!=null?sensor.VisibleHeroesNear(fallback.transform.position,spec.Radius):0;
            for(int i=0;i<sensor.ObservationCount;i++)
            {
                var sample=sensor.ObservationAt(i);
                if(!sample.Hero||sample.Target==null||Time.time-sample.SeenAt>.4f||
                    HeroCombatRules.Distance(transform.position,sample.Position)>spec.Range)continue;
                int count=sensor.VisibleHeroesNear(sample.Position,spec.Radius);
                if(count>bestCount){bestCount=count;best=sample.Target;}
            }
            return best!=null&&sensor.CanObserve(best)?best:fallback;
        }
    }
}


