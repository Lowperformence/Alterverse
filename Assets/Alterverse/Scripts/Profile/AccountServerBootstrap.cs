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
        public bool StartupAttemptFinished { get; private set; }
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
            if(subscribed&&UnityServices.State==ServicesInitializationState.Initialized&&AuthenticationService.Instance.IsSignedIn&&
                (!GooglePlayAccountSession.Supported||GooglePlayAccountReady))return Task.FromResult(true);
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
                if(GooglePlayAccountSession.Supported)
                {
                    // A live cloud account may only be changed through the existing
                    // explicit restore/save checks. A cached startup session has no
                    // authority to select a player before Google Play signs in.
                    if(connectedPlayer!=null||AccountProfilePrefs.Attached)
                        return await ConnectGoogleCoreAsync(false,false);
                    return await SignInGooglePrimaryAsync(false,false);
                }
                if(!auth.IsSignedIn)
                {
                    // Editor/desktop development retains its existing identity flow.
                    await auth.SignInAnonymouslyAsync();
                }
                OnSignedIn();
                return auth.IsAuthorized;
            }
            catch(Exception){Status="Account sign-in unavailable. Retry from Profile.";return false;}
            finally{Busy=false;StartupAttemptFinished=true;}
        }
        async Task<bool> SignInGooglePrimaryAsync(bool interactive,bool restore)
        {
            string configuration=GooglePlayAccountSession.ConfigurationError;
            if(!string.IsNullOrEmpty(configuration))
            {Status="Google Play account setup incomplete.";GoogleStatus=configuration;return false;}
            Status="Connecting Google Play account...";
            GoogleStatus="Signing in with Google Play Games...";
            string code=await GooglePlayAccountSession.AuthorizationCodeAsync(interactive);
            if(string.IsNullOrEmpty(code))
            {
                Status="Google Play sign-in required.";
                GoogleStatus=string.IsNullOrEmpty(GooglePlayAccountSession.LastError)?
                    "Google Play sign-in unavailable. Tap CONNECT GOOGLE to retry.":GooglePlayAccountSession.LastError;
                return false;
            }
            var auth=AuthenticationService.Instance;
            // Never restore or link a generated guest on Android. Existing Google
            // players are selected by the provider, regardless of a cached Unity ID.
            if(auth.IsSignedIn)auth.SignOut();
            bool firstGoogleAccount=!restore&&PlayerPrefs.GetInt(KnownAccountOnDeviceKey,0)==0;
            try
            {
                await auth.SignInWithGooglePlayGamesAsync(code,new SignInOptions{CreateAccount=firstGoogleAccount});
                if(await VerifyGoogleLinkAsync())return true;
                GoogleStatus="Google account could not be verified. Tap CONNECT GOOGLE to retry.";
            }
            catch(Exception)
            {GoogleStatus="Google account sign-in failed. Tap CONNECT GOOGLE to retry.";}
            Status="Google Play sign-in required.";
            return false;
        }
        void OnSignedIn()
        {
            if(GooglePlayAccountSession.Supported&&!GooglePlayAccountReady)
            {
                Status="Verifying Google Play account...";
                return;
            }
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
            if(GooglePlayAccountSession.Supported)
            {Status="Use Google Play Games to sign in on Android.";return false;}
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
                // Retry native Google sign-in without reviving a cached guest identity.
                if(UnityServices.State!=ServicesInitializationState.Initialized ||
                    !GooglePlayAccountSession.Supported)return false;
                Busy=true;
                try
                {
                    if(connectedPlayer!=null||AccountProfilePrefs.Attached)
                        return await ConnectGoogleCoreAsync(true,restore);
                    return await SignInGooglePrimaryAsync(true,restore);
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
            OnSignedIn(); // Only the verified identity may start profile, wallet and social sync.
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
            {GoogleStatus=string.IsNullOrEmpty(GooglePlayAccountSession.LastError)?
                "Google Play sign-in failed. Tap RECONNECT to retry.":GooglePlayAccountSession.LastError;return false;}
            if(await VerifyGoogleLinkAsync())return true;
            var auth=AuthenticationService.Instance;
            var info=await auth.GetPlayerInfoAsync();
            if(!restore)
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
            if(string.IsNullOrEmpty(code)){GoogleStatus=string.IsNullOrEmpty(GooglePlayAccountSession.LastError)?
                "Google authorization unavailable. Check the Web Client ID and identity provider setup.":GooglePlayAccountSession.LastError;return false;}
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
                if(!await VerifyGoogleLinkAsync())
                {GoogleStatus="Google account link could not be verified. Purchases remain disabled.";return false;}
                GooglePlayShardStore.Instance?.Reconnect();
                return true;
            }
            catch(Exception)
            {
                GoogleStatus="Google Play sign-in failed. Saved progress is kept; tap CONNECT GOOGLE to retry.";
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


