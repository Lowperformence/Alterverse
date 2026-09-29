using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Match
{
public enum PlanetBuffType{Flux,Guard}
[RequireComponent(typeof(Health))]public sealed class PlanetBuffReceiver:MonoBehaviour
{
private float fluxUntil,guardUntil;public bool FluxActive=>Time.time<fluxUntil;public bool GuardActive=>Time.time<guardUntil;public float FluxRemaining=>Mathf.Max(0,fluxUntil-Time.time);public float GuardRemaining=>Mathf.Max(0,guardUntil-Time.time);public float ActiveRemaining=>Mathf.Max(FluxRemaining,GuardRemaining);public float CooldownMultiplier=>1f;public float MoveSpeedMultiplier=>1f+(GuardActive ? 0.10f : 0f);public const float FluxDamageBonus=.15f,GuardMinionDamageBonus=.15f;public float OutgoingDamageMultiplier=>FluxActive?1f+FluxDamageBonus:1f;public float MinionDamageMultiplier=>1f+(GuardActive?GuardMinionDamageBonus:0f);public float DamageTakenMultiplier=>1f;public string ActiveLabel=>FluxActive&&GuardActive?"FLUX + GUARD":FluxActive?"FLUX • +15% DAMAGE":GuardActive?"GUARD • +15% MINION DMG • +10% MOVE":"";
public void Apply(PlanetBuffType type,float duration){float until=Time.time+Mathf.Max(1,duration);if(type==PlanetBuffType.Flux)fluxUntil=Mathf.Max(fluxUntil,until);else guardUntil=Mathf.Max(guardUntil,until);}
}
}
