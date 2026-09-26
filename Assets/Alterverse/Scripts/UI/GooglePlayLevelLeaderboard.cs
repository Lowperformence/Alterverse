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
    /// Google Play Games global leaderboard bridge for lifetime match wins.
    /// </summary>
    public static class GooglePlayLevelLeaderboard
    {
        // MOST WINS leaderboard configured in Google Play Console.
        public const string MostWinsLeaderboardId="CgkI57GIjPsdEAIQAA";

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
        private static int lastSubmittedScore=-1;
        private static ConnectionState state=ConnectionState.Idle;
        private static string status="Google Play Games is not connected.";

        public static ConnectionState State=>IsConfigured?state:ConnectionState.NotConfigured;
        public static string Status=>IsConfigured?status:"Create the MOST WINS leaderboard in Google Play Console, then add its leaderboard ID.";
        public static bool IsConfigured=>!string.IsNullOrWhiteSpace(MostWinsLeaderboardId)&&
            !MostWinsLeaderboardId.StartsWith("REPLACE_",StringComparison.Ordinal);
        public static bool IsAuthenticated=>Social.localUser!=null&&Social.localUser.authenticated;
        public static string PlayerName=>IsAuthenticated&&!string.IsNullOrWhiteSpace(Social.localUser.userName)
            ?Social.localUser.userName:"Not signed in";
        public static int CurrentScore=>Mathf.Max(0,MatchRewardService.MatchesWon);
        public static int LastSubmittedScore=>lastSubmittedScore;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            activationAttempted=false;
            lastSubmittedScore=-1;
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
                state=ConnectionState.Ready;
                status="Google Play Games connected.";
                SubmitCurrentScore();
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
                SubmitCurrentScore();
            });
#else
            state=ConnectionState.Unsupported;
            status="Google Play leaderboard is available in the Android build.";
#endif
        }

        public static void SubmitCurrentScore()
        {
            if(!IsConfigured)return;
#if UNITY_ANDROID && !UNITY_EDITOR
            ActivateGooglePlayProviderIfPresent();
            if(!IsAuthenticated)
            {
                AuthenticateAndSync();
                return;
            }

            int wins=CurrentScore;
            state=ConnectionState.Submitting;
            status="Syncing wins "+wins+"...";
            Social.ReportScore(wins,MostWinsLeaderboardId,success=>
            {
                if(!success)
                {
                    Fail("Win sync failed. Check your connection and Play Games setup.");
                    return;
                }

                lastSubmittedScore=wins;
                state=ConnectionState.Ready;
                status="Wins "+wins+" synced to the global ranking.";
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
                    SubmitCurrentScore();
                    ShowNativeLeaderboardUi();
                });
                return;
            }

            SubmitCurrentScore();
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
                        MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                        for (int i = 0; i < methods.Length; i++)
                        {
                            MethodInfo showSpecific = methods[i];
                            if (showSpecific.Name != "ShowLeaderboardUI")
                                continue;

                            ParameterInfo[] parameters = showSpecific.GetParameters();
                            if (parameters.Length == 0 || parameters[0].ParameterType != typeof(string))
                                continue;

                            object[] arguments = new object[parameters.Length];
                            arguments[0] = MostWinsLeaderboardId;
                            bool supported = true;
                            for (int parameter = 1; parameter < parameters.Length; parameter++)
                            {
                                ParameterInfo info = parameters[parameter];
                                if (info.IsOptional)
                                {
                                    arguments[parameter] = info.DefaultValue;
                                }
                                else if (info.ParameterType.IsEnum)
                                {
                                    try
                                    {
                                        arguments[parameter] = Enum.Parse(info.ParameterType, "AllTime", true);
                                    }
                                    catch
                                    {
                                        supported = false;
                                        break;
                                    }
                                }
                                else if (!typeof(Delegate).IsAssignableFrom(info.ParameterType) &&
                                         info.ParameterType.IsValueType &&
                                         Nullable.GetUnderlyingType(info.ParameterType) == null)
                                {
                                    supported = false;
                                    break;
                                }
                            }
                            if (!supported)
                                continue;

                            showSpecific.Invoke(instance, arguments);
                            return;
                        }
                    }
                }
            }
            catch(Exception exception)
            {
                Debug.LogWarning("DIMENSIONS FRONT: direct Google Play leaderboard UI failed: "+exception.Message);
            }

            // Older plugin versions expose only the generic Unity Social UI.
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
            const string typeName = "GooglePlayGames.PlayGamesPlatform";
            Type resolved = Type.GetType(typeName + ", GooglePlayGames", false)
                ?? Type.GetType(typeName + ", GooglePlayGames.Android", false)
                ?? Type.GetType(typeName + ", Google.Play.Games", false);
            if (resolved != null)
                return resolved;

            // The plugin assembly name differs between the legacy and current Unity packages.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolved = assembly.GetType(typeName, false);
                if (resolved != null)
                    return resolved;
            }
            return null;
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
