using Alterverse.Characters;using Alterverse.Match;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(Health))]public sealed class SoftTargeting:MonoBehaviour
{
[SerializeField]private float stickSeconds=1.25f;private const float HeroPriorityAlignment=.15f;private Health current,self;private float stickyUntil;private TeamMember team;public Health Current=>Valid(current)?current:null;private void Awake(){team=GetComponent<TeamMember>();self=GetComponent<Health>();}
public Health Acquire(float range,Vector3 aimDirection,bool preferHeroes=true)
{
bool sticky=Valid(current)&&Time.time<stickyUntil&&HeroCombatRules.TargetDistance(transform.position,current)<=range;
if(sticky&&(!preferHeroes||current.GetComponent<HeroIdentity>()!=null))return current;
var candidates=Health.Active;int count=candidates.Count;Health best=null,bestHero=null;float bestScore=float.MinValue,bestHeroScore=float.MinValue;Vector3 aim=aimDirection;aim.y=0;if(aim.sqrMagnitude<.05f)aim=transform.forward;aim.Normalize();
for(int i=0;i<count;i++)
{
Health h=candidates[i];if(!Valid(h))continue;float dist=HeroCombatRules.TargetDistance(transform.position,h);if(dist>range)continue;Vector3 d=h.transform.position-transform.position;d.y=0;float magnitude=Mathf.Max(.01f,d.magnitude),alignment=Vector3.Dot(aim,d/magnitude);bool hero=h.GetComponent<HeroIdentity>()!=null;bool objective=h.GetComponent<DimensionPlanet>()!=null;float score=(range-dist)*1.2f+alignment*3.5f+(objective?1.2f:0);
if(score>bestScore){bestScore=score;best=h;}
// A real enemy hero in a reasonable forward cone beats minions/objectives. This prevents melee
// heroes from wasting their short engage window on a soldier standing slightly closer to them.
if(preferHeroes&&hero&&alignment>=HeroPriorityAlignment&&score>bestHeroScore){bestHeroScore=score;bestHero=h;}
}
if(bestHero!=null)best=bestHero;else if(sticky)return current;current=best;stickyUntil=Time.time+stickSeconds;return current;
}
private float PlanarDistanceSquared(Health target){Vector3 delta=target.transform.position-transform.position;delta.y=0;return delta.sqrMagnitude;}public void Release(){current=null;stickyUntil=0;}public void RegisterOffensiveAction(){if(Valid(current))stickyUntil=Time.time+stickSeconds;}private bool Valid(Health h){if(h==null||h.IsDead||!h.isActiveAndEnabled||h==self)return false;RealityCore core=h.GetComponent<RealityCore>();if(core!=null)return core.IsVulnerable&&core.Team!=(team!=null?team.Team:0);DimensionPlanet planet=h.GetComponent<DimensionPlanet>();if(planet!=null)return planet.Available;TeamMember other=h.GetComponent<TeamMember>();if(other!=null&&team!=null)return other.Team!=team.Team;LaneMinion minion=h.GetComponent<LaneMinion>();if(minion!=null)return minion.Team!=(team!=null?team.Team:0);LaneTower tower=h.GetComponent<LaneTower>();if(tower!=null)return tower.Team!=(team!=null?team.Team:0);return false;}
}
}
