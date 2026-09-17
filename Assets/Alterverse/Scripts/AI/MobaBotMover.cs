using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
namespace Alterverse.AI
{
[RequireComponent(typeof(Health),typeof(MobaBotBrain),typeof(BotLanePlan),typeof(BotTargetSensor))]
public sealed class MobaBotMover:MonoBehaviour
{
[SerializeField]private float fallbackMoveSpeed=3.35f;[SerializeField]private float laneStopDistance=.45f;[SerializeField]private float chaseLimit=8.5f;[SerializeField]private float turnSpeed=9f;private const float ArenaHalfWidth=41f,ArenaHalfHeight=35f;private Health health;private MobaBotBrain brain;private BotLanePlan lanePlan;private BotTargetSensor sensor;private HeroCombatProfile profile;
private float MoveSpeed=>profile!=null?profile.MoveSpeed:fallbackMoveSpeed;private float CombatStop=>profile!=null?Mathf.Max(1.2f,profile.AttackRange*.82f):1.5f;
private void Awake(){health=GetComponent<Health>();brain=GetComponent<MobaBotBrain>();lanePlan=GetComponent<BotLanePlan>();sensor=GetComponent<BotTargetSensor>();profile=GetComponent<HeroCombatProfile>();}
private void Update(){if(health==null||health.IsDead||brain==null||lanePlan==null)return;Vector3 destination=lanePlan.GetDestination(brain.State);float stopDistance=laneStopDistance;if(brain.State==MobaBotBrain.BotState.Fight&&sensor!=null){Health target=sensor.CurrentTarget;if(target!=null&&!target.IsDead){Vector3 d=target.transform.position-transform.position;d.y=0;if(d.sqrMagnitude<=chaseLimit*chaseLimit){destination=target.transform.position;stopDistance=CombatStop;}}}MoveToward(destination,stopDistance);}
private void MoveToward(Vector3 destination,float stopDistance){Vector3 delta=destination-transform.position;delta.y=0;if(delta.sqrMagnitude<=stopDistance*stopDistance)return;Vector3 direction=delta.normalized;Vector3 next=transform.position+direction*MoveSpeed*Time.deltaTime;next.x=Mathf.Clamp(next.x,-ArenaHalfWidth,ArenaHalfWidth);next.z=Mathf.Clamp(next.z,-ArenaHalfHeight,ArenaHalfHeight);transform.position=next;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),turnSpeed*Time.deltaTime);}
}
}
