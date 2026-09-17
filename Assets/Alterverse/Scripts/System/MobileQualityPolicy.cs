using UnityEngine;
namespace Alterverse.Systems
{
public enum MobileQualityTier{Battery,Balanced,Performance}
public static class MobileQualityPolicy
{
private const string Key="DF_QualityTier";public static MobileQualityTier Tier{get=>(MobileQualityTier)Mathf.Clamp(PlayerPrefs.GetInt(Key,(int)MobileQualityTier.Balanced),0,2);set{PlayerPrefs.SetInt(Key,(int)value);PlayerPrefs.Save();Apply();}}public static int TargetFrameRate=>Tier==MobileQualityTier.Battery?30:60;[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]private static void ApplyOnLoad()=>Apply();public static void Apply(){Application.targetFrameRate=TargetFrameRate;QualitySettings.vSyncCount=0;QualitySettings.shadowDistance=Tier==MobileQualityTier.Battery?18f:Tier==MobileQualityTier.Balanced?28f:38f;QualitySettings.lodBias=Tier==MobileQualityTier.Battery?.7f:Tier==MobileQualityTier.Balanced?1f:1.25f;}
}
}
