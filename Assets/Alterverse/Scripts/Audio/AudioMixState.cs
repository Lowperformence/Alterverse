using UnityEngine;
namespace Alterverse.Audio
{
public static class AudioMixState
{
private static float combatUntil;public static bool CombatActive=>Time.unscaledTime<combatUntil;public static void MarkCombat(float seconds=1.4f){combatUntil=Mathf.Max(combatUntil,Time.unscaledTime+seconds);}public static float WorldGain=>CombatActive?.72f:1f;public static float CriticalGain=>CombatActive?1f:.92f;public static void Reset(){combatUntil=0f;}
}
}
