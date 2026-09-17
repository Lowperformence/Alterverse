using Alterverse.Characters;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(Health))]public sealed class LaneMinion:MonoBehaviour
{
[SerializeField]private float moveSpeed=2.8f,detectionRange=7.5f,attackRange=1.85f,attackDamage=3.25f,attackCooldown=1.35f;private Health health,target;private int team;private Vector3 destination;private float nextAttackTime,nextScanTime;public int Team=>team;private void Awake()=>health=GetComponent<Health>();public void Initialize(int teamId,Vector3 laneDestination){team=teamId;destination=laneDestination;nextAttackTime=Time.time+Random.Range(.2f,.65f);}
private void Update(){if(Time.timeScale<=0f||health==null||health.IsDead)return;if(!IsValidTarget(target)||Time.time>=nextScanTime){target=FindTarget();nextScanTime=Time.time+.2f;}if(target!=null){Vector3 delta=target.transform.position-transform.position;delta.y=0;float range=(target.GetComponent<LaneTower>()!=null||target.GetComponent<RealityCore>()!=null)?2.4f:attackRange;if(delta.sqrMagnitude<=range*range){Face(delta);if(Time.time>=nextAttackTime){nextAttackTime=Time.time+attackCooldown;target.TakeDamage(attackDamage,gameObject);Alterverse.Visuals.PrototypeVfx.ProjectileLine(transform.position+Vector3.up*.15f,target.transform.position+Vector3.up*.15f,team==0?new Color(.76f,.24f,1f):new Color(.18f,.92f,1f),.065f);}return;}MoveTowards(target.transform.position);return;}RealityCore core=FindEnemyCore();if(!HasEnemyBastion()&&core!=null&&core.IsVulnerable){MoveTowards(core.transform.position);return;}MoveTowards(destination);}
private bool IsValidTarget(Health candidate){if(candidate==null||candidate.IsDead)return false;Vector3 d=candidate.transform.position-transform.position;d.y=0;return d.sqrMagnitude<=detectionRange*detectionRange*1.45f;}
private Health FindTarget(){Health waveOpponent=FindOpposingMinion();if(waveOpponent!=null)return waveOpponent;Health best=null;float bestScore=float.MaxValue,maxSqr=detectionRange*detectionRange;foreach(Health candidate in FindObjectsByType<Health>()){if(candidate==null||candidate==health||candidate.IsDead)continue;bool enemy=false;float priority=1f;LaneMinion minion=candidate.GetComponent<LaneMinion>();if(minion!=null){enemy=minion.Team!=team;priority=.42f;}else{LaneTower tower=candidate.GetComponent<LaneTower>();if(tower!=null){enemy=tower.Team!=team;priority=.78f;}else{RealityCore core=candidate.GetComponent<RealityCore>();if(core!=null){enemy=core.Team!=team&&core.IsVulnerable;priority=.9f;}else{TeamMember member=candidate.GetComponent<TeamMember>();if(member!=null){enemy=member.Team!=team;priority=1.15f;}}}}if(!enemy)continue;Vector3 delta=candidate.transform.position-transform.position;delta.y=0;float sqr=delta.sqrMagnitude;if(sqr>maxSqr)continue;float lanePenalty=1f+Mathf.Abs(delta.z)*.12f;float score=sqr*priority*lanePenalty;if(score<bestScore){bestScore=score;best=candidate;}}return best;}
private Health FindOpposingMinion()
{
// Keep a live wave opponent until it dies or leaves detection range.
if(IsValidTarget(target)){LaneMinion current=target.GetComponent<LaneMinion>();if(current!=null&&current.Team!=team)return target;}
Health closest=null;float nearest=detectionRange*detectionRange;
foreach(LaneMinion other in FindObjectsByType<LaneMinion>())
{
if(other==null||other==this||other.Team==team)continue;
Health hp=other.GetComponent<Health>();if(hp==null||hp.IsDead)continue;
Vector3 delta=other.transform.position-transform.position;delta.y=0;
if(delta.sqrMagnitude<nearest){nearest=delta.sqrMagnitude;closest=hp;}
}
return closest;
}
private bool HasEnemyBastion(){foreach(LaneTower tower in FindObjectsByType<LaneTower>()){if(tower!=null&&tower.Team!=team){Health h=tower.GetComponent<Health>();if(h!=null&&!h.IsDead)return true;}}return false;}private RealityCore FindEnemyCore(){foreach(RealityCore core in FindObjectsByType<RealityCore>())if(core!=null&&core.Team!=team)return core;return null;}
private void MoveTowards(Vector3 point){Vector3 direction=point-transform.position;direction.y=0;if(direction.sqrMagnitude<.16f)return;float distance=direction.magnitude;direction.Normalize();transform.position+=direction*Mathf.Min(moveSpeed*Time.deltaTime,distance);Face(direction);}private void Face(Vector3 direction){direction.y=0;if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(direction),10*Time.deltaTime);}
}
}