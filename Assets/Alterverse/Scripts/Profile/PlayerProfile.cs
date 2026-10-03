using UnityEngine;using Alterverse.Shop;using System.Text;
namespace Alterverse.Profile
{
public static class PlayerProfile
{
private const string NameKey="DF_PlayerName",AvatarKey="DF_Avatar",MasterKey="DF_MasterVolume",MusicKey="DF_MusicVolume",SfxKey="DF_SfxVolume",VoiceKey="DF_VoiceChatVolume";public const int MinNameLength=3,MaxNameLength=16;public static string PlayerName{get=>SanitizeName(Alterverse.Profile.AccountProfilePrefs.GetString(NameKey,"Traveler"));set{Alterverse.Profile.AccountProfilePrefs.SetString(NameKey,SanitizeName(value));Alterverse.Profile.AccountProfilePrefs.Save();}}public static string SanitizeName(string value){if(string.IsNullOrWhiteSpace(value))return"Traveler";StringBuilder b=new StringBuilder(MaxNameLength);bool lastSpace=false;foreach(char ch in value.Trim()){if(b.Length>=MaxNameLength)break;bool valid=char.IsLetterOrDigit(ch)||ch=='_'||ch=='-'||ch==' ';if(!valid)continue;if(ch==' '){if(lastSpace)continue;lastSpace=true;}else lastSpace=false;b.Append(ch);}string clean=b.ToString().Trim();return clean.Length<MinNameLength?"Traveler":clean;}public static bool IsValidName(string value){if(string.IsNullOrWhiteSpace(value))return false;string trimmed=value.Trim();if(trimmed.Length<MinNameLength||trimmed.Length>MaxNameLength)return false;string clean=SanitizeName(trimmed);return clean.Length>=MinNameLength&&string.Equals(clean,trimmed,System.StringComparison.Ordinal);}public static int Avatar{get{int id=Mathf.Max(0,Alterverse.Profile.AccountProfilePrefs.GetInt(AvatarKey,0));return AvatarInventory.IsOwned(id)?id:0;}set{if(!AvatarInventory.IsOwned(value))return;Alterverse.Profile.AccountProfilePrefs.SetInt(AvatarKey,Mathf.Max(0,value));Alterverse.Profile.AccountProfilePrefs.Save();}}public static string DisplayTitle=>FounderEntitlement.Owned?FounderPackCatalog.FounderTitle:"TRAVELER";public static bool HasFounderFrame=>FounderEntitlement.Owned;public static float MasterVolume{get=>ReadVolume(MasterKey,1);set=>SaveFloat(MasterKey,value);}public static float MusicVolume{get=>ReadVolume(MusicKey,.8f);set=>SaveFloat(MusicKey,value);}public static float SfxVolume{get=>ReadVolume(SfxKey,1);set=>SaveFloat(SfxKey,value);}public static float VoiceChatVolume{get=>ReadVolume(VoiceKey,1);set=>SaveFloat(VoiceKey,value);}public static void SetAudioVolumes(float master,float music,float sfx,bool persist)
{
Alterverse.Profile.AccountProfilePrefs.SetFloat(MasterKey,Mathf.Clamp01(master));
Alterverse.Profile.AccountProfilePrefs.SetFloat(MusicKey,Mathf.Clamp01(music));
Alterverse.Profile.AccountProfilePrefs.SetFloat(SfxKey,Mathf.Clamp01(sfx));
if(persist)Alterverse.Profile.AccountProfilePrefs.Save();
}
public static void SetVoiceChatVolume(float voice,bool persist)
{
Alterverse.Profile.AccountProfilePrefs.SetFloat(VoiceKey,Mathf.Clamp01(voice));
if(persist)Alterverse.Profile.AccountProfilePrefs.Save();
}
private static float ReadVolume(string key,float fallback){float v=Alterverse.Profile.AccountProfilePrefs.GetFloat(key,fallback);return float.IsNaN(v)||float.IsInfinity(v)?fallback:Mathf.Clamp01(v);}
public const string Language="English";private static void SaveFloat(string key,float value){Alterverse.Profile.AccountProfilePrefs.SetFloat(key,Mathf.Clamp01(value));Alterverse.Profile.AccountProfilePrefs.Save();}
}
}


