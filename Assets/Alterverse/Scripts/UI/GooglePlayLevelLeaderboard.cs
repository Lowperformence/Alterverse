using System;
using System.Reflection;
using Alterverse.Profile;
using UnityEngine;
using UnityEngine.SocialPlatforms;

// Google Play Games compatibility bridge. The project currently imports the
// Google Play Games plugin as Assets, so reflection keeps this source compiling
// even when that optional plugin is temporarily absent in another checkout.
#pragma warning disable CS0618 // Unity Social API is deprecated, but remains the fallback here.

namespace Alterverse.Online
{
    /// <summary>
    /// Google Play Games global leaderboard bridge for the player's account level.
    /// </summary>
    public static class GooglePlayLevelLeaderboard
    {
        // GLOBAL LEVEL leaderboard from Google Play Console / generated GPGSIds.cs.
        public const string GlobalLevelLeaderboardId="CgkI57GLjPsdEAIQAg";

        public enum ConnectionState
        {
            NotConfigured,
            Idle,
            Authenticating,
            Ready,
            Submitting,
            Error,
            Unsupported
        }

        private static bool activationAttempted;
        private static int lastSubmittedLevel=-1;
        private static ConnectionState state=ConnectionState.Idle;
        private static string status="Google Play Games is not connected.";

        public static ConnectionState State=>IsConfigured?state:ConnectionState.NotConfigured;
        public static string Status=>IsConfigured?status:"Create the GLOBAL LEVEL leaderboard in Google Play Console, then add its leaderboard ID.";
        public static bool IsConfigured=>!string.IsNullOrWhiteSpace(GlobalLevelLeaderboardId)&&
            !GlobalLevelLeaderboardId.StartsWith("REPLACE_",StringComparison.Ordinal);
        public static bool IsAuthenticated=>Social.localUser!=null&&Social.localUser.authenticated;
        public static string PlayerName=>IsAuthenticated&&!string.IsNullOrWhiteSpace(Social.localUser.userName)
            ?Social.localUser.userName:"Not signed in";
        public static int CurrentLevel=>Mathf.Max(1,PlayerExperience.Level);
        public static int LastSubmittedLevel=>lastSubmittedLevel;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            activationAttempted=false;
            lastSubmittedLevel=-1;
            state=ConnectionState.Idle;
            status="Google Play Games is not connected.";
        }

