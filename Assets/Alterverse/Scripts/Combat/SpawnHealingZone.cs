using UnityEngine;
namespace Alterverse.Combat
{
public sealed class SpawnHealingZone:MonoBehaviour
{
[SerializeField]private float healPerSecond=40.32f;[SerializeField]private int team;[SerializeField]private float refreshInterval=.5f;private Health legacyTarget;private TeamMember[] cachedMembers=System.Array.Empty<TeamMember>();private float nextRefresh;
public int Team=>team;
public float Radius=>ArenaLayout.HealingRadius;
public bool ContainsWorldPoint(Vector3 point)=>ArenaLayout.IsInsideHealingArea(point,transform.position);
public void Initialize(Health playerHealth)=>legacyTarget=playerHealth;public void InitializeTeam(int assignedTeam)=>team=Mathf.Clamp(assignedTeam,0,1);
private void Update(){if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;if(legacyTarget!=null&&!legacyTarget.IsDead){TeamMember member=legacyTarget.GetComponent<TeamMember>();if(member==null||member.Team==team)HealIfInside(legacyTarget);}var activeHealth=Health.Active;for(int i=0;i<activeHealth.Count;i++){Health health=activeHealth[i];if(health==null||health==legacyTarget||health.IsDead)continue;TeamMember member=health.GetComponent<TeamMember>();if(member==null||member.Team!=team)continue;HealIfInside(health);}}
private void HealIfInside(Health health){if(ContainsWorldPoint(health.transform.position))health.Heal(healPerSecond*Time.deltaTime);}
}
}
