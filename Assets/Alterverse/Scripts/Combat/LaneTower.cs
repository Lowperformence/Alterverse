using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(Health))]public sealed class LaneTower:MonoBehaviour
{
[SerializeField]private float attackRange=5.25f,attackDamage=18f,attackCooldown=1.2f;private readonly Collider[] hitBuffer=new Collider[32];private Health health;private int team;private float nextAttackTime;public int Team=>team;private void Awake()=>health=GetComponent<Health>();public void Initialize(int teamId)=>team=teamId;
private void Update(){if(health==null||health.IsDead||Time.time<nextAttackTime)return;Health target=FindEnemyMinion();if(target==null)return;nextAttackTime=Time.time+attackCooldown;target.TakeDamage(attackDamage,gameObject);SpawnShot(target.transform.position);}
private Health FindEnemyMinion(){int count=Physics.OverlapSphereNonAlloc(transform.position,attackRange,hitBuffer,~0,QueryTriggerInteraction.Ignore);Health closest=null;float closestSqr=float.MaxValue;for(int i=0;i<count;i++){Collider hit=hitBuffer[i];if(hit==null)continue;Health candidate=hit.GetComponentInParent<Health>();if(candidate==null||candidate==health||candidate.IsDead)continue;LaneMinion minion=candidate.GetComponent<LaneMinion>();if(minion==null||minion.Team==team)continue;float sqr=(candidate.transform.position-transform.position).sqrMagnitude;if(sqr<closestSqr){closestSqr=sqr;closest=candidate;}}return closest;}
private void SpawnShot(Vector3 target){Color c=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;Vector3 origin=transform.position+Vector3.up*1.7f,direction=target-origin;float distance=direction.magnitude;if(distance<.01f)return;GameObject beam=GameObject.CreatePrimitive(PrimitiveType.Cube);beam.name="DF_TowerShot";beam.transform.position=origin+direction*.5f;beam.transform.rotation=Quaternion.LookRotation(direction);beam.transform.localScale=new Vector3(.11f,.11f,distance);Collider col=beam.GetComponent<Collider>();if(col!=null)col.enabled=false;Renderer r=beam.GetComponent<Renderer>();if(r!=null)r.material.color=c;PrototypeVfx.Ring(target,c,.65f,.18f);Destroy(beam,.1f);}
}
}