        public static void AuthenticateAndSync()
        {
            if(!IsConfigured)
            {
                state=ConnectionState.NotConfigured;
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if(ResolvePlayGamesPlatformType()==null)
            {
                state=ConnectionState.Unsupported;
                status="Google Play Games plugin is missing from this build.";
                return;
            }
            ActivateGooglePlayProviderIfPresent();
            if(Social.localUser==null)
            {
                Fail("Google Play Games provider is unavailable.");
                return;
            }

            if(Social.localUser.authenticated)
            {
                state=ConnectionState.Ready;
                status="Google Play Games connected.";
                SubmitCurrentLevel();
                return;
            }

            state=ConnectionState.Authenticating;
            status="Signing in to Google Play Games...";
            Social.localUser.Authenticate(success=>
            {
                if(!success)
                {
                    Fail("Google Play Games sign-in failed.");
                    return;
                }

                state=ConnectionState.Ready;
                status="Google Play Games connected.";
                SubmitCurrentLevel();
            });
#else
            state=ConnectionState.Unsupported;
            status="Google Play leaderboard is available in the Android build.";
#endif
        }

        public static void SubmitCurrentLevel()
        {
            if(!IsConfigured)return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ActivateGooglePlayProviderIfPresent();
            if(!IsAuthenticated)
            {
                AuthenticateAndSync();
                return;
            }

            int level=CurrentLevel;
            state=ConnectionState.Submitting;
            status="Syncing level "+level+"...";
            Social.ReportScore(level,GlobalLevelLeaderboardId,success=>
            {
                if(!success)
                {
                    Fail("Level sync failed. Check your connection and Play Games setup.");
                    return;
                }

                lastSubmittedLevel=level;
                state=ConnectionState.Ready;
                status="Level "+level+" synced to the global ranking.";
            });
#else
            state=ConnectionState.Unsupported;
            status="Google Play leaderboard is available in the Android build.";
#endif
        }

        public static void ShowLeaderboard()
        {
            if(!IsConfigured)
            {
                state=ConnectionState.NotConfigured;
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if(ResolvePlayGamesPlatformType()==null)
            {
                state=ConnectionState.Unsupported;
                status="Google Play Games plugin is missing from this build.";
                return;
            }
            ActivateGooglePlayProviderIfPresent();
            if(Social.localUser==null)
            {
                Fail("Google Play Games provider is unavailable.");
                return;
            }

            if(!IsAuthenticated)
            {
                state=ConnectionState.Authenticating;
                status="Signing in to Google Play Games...";
                Social.localUser.Authenticate(success=>
                {
                    if(!success)
                    {
                        Fail("Google Play Games sign-in failed.");
                        return;
                    }

                    state=ConnectionState.Ready;
                    status="Google Play Games connected.";
                    SubmitCurrentLevel();
                    ShowNativeLeaderboardUi();
                });
                return;
            }

            SubmitCurrentLevel();
            ShowNativeLeaderboardUi();
#else
            state=ConnectionState.Unsupported;
            status="Google Play leaderboard is available in the Android build.";
#endif
        }

        /// <summary>
        /// Opens this specific Google Play leaderboard through PlayGamesPlatform.Instance.
        /// This bypasses cases where Unity's generic Social.ShowLeaderboardUI() silently
        /// does nothing even though Google Play Games authentication succeeded.
        /// </summary>
        private static void ShowNativeLeaderboardUi()
        {
            try
            {
                Type type=ResolvePlayGamesPlatformType();
                if(type!=null)
                {
                    PropertyInfo instanceProperty=type.GetProperty("Instance",BindingFlags.Public|BindingFlags.Static);
                    object instance=instanceProperty?.GetValue(null,null);
                    if(instance!=null)
                    {
                        MethodInfo showSpecific=type.GetMethod(
                            "ShowLeaderboardUI",
                            BindingFlags.Public|BindingFlags.Instance,
                            null,
                            new[]{typeof(string)},
                            null);

                        if(showSpecific!=null)
                        {
                            showSpecific.Invoke(instance,new object[]{GlobalLevelLeaderboardId});
                            return;
                        }
                    }
                }
            }
            catch(Exception exception)
            {
                Debug.LogWarning("DIMENSIONS FRONT: direct Google Play leaderboard UI failed: "+exception.Message);
            }

            // Fallback for plugin versions where the one-argument extension is unavailable.
            Social.ShowLeaderboardUI();
        }

        private static void ActivateGooglePlayProviderIfPresent()
        {
            if(activationAttempted)return;
            activationAttempted=true;

            try
            {
                Type type=ResolvePlayGamesPlatformType();
                if(type==null)
                {
                    Debug.LogWarning("DIMENSIONS FRONT: Google Play Games PlayGamesPlatform type was not found.");
                    return;
                }

                MethodInfo activate=type.GetMethod("Activate",BindingFlags.Public|BindingFlags.Static);
                if(activate==null)
                {
                    Debug.LogWarning("DIMENSIONS FRONT: Google Play Games Activate() method was not found.");
                    return;
                }

                activate.Invoke(null,null);
            }
            catch(Exception exception)
            {
                Debug.LogWarning("DIMENSIONS FRONT: Google Play Games activation failed: "+exception.Message);
            }
        }

        private static Type ResolvePlayGamesPlatformType()
        {
            return Type.GetType("GooglePlayGames.PlayGamesPlatform, GooglePlayGames",false)
                ?? Type.GetType("GooglePlayGames.PlayGamesPlatform, GooglePlayGames.Android",false);
        }

        private static void Fail(string message)
        {
            state=ConnectionState.Error;
            status=message;
            Debug.LogWarning("DIMENSIONS FRONT: "+message);
        }
    }
}
#pragma warning restore CS0618
