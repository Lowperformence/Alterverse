using UnityEngine;
namespace Alterverse.AI
{
public sealed class BotLanePlan:MonoBehaviour
{
[SerializeField]private int team;[SerializeField]private float laneZ;[SerializeField]private float homeX=-32f;[SerializeField]private float stagingX=4f;[SerializeField]private float pressureX=18f;[SerializeField]private float enemySideX=30f;
public Vector3 Home=>new(homeX,transform.position.y,laneZ);public Vector3 Staging=>new(stagingX,transform.position.y,laneZ);public Vector3 Pressure=>new(pressureX,transform.position.y,laneZ);public Vector3 EnemySide=>new(enemySideX,transform.position.y,laneZ);
public void Initialize(int assignedTeam,float assignedLaneZ){team=Mathf.Clamp(assignedTeam,0,1);laneZ=assignedLaneZ>=0?16f:-16f;float direction=team==0?1f:-1f;homeX=-32f*direction;stagingX=4f*direction;pressureX=18f*direction;enemySideX=30f*direction;}
public Vector3 GetDestination(MobaBotBrain.BotState state){switch(state){case MobaBotBrain.BotState.Retreat:case MobaBotBrain.BotState.Recover:return Home;case MobaBotBrain.BotState.Fight:return Pressure;default:return Staging;}}
}
}
