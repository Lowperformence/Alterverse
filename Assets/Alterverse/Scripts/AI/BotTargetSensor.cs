using UnityEngine;using Alterverse.Characters;using Alterverse.Combat;
namespace Alterverse.AI
{
[RequireComponent(typeof(TeamMember))]public sealed class BotTargetSensor:MonoBehaviour
{
[SerializeField]private float fallbackScanRadius=7f,scanInterval=.35f;private TeamMember teamMember;private Health selfHealth,currentTarget;private HeroCombatProfile profile;private float nextScanTime;public Health CurrentTarget=>currentTarget;
private void Awake(){teamMember=GetComponent<TeamMember>();selfHealth=GetComponent<Health>();profile=GetComponent<HeroCombatProfile>();}
private void Update(){if(selfHealth!=null&&selfHealth.IsDead){currentTarget=null;return;}if(Time.time<nextScanTime)return;nextScanTime=Time.time+Mathf.Max(.15f,scanInterval);Scan();}
private void Scan(){currentTarget=null;float bestScore=float.MaxValue;float scanRadius=profile!=null?Mathf.Max(fallbackScanRadius,profile.AttackRange+2.5f):fallbackScanRadius;Collider[] hits=Physics.OverlapSphere(transform.position,scanRadius);for(int i=0;i<hits.Length;i++){TeamMember candidateTeam=hits[i].GetComponentInParent<TeamMember>();if(candidateTeam==null||candidateTeam==teamMember||!teamMember.IsEnemy(candidateTeam))continue;Health candidateHealth=candidateTeam.GetComponent<Health>();if(candidateHealth==null||candidateHealth.IsDead)continue;Vector3 delta=candidateHealth.transform.position-transform.position;delta.y=0;float healthBias=Mathf.Lerp(.62f,1f,candidateHealth.Normalized);HeroCombatProfile enemyProfile=candidateHealth.GetComponent<HeroCombatProfile>();float threatBias=enemyProfile!=null&&enemyProfile.Sustain<.4f?.88f:1f;float score=delta.sqrMagnitude*healthBias*threatBias;if(score>=bestScore)continue;bestScore=score;currentTarget=candidateHealth;}}
}
}
