using System;
using System.Threading.Tasks;
using Alterverse.Profile;
using Alterverse.Shop;
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
        public const string MostWinsLeaderboardId="CgkI57GljPsdEAIQAA";
        public enum ConnectionState { NotConfigured, Idle, Authenticating, Ready, Submitting, Error, Unsupported }

        [Serializable] private sealed class SettingsSnapshot { public string mAppId; }
        private static ConnectionState state=ConnectionState.Idle;
        private static string status="Google Play Games is not connected.";
        private static bool authenticating, submitting, openingUi, openAfterAuthentication,verifyRequested;
        private static int lastSubmittedScore=-1, generation;
        private static long lastVerifiedScore=-1;
        private static string lastGoogleFormattedScore;
        private static string lastSubmittedPlayer;
        private static bool? hasGameAppId;

        public static bool HasLeaderboardId=>!string.IsNullOrWhiteSpace(MostWinsLeaderboardId)&&
            !MostWinsLeaderboardId.StartsWith("REPLACE_",StringComparison.Ordinal);
        public static bool IsConfigured=>HasLeaderboardId&&HasGameAppId&&
            string.IsNullOrEmpty(GooglePlayAccountSession.NativeConfigurationError);
        public static ConnectionState State=>IsConfigured?state:ConnectionState.NotConfigured;
        public static string Status=>!HasLeaderboardId?"Add the MOST WINS leaderboard ID from Google Play Console.":
            !HasGameAppId?"Play Games setup incomplete: game App ID is missing. Run Android Setup with the Play Console resources.":
            !string.IsNullOrEmpty(GooglePlayAccountSession.NativeConfigurationError)?
                GooglePlayAccountSession.NativeConfigurationError:status;
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
                string name=IsAuthenticated?Alterverse.Profile.PlayerProfile.PlayerName:null;
                return string.IsNullOrWhiteSpace(name)?"Not signed in":name;
#else
                return "Not signed in";
