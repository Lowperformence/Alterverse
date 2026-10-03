using UnityEngine;
namespace Alterverse.Profile
{
public static class PlayerPreferences
{
private const string ScreenShakeKey="DF_ScreenShake",DamageNumbersKey="DF_DamageNumbers",HapticsKey="DF_Haptics",TargetAssistKey="DF_TargetAssist";public static bool ScreenShake{get=>Get(ScreenShakeKey,true);set=>Set(ScreenShakeKey,value);}public static bool DamageNumbers{get=>Get(DamageNumbersKey,true);set=>Set(DamageNumbersKey,value);}public static bool Haptics{get=>Get(HapticsKey,true);set=>Set(HapticsKey,value);}public static bool TargetAssist{get=>Get(TargetAssistKey,true);set=>Set(TargetAssistKey,value);}private static bool Get(string key,bool fallback)=>Alterverse.Profile.AccountProfilePrefs.GetInt(key,fallback?1:0)==1;private static void Set(string key,bool value){Alterverse.Profile.AccountProfilePrefs.SetInt(key,value?1:0);Alterverse.Profile.AccountProfilePrefs.Save();}
}
}

