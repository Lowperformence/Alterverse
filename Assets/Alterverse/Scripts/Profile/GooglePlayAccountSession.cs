using System;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Alterverse.Profile
{
    // One native sign-in at a time for account recovery and the leaderboard.
    public static class GooglePlayAccountSession
    {
        [Serializable] sealed class Settings { public string mAppId,mWebClientId; }
        static Task<bool> signingIn;
        public static bool Supported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        // Native sign-in / leaderboard browsing do not request a server auth code.
        // Only linking the cloud account requires the OAuth Web Client ID.
        public static string NativeConfigurationError=>ReadConfigurationError(false);
        public static string ConfigurationError=>ReadConfigurationError(true);
        static string ReadConfigurationError(bool requireWebClient)
        {
                var asset=Resources.Load("PlayGamesSettings");
                if(asset==null)return "Google Play Games setup is missing.";
                try
                {
                    var settings=JsonUtility.FromJson<Settings>(JsonUtility.ToJson(asset));
                    if(!ulong.TryParse(settings?.mAppId,out ulong id)||id==0)return "Google Play Games App ID is missing.";
                    if(requireWebClient&&string.IsNullOrWhiteSpace(settings.mWebClientId))return "Google Play Games Web Client ID is missing.";
                    return "";
                }
                catch(Exception){return "Google Play Games setup could not be read.";}
        }
        public static bool IsAuthenticated
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return PlayGamesPlatform.Instance.IsAuthenticated();
#else
                return false;
#endif
            }
        }
        public static string PlayerId
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return IsAuthenticated?PlayGamesPlatform.Instance.localUser.id:null;
#else
                return null;
#endif
            }
        }
        public static string DisplayName
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try { return IsAuthenticated ? PlayGamesPlatform.Instance.GetUserDisplayName() : null; }
                catch (Exception) { return null; }
#else
                return null;
#endif
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()=>signingIn=null;

        public static Task<bool> AuthenticateAsync(bool interactive=false)
        {
            if(signingIn!=null&&!signingIn.IsCompleted)return signingIn;
            return signingIn=AuthenticateCoreAsync(interactive);
        }
        static async Task<bool> AuthenticateCoreAsync(bool interactive)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var done=new TaskCompletionSource<bool>();
            try
            {
                PlayGamesPlatform.Activate();
                if(IsAuthenticated)return true;
                if(interactive)PlayGamesPlatform.Instance.ManuallyAuthenticate(result=>done.TrySetResult(result==SignInStatus.Success));
                else PlayGamesPlatform.Instance.Authenticate(result=>done.TrySetResult(result==SignInStatus.Success));
                if(await Task.WhenAny(done.Task,Task.Delay(30000))!=done.Task)return false;
                return await done.Task;
            }
            catch(Exception){return false;}
#else
            await Task.CompletedTask;return false;
#endif
        }
        public static async Task<string> AuthorizationCodeAsync(bool interactive=false)
        {
            if(!Supported||!string.IsNullOrEmpty(ConfigurationError)||!await AuthenticateAsync(interactive))return null;
#if UNITY_ANDROID && !UNITY_EDITOR
            var done=new TaskCompletionSource<string>();
            try
            {
                // Codes are one-use credentials. Request a new code for every link/sign-in attempt.
                PlayGamesPlatform.Instance.RequestServerSideAccess(true,code=>done.TrySetResult(code));
                if(await Task.WhenAny(done.Task,Task.Delay(30000))!=done.Task)return null;
                return await done.Task;
            }
            catch(Exception){return null;}
#else
            return null;
#endif
        }
    }
}


