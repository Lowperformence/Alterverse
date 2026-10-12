using UnityEngine;
using Alterverse.Shop;

namespace Alterverse.Profile
{
    public static class PlayerProfile
    {
        const string NameKey="DF_PlayerName", AvatarKey="DF_Avatar", MasterKey="DF_MasterVolume",
            MusicKey="DF_MusicVolume", SfxKey="DF_SfxVolume";
        public const int MinNameLength=3, MaxNameLength=16;
        public static string PlayerName
        {
            get
            {
                string value=AccountProfilePrefs.GetString(NameKey, "");
                if (SafeIdentityCatalog.IsPlayerName(value)) return value;
                // When signed in, wait for the server-assigned name. Never invent
                // and save a second identity locally for a connected account.
                if (AccountProfilePrefs.Attached) return "CONNECTING...";
                value=SafeIdentityCatalog.GeneratePlayerName();
                AccountProfilePrefs.SetString(NameKey,value); AccountProfilePrefs.Save();
                return value;
            }
        }
        // Retained as a compatibility entry point for older callers.
        // Registered players must keep their original assigned name forever.
        public static void RerollName() { }
        public static string SanitizeName(string value) => SafeIdentityCatalog.DisplayPlayerName(value);
        public static bool IsValidName(string value) => SafeIdentityCatalog.IsPlayerName(value);
        public static int Avatar
        {
            get { int id=Mathf.Max(0,AccountProfilePrefs.GetInt(AvatarKey,0)); return AvatarInventory.IsOwned(id)?id:0; }
            set { if (!AvatarInventory.IsOwned(value)) return; AccountProfilePrefs.SetInt(AvatarKey,Mathf.Max(0,value)); AccountProfilePrefs.Save(); }
        }
        public static string DisplayTitle=>FounderEntitlement.Owned?FounderPackCatalog.FounderTitle:"TRAVELER";
        public static bool HasFounderFrame=>FounderEntitlement.Owned;
        public static float MasterVolume { get=>ReadVolume(MasterKey,1); set=>SaveFloat(MasterKey,value); }
        public static float MusicVolume { get=>ReadVolume(MusicKey,.8f); set=>SaveFloat(MusicKey,value); }
        public static float SfxVolume { get=>ReadVolume(SfxKey,1); set=>SaveFloat(SfxKey,value); }
        public static void SetAudioVolumes(float master,float music,float sfx,bool persist)
        {
            AccountProfilePrefs.SetFloat(MasterKey,Mathf.Clamp01(master));
            AccountProfilePrefs.SetFloat(MusicKey,Mathf.Clamp01(music));
            AccountProfilePrefs.SetFloat(SfxKey,Mathf.Clamp01(sfx));
            if(persist)AccountProfilePrefs.Save();
        }
        static float ReadVolume(string key,float fallback)
        {
            float value=AccountProfilePrefs.GetFloat(key,fallback);
            return float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp01(value);
        }
        static void SaveFloat(string key,float value)
        { AccountProfilePrefs.SetFloat(key,Mathf.Clamp01(value)); AccountProfilePrefs.Save(); }
        public const string Language="English";
    }
}
