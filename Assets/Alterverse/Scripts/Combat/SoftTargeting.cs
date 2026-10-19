using Alterverse.Characters;using Alterverse.Match;using UnityEngine;
namespace Alterverse.Combat
{
[RequireComponent(typeof(Health))]public sealed class SoftTargeting:MonoBehaviour
{
[SerializeField]private float stickSeconds=1.25f;private Health current,self;private float stickyUntil;private TeamMember team;public Health Current=>Valid(current)?current:null;private void Awake(){team=GetComponent<TeamMember>();self=GetComponent<Health>();}
private Health basicTarget;private float basicHoldUntil;
public Health AcquireBasic(float range,Vector3 aimDirection)
{
 Health previous=basicTarget;
 bool holding=Time.time<basicHoldUntil&&Valid(previous)&&
  HeroCombatRules.TargetDistance(transform.position,previous)<=range+.2f;
 Health selected=Acquire(range,aimDirection);
 // Preserve hero priority: a newly eligible hero can replace a soldier/objective.
 // Within the same class, changing HP/distance must not flip a held focus every frame.
 if(holding&&(selected==null||previous.GetComponent<HeroIdentity>()!=null||selected.GetComponent<HeroIdentity>()==null))
  selected=previous;
 basicTarget=current=selected;
 basicHoldUntil=selected!=null?Time.time+.2f:0f;
 // Keep only presentation/commitment in the edge band; no attack gains range.
 return selected!=null&&HeroCombatRules.TargetDistance(transform.position,selected)<=range?selected:null;
}
// Manual aiming deliberately bypasses sticky target binding. Eligibility is angular first.
public Health AcquireCone(float range,Vector3 direction,float coneAngle,bool heroesOnly=false)
=>AcquireAssisted(range,direction,coneAngle*.5f,heroesOnly);
public Health AcquireAssisted(float range,Vector3 direction,float angle,bool heroesOnly=false)
{
current=angle>0?AimGeometry.Select(gameObject,direction,range,angle,heroesOnly:heroesOnly):null;
stickyUntil=0;return current;
}
public Health Acquire(float range,Vector3 aimDirection,bool preferHeroes=true)
{
var identity=GetComponent<HeroIdentity>();
if(preferHeroes&&identity!=null&&HeroAttackDelivery.IsRanged(identity.Hero))
{
 Health selected=null;
 for(int i=0;i<Health.Active.Count;i++)
 {
  Health candidate=Health.Active[i];
  if(!Valid(candidate)||HeroCombatRules.TargetDistance(transform.position,candidate)>range)continue;
  if(HeroCombatRules.PreferConeTarget(candidate,selected,transform.position))selected=candidate;
 }
 current=selected;stickyUntil=Time.time+stickSeconds;return current;
}
bool sticky=Valid(current)&&Time.time<stickyUntil&&HeroCombatRules.TargetDistance(transform.position,current)<=range;
if(sticky&&(!preferHeroes||current.GetComponent<HeroIdentity>()!=null))return current;
var candidates=Health.Active;int count=candidates.Count;Health best=null,bestHero=null;float bestScore=float.MinValue,bestHeroScore=float.MinValue;Vector3 aim=aimDirection;aim.y=0;if(aim.sqrMagnitude<.05f)aim=transform.forward;aim.Normalize();
for(int i=0;i<count;i++)
{
Health h=candidates[i];if(!Valid(h))continue;float dist=HeroCombatRules.TargetDistance(transform.position,h);if(dist>range)continue;Vector3 d=h.transform.position-transform.position;d.y=0;float magnitude=Mathf.Max(.01f,d.magnitude),alignment=Vector3.Dot(aim,d/magnitude);bool hero=h.GetComponent<HeroIdentity>()!=null;bool objective=h.GetComponent<DimensionPlanet>()!=null;float score=(range-dist)*1.2f+alignment*3.5f+(objective?1.2f:0);
if(score>bestScore){bestScore=score;best=h;}
// Hero priority is a target-class rule, independent of facing. Alignment only ranks
// targets within a class; a sticky soldier/mech cannot hide an in-range enemy hero.
if(preferHeroes&&hero&&score>bestHeroScore){bestHeroScore=score;bestHero=h;}
}
if(bestHero!=null)best=bestHero;else if(sticky)return current;current=best;stickyUntil=Time.time+stickSeconds;return current;
}
public void Release(){current=basicTarget=null;stickyUntil=basicHoldUntil=0;}public void RegisterOffensiveAction(){if(Valid(current))stickyUntil=Time.time+stickSeconds;}private bool Valid(Health h){if(h==null||h.IsDead||!h.isActiveAndEnabled||h==self)return false;RealityCore core=h.GetComponent<RealityCore>();if(core!=null)return core.IsVulnerable&&core.Team!=(team!=null?team.Team:0);DimensionPlanet planet=h.GetComponent<DimensionPlanet>();if(planet!=null)return planet.Available;TeamMember other=h.GetComponent<TeamMember>();if(other!=null&&team!=null)return other.Team!=team.Team;LaneMinion minion=h.GetComponent<LaneMinion>();if(minion!=null)return minion.Team!=(team!=null?team.Team:0);LaneTower tower=h.GetComponent<LaneTower>();if(tower!=null)return tower.Team!=(team!=null?team.Team:0);return false;}
}
}

