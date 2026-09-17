using UnityEngine;using Alterverse.Shop;
namespace Alterverse.Profile
{
public static class PlayerProfile
{
private const string NameKey="DF_PlayerName",AvatarKey="DF_Avatar",MasterKey="DF_MasterVolume",MusicKey="DF_MusicVolume",SfxKey="DF_SfxVolume";public static string PlayerName{get=>PlayerPrefs.GetString(NameKey,"Traveler");set{string clean=string.IsNullOrWhiteSpace(value)?"Traveler":value.Trim();PlayerPrefs.SetString(NameKey,clean.Length>16?clean.Substring(0,16):clean);PlayerPrefs.Save();}}public static int Avatar{get{int id=Mathf.Max(0,PlayerPrefs.GetInt(AvatarKey,0));return AvatarInventory.IsOwned(id)?id:0;}set{if(!AvatarInventory.IsOwned(value))return;PlayerPrefs.SetInt(AvatarKey,Mathf.Max(0,value));PlayerPrefs.Save();}}public static string DisplayTitle=>FounderEntitlement.Owned?FounderPackCatalog.FounderTitle:"TRAVELER";public static bool HasFounderFrame=>FounderEntitlement.Owned;public static float MasterVolume{get=>PlayerPrefs.GetFloat(MasterKey,1);set=>SaveFloat(MasterKey,value);}public static float MusicVolume{get=>PlayerPrefs.GetFloat(MusicKey,.8f);set=>SaveFloat(MusicKey,value);}public static float SfxVolume{get=>PlayerPrefs.GetFloat(SfxKey,1);set=>SaveFloat(SfxKey,value);}public const string Language="English";private static void SaveFloat(string key,float value){PlayerPrefs.SetFloat(key,Mathf.Clamp01(value));PlayerPrefs.Save();}
}
}
