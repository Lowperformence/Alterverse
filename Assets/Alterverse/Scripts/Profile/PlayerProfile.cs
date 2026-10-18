using UnityEngine;
using Alterverse.Shop;

namespace Alterverse.Profile
{
    public static class PlayerProfile
    {
        const string NameKey="DF_PlayerName", AvatarKey="DF_Avatar", MasterKey="DF_MasterVolume",
            MusicKey="DF_MusicVolume", SfxKey="DF_SfxVolume";
        public const int MinNameLength=3, MaxNameLength=16;
        public const string PlayGamesNameKey="DF_PlayGamesDisplayName";
        public static string PlayerName
        {
            get
            {
                string value=AccountProfilePrefs.GetString(NameKey, "");
                if (SafeIdentityCatalog.IsPlayerName(value))
                {
                    // Do not briefly show a server-generated identity while the
                    // Google account and correct cloud profile are still being verified.
                    if (GooglePlayAccountSession.Supported && !AccountServerBootstrap.CloudProfileReady)
                        return "CONNECTING...";
                    string alias=AccountProfilePrefs.Attached?AccountProfilePrefs.GetString(PlayGamesNameKey, ""):null;
                    return SafeIdentityCatalog.IsDisplayName(alias)?alias:value;
                }
                // When signed in, wait for the server-assigned name. Never invent
                // and save a second identity locally for a connected account.
                if (AccountProfilePrefs.Attached) return "CONNECTING...";
                // Never show or persist a fabricated local player identity on Android
                // while the actual cloud account is being recovered.
                if (GooglePlayAccountSession.Supported)
                    return AccountServerBootstrap.Instance==null || AccountServerBootstrap.Instance.Busy
                        ? "CONNECTING..." : "SIGN IN REQUIRED";
                value=SafeIdentityCatalog.GeneratePlayerName();
                AccountProfilePrefs.SetString(NameKey,value); AccountProfilePrefs.Save();
                return value;
            }
        }
        // Only the verified Google-link path calls this, after the matching cloud
        // profile has loaded. A late callback must never edit another account.
        internal static bool TryUseGooglePlayName(string verifiedAccount,string displayName)
        {
            if(!AccountProfilePrefs.Attached||string.IsNullOrEmpty(verifiedAccount)||AccountProfilePrefs.Account!=verifiedAccount)return false;
            // Use the verified Google Play Games display name for this account.
            // If it changes in Google Play, update the game's public display alias too.
            // The server-assigned internal identity remains unchanged.
            string value=SafeIdentityCatalog.NormalizePlayGamesName(displayName);
            if(value==null||!SafeIdentityCatalog.IsDisplayName(value))return false;
            if(AccountProfilePrefs.GetString(PlayGamesNameKey,"")==value)return false;
            AccountProfilePrefs.SetString(PlayGamesNameKey,value);AccountProfilePrefs.Save();
            return true;
        }
        // Retained as a compatibility entry point for older callers.
        // The canonical account name remains fixed; Google supplies a display alias.
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

