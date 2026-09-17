using UnityEngine;
namespace Alterverse.AI
{
public sealed class BotLanePlan:MonoBehaviour
{
[SerializeField]private int team;[SerializeField]private float laneZ;[SerializeField]private float homeX=-32f;[SerializeField]private float stagingX=1.5f;[SerializeField]private float pressureX=19f;[SerializeField]private float enemySideX=29f;private float lanePhaseOffset;
public Vector3 Home=>new(team==0?-34f:34f,transform.position.y,team==0?-4f:4f);public Vector3 Staging=>new(stagingX,transform.position.y,laneZ);public Vector3 Pressure=>new(pressureX,transform.position.y,laneZ);public Vector3 EnemySide=>new(enemySideX,transform.position.y,laneZ);
public void Initialize(int assignedTeam,float assignedLaneZ){team=Mathf.Clamp(assignedTeam,0,1);laneZ=assignedLaneZ>=0?16f:-16f;float direction=team==0?1f:-1f;homeX=-32f*direction;stagingX=1.5f*direction;pressureX=19f*direction;enemySideX=29f*direction;lanePhaseOffset=Mathf.Abs(gameObject.name.GetHashCode()%7)*.7f;}
public Vector3 GetDestination(MobaBotBrain.BotState state){if(state==MobaBotBrain.BotState.Retreat||state==MobaBotBrain.BotState.Recover)return Home;if(state==MobaBotBrain.BotState.Fight)return Pressure;float direction=team==0?1f:-1f;float wave=Mathf.PingPong((Time.time+lanePhaseOffset)*.12f,1f);float activeX=Mathf.Lerp(stagingX,pressureX,wave);Vector3 destination=new(activeX,transform.position.y,laneZ);if(Mathf.Abs(transform.position.x-homeX)<3f)destination.x=8f*direction;return destination;}
}
}