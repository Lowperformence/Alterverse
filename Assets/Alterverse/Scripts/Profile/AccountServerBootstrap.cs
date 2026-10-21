using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Alterverse.Shop;
using Alterverse.Online;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Alterverse.Profile
{
    // Uses the same Unity identity as multiplayer. Server hashes the verified JWT sub
    // with SHA-256, matching Google's obfuscated account ID without exposing credentials.
    public sealed class AccountServerBootstrap : MonoBehaviour
    {
        [Serializable] sealed class Configuration { public string httpsEndpoint; }
        public static AccountServerBootstrap Instance { get; private set; }
        // Persistent migration guard: an installation that has previously connected
        // a Unity account must never create a replacement identity on a login error.
        private const string KnownAccountOnDeviceKey="DF_KnownUnityAccountOnDevice_v1";
        public string Status { get; private set; } = "Account server not configured.";
        public bool Busy { get; private set; }
        string endpoint,connectedPlayer;
        bool subscribed;
        Task<bool> authenticating;
        string linkedUnityPlayer,linkedGooglePlayer;
        public string GoogleStatus { get; private set; }="Connect Google Play Games to restore progress and purchases.";
        public bool GoogleRestoreAvailable { get; private set; }
        public bool GooglePlayAccountReady=>UnityServices.State==ServicesInitializationState.Initialized&&
            AuthenticationService.Instance.IsAuthorized&&AuthenticationService.Instance.PlayerId==linkedUnityPlayer&&
            !string.IsNullOrEmpty(linkedGooglePlayer)&&GooglePlayAccountSession.PlayerId==linkedGooglePlayer;
        // The UI and match launcher must wait until the verified Google identity's
        // cloud profile has been loaded, not just until Unity sign-in completes.
        public static bool CloudProfileReady=>!GooglePlayAccountSession.Supported ||
            (Instance?.GooglePlayAccountReady==true &&
             PaidShardCommerce.Instance?.Ready==true &&
             AccountProfilePrefs.Attached &&
             AccountProfilePrefs.Account==PaidShardCommerce.Instance.AccountHash);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
#if !UNITY_SERVER
            if(!Application.isBatchMode && !Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Requested && Instance==null)
                new GameObject("DF_AccountServerBootstrap").AddComponent<AccountServerBootstrap>();
#endif
        }
        void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;DontDestroyOnLoad(gameObject);
            PaidShardCommerce.EntitlementsChanged+=OnCloudProfileChanged;
        }
        async void Start(){await EnsureAuthenticatedAsync();}
        public Task<bool> EnsureAuthenticatedAsync()
        {
            if(authenticating!=null&&!authenticating.IsCompleted)return authenticating;
            if(Busy)return Task.FromResult(false);
            if(subscribed&&UnityServices.State==ServicesInitializationState.Initialized&&AuthenticationService.Instance.IsSignedIn)return Task.FromResult(true);
            return authenticating=InitializeAsync();
        }
        async Task<bool> InitializeAsync()
        {
            Busy=true;
            try
            {
                var config=Resources.Load<TextAsset>("Commerce/AccountServerConfig");
                endpoint=config!=null?JsonUtility.FromJson<Configuration>(config.text)?.httpsEndpoint:null;
                if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https")
                {endpoint=null;Status="Account server address required.";}
                if(UnityServices.State!=ServicesInitializationState.Initialized)await UnityServices.InitializeAsync();
                if(!subscribed)
                {
                    AuthenticationService.Instance.SignedIn+=OnSignedIn;
                    AuthenticationService.Instance.SignedOut+=OnSignedOut;
                    AuthenticationService.Instance.Expired+=OnExpired;subscribed=true;
                }
                var auth=AuthenticationService.Instance;
                if(!auth.IsSignedIn && GooglePlayAccountSession.Supported)
                {
                    // Do not fall back to an unrelated local account when the native
                    // Google Play Games configuration is incomplete.
                    string configuration=GooglePlayAccountSession.ConfigurationError;
                    if(!string.IsNullOrEmpty(configuration))
                    {
                        Status="Google Play account setup incomplete.";
                        GoogleStatus=configuration;
                        return false;
                    }
                }
                if(!auth.IsSignedIn)
                {
                    if(GooglePlayAccountSession.Supported)
                    {
                        // Google Play Games is the primary Android identity. Always try
                        // the existing Google-linked Unity account before restoring a
                        // possibly temporary cached Unity account from an older build.
                        bool cachedSession=auth.SessionTokenExists;
                        string code=await GooglePlayAccountSession.AuthorizationCodeAsync();
                        if(!string.IsNullOrEmpty(code))
                        {
                            try
                            {
                                // With a cached player, NEVER create a second Google account:
                                // a missing Google link must instead leave the old cached
                                // identity available for safe in-place linking.
                                bool firstAccountOnDevice=!cachedSession &&
                                    PlayerPrefs.GetInt(KnownAccountOnDeviceKey,0)==0;
                                await auth.SignInWithGooglePlayGamesAsync(code,
                                    new SignInOptions{CreateAccount=firstAccountOnDevice});
                            }
                            catch(Exception)
                            {
                                GoogleStatus="Google account sign-in failed. Reconnect Google Play to retry.";
                            }
                        }
                        if(!auth.IsSignedIn&&cachedSession&&auth.SessionTokenExists)
                        {
                            // SignInAnonymously restores the cached Unity session here;
                            // it must never run without a token on Android.
                            await auth.SignInAnonymouslyAsync();
                        }
                        if(!auth.IsSignedIn)
                        {
                            Status="Google Play sign-in required. Existing progress is safe.";
                            GoogleStatus="Unable to sign in to Google Play. Tap CONNECT GOOGLE to retry. No guest account was created.";
                            return false;
                        }
                    }
                    else
                    {
                        // Other platforms retain the existing guest sign-in flow.
                        await auth.SignInAnonymouslyAsync();
                    }
                }
                OnSignedIn();
                if(GooglePlayAccountSession.Supported)await ConnectGoogleCoreAsync(false,false);
                return auth.IsAuthorized;
            }
            catch(Exception){Status="Account sign-in unavailable. Retry from Profile.";return false;}
            finally{Busy=false;}
        }
        void OnSignedIn()
        {
            var auth=AuthenticationService.Instance;
            if(string.IsNullOrEmpty(endpoint)||!auth.IsAuthorized||connectedPlayer==auth.PlayerId&&PaidShardCommerce.Instance!=null)return;
            connectedPlayer=auth.PlayerId;
            if(GooglePlayAccountSession.Supported && PlayerPrefs.GetInt(KnownAccountOnDeviceKey,0)==0)
            {
                PlayerPrefs.SetInt(KnownAccountOnDeviceKey,1);
                PlayerPrefs.Save();
            }
            using var hash=SHA256.Create();
            string account=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(auth.PlayerId))).Replace("-","").ToLowerInvariant();
            ProfileTransaction.Recover();
            PaidShardCommerce.Connect(endpoint,()=>AuthenticationService.Instance.IsAuthorized?AuthenticationService.Instance.AccessToken:null,account);
            SocialService.Connect(endpoint,()=>AuthenticationService.Instance.IsAuthorized?AuthenticationService.Instance.AccessToken:null);
            Status="Account connected.";
        }
        void OnSignedOut(){connectedPlayer=linkedUnityPlayer=linkedGooglePlayer=null;SocialService.Disconnect();PaidShardCommerce.Disconnect();Status="Signed out.";}
        void OnExpired(){Status="Session expired. Sign in to synchronize.";PaidShardCommerce.Instance?.Refresh();}
        void OnCloudProfileChanged()
        {
            ApplyGooglePlayDisplayName();
            GooglePlayLevelLeaderboard.SubmitCurrentScore();
        }
        void ApplyGooglePlayDisplayName()
        {
            if(!GooglePlayAccountReady)return;
            using var hash=SHA256.Create();
            string account=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(linkedUnityPlayer))).Replace("-", "").ToLowerInvariant();
            if(PlayerProfile.TryUseGooglePlayName(account,GooglePlayAccountSession.DisplayName)&&PaidShardCommerce.Instance?.Ready==true)
                _=PaidShardCommerce.Instance.FlushProfileAsync();
        }
        void Update()
        {
            if(UnityServices.State==ServicesInitializationState.Initialized&&AuthenticationService.Instance.IsAuthorized&&subscribed)
            {
                if(connectedPlayer!=AuthenticationService.Instance.PlayerId)OnSignedIn();
            }
        }
        public async Task<bool> SignInAsync(string username,string password,bool register)
        {
            if (Alterverse.Multiplayer.RankedService.Instance?.Searching == true || Alterverse.Multiplayer.RankedService.Instance?.Busy == true)
            {Status="Cancel Ranked matchmaking before changing accounts.";return false;}
            if(Busy||string.IsNullOrWhiteSpace(username)||string.IsNullOrEmpty(password))return false;
            if(GooglePlayShardStore.Instance?.Busy==true||GooglePlayShardStore.Instance?.PendingRecovery==true||PaidShardCommerce.Spending)
            {Status="Finish purchase recovery before changing accounts.";return false;}
            if(Alterverse.Multiplayer.CoopLobbyService.Instance?.Busy==true||Alterverse.Multiplayer.OnlinePartyService.Instance?.Busy==true||Alterverse.Multiplayer.EdgegapOnlineFlow.Instance?.Busy==true||Alterverse.Multiplayer.CoopAuthority.Active||Alterverse.Multiplayer.CoopLobbyService.Instance?.InLobby==true||Alterverse.Multiplayer.OnlinePartyService.Instance?.InParty==true)
            {Status="Leave the match or party before changing accounts.";return false;}
            Busy=true;
            try
            {
                if(string.IsNullOrEmpty(endpoint)){Status="Account server address required.";return false;}
                if(UnityServices.State!=ServicesInitializationState.Initialized)await UnityServices.InitializeAsync();
                if(PaidShardCommerce.Instance!=null&&PaidShardCommerce.Instance.Ready)
                    if(!await PaidShardCommerce.Instance.FlushProfileAsync()){Status="Save your account before changing login.";return false;}
                var auth=AuthenticationService.Instance;
                if(register)
                {
                    // Link the existing guest instead of creating a new identity and losing progress.
                    if(!auth.IsSignedIn)
                    {
                        if(GooglePlayAccountSession.Supported)
                        {
                            Status="Connect Google Play before registering a login.";
                            return false;
                        }
                        await auth.SignInAnonymouslyAsync();
                    }
                    await auth.AddUsernamePasswordAsync(username.Trim(),password);
                    Status="Account secured. Use this login on your other devices.";
                }
                else
                {
                    if(auth.IsSignedIn)auth.SignOut();
                    await auth.SignInWithUsernamePasswordAsync(username.Trim(),password);
                    OnSignedIn();Status="Signed in. Loading your account…";
                }
                await VerifyGoogleLinkAsync();
                return true;
            }
            catch(Exception){Status="Sign-in failed. Check login details and try again.";return false;}
            finally{Busy=false;}
        }

        public async Task<bool> ConnectGoogleAsync(bool restore=false)
        {
            if (Alterverse.Multiplayer.RankedService.Instance?.Searching == true || Alterverse.Multiplayer.RankedService.Instance?.Busy == true)
            {GoogleStatus="Cancel Ranked matchmaking before connecting accounts.";return false;}
            if(Busy)return false;
            if(Alterverse.Multiplayer.CoopLobbyService.Instance?.Busy==true||Alterverse.Multiplayer.OnlinePartyService.Instance?.Busy==true||
                Alterverse.Multiplayer.EdgegapOnlineFlow.Instance?.Busy==true||Alterverse.Multiplayer.CoopAuthority.Active||
                Alterverse.Multiplayer.CoopLobbyService.Instance?.InLobby==true||Alterverse.Multiplayer.OnlinePartyService.Instance?.InParty==true||
                GameObject.Find("DF_MatchWorld")!=null)
            {GoogleStatus="Leave the match or party before connecting accounts.";return false;}
            if(GooglePlayShardStore.Instance?.Busy==true||PaidShardCommerce.Spending||restore&&GooglePlayShardStore.Instance?.PendingRecovery==true)
            {GoogleStatus="Finish purchase recovery before changing accounts.";return false;}
            if(!await EnsureAuthenticatedAsync())
            {
                // Manual recovery may be necessary when an existing installation lost its
                // cached Unity session. Only sign in to an EXISTING Google-linked Unity
                // account; never create a replacement identity during recovery.
                if(UnityServices.State!=ServicesInitializationState.Initialized ||
                    AuthenticationService.Instance.IsSignedIn || !GooglePlayAccountSession.Supported)return false;
                Busy=true;
                try
                {
                    string configuration=GooglePlayAccountSession.ConfigurationError;
                    if(!string.IsNullOrEmpty(configuration))
                    {
                        GoogleStatus=configuration;
                        return false;
                    }
                    string code=await GooglePlayAccountSession.AuthorizationCodeAsync(true);
                    if(string.IsNullOrEmpty(code))
                    {
                        GoogleStatus="Google authorization unavailable. Check Play Games configuration.";
                        return false;
                    }
                    // A genuinely fresh installation may create its first Google-linked
                    // account here after an earlier network retry. Previously used
                    // installations may only recover an existing account.
                    bool firstAccountOnDevice=PlayerPrefs.GetInt(KnownAccountOnDeviceKey,0)==0;
                    await AuthenticationService.Instance.SignInWithGooglePlayGamesAsync(code,
                        new SignInOptions{CreateAccount=firstAccountOnDevice});
                    OnSignedIn();
                    return await VerifyGoogleLinkAsync();
                }
                catch(Exception)
                {
                    GoogleStatus="Existing Google account could not be restored. No new account was created.";
                    return false;
                }
                finally{Busy=false;}
            }
            if(Busy)return false;
            Busy=true;
            try{return await ConnectGoogleCoreAsync(true,restore);}
            catch(Exception){GoogleStatus="Google account connection failed. Retry from Profile.";return false;}
            finally{Busy=false;}
        }

        async Task<bool> VerifyGoogleLinkAsync()
        {
            linkedUnityPlayer=linkedGooglePlayer=null;
            if(!GooglePlayAccountSession.IsAuthenticated||!AuthenticationService.Instance.IsAuthorized)return false;
            string player=AuthenticationService.Instance.PlayerId,google=GooglePlayAccountSession.PlayerId;
            var info=await AuthenticationService.Instance.GetPlayerInfoAsync();
            string provider=info.GetGooglePlayGamesId();
            if(string.IsNullOrEmpty(provider)||provider!=google||google!=GooglePlayAccountSession.PlayerId||
                !AuthenticationService.Instance.IsAuthorized||player!=AuthenticationService.Instance.PlayerId)return false;
            linkedUnityPlayer=player;linkedGooglePlayer=provider;
            ApplyGooglePlayDisplayName();
            GoogleRestoreAvailable=false;
            GoogleStatus="Google Play account linked. Progress and purchases use your cloud account.";
            GooglePlayLevelLeaderboard.SubmitCurrentScore();
            return true;
        }

        async Task<bool> ConnectGoogleCoreAsync(bool interactive,bool restore)
        {
            if(!GooglePlayAccountSession.Supported){GoogleStatus="Google account connection is available in the Android build.";return false;}
            string configuration=GooglePlayAccountSession.ConfigurationError;
            if(!string.IsNullOrEmpty(configuration)){GoogleStatus=configuration;return false;}
            if(!await GooglePlayAccountSession.AuthenticateAsync(interactive))
            {GoogleStatus=GooglePlayAccountSession.LastError;return false;}
            if(await VerifyGoogleLinkAsync())return true;
            var auth=AuthenticationService.Instance;
            var info=await auth.GetPlayerInfoAsync();
            if(!string.IsNullOrEmpty(info.GetGooglePlayGamesId())&&!restore)
            {GoogleRestoreAvailable=true;GoogleStatus="This game account belongs to another Google player. Restore the selected Google account.";return false;}
            if(restore)
            {
                if(!GoogleRestoreAvailable)return false;
                var backend=PaidShardCommerce.Instance;
                if(backend!=null&&backend.HasPaidHistory&&string.IsNullOrEmpty(info.GetGooglePlayGamesId()))
                {GoogleStatus="This game account has a purchase history. Contact support before switching; purchases cannot be merged automatically.";return false;}
                if(backend==null||!backend.Ready||!await backend.FlushProfileAsync())
                {GoogleStatus="Save the current cloud account before restoring another account.";return false;}
            }
            string code=await GooglePlayAccountSession.AuthorizationCodeAsync(interactive);
            if(string.IsNullOrEmpty(code)){GoogleStatus="Google authorization unavailable. Check the Web Client ID and identity provider setup.";return false;}
            string previousPlayer=auth.PlayerId;
            try
            {
                if(restore)
                {
                    var backend=PaidShardCommerce.Instance;
                    // Save edits made while the native Google authorization dialog was open.
                    if(backend==null||!backend.Ready||!await backend.FlushProfileAsync()||AccountProfilePrefs.Dirty)
                    {GoogleStatus="Save the current cloud account before restoring another account.";return false;}
                    // UI/native authorization can take time. Recheck owners immediately before switching.
                    if(GooglePlayShardStore.Instance?.Busy==true||GooglePlayShardStore.Instance?.PendingRecovery==true||PaidShardCommerce.Spending||
                        Alterverse.Multiplayer.CoopLobbyService.Instance?.Busy==true||Alterverse.Multiplayer.OnlinePartyService.Instance?.Busy==true||
                        Alterverse.Multiplayer.EdgegapOnlineFlow.Instance?.Busy==true||Alterverse.Multiplayer.CoopAuthority.Active||
                        Alterverse.Multiplayer.CoopLobbyService.Instance?.InLobby==true||Alterverse.Multiplayer.OnlinePartyService.Instance?.InParty==true||
                        GameObject.Find("DF_MatchWorld")!=null)
                    {GoogleStatus="Finish purchases and leave the match or party before restoring accounts.";return false;}
                    auth.SignOut();
                    await auth.SignInWithGooglePlayGamesAsync(code,new SignInOptions{CreateAccount=false});
                    OnSignedIn();
                }
                else await auth.LinkWithGooglePlayGamesAsync(code);
                if(!await VerifyGoogleLinkAsync())
                {GoogleStatus="Google account link could not be verified. Purchases remain disabled.";return false;}
                GooglePlayShardStore.Instance?.Reconnect();
                return true;
            }
            catch(AuthenticationException error) when(error.ErrorCode==AuthenticationErrorCodes.AccountAlreadyLinked)
            {
                GoogleRestoreAvailable=true;
                GoogleStatus="This Google account has an existing cloud save. Use RESTORE GOOGLE; the current save is kept separately.";
                return false;
            }
            catch(Exception)
            {
                if(restore&&!auth.IsSignedIn)
                {
                    try
                    {
                        if(auth.SessionTokenExists)
                        {
                            await auth.SignInAnonymouslyAsync();
                            if(auth.PlayerId!=previousPlayer)GoogleStatus="Previous account could not be recovered. Contact support.";
                            else GoogleStatus="Restore failed. Your previous account is still selected.";
                        }
                        else GoogleStatus="Restore failed. Previous session unavailable; no new guest was created.";
                    }
                    catch(Exception){GoogleStatus="Restore failed. Retry sign-in before making purchases.";}
                }
                else GoogleStatus="Google link failed. Check the Google Play Games identity provider and retry.";
                return false;
            }
        }
        void OnDestroy()
        {
            PaidShardCommerce.EntitlementsChanged-=OnCloudProfileChanged;
            if(subscribed&&UnityServices.State==ServicesInitializationState.Initialized)
            {
                AuthenticationService.Instance.SignedIn-=OnSignedIn;
                AuthenticationService.Instance.SignedOut-=OnSignedOut;
                AuthenticationService.Instance.Expired-=OnExpired;
            }
            if(Instance==this)Instance=null;
        }
    }
}
