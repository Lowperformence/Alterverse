using UnityEngine;
namespace Alterverse.Performance
{
public enum MobileQuality{Battery,Balanced,Performance}
public static class MobilePerformanceProfile
{
private const string Key="DF_MobileQuality";public static MobileQuality Current{get=>(MobileQuality)Mathf.Clamp(PlayerPrefs.GetInt(Key,(int)MobileQuality.Balanced),0,2);set{PlayerPrefs.SetInt(Key,(int)value);PlayerPrefs.Save();Apply(value);}}[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void AutoApply()=>Apply(Current);public static void Apply(MobileQuality quality){Application.targetFrameRate=quality==MobileQuality.Battery?30:60;QualitySettings.vSyncCount=0;QualitySettings.lodBias=quality==MobileQuality.Battery?.65f:quality==MobileQuality.Balanced?.85f:1f;QualitySettings.shadowDistance=quality==MobileQuality.Battery?18f:quality==MobileQuality.Balanced?28f:38f;QualitySettings.antiAliasing=quality==MobileQuality.Performance?2:0;}
}
}
