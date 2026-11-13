using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Combat;
using UnityEngine;
namespace Alterverse.Match
{
public sealed class DimensionsFrontCombatBridge:MonoBehaviour
{
[SerializeField,Min(.1f)]private float refreshInterval=.5f;
private readonly HashSet<Health> tracked=new();
private readonly HashSet<Health> inspected=new();
private DimensionsFront front;
private DimensionsFrontVisual visual;
private float nextRefresh;
private void Start(){front=Object.FindAnyObjectByType<DimensionsFront>();visual=Object.FindAnyObjectByType<DimensionsFrontVisual>();RefreshHeroes();}
private void Update(){if(Time.unscaledTime<nextRefresh)return;nextRefresh=Time.unscaledTime+refreshInterval;if(front==null)front=Object.FindAnyObjectByType<DimensionsFront>();if(visual==null)visual=Object.FindAnyObjectByType<DimensionsFrontVisual>();RefreshHeroes();}
private static bool IsHero(TeamMember member)=>member!=null&&(member.GetComponent<HeroIdentity>()!=null||member.GetComponent<HeroRespawn>()!=null||member.GetComponent<MobaBotRespawn>()!=null||member.GetComponent<Alterverse.Multiplayer.CoopHeroSlot>()!=null);
private void RefreshHeroes()
{
    if(inspected.Count>256){inspected.Clear();foreach(Health h in tracked)if(h!=null)inspected.Add(h);}
    var active=Health.Active;
    for(int i=0;i<active.Count;i++)
    {
        Health health=active[i];
        if(health==null||!inspected.Add(health))continue;
        TeamMember member=health.GetComponent<TeamMember>();
        if(!IsHero(member)||!tracked.Add(health))continue;
        health.DiedWithSource+=OnHeroDied;
    }
}
private void OnHeroDied(Health victim,GameObject source){if(front==null||victim==null||source==null)return;TeamMember victimTeam=victim.GetComponent<TeamMember>(),killerTeam=source.GetComponentInParent<TeamMember>();if(!IsHero(victimTeam)||!IsHero(killerTeam)||victimTeam.Team==killerTeam.Team)return;float worldZ=victim.transform.position.z;front.RegisterHeroKill(killerTeam.Team,worldZ);visual?.PulseImpact(worldZ,killerTeam.Team);MatchTelemetry.Instance?.RecordFrontContribution(source,front.HeroKillInfluence);}
private void OnDestroy(){foreach(Health health in tracked)if(health!=null)health.DiedWithSource-=OnHeroDied;tracked.Clear();inspected.Clear();}
}
}
