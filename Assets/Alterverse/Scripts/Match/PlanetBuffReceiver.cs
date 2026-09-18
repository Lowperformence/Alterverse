using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Match
{
public enum PlanetBuffType{Flux,Guard}
[RequireComponent(typeof(Health))]public sealed class PlanetBuffReceiver:MonoBehaviour
{
private Health health;private HeroCombatProfile profile;private float fluxUntil,guardUntil;public bool FluxActive=>Time.time<fluxUntil;public bool GuardActive=>Time.time<guardUntil;public float FluxRemaining=>Mathf.Max(0,fluxUntil-Time.time);public float GuardRemaining=>Mathf.Max(0,guardUntil-Time.time);public float ActiveRemaining=>Mathf.Max(FluxRemaining,GuardRemaining);public float CooldownMultiplier=>FluxActive ? .82f : 1f;public float MoveSpeedMultiplier=>FluxActive ? 1.08f : 1f;public const float FluxMinionDamageBonus=.30f,GuardMinionDamageBonus=.15f;public float MinionDamageMultiplier=>1f+(FluxActive?FluxMinionDamageBonus:0f)+(GuardActive?GuardMinionDamageBonus:0f);public float DamageTakenMultiplier=>GuardActive ? .82f : 1f;public string ActiveLabel=>FluxActive&&GuardActive?"FLUX + GUARD":FluxActive?"FLUX • +30% MINION DMG":GuardActive?"GUARD • +15% MINION DMG":"";
private void Awake(){health=GetComponent<Health>();profile=GetComponent<HeroCombatProfile>();}public void Apply(PlanetBuffType type,float duration){float until=Time.time+Mathf.Max(1,duration);if(type==PlanetBuffType.Flux)fluxUntil=Mathf.Max(fluxUntil,until);else{guardUntil=Mathf.Max(guardUntil,until);if(health!=null&&!health.IsDead)health.Heal((profile!=null?profile.Health:health.Max)*.12f);}}
}
}
