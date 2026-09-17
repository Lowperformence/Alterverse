using UnityEngine;using Alterverse.Combat;using Alterverse.Characters;
namespace Alterverse.AI
{
[RequireComponent(typeof(Health))]public sealed class MobaBotBrain:MonoBehaviour
{
public enum BotState{Lane,Fight,Retreat,Recover}[SerializeField]private int team;[SerializeField]private float laneZ;[SerializeField,Range(.1f,1f)]private float aggression=.55f,caution=.55f;[SerializeField]private float decisionInterval=.35f,lowHealthThreshold=.30f;private Health health;private MobaBotRespawn respawn;private BotPersonality personality;private BotTargetSensor targetSensor;private BotState state=BotState.Lane;private float nextDecisionTime;private bool wasDead;
public int Team=>team;public float LaneZ=>laneZ;public BotState State=>state;public float Aggression=>aggression;public float Caution=>caution;
private void Awake(){health=GetComponent<Health>();respawn=GetComponent<MobaBotRespawn>();personality=GetComponent<BotPersonality>();targetSensor=GetComponent<BotTargetSensor>();ApplyPersonality();aggression=Mathf.Clamp01(aggression+Random.Range(-.06f,.06f));caution=Mathf.Clamp01(caution+Random.Range(-.06f,.06f));decisionInterval+=Random.Range(-.05f,.08f);}
private void Update(){if(health==null)return;if(health.IsDead){wasDead=true;return;}if(respawn!=null&&respawn.IsRespawning)return;if(wasDead)ResumeLaneAfterRespawn();if(Time.time<nextDecisionTime)return;nextDecisionTime=Time.time+Mathf.Max(.15f,decisionInterval);Decide();}
public void Initialize(int assignedTeam,float assignedLaneZ,float personalityAggression=.55f,float personalityCaution=.55f){team=assignedTeam;laneZ=assignedLaneZ;aggression=Mathf.Clamp01(personalityAggression);caution=Mathf.Clamp01(personalityCaution);state=BotState.Lane;nextDecisionTime=0;}
public void ResumeLaneAfterRespawn(){wasDead=false;state=BotState.Lane;nextDecisionTime=Time.time+.1f;BotTargetSensor sensor=GetComponent<BotTargetSensor>();if(sensor!=null)sensor.ClearTarget();}
public void RefreshPersonality(){personality=GetComponent<BotPersonality>();targetSensor=GetComponent<BotTargetSensor>();ApplyPersonality();}private void ApplyPersonality(){if(personality==null)return;aggression=personality.Aggression;caution=personality.Caution;decisionInterval=personality.ReactionDelay;lowHealthThreshold=Mathf.Lerp(.22f,.42f,personality.Caution);}
private void Decide(){float hp=health.Normalized;if(hp<=lowHealthThreshold){state=BotState.Retreat;return;}if((state==BotState.Retreat||state==BotState.Recover)&&hp<.8f){state=BotState.Recover;return;}if(hp>=.8f&&(state==BotState.Retreat||state==BotState.Recover)){state=BotState.Lane;return;}Health enemy=targetSensor!=null?targetSensor.CurrentTarget:null;if(enemy!=null&&!enemy.IsDead&&hp>=Mathf.Lerp(.72f,.42f,aggression)){state=BotState.Fight;return;}state=BotState.Lane;}
}
}