using System;
using System.Reflection;
using Alterverse.Profile;
using UnityEngine;
using UnityEngine.SocialPlatforms;

// Temporary compatibility bridge until the Google Play Games plugin is connected.
#pragma warning disable CS0618 // Unity Social API is deprecated, but remains the compile-time fallback here.

namespace Alterverse.Online
{
    /// <summary>
    /// Google Play Games global leaderboard bridge for the player's account level.
    /// The concrete leaderboard ID is supplied by the Google Play Console.
    /// Uses Unity's Social API so the project still compiles before the Google Play
    /// Games Unity plugin is imported/configured.
    /// </summary>
    public static class GooglePlayLevelLeaderboard
    {
        // Replace this after the GLOBAL LEVEL leaderboard is created in Play Console.
        public const string GlobalLevelLeaderboardId="REPLACE_WITH_GOOGLE_PLAY_LEVEL_LEADERBOARD_ID";

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
            ActivateGooglePlayProviderIfPresent();
            if(Social.localUser==null)
            {
                Fail("Google Play Games provider is unavailable.");
                return;
            }
            if(Social.localUser.authenticated)
            {
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
            ActivateGooglePlayProviderIfPresent();
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
                    SubmitCurrentLevel();
                    Social.ShowLeaderboardUI();
                });
                return;
            }

            SubmitCurrentLevel();
            Social.ShowLeaderboardUI();
#else
            state=ConnectionState.Unsupported;
            status="Google Play leaderboard is available in the Android build.";
#endif
        }

        private static void ActivateGooglePlayProviderIfPresent()
        {
            if(activationAttempted)return;
            activationAttempted=true;

            // Reflection keeps this source independent from the optional Google Play Games
            // Unity plugin while still activating it automatically once installed.
            try
            {
                Assembly[] assemblies=AppDomain.CurrentDomain.GetAssemblies();
                for(int i=0;i<assemblies.Length;i++)
                {
                    Type type=assemblies[i].GetType("GooglePlayGames.PlayGamesPlatform",false);
                    if(type==null)continue;
                    MethodInfo activate=type.GetMethod("Activate",BindingFlags.Public|BindingFlags.Static);
                    if(activate!=null)activate.Invoke(null,null);
                    return;
                }
            }
            catch(Exception exception)
            {
                Debug.LogWarning("DIMENSIONS FRONT: Google Play Games activation failed: "+exception.Message);
            }
        }

        private static void Fail(string message)
        {
            state=ConnectionState.Error;
            status=message;
        }
    }
}
#pragma warning restore CS0618
