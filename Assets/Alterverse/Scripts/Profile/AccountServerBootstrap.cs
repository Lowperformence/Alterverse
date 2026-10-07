using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Alterverse.Shop;
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
        public string Status { get; private set; } = "Account server not configured.";
        public bool Busy { get; private set; }
        string endpoint,connectedPlayer;
        bool subscribed;
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
        }
        async void Start()
        {
            try
            {
                var config=Resources.Load<TextAsset>("Commerce/AccountServerConfig");
                endpoint=config!=null?JsonUtility.FromJson<Configuration>(config.text)?.httpsEndpoint:null;
                if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme!="https")
                {Status="Account server address required.";return;}
                if(UnityServices.State!=ServicesInitializationState.Initialized)await UnityServices.InitializeAsync();
                AuthenticationService.Instance.SignedIn+=OnSignedIn;
                AuthenticationService.Instance.SignedOut+=OnSignedOut;
                AuthenticationService.Instance.Expired+=OnExpired;subscribed=true;
                if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
                else OnSignedIn();
            }
            catch(Exception){Status="Account sign-in unavailable. Retry from Profile.";}
        }
        void OnSignedIn()
        {
            var auth=AuthenticationService.Instance;
            if(!auth.IsAuthorized||connectedPlayer==auth.PlayerId&&PaidShardCommerce.Instance!=null)return;
            connectedPlayer=auth.PlayerId;
            using var hash=SHA256.Create();
            string account=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(auth.PlayerId))).Replace("-","").ToLowerInvariant();
            ProfileTransaction.Recover();
            PaidShardCommerce.Connect(endpoint,()=>AuthenticationService.Instance.IsAuthorized?AuthenticationService.Instance.AccessToken:null,account);
            SocialService.Connect(endpoint,()=>AuthenticationService.Instance.IsAuthorized?AuthenticationService.Instance.AccessToken:null);
            Status="Account connected.";
        }
        void OnSignedOut(){connectedPlayer=null;SocialService.Disconnect();PaidShardCommerce.Disconnect();Status="Signed out.";}
        void OnExpired(){Status="Session expired. Sign in to synchronize.";PaidShardCommerce.Instance?.Refresh();}
        void Update()
        {
            if(UnityServices.State==ServicesInitializationState.Initialized&&AuthenticationService.Instance.IsAuthorized&&subscribed)
            {
                if(connectedPlayer!=AuthenticationService.Instance.PlayerId)OnSignedIn();
            }
        }
        public async Task<bool> SignInAsync(string username,string password,bool register)
        {
            if(Busy||string.IsNullOrWhiteSpace(username)||string.IsNullOrEmpty(password))return false;
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
                    if(!auth.IsSignedIn)await auth.SignInAnonymouslyAsync();
                    await auth.AddUsernamePasswordAsync(username.Trim(),password);
                    Status="Account secured. Use this login on your other devices.";
                }
                else
                {
                    if(auth.IsSignedIn)auth.SignOut();
                    await auth.SignInWithUsernamePasswordAsync(username.Trim(),password);
                    OnSignedIn();Status="Signed in. Loading your account…";
                }
                return true;
            }
            catch(Exception){Status="Sign-in failed. Check login details and try again.";return false;}
            finally{Busy=false;}
        }
        void OnDestroy()
        {
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


