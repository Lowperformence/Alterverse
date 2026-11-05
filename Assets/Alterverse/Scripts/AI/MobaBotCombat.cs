using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health), typeof(MobaBotBrain), typeof(BotTargetSensor))]
    public sealed class MobaBotCombat : MonoBehaviour
    {
        [SerializeField] private float fallbackAttackRange=1.65f, fallbackAttackDamage=9f, attackCooldown=1.05f;
        private HeroLocomotion locomotion;
        private Health health, observedTarget;
        private MobaBotBrain brain;
        private BotTargetSensor sensor;
        private HeroCombatProfile profile;
        private float nextAttackTime, targetReadyAt, nextSightCheck;
        private float AttackRange => profile!=null ? profile.AttackRange : fallbackAttackRange;
        private float AttackDamage => profile!=null ? profile.Attack : fallbackAttackDamage;
        private float Cooldown => HeroCombatRules.Interval(gameObject,profile!=null ? profile.AttackInterval : attackCooldown);

        private void Awake()
        {
            locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();
            health=GetComponent<Health>();brain=GetComponent<MobaBotBrain>();
            sensor=GetComponent<BotTargetSensor>();profile=GetComponent<HeroCombatProfile>();
        }
        private void Update()
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;
            if(Time.timeScale<=0f||health==null||health.IsDead||!HeroCombatStatus.CanAct(gameObject)||brain==null||sensor==null)return;
            Health target=sensor.CurrentTarget;
            if(target==null||target.IsDead){observedTarget=null;return;}

            // Retreating bots should not become harmless targets. They keep moving toward safety,
            // but may return basic attacks against a visible hero that is actively pressuring them.
            bool recovering=brain.State==MobaBotBrain.BotState.Retreat||brain.State==MobaBotBrain.BotState.Recover;
            bool heroThreat=target.GetComponent<HeroIdentity>()!=null;
            float range=AttackRange;
            float targetDistance=HeroCombatRules.BasicDistance(transform.position,target);
            bool counterFire=recovering&&heroThreat&&
                (sensor.IsRecentAttacker(target)||targetDistance<=range*.92f);
            if(recovering&&!brain.IsLocalBaseThreat(target)&&!counterFire)return;
            if(target!=observedTarget)
            {
                observedTarget=target;
                float reactionScale=counterFire ? 0.28f : 0.65f;
                targetReadyAt=Time.time+Mathf.Clamp(brain.Difficulty.ReactionDelay*reactionScale*brain.DecisionRhythm,
                    counterFire ? 0.025f : 0.06f,counterFire ? 0.18f : 0.55f);
            }
            if(targetDistance>range)return;
            Vector3 delta=sensor.ObservedTargetPosition-transform.position;delta.y=0;
            if(delta.sqrMagnitude>.01f)locomotion.FaceAction(delta,.1f);
            if(Time.time<targetReadyAt||Time.time<nextAttackTime||Time.time<nextSightCheck)return;
            nextSightCheck=Time.time+.035f;
            if(!sensor.CanObserve(target)||HeroCombatRules.BasicDistance(transform.position,target)>range)return;
            nextAttackTime=Time.time+Cooldown;
            HeroCombatRules.BasicHit(gameObject,target,AttackDamage);
        }
    }
}
