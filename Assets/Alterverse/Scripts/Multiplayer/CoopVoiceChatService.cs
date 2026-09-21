using System;
using System.Text;
using System.Threading.Tasks;
using Alterverse.Profile;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Private lobby voice chat. A lobby join code maps to one non-positional Vivox audio channel.
    /// The microphone is open by default after the player explicitly grants OS permission.
    /// Voice stays connected while the session moves from lobby to match and disconnects when
    /// the player leaves the private co-op session.
    /// </summary>
    public sealed class CoopVoiceChatService : MonoBehaviour
    {
        private const string ChannelPrefix="DF_COOP_VOICE_";
        private const string BluetoothConnectPermission="android.permission.BLUETOOTH_CONNECT";

        public static CoopVoiceChatService Instance { get; private set; }

        private string activeChannel="";
        private string attemptedChannel="";
        private string status="VOICE READY";
        private string lastError="";
        private bool initialized;
        private bool busy;
        private bool permissionDenied;
        private bool muted;

        public string Status=>status;
        public string LastError=>lastError;
        public bool Busy=>busy;
        public bool PermissionDenied=>permissionDenied;
        public bool HasMicrophonePermission=>CheckMicrophonePermission();
        public bool IsConnected=>
            !string.IsNullOrEmpty(activeChannel)&&initialized&&VivoxService.Instance.IsLoggedIn&&
            VivoxService.Instance.ActiveChannels.ContainsKey(activeChannel);
        public bool IsMuted=>muted||(initialized&&VivoxService.Instance.IsLoggedIn&&VivoxService.Instance.IsInputDeviceMuted);
        public int ParticipantCount
        {
            get
            {
                if(!IsConnected)return 0;
                return VivoxService.Instance.ActiveChannels.TryGetValue(activeChannel,out var participants)
                    ?participants.Count:0;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if(Instance==null)new GameObject("DF_CoopVoiceChat").AddComponent<CoopVoiceChatService>();
        }

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            CoopLobbyService lobby=CoopLobbyService.Instance;
            if(lobby==null||!lobby.InLobby)
            {
                attemptedChannel="";
                if(!string.IsNullOrEmpty(activeChannel)&&!busy)_=LeaveVoiceAsync();
                return;
            }

            string desired=BuildChannelName(lobby.JoinCode);
            if(string.IsNullOrEmpty(desired)||desired==activeChannel||desired==attemptedChannel||busy)return;
            attemptedChannel=desired;
            _=JoinLobbyVoiceAsync(desired,false);
        }

        public void RequestMicrophoneAndJoin()
        {
            CoopLobbyService lobby=CoopLobbyService.Instance;
            if(lobby==null||!lobby.InLobby||busy)return;
            attemptedChannel="";
            permissionDenied=false;
            string desired=BuildChannelName(lobby.JoinCode);
            if(!string.IsNullOrEmpty(desired))
            {
                attemptedChannel=desired;
                _=JoinLobbyVoiceAsync(desired,true);
            }
        }

        public void ToggleMute()
        {
            if(!initialized||!VivoxService.Instance.IsLoggedIn)return;
            muted=!IsMuted;
            if(muted)
            {
                VivoxService.Instance.MuteInputDevice();
                status="VOICE CONNECTED • MIC MUTED";
            }
            else
            {
                VivoxService.Instance.UnmuteInputDevice();
                status="VOICE CONNECTED • MIC OPEN";
            }
        }

        private async Task JoinLobbyVoiceAsync(string channel,bool userInitiated)
        {
            if(busy)return;
            busy=true;lastError="";
            try
            {
                status="VOICE • CHECKING MICROPHONE";
                if(!await EnsureMicrophonePermissionAsync(userInitiated))
                {
                    permissionDenied=true;
                    status="MICROPHONE PERMISSION NEEDED";
                    return;
                }

                permissionDenied=false;
                status="VOICE • CONNECTING";

                if(UnityServices.State!=ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if(!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                if(!initialized)
                {
                    await VivoxService.Instance.InitializeAsync();
                    initialized=true;
                }

                if(!VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LoginAsync(new LoginOptions
                    {
                        DisplayName=SafeDisplayName(PlayerProfile.PlayerName)
                    });
                }

                if(!string.IsNullOrEmpty(activeChannel)&&activeChannel!=channel&&
                   VivoxService.Instance.ActiveChannels.ContainsKey(activeChannel))
                    await VivoxService.Instance.LeaveChannelAsync(activeChannel);

                if(!VivoxService.Instance.ActiveChannels.ContainsKey(channel))
                    await VivoxService.Instance.JoinGroupChannelAsync(channel,ChatCapability.AudioOnly);

                activeChannel=channel;
                muted=false;
                // Open microphone is the default for private co-op after the player has explicitly
                // granted microphone permission. Players can still mute at any time in lobby or match.
                VivoxService.Instance.UnmuteInputDevice();
                status="VOICE CONNECTED • MIC OPEN";
            }
            catch(Exception ex)
            {
                activeChannel="";
                attemptedChannel="";
                lastError=ex.Message??"Voice service error.";
                status="VOICE UNAVAILABLE";
                Debug.LogWarning("DIMENSIONS FRONT VOICE: "+ex);
            }
            finally{busy=false;}
        }

        private async Task LeaveVoiceAsync()
        {
            if(busy)return;
            busy=true;
            string leaving=activeChannel;
            activeChannel="";
            try
            {
                if(initialized&&VivoxService.Instance.IsLoggedIn&&!string.IsNullOrEmpty(leaving)&&
                   VivoxService.Instance.ActiveChannels.ContainsKey(leaving))
                    await VivoxService.Instance.LeaveChannelAsync(leaving);
                status="VOICE READY";
            }
            catch(Exception ex)
            {
                lastError=ex.Message??"Could not leave voice channel.";
                status="VOICE DISCONNECT ERROR";
                Debug.LogWarning("DIMENSIONS FRONT VOICE: "+ex);
            }
            finally{busy=false;}
        }

        private static string BuildChannelName(string joinCode)
        {
            if(string.IsNullOrWhiteSpace(joinCode))return "";
            StringBuilder b=new(ChannelPrefix);
            string normalized=CoopLobbyService.NormalizeJoinCode(joinCode).ToUpperInvariant();
            for(int i=0;i<normalized.Length;i++)
            {
                char c=normalized[i];
                if(char.IsLetterOrDigit(c)||c=='-'||c=='_')b.Append(c);
            }
            return b.Length>ChannelPrefix.Length?b.ToString():"";
        }

        private static string SafeDisplayName(string raw)
        {
            if(string.IsNullOrWhiteSpace(raw))return "Traveler";
            string value=raw.Trim();
            return value.Length<=32?value:value.Substring(0,32);
        }

        private static bool CheckMicrophonePermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#elif UNITY_IOS && !UNITY_EDITOR
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
            return true;
#endif
        }

        private async Task<bool> EnsureMicrophonePermissionAsync(bool userInitiated)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                status=userInitiated?"REQUESTING MICROPHONE ACCESS":"MICROPHONE ACCESS REQUIRED";
                bool granted=await RequestAndroidPermissionAsync(Permission.Microphone);
                if(!granted)return false;
            }

            if(AndroidSdkLevel()>=31&&!Permission.HasUserAuthorizedPermission(BluetoothConnectPermission))
            {
                // Recommended by Vivox for Android 12+ Bluetooth headsets. A denial does not
                // block phone microphone/speaker voice, so voice can continue.
                await RequestAndroidPermissionAsync(BluetoothConnectPermission);
            }
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
#elif UNITY_IOS && !UNITY_EDITOR
            if(Application.HasUserAuthorization(UserAuthorization.Microphone))return true;
            status="REQUESTING MICROPHONE ACCESS";
            AsyncOperation request=Application.RequestUserAuthorization(UserAuthorization.Microphone);
            while(!request.isDone)await Task.Yield();
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
            await Task.Yield();
            return true;
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static Task<bool> RequestAndroidPermissionAsync(string permission)
        {
            if(Permission.HasUserAuthorizedPermission(permission))return Task.FromResult(true);

            var completion=new TaskCompletionSource<bool>();
            var callbacks=new PermissionCallbacks();
            callbacks.PermissionGranted+=_=>completion.TrySetResult(true);
            callbacks.PermissionDenied+=_=>completion.TrySetResult(false);
            callbacks.PermissionRequestDismissed+=_=>completion.TrySetResult(false);
            Permission.RequestUserPermission(permission,callbacks);
            return completion.Task;
        }

        private static int AndroidSdkLevel()
        {
            try
            {
                using var version=new AndroidJavaClass("android.os.Build$VERSION");
                return version.GetStatic<int>("SDK_INT");
            }
            catch{return 0;}
        }
#endif

        private void OnDestroy()
        {
            if(Instance==this)Instance=null;
        }
    }
}
