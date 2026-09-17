using System.Collections.Generic;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
namespace Alterverse.Match
{
public sealed class MatchTelemetry:MonoBehaviour
{
public sealed class HeroStats{public HeroId Hero;public int Team;public float DamageDealt,DamageTaken,Healing;public int Kills,Deaths,Assists;}
public static MatchTelemetry Instance{get;private set;}private readonly Dictionary<Health,HeroStats> stats=new();public IEnumerable<HeroStats> Stats=>stats.Values;
private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;}
private void OnDestroy(){if(Instance==this)Instance=null;}
public HeroStats Get(Health health){if(health==null)return null;if(stats.TryGetValue(health,out HeroStats s))return s;HeroIdentity id=health.GetComponent<HeroIdentity>();TeamMember tm=health.GetComponent<TeamMember>();if(id==null||tm==null)return null;s=new HeroStats{Hero=id.Hero,Team=tm.Team};stats.Add(health,s);return s;}
public void RecordDamage(Health victim,GameObject source,float amount){if(victim==null||source==null||amount<=0)return;Health attacker=source.GetComponent<Health>();HeroStats v=Get(victim),a=Get(attacker);if(v!=null)v.DamageTaken+=amount;if(a!=null&&a!=v)a.DamageDealt+=amount;}
public void RecordHealing(Health target,float amount){HeroStats s=Get(target);if(s!=null&&amount>0)s.Healing+=amount;}
public void RecordDeath(Health victim,GameObject source){HeroStats v=Get(victim);if(v!=null)v.Deaths++;Health killer=source!=null?source.GetComponent<Health>():null;HeroStats k=Get(killer);if(k!=null&&k!=v)k.Kills++;}
public void LogSummary(){foreach(HeroStats s in stats.Values)Debug.Log($"[ALTERVERSE BALANCE] T{s.Team} {s.Hero} K{s.Kills}/D{s.Deaths} DMG {s.DamageDealt:0} TAKEN {s.DamageTaken:0} HEAL {s.Healing:0}");}
}
}
