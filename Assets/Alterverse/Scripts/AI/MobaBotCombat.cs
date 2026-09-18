using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health),typeof(MobaBotBrain),typeof(BotTargetSensor))]
    public sealed class MobaBotCombat : MonoBehaviour
    {
        [SerializeField]private float fallbackAttackRange=1.65f;[SerializeField]private float fallbackAttackDamage=9f;[SerializeField]private float attackCooldown=1.05f;
        private HeroLocomotion locomotion;private Health health;private MobaBotBrain brain;private BotTargetSensor sensor;private HeroCombatProfile profile;private PlanetBuffReceiver planetBuff;private float nextAttackTime;
        private float AttackRange=>profile!=null?profile.AttackRange:fallbackAttackRange;private float AttackDamage=>profile!=null?profile.Attack:fallbackAttackDamage;private float Cooldown=>HeroCombatRules.Interval(gameObject,profile!=null?profile.AttackInterval:attackCooldown);
        private void Awake(){locomotion=GetComponent<HeroLocomotion>()??gameObject.AddComponent<HeroLocomotion>();health=GetComponent<Health>();brain=GetComponent<MobaBotBrain>();sensor=GetComponent<BotTargetSensor>();profile=GetComponent<HeroCombatProfile>();planetBuff=GetComponent<PlanetBuffReceiver>();}
        private void Update(){if(Time.timeScale<=0f||health==null||health.IsDead||!HeroCombatStatus.CanAct(gameObject)||brain==null||sensor==null||brain.State!=MobaBotBrain.BotState.Fight)return;if(planetBuff==null)planetBuff=GetComponent<PlanetBuffReceiver>();Health target=sensor.CurrentTarget;if(target==null||target.IsDead)return;Vector3 delta=target.transform.position-transform.position;delta.y=0f;float range=AttackRange;if(delta.sqrMagnitude>range*range)return;if(delta.sqrMagnitude>.01f)locomotion.FaceAction(delta,.1f);if(Time.time<nextAttackTime)return;nextAttackTime=Time.time+Cooldown;HeroCombatRules.BasicHit(gameObject,target,AttackDamage);}

    }
}

