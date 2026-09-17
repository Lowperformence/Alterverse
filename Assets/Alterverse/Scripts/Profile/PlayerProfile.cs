using UnityEngine;
namespace Alterverse.Profile
{
public static class PlayerProfile
{
private const string NameKey="DF_PlayerName",AvatarKey="DF_Avatar",MasterKey="DF_MasterVolume",MusicKey="DF_MusicVolume",SfxKey="DF_SfxVolume";public static string PlayerName{get=>PlayerPrefs.GetString(NameKey,"Traveler");set{string clean=string.IsNullOrWhiteSpace(value)?"Traveler":value.Trim();PlayerPrefs.SetString(NameKey,clean.Length>16?clean.Substring(0,16):clean);PlayerPrefs.Save();}}public static int Avatar{get=>Mathf.Clamp(PlayerPrefs.GetInt(AvatarKey,0),0,5);set{PlayerPrefs.SetInt(AvatarKey,Mathf.Clamp(value,0,5));PlayerPrefs.Save();}}public static float MasterVolume{get=>PlayerPrefs.GetFloat(MasterKey,1);set=>SaveFloat(MasterKey,value);}public static float MusicVolume{get=>PlayerPrefs.GetFloat(MusicKey,.8f);set=>SaveFloat(MusicKey,value);}public static float SfxVolume{get=>PlayerPrefs.GetFloat(SfxKey,1);set=>SaveFloat(SfxKey,value);}public const string Language="English";private static void SaveFloat(string key,float value){PlayerPrefs.SetFloat(key,Mathf.Clamp01(value));PlayerPrefs.Save();}
}
}
