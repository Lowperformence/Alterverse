using System;using UnityEngine;using Alterverse.Match;
namespace Alterverse.Combat
{
public sealed class Health:MonoBehaviour
{
[SerializeField,Min(1f)]private float maxHealth=100f;[SerializeField]private bool destroyOnDeath;public float Current{get;private set;}public float Max=>maxHealth;public bool IsDead{get;private set;}public float Normalized=>maxHealth<=0?0:Current/maxHealth;public GameObject LastDamageSource{get;private set;}public event Action<float,float> Changed;public event Action Died;public event Action<Health,GameObject> DiedWithSource;
private void Awake(){Current=maxHealth;}public void Configure(float newMaxHealth,bool shouldDestroyOnDeath=false){maxHealth=Mathf.Max(1,newMaxHealth);destroyOnDeath=shouldDestroyOnDeath;IsDead=false;Current=maxHealth;LastDamageSource=null;Changed?.Invoke(Current,maxHealth);}public void TakeDamage(float amount)=>TakeDamage(amount,null);
public void TakeDamage(float amount,GameObject source){if(IsDead||amount<=0||IsFriendlySource(source))return;PlanetBuffReceiver buff=GetComponent<PlanetBuffReceiver>();if(buff!=null)amount*=buff.DamageTakenMultiplier;float applied=Mathf.Min(amount,Current);if(source!=null){LastDamageSource=source;MatchScoreboard.Instance?.RecordDamage(this,source);MatchTelemetry.Instance?.RecordDamage(this,source,applied);}Current=Mathf.Max(0,Current-amount);Changed?.Invoke(Current,maxHealth);if(Current<=0){IsDead=true;Died?.Invoke();DiedWithSource?.Invoke(this,LastDamageSource);MatchTelemetry.Instance?.RecordDeath(this,LastDamageSource);if(destroyOnDeath)Destroy(gameObject);}}
private bool IsFriendlySource(GameObject source){if(source==null)return false;TeamMember victimTeam=GetComponent<TeamMember>();TeamMember sourceTeam=source.GetComponentInParent<TeamMember>();return victimTeam!=null&&sourceTeam!=null&&victimTeam.Team==sourceTeam.Team;}
public void Heal(float amount){if(IsDead||amount<=0)return;float applied=Mathf.Min(amount,maxHealth-Current);Current=Mathf.Min(maxHealth,Current+amount);if(applied>0)MatchTelemetry.Instance?.RecordHealing(this,applied);Changed?.Invoke(Current,maxHealth);}public void RestoreFull(){IsDead=false;Current=maxHealth;LastDamageSource=null;Changed?.Invoke(Current,maxHealth);}
}
}
