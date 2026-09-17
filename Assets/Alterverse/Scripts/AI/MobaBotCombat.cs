using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Match;

namespace Alterverse.AI
{
    [RequireComponent(typeof(Health),typeof(MobaBotBrain),typeof(BotTargetSensor))]
    public sealed class MobaBotCombat : MonoBehaviour
    {
        [SerializeField]private float fallbackAttackRange=1.65f;[SerializeField]private float fallbackAttackDamage=9f;[SerializeField]private float attackCooldown=1.05f;[SerializeField]private float turnSpeed=10f;
        private Health health;private MobaBotBrain brain;private BotTargetSensor sensor;private HeroCombatProfile profile;private PlanetBuffReceiver planetBuff;private float nextAttackTime;
        private float AttackRange=>profile!=null?profile.AttackRange:fallbackAttackRange;private float AttackDamage=>profile!=null?profile.Attack:fallbackAttackDamage;private float Cooldown=>attackCooldown*(planetBuff!=null?planetBuff.CooldownMultiplier:1f);
        private void Awake(){health=GetComponent<Health>();brain=GetComponent<MobaBotBrain>();sensor=GetComponent<BotTargetSensor>();profile=GetComponent<HeroCombatProfile>();planetBuff=GetComponent<PlanetBuffReceiver>();}
        private void Update(){if(Time.timeScale<=0f||health==null||health.IsDead||brain==null||sensor==null||brain.State!=MobaBotBrain.BotState.Fight)return;if(planetBuff==null)planetBuff=GetComponent<PlanetBuffReceiver>();Health target=sensor.CurrentTarget;if(target==null||target.IsDead)return;Vector3 delta=target.transform.position-transform.position;delta.y=0f;float range=AttackRange;if(delta.sqrMagnitude>range*range)return;if(delta.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta.normalized),turnSpeed*Time.deltaTime);if(Time.time<nextAttackTime)return;nextAttackTime=Time.time+Cooldown;target.TakeDamage(AttackDamage,gameObject);
HeroIdentity identity=GetComponent<HeroIdentity>();Color color=identity!=null?Alterverse.Visuals.StylizedArtDirection.HeroAccent(identity.Hero):Color.white;
if(range>4f)Alterverse.Visuals.PrototypeVfx.ProjectileLine(transform.position,target.transform.position,color,.08f);
else Alterverse.Visuals.PrototypeVfx.Slash(transform,color,1.2f);}
    }
}
