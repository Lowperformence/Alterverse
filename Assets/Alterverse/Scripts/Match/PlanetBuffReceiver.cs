using Alterverse.Characters;using Alterverse.Combat;using UnityEngine;
namespace Alterverse.Match
{
public enum PlanetBuffType{Flux,Guard}
[RequireComponent(typeof(Health))]public sealed class PlanetBuffReceiver:MonoBehaviour
{
private float fluxUntil,guardUntil;public bool FluxActive=>Time.time<fluxUntil;public bool GuardActive=>Time.time<guardUntil;public float FluxRemaining=>Mathf.Max(0,fluxUntil-Time.time);public float GuardRemaining=>Mathf.Max(0,guardUntil-Time.time);public float ActiveRemaining=>Mathf.Max(FluxRemaining,GuardRemaining);public float CooldownMultiplier=>FluxActive ? .82f : 1f;public float MoveSpeedMultiplier=>1f+(FluxActive?.08f:0f)+(GuardActive?.10f:0f);public const float FluxMinionDamageBonus=.30f,GuardMinionDamageBonus=.15f;public float MinionDamageMultiplier=>1f+(FluxActive?FluxMinionDamageBonus:0f)+(GuardActive?GuardMinionDamageBonus:0f);public float DamageTakenMultiplier=>1f;public string ActiveLabel=>FluxActive&&GuardActive?"FLUX + GUARD":FluxActive?"FLUX • +30% MINION DMG • +8% MOVE":GuardActive?"GUARD • +15% MINION DMG • +10% MOVE":"";
public void Apply(PlanetBuffType type,float duration){float until=Time.time+Mathf.Max(1,duration);if(type==PlanetBuffType.Flux)fluxUntil=Mathf.Max(fluxUntil,until);else guardUntil=Mathf.Max(guardUntil,until);}
}
}