#endif
            }
        }
        public static int CurrentScore=>Mathf.Max(0,MatchRewardService.MatchesWon);
        public static int LastSubmittedScore=>lastSubmittedScore;
        public static long LastVerifiedScore=>lastVerifiedScore;
        public static string LastGoogleFormattedScore=>lastGoogleFormattedScore;

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
            authenticating=submitting=openingUi=openAfterAuthentication=verifyRequested=false;
            lastSubmittedScore=-1;lastSubmittedPlayer=null;
            lastVerifiedScore=-1;lastGoogleFormattedScore=null;
            state=ConnectionState.Idle;status="Google Play Games is not connected.";
        }

        public static async void AuthenticateAndSync(bool interactive=false)
        {
            if(!IsConfigured){state=ConnectionState.NotConfigured;return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            verifyRequested|=interactive;
            if(authenticating)return;
            if(IsAuthenticated){SubmitCurrentScore(interactive);return;}
            authenticating=true;state=ConnectionState.Authenticating;
            status="Signing in to Google Play Games...";
            int requestGeneration=generation;
            bool success=await GooglePlayAccountSession.AuthenticateAsync(interactive);
            if(requestGeneration!=generation)return;
            authenticating=false;
            if(!success)
            {
                openAfterAuthentication=false;
                Fail("Play Games sign-in failed. Tap CONNECT to retry; check the installed build's Play Games setup.");
                return;
            }
            state=ConnectionState.Ready;status="Google Play Games connected.";
            SubmitCurrentScore(interactive);
#else
            Unsupported();
#endif
        }

        public static void SubmitCurrentScore()=>SubmitCurrentScore(false);
        private static async void SubmitCurrentScore(bool force)
        {
            if(!IsConfigured)return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!IsAuthenticated){AuthenticateAndSync();return;}
            if(submitting)return;
            if(AccountServerBootstrap.Instance?.GooglePlayAccountReady!=true||PaidShardCommerce.Instance?.Ready!=true||
                !AccountProfilePrefs.Attached||AccountProfilePrefs.Account!=PaidShardCommerce.Instance.AccountHash)
            {
                state=ConnectionState.Idle;
                string setup=GooglePlayAccountSession.ConfigurationError;
                status=!string.IsNullOrEmpty(setup)?setup+" Cloud win sync is unavailable.":
                    "Waiting for the linked Google cloud profile before syncing wins. Connect Google in PROFILE.";
                // Browsing remains available even if the linked cloud save is unavailable.
                FinishPendingOpen();
                return;
            }
            string player=PlayGamesPlatform.Instance.localUser.id;
            string account=PaidShardCommerce.Instance.AccountHash;
            if(lastSubmittedPlayer!=player){lastSubmittedPlayer=player;lastSubmittedScore=-1;lastVerifiedScore=-1;lastGoogleFormattedScore=null;}
            submitting=true;state=ConnectionState.Submitting;status="Saving your account before syncing wins...";
            int requestGeneration=generation;
            try
            {
                // A win can arrive while a save or native callback is in flight.
                // Coalesce it, and never submit another account's cached profile.
                for(int pass=0;pass<3;pass++)
                {
                    if(AccountProfilePrefs.Dirty)
                    {
                        var save=PaidShardCommerce.Instance.FlushProfileAsync();
                        bool completed=await Task.WhenAny(save,Task.Delay(20000))==save;
                        if(requestGeneration!=generation)return;
                        if(!SameScoreAccount(player,account))return;
                        if(!completed||!await save){Fail("Account save pending. Retry SYNC WINS after synchronization.");return;}
                    }
                    if(requestGeneration!=generation||!SameScoreAccount(player,account))return;
                    int wins=CurrentScore;
                    if(force||lastSubmittedScore!=wins)
                    {
                        status="Sending "+wins+" wins to Google Play...";
                        var sent=new TaskCompletionSource<bool>();
                        PlayGamesPlatform.Instance.ReportScore(wins,MostWinsLeaderboardId,result=>sent.TrySetResult(result));
                        bool completed=await Task.WhenAny(sent.Task,Task.Delay(15000))==sent.Task;
                        if(requestGeneration!=generation)return;
                        if(!SameScoreAccount(player,account))return;
                        if(!completed||!await sent.Task){Fail("Google win upload failed or timed out. Retry SYNC WINS.");return;}
                        lastSubmittedScore=wins;
                    }
                    force=false;
                    if(CurrentScore!=wins)continue;
                    state=ConnectionState.Ready;status="Reported "+wins+" wins to Google Play.";
                    if(verifyRequested||openAfterAuthentication)
                    {
                        verifyRequested=false;status="Checking the score stored by Google Play...";
                        var loaded=new TaskCompletionSource<LeaderboardScoreData>();
                        PlayGamesPlatform.Instance.LoadScores(MostWinsLeaderboardId,LeaderboardStart.PlayerCentered,1,
                            LeaderboardCollection.Public,LeaderboardTimeSpan.AllTime,result=>loaded.TrySetResult(result));
                        bool completed=await Task.WhenAny(loaded.Task,Task.Delay(15000))==loaded.Task;
                        if(requestGeneration!=generation)return;
                        if(!SameScoreAccount(player,account))return;
                        var result=completed?await loaded.Task:null;
                        if(result?.Valid!=true||result.PlayerScore==null)
                        {Fail("Wins sent; Google score could not be read back. Retry SYNC WINS.");return;}
                        lastVerifiedScore=result.PlayerScore.value;
                        lastGoogleFormattedScore=result.PlayerScore.formattedValue;
                        state=lastVerifiedScore>=wins?ConnectionState.Ready:ConnectionState.Error;
                        status=lastVerifiedScore>=wins?
                            "Google confirms "+lastVerifiedScore+" wins. Google display: "+lastGoogleFormattedScore:
                            "Sent "+wins+" wins; Google still reports "+lastVerifiedScore+". Retry SYNC WINS.";
                    }
                    if(CurrentScore!=wins){verifyRequested|=openAfterAuthentication;continue;}
                    // Native UI must open after upload/readback, not alongside them.
                    FinishPendingOpen();return;
                }
                Fail("New wins are waiting. Tap SYNC WINS to finish synchronization.");
            }
            catch(Exception e){if(requestGeneration==generation)Fail("Win sync unavailable ("+e.GetType().Name+"). Retry SYNC WINS.");}
            finally{if(requestGeneration==generation)submitting=false;}
#else
            await Task.CompletedTask;
            Unsupported();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static bool SameScoreAccount(string player,string account)
        {
            if(IsAuthenticated&&PlayGamesPlatform.Instance.localUser.id==player&&
                AccountServerBootstrap.Instance?.GooglePlayAccountReady==true&&PaidShardCommerce.Instance?.Ready==true&&
                PaidShardCommerce.Instance.AccountHash==account&&AccountProfilePrefs.Attached&&AccountProfilePrefs.Account==account)return true;
            lastSubmittedScore=-1;lastVerifiedScore=-1;lastGoogleFormattedScore=null;
            openAfterAuthentication=verifyRequested=false;
            state=ConnectionState.Idle;status="Account changed. Reconnect and retry SYNC WINS.";return false;
        }
        private static void FinishPendingOpen()
        {
            if(!openAfterAuthentication)return;
            openAfterAuthentication=false;ShowNativeLeaderboardUi();
        }
#endif

        public static void ShowLeaderboard()
        {
            if(!IsConfigured){state=ConnectionState.NotConfigured;return;}
#if UNITY_ANDROID && !UNITY_EDITOR
            // Viewing Google's ranking only needs native Play Games sign-in.
            // SubmitCurrentScore retains the verified cloud-profile/account guard.
            if(openingUi)return;
            openAfterAuthentication=verifyRequested=true;
            if(!IsAuthenticated)
            {
                openAfterAuthentication=true;
                AuthenticateAndSync(true);
                return;
            }
            SubmitCurrentScore(true);
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
        private static void Fail(string message){openAfterAuthentication=verifyRequested=false;state=ConnectionState.Error;status=message;Debug.LogWarning("DIMENSIONS FRONT: "+message);}
    }
}

