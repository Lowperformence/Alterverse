using Alterverse.Characters;using Alterverse.Match;using Alterverse.Visuals;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(Health))]public sealed class RealityCore:MonoBehaviour
{
[SerializeField]private int team;[SerializeField]private float vulnerableBelowPercent=20f;[SerializeField]private float attackRange=6.5f,attackDamage=6f,attackCooldown=1.55f;private Health health;private DimensionsFront front;private float nextAttack;private bool deathHandled;public int Team=>team;public bool IsVulnerable{get{ResolveFront();if(front==null)return false;float ownControl=team==0?front.ZeroControl:front.LumenControl;return ownControl<vulnerableBelowPercent;}}
private void Awake(){health=GetComponent<Health>();health.DamageGate=()=>IsVulnerable;health.Died+=OnDied;}private void OnDestroy(){if(health!=null){health.Died-=OnDied;health.DamageGate=null;}}public void Initialize(int teamId){team=Mathf.Clamp(teamId,0,1);ResolveFront();}
private void Update(){if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay)return;if(Time.timeScale<=0f||health==null||health.IsDead)return;ResolveFront();if(!IsVulnerable)return;if(Time.time<nextAttack)return;Health target=FindTarget();if(target==null)return;nextAttack=Time.time+attackCooldown;target.TakeDamage(attackDamage,gameObject);Color c=team==0?StylizedArtDirection.ZeroEnergy:StylizedArtDirection.LumenEnergy;PrototypeVfx.ProjectileLine(transform.position+Vector3.up*1.7f,target.transform.position+Vector3.up*.6f,c,.13f);}
private Health FindTarget(){Health best=null;float bestSqr=attackRange*attackRange;var activeHealth = Health.Active; for(int i=0;i<activeHealth.Count;i++) { Health candidate = activeHealth[i];{if(candidate==null||candidate==health||candidate.IsDead)continue;bool enemy=false;LaneMinion minion=candidate.GetComponent<LaneMinion>();if(minion!=null)enemy=minion.Team!=team;else{TeamMember member=candidate.GetComponent<TeamMember>();if(member!=null)enemy=member.Team!=team;}if(!enemy)continue;float sqr=(candidate.transform.position-transform.position).sqrMagnitude;if(sqr<bestSqr){bestSqr=sqr;best=candidate;}}}return best;}
private void ResolveFront(){if(front==null)front=FindAnyObjectByType<DimensionsFront>();}private void OnDied(){if(deathHandled)return;deathHandled=true;ResolveFront();front?.RegisterCoreDestroyed(team);}
}
}
