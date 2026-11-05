using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if !UNITY_SERVER
using Unity.Services.Authentication;
using Unity.Services.Vivox;
#endif
namespace Alterverse.Multiplayer
{
    public sealed class PartyVoiceService:MonoBehaviour
    {
        public static PartyVoiceService Instance{get;private set;}
        public bool NeedsConsent{get;private set;}
        public bool Busy{get;private set;}
        public bool MicrophoneOn{get;private set;}
        public bool HasVoiceRoom=>room.Length>0;
        public string PlayerMuteStatus{get;private set;}="";
        public readonly struct VoicePlayer
        {
            public readonly string PlayerId,DisplayName;
            public readonly bool Muted,Pending;
            public VoicePlayer(string id,string name,bool muted,bool pending)
            {PlayerId=id;DisplayName=name;Muted=muted;Pending=pending;}
        }
        // Session-only preferences are keyed by identity, never a duplicate/changeable display name.
        private readonly Dictionary<string,bool> playerMutes=new(StringComparer.Ordinal);
        private readonly Dictionary<string,float> muteRequestTimes=new(StringComparer.Ordinal);
        private string muteOwner="";
        private bool participantEventsBound;
        public string Status{get;private set;}="VOICE OFF";
        private string room="",joinedRoom="";
        private int generation;
        private bool initialized,focused=true,paused;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Ensure()
        {
#if !UNITY_SERVER
            if(Instance==null)new GameObject("DF_PartyVoice").AddComponent<PartyVoiceService>();
#endif
        }
        private void Awake(){if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);}
        private void Update()
        {
#if !UNITY_SERVER
            RefreshMuteOwner();
            string next=OnlinePartyService.Instance?.VoiceRoomId??"";
            if(next!=room)
            {
                generation++;Mute();room=next;NeedsConsent=room.Length>0;
                playerMutes.Clear();muteRequestTimes.Clear();PlayerMuteStatus="";
                Status="VOICE OFF";
            }
            if(!Busy&&joinedRoom.Length>0&&joinedRoom!=room)_=DisconnectAsync();
            if(MicrophoneOn&&(!focused||paused||!PermissionGranted()))Mute();
#endif
        }
        public void AskAgain(){if(room.Length>0&&!Busy)NeedsConsent=true;}
        public void Decline(){generation++;NeedsConsent=false;Mute();if(!Busy)_=DisconnectAsync();}
        public async Task EnableAsync()
        {
#if !UNITY_SERVER
            if(Busy||room.Length==0||!focused||paused)return;
            Busy=true;NeedsConsent=false;int own=generation;string target=room;
            try
            {
                if(!await RequestPermissionAsync()){Status="MICROPHONE PERMISSION DENIED";return;}
                float focusDeadline=Time.realtimeSinceStartup+10f;
                while(own==generation&&(!focused||paused)&&Time.realtimeSinceStartup<focusDeadline)await Task.Yield();
                if(own!=generation||!focused||paused)return;
                if(!AuthenticationService.Instance.IsSignedIn){Status="VOICE UNAVAILABLE";return;}
                if(joinedRoom!=target&&VivoxService.Instance.IsLoggedIn)await DisconnectCoreAsync();
                if(!initialized){await VivoxService.Instance.InitializeAsync();initialized=true;}
                RefreshMuteOwner();
                if(!participantEventsBound)
                {VivoxService.Instance.ParticipantAddedToChannel+=OnVoiceParticipantAdded;participantEventsBound=true;}
                VivoxService.Instance.MuteInputDevice();
                if(!VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LoginAsync(new LoginOptions{
                    EnableTTS=false,DisplayName=Alterverse.Profile.PlayerProfile.PlayerName});
                if(own!=generation||!focused||paused)return;
                if(joinedRoom!=target)
                {
                    await VivoxService.Instance.JoinGroupChannelAsync(ChannelFor(target),ChatCapability.AudioOnly);
                    joinedRoom=target;
                }
                if(own!=generation||!focused||paused)return;
                VivoxService.Instance.UnmuteInputDevice();MicrophoneOn=true;Status="MICROPHONE ON";
            }
            catch(Exception){Mute();Status="VOICE UNAVAILABLE";}
            finally
            {
                if(!MicrophoneOn&&initialized)
                {try{await DisconnectCoreAsync();}catch(Exception){}}
                Busy=false;
            }
#else
            await Task.CompletedTask;
#endif
        }
        public void Mute()
        {
#if !UNITY_SERVER
            if(initialized)VivoxService.Instance.MuteInputDevice();
#endif
            MicrophoneOn=false;
            if(Status=="MICROPHONE ON")Status="MICROPHONE MUTED";
        }

        public void CopyVoicePlayers(List<VoicePlayer> destination)
        {
            if(destination==null)return;
            destination.Clear();
#if !UNITY_SERVER
            RefreshMuteOwner();
            if(!initialized||joinedRoom!=room||room.Length==0||!VivoxService.Instance.IsLoggedIn)return;
            if(!VivoxService.Instance.ActiveChannels.TryGetValue(ChannelFor(room),out var participants))return;
            foreach(var participant in participants)
            {
                if(participant==null||participant.IsSelf||string.IsNullOrEmpty(participant.PlayerId))continue;
                bool pending=false;
                if(playerMutes.TryGetValue(participant.PlayerId,out bool desired)&&desired!=participant.IsMuted&&
                    muteRequestTimes.TryGetValue(participant.PlayerId,out float requestedAt))
                {
                    pending=Time.realtimeSinceStartup-requestedAt<5f;
                    if(!pending)PlayerMuteStatus="Voice control failed. Please try again.";
                }
                destination.Add(new VoicePlayer(participant.PlayerId,
                    string.IsNullOrWhiteSpace(participant.DisplayName)?"PLAYER":participant.DisplayName,
                    participant.IsMuted,pending));
            }
#endif
        }

        public bool SetPlayerMuted(string playerId,bool muted)
        {
#if !UNITY_SERVER
            RefreshMuteOwner();
            if(!initialized||room.Length==0||joinedRoom!=room||string.IsNullOrEmpty(playerId)||
                !VivoxService.Instance.IsLoggedIn||playerId==AuthenticationService.Instance.PlayerId)return false;
            bool found=false;
            try
            {
                if(!VivoxService.Instance.ActiveChannels.TryGetValue(ChannelFor(room),out var party))return false;
                foreach(var participant in party)
                    if(participant.PlayerId==playerId&&!participant.IsSelf){found=true;break;}
                if(!found)return false;
                // Apply locally in every active channel so a second channel cannot bypass this mute.
                foreach(var channel in VivoxService.Instance.ActiveChannels.Values)
                    foreach(var participant in channel)
                        if(participant.PlayerId==playerId&&!participant.IsSelf)
                        {
                            if(muted)participant.MutePlayerLocally();
                            else participant.UnmutePlayerLocally();
                        }
                playerMutes[playerId]=muted;muteRequestTimes[playerId]=Time.realtimeSinceStartup;PlayerMuteStatus="";
                return true;
            }
            catch(Exception){PlayerMuteStatus="Voice control failed. Please try again.";return false;}
#else
            return false;
#endif
        }
#if !UNITY_SERVER
        private void RefreshMuteOwner()
        {
            if(!initialized)return; // Do not touch Unity Authentication before voice has initialized.
            string owner=AuthenticationService.Instance.IsSignedIn?AuthenticationService.Instance.PlayerId??"":"";
            if(owner==muteOwner)return;
            muteOwner=owner;playerMutes.Clear();muteRequestTimes.Clear();PlayerMuteStatus="";
        }
        private void OnVoiceParticipantAdded(VivoxParticipant participant)
        {
            RefreshMuteOwner();
            if(participant==null||participant.IsSelf||room.Length==0||
                !playerMutes.TryGetValue(participant.PlayerId,out bool muted))return;
            try
            {
                muteRequestTimes[participant.PlayerId]=Time.realtimeSinceStartup;
                if(muted)participant.MutePlayerLocally();
                else participant.UnmutePlayerLocally();
            }
            catch(Exception){PlayerMuteStatus="Voice control failed. Please try again.";}
        }
#endif
        private async Task DisconnectAsync()
        {
            Busy=true;
            try{
#if !UNITY_SERVER
                await DisconnectCoreAsync();
#else
                await Task.CompletedTask;
#endif
            }catch(Exception){}finally{joinedRoom="";Busy=false;}
        }
#if !UNITY_SERVER
        private async Task DisconnectCoreAsync()
        {
            if(!initialized)return;
            VivoxService.Instance.MuteInputDevice();MicrophoneOn=false;
            if(VivoxService.Instance.IsLoggedIn)await VivoxService.Instance.LogoutAsync();
            joinedRoom="";
        }
#endif
        public static string ChannelFor(string id)
        {
            using var hash=SHA256.Create();byte[] bytes=hash.ComputeHash(Encoding.UTF8.GetBytes(id??""));
            var text=new StringBuilder("df_party_");for(int i=0;i<16;i++)text.Append(bytes[i].ToString("x2"));
            return text.ToString();
        }
        private static bool PermissionGranted()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#elif UNITY_IOS && !UNITY_EDITOR
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
            return true;
#endif
        }
        private static async Task<bool> RequestPermissionAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
            if(PermissionGranted())return true;
            var answer=new TaskCompletionSource<bool>();
            var callbacks=new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted+=_=>answer.TrySetResult(true);
            callbacks.PermissionDenied+=_=>answer.TrySetResult(false);
            callbacks.PermissionDeniedAndDontAskAgain+=_=>answer.TrySetResult(false);
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);
            return await Task.WhenAny(answer.Task,Task.Delay(60000))==answer.Task&&await answer.Task;
#elif UNITY_IOS && !UNITY_EDITOR
            var request=Application.RequestUserAuthorization(UserAuthorization.Microphone);
            while(!request.isDone)await Task.Yield();
            return PermissionGranted();
#else
            await Task.CompletedTask;return true;
#endif
        }
        private void OnApplicationFocus(bool value){focused=value;if(!value)Mute();}
        private void OnApplicationPause(bool value){paused=value;if(value)Mute();}
        private void OnDestroy()
        {
            if(Instance!=this)return;
#if !UNITY_SERVER
            if(participantEventsBound)VivoxService.Instance.ParticipantAddedToChannel-=OnVoiceParticipantAdded;
#endif
            participantEventsBound=false;playerMutes.Clear();muteRequestTimes.Clear();generation++;Mute();
            if(!Busy)_=DisconnectAsync();Instance=null;
        }
    }
}
