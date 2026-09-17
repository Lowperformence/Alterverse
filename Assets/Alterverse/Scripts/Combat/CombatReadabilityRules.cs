using UnityEngine;
namespace Alterverse.Combat
{
public static class CombatReadabilityRules
{
public static bool IsHeavy(float damage,float maxHealth)=>damage>=Mathf.Max(45f,maxHealth*.16f);public static float ImpactScale(float damage)=>Mathf.Clamp(.25f+damage*.008f,.3f,.65f);public static float HitStopDuration(float damage,float maxHealth)=>IsHeavy(damage,maxHealth)?.045f:.018f;public static float HitStopScale(float damage,float maxHealth)=>IsHeavy(damage,maxHealth)?.12f:.42f;public const string Rule="Readable impact first; spectacle must never hide targeting or health state.";
}
}
