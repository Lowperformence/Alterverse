using System;
using Alterverse.Profile;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Alterverse.Online
{
    /// <summary>Google Play Games all-time lifetime match wins.</summary>
    public static class GooglePlayLevelLeaderboard
    {
        public const string MostWinsLeaderboardId="CgkI57GIjPsdEAIQAA";
        public enum ConnectionState { NotConfigured, Idle, Authenticating, Ready, Submitting, Error, Unsupported }

        [Serializable] private sealed class SettingsSnapshot { public string mAppId; }
        private static ConnectionState state=ConnectionState.Idle;
        private static string status="Google Play Games is not connected.";
        private static bool authenticating, submitting, openingUi, openAfterAuthentication;
        private static int lastSubmittedScore=-1, generation;
        private static string lastSubmittedPlayer;
        private static bool? hasGameAppId;

        public static bool HasLeaderboardId=>!string.IsNullOrWhiteSpace(MostWinsLeaderboardId)&&
            !MostWinsLeaderboardId.StartsWith("REPLACE_",StringComparison.Ordinal);
        public static bool IsConfigured=>HasLeaderboardId&&HasGameAppId;
        public static ConnectionState State=>IsConfigured?state:ConnectionState.NotConfigured;
        public static string Status=>!HasLeaderboardId?"Add the MOST WINS leaderboard ID from Google Play Console.":
            !HasGameAppId?"Play Games setup incomplete: game App ID is missing. Run Android Setup with the Play Console resources.":status;
        public static bool IsBusy=>authenticating||submitting||openingUi;
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
        public static string PlayerName
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                string name=IsAuthenticated?PlayGamesPlatform.Instance.localUser.userName:null;
                return string.IsNullOrWhiteSpace(name)?"Not signed in":name;
#else
                return "Not signed in";
#endif
            }
        }
        public static int CurrentScore=>Mathf.Max(0,MatchRewardService.MatchesWon);
        public static int LastSubmittedScore=>lastSubmittedScore;

        private static bool HasGameAppId
        {
            get
            {
                if(hasGameAppId.HasValue)return hasGameAppId.Value;
                // Read the same bundled settings as the plugin. The leaderboard ID alone
                // does not configure the Android game and cannot substitute for its App ID.
                var asset=Resources.Load("PlayGamesSettings");
                if(asset==null){hasGameAppId=false;return false;}
                try
                {
                    var settings=JsonUtility.FromJson<SettingsSnapshot>(JsonUtility.ToJson(asset));
                    hasGameAppId=ulong.TryParse(settings?.mAppId,out ulong number)&&number>0;
                }
                catch(Exception){hasGameAppId=false;}
                return hasGameAppId.Value;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            generation++;
            hasGameAppId=null;
            authenticating=submitting=openingUi=openAfterAuthentication=false;
            lastSubmittedScore=-1;lastSubmittedPlayer=null;
            state=ConnectionState.Idle;status="Google Play Games is not connected.";
        }

        public static void AuthenticateAndSync(bool interactive=false)
        {
            if(!IsConfigured){state=ConnectionState.NotConfigured;return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            if(authenticating)return;
            PlayGamesPlatform.Activate();
            if(IsAuthenticated){SubmitCurrentScore();return;}
            authenticating=true;state=ConnectionState.Authenticating;
            status="Signing in to Google Play Games...";
            int requestGeneration=generation;
            Action<SignInStatus> completed=result=>
            {
                if(requestGeneration!=generation)return;
                authenticating=false;
                if(result!=SignInStatus.Success)
                {
                    openAfterAuthentication=false;
                    Fail("Play Games sign-in failed ("+result+"). Tap CONNECT to retry; check the installed build's Play Games setup.");
                    return;
                }
                state=ConnectionState.Ready;status="Google Play Games connected.";
                SubmitCurrentScore();
                if(openAfterAuthentication){openAfterAuthentication=false;ShowNativeLeaderboardUi();}
            };
            // Authenticate observes automatic sign-in. A user-requested retry must use
            // ManuallyAuthenticate; repeating automatic authentication will not prompt.
            try
            {
                if(interactive)PlayGamesPlatform.Instance.ManuallyAuthenticate(completed);
                else PlayGamesPlatform.Instance.Authenticate(completed);
            }
            catch(Exception e){authenticating=false;openAfterAuthentication=false;Fail("Play Games sign-in could not start: "+e.Message);}
#else
            Unsupported();
#endif
        }

        public static void SubmitCurrentScore()
        {
            if(!IsConfigured)return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!IsAuthenticated){AuthenticateAndSync();return;}
            if(submitting)return;
            string player=PlayGamesPlatform.Instance.localUser.id;
            if(lastSubmittedPlayer!=player){lastSubmittedPlayer=player;lastSubmittedScore=-1;}
            int wins=CurrentScore;
            if(lastSubmittedScore==wins){state=ConnectionState.Ready;status="Wins "+wins+" synced to the global ranking.";return;}
            submitting=true;state=ConnectionState.Submitting;status="Syncing wins "+wins+"...";
            int requestGeneration=generation;
            try
            {
                PlayGamesPlatform.Instance.ReportScore(wins,MostWinsLeaderboardId,success=>
                {
                    if(requestGeneration!=generation)return;
                    submitting=false;
                    if(!success){Fail("Win sync failed. Check the connection and published Play Games leaderboard.");return;}
                    if(!IsAuthenticated||PlayGamesPlatform.Instance.localUser.id!=player){lastSubmittedScore=-1;return;}
                    lastSubmittedScore=wins;state=ConnectionState.Ready;status="Wins "+wins+" synced to the global ranking.";
                    // A match can finish while the previous score is still being sent.
                    if(CurrentScore!=wins)SubmitCurrentScore();
                });
            }
            catch(Exception e){submitting=false;Fail("Win sync could not start: "+e.Message);}
#else
            Unsupported();
#endif
        }

        public static void ShowLeaderboard()
        {
            if(!IsConfigured){state=ConnectionState.NotConfigured;return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            if(openingUi)return;
            if(!IsAuthenticated)
            {
                openAfterAuthentication=true;
                AuthenticateAndSync(true);
                return;
            }
            SubmitCurrentScore();ShowNativeLeaderboardUi();
#else
            Unsupported();
#endif
        }

        private static void ShowNativeLeaderboardUi()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(openingUi)return;
            openingUi=true;
            int requestGeneration=generation;
            try
            {
                PlayGamesPlatform.Instance.ShowLeaderboardUI(MostWinsLeaderboardId,LeaderboardTimeSpan.AllTime,result=>
                {
                    if(requestGeneration!=generation)return;
                    openingUi=false;
                    if(result!=UIStatus.Valid&&result!=UIStatus.UserClosedUI)Fail("Play Games ranking could not open ("+result+"). Check published leaderboard, tester access and signing certificate.");
                });
            }
            catch(Exception e){openingUi=false;Fail("Play Games ranking could not open: "+e.Message);}
#endif
        }

        private static void Unsupported(){state=ConnectionState.Unsupported;status="Google Play leaderboard is available in the Android build.";}
        private static void Fail(string message){state=ConnectionState.Error;status=message;Debug.LogWarning("DIMENSIONS FRONT: "+message);}
    }
}
