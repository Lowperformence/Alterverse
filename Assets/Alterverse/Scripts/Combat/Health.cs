using System;using UnityEngine;using Alterverse.Match;
namespace Alterverse.Combat
{
public sealed class Health:MonoBehaviour
{
private static readonly System.Collections.Generic.List<Health> active=new(128);
public static System.Collections.Generic.IReadOnlyList<Health> Active=>active;
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]private static void ResetRegistry()=>active.Clear();
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void RebuildRegistry(){active.Clear();foreach(Health h in FindObjectsByType<Health>())if(h.isActiveAndEnabled)active.Add(h);}
private void OnEnable(){if(!active.Contains(this))active.Add(this);}
private void OnDisable()=>active.Remove(this);
[SerializeField,Min(1f)]private float maxHealth=100f;[SerializeField]private bool destroyOnDeath;private float shield,shieldUntil;public float Shield=>Time.time<shieldUntil?shield:0f;public void AddShield(float amount,float duration)=>AddShield(amount,duration,true);public void AddShield(float amount,float duration,bool playAudio){if(!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState||IsDead||amount<=0)return;shield=Mathf.Max(Shield,Mathf.Min(amount,maxHealth*.4f));shieldUntil=Mathf.Max(shieldUntil,Time.time+Mathf.Max(0,duration));if(playAudio)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Shield,transform.position,.3f);Changed?.Invoke(Current,maxHealth);}private void ClearTransient(){shield=shieldUntil=0;GetComponent<HeroCombatStatus>()?.Clear();}public float Current{get;private set;}public float Max=>maxHealth;public bool IsDead{get;private set;}public float Normalized=>maxHealth<=0?0:Current/maxHealth;public GameObject LastDamageSource{get;private set;}public event Action<float,float> Changed;public event Action<float,float,GameObject> Damaged;public event Action Died;public event Action<Health,GameObject> DiedWithSource;public Func<bool> DamageGate{get;set;}
private void Awake(){Current=maxHealth;}public void Configure(float newMaxHealth,bool shouldDestroyOnDeath=false){maxHealth=Mathf.Max(1,newMaxHealth);destroyOnDeath=shouldDestroyOnDeath;ClearTransient();IsDead=false;Current=maxHealth;LastDamageSource=null;Changed?.Invoke(Current,maxHealth);}public void TakeDamage(float amount)=>TakeDamage(amount,null);
public void TakeDamage(float amount,GameObject source){if(!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState||IsDead||amount<=0||IsFriendlySource(source)||(DamageGate!=null&&!DamageGate()))return;Alterverse.AI.MobaBotBrain bot=GetComponent<Alterverse.AI.MobaBotBrain>();TeamMember team=GetComponent<TeamMember>();if(bot!=null&&team!=null&&team.Team==1)amount*=bot.Difficulty.DamageTakenMultiplier;PlanetBuffReceiver buff=GetComponent<PlanetBuffReceiver>();if(buff!=null)amount*=buff.DamageTakenMultiplier;float absorbed=Mathf.Min(Shield,amount);shield=Shield-absorbed;amount-=absorbed;float applied=Mathf.Min(amount,Current);if(source!=null){LastDamageSource=source;MatchScoreboard.Instance?.RecordDamage(this,source);MatchTelemetry.Instance?.RecordDamage(this,source,applied);}Current=Mathf.Max(0,Current-amount);Changed?.Invoke(Current,maxHealth);Damaged?.Invoke(applied,absorbed,source);if(applied>0&&GetComponent<RealityCore>()!=null)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.CoreHit,transform.position,.65f);if(Current<=0){if(GetComponent<LaneMinion>()!=null)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.MinionDeath,transform.position,.22f);if(GetComponent<Alterverse.Characters.HeroIdentity>()!=null){Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.HeroDeath,transform.position,.78f);FractureCorePickup.Spawn(transform.position);}IsDead=true;ClearTransient();Died?.Invoke();DiedWithSource?.Invoke(this,LastDamageSource);MatchTelemetry.Instance?.RecordDeath(this,LastDamageSource);if(destroyOnDeath)Destroy(gameObject);}}
private bool IsFriendlySource(GameObject source){if(source==null)return false;TeamMember victimTeam=GetComponent<TeamMember>();TeamMember sourceTeam=source.GetComponentInParent<TeamMember>();return victimTeam!=null&&sourceTeam!=null&&victimTeam.Team==sourceTeam.Team;}
public void Heal(float amount)=>Heal(amount,true);public void Heal(float amount,bool playAudio){if(!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState||IsDead||amount<=0)return;float applied=Mathf.Min(amount,maxHealth-Current);Current=Mathf.Min(maxHealth,Current+amount);if(applied>0){MatchTelemetry.Instance?.RecordHealing(this,applied);if(playAudio)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.Heal,transform.position,.18f);}Changed?.Invoke(Current,maxHealth);}
public void ApplyNetworkSnapshot(float current,float currentShield,bool dead)
{
    Current=Mathf.Clamp(current,0,maxHealth);
    shield=Mathf.Clamp(currentShield,0,maxHealth*.4f);
    shieldUntil=shield>0?Time.time+.35f:0;
    IsDead=dead||Current<=0;
    if(!IsDead&&Current<=0)Current=Mathf.Min(1,maxHealth);
    Changed?.Invoke(Current,maxHealth);
}
public void RestoreFull(){if(!Alterverse.Multiplayer.CoopAuthority.CanMutateAuthoritativeState)return;ClearTransient();IsDead=false;Current=maxHealth;LastDamageSource=null;Changed?.Invoke(Current,maxHealth);}
}
}

