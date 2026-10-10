using UnityEngine;
namespace Alterverse.Combat
{
public sealed class SpawnHealingZone:MonoBehaviour
{
[SerializeField]private float radius=4.5f;[SerializeField]private float healPerSecond=40.32f;[SerializeField]private int team;[SerializeField]private float refreshInterval=.5f;private Health legacyTarget;private TeamMember[] cachedMembers=System.Array.Empty<TeamMember>();private float nextRefresh;
public int Team=>team;
public void Initialize(Health playerHealth)=>legacyTarget=playerHealth;public void InitializeTeam(int assignedTeam)=>team=Mathf.Clamp(assignedTeam,0,1);
private void Update(){if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;if(Time.time>=nextRefresh){nextRefresh=Time.time+Mathf.Max(.1f,refreshInterval);cachedMembers=Object.FindObjectsByType<TeamMember>();}if(legacyTarget!=null&&!legacyTarget.IsDead){TeamMember member=legacyTarget.GetComponent<TeamMember>();if(member==null||member.Team==team)HealIfInside(legacyTarget);}for(int i=0;i<cachedMembers.Length;i++){TeamMember member=cachedMembers[i];if(member==null||member.Team!=team)continue;Health health=member.GetComponent<Health>();if(health==null||health==legacyTarget||health.IsDead)continue;HealIfInside(health);}}
private void HealIfInside(Health health){Vector3 delta=health.transform.position-transform.position;delta.y=0;if(delta.sqrMagnitude<=radius*radius)health.Heal(healPerSecond*Time.deltaTime);}
}
}