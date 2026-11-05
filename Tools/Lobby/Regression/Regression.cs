using System;
using System.Threading.Tasks;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;
using UnityEngine;
using Unity.Netcode;
static class Regression
{
    static int checks;
    static void Check(bool value,string label){checks++;if(!value)throw new Exception(label);}
    static void Call(object obj,string method,params object[] args)=>GameObject.Invoke(obj,method,args);
    static GameObject Actor(int team,Vector3 point,int slot=-1)
    {
        var actor=new GameObject();actor.transform.position=point;actor.AddComponent<HeroIdentity>();
        actor.AddComponent<Health>();actor.AddComponent<TeamMember>().Team=team;
        if(slot>=0)actor.AddComponent<CoopHeroSlot>().Slot=slot;
        return actor;
    }
    static void Nebula()
    {
        GameObject.All.Clear();CoopAuthority.Active=false;CoopAuthority.ActivePlan=null;
        OfflineLobbyRoster.Begin(HeroId.Bolt7);
        var field=new GameObject().AddComponent<CosmicNebulaField>();
        var cloud=CosmicNebulaLayout.Create(OfflineLobbyRoster.MatchId)[0];
        var target=Actor(1,new Vector3(cloud.X,1,cloud.Z));var enemy=Actor(0,new Vector3(0,1,0));
        var ally=Actor(1,new Vector3(0,1,0));var health=target.GetComponent<Health>();
        Check(CombatVisibility.HiddenFrom(enemy,health),"enemy outside cloud loses vision");
        Check(!CombatVisibility.HiddenFrom(ally,health),"allies retain vision");
        enemy.transform.position=target.transform.position;
        Check(!CombatVisibility.HiddenFrom(enemy,health),"entering same cloud gives vision");
        enemy.transform.position=new Vector3(0,1,0);
        LocalHeroContext.Member=enemy.GetComponent<TeamMember>();
        var renderer=target.AddComponent<Renderer>();var conceal=target.AddComponent<CosmicNebulaConcealment>();
        Application.isBatchMode=false;Call(conceal,"LateUpdate");Check(renderer.forceRenderingOff,"hidden hero renderers suppressed");
        renderer.enabled=false;
        CosmicNebulaState.OffensiveAction(target);Call(conceal,"LateUpdate");
        Check(!renderer.forceRenderingOff&&!renderer.enabled,"reveal preserves another system's renderer state");
        Check(!CombatVisibility.HiddenFrom(enemy,health),"attack reveals for enemy");
        Time.time+=2.1f;Check(CombatVisibility.HiddenFrom(enemy,health),"expired reveal hides again");
        Call(conceal,"OnDisable");Check(!renderer.forceRenderingOff,"disabled concealment restores renderer");
        // Owner sees the concealed model at 40% opacity; shared source materials stay untouched.
        var ownRenderer=target.AddComponent<MeshRenderer>();var source=new Material(new Shader());
        source.SetColor("_BaseColor",new Color(.2f,.4f,.6f,1));ownRenderer.sharedMaterials=new[]{source};
        LocalHeroContext.Member=target.GetComponent<TeamMember>();Time.unscaledTime+=1;Call(conceal,"LateUpdate");
        var ghost=ownRenderer.sharedMaterials[0];
        Check(ghost!=source&&ghost.shader.name=="DimensionsFront/VoidKnightStealth"&&Math.Abs(ghost.GetFloat("_StealthOpacity")-.4f)<.001,"owner sees concealed hero using explicit 40% opacity shader");
        Check(Math.Abs(source.GetColor("_BaseColor").a-1)<.001&&Math.Abs(ghost.GetColor("_StealthTint").g-.4f)<.001,"shared material remains unchanged and ghost keeps tint");
        LocalHeroContext.Member=enemy.GetComponent<TeamMember>();Call(conceal,"LateUpdate");
        Check(ownRenderer.forceRenderingOff&&ownRenderer.sharedMaterials[0]==source,"enemy loses visibility immediately when outside cloud");
        LocalHeroContext.Member=target.GetComponent<TeamMember>();Call(conceal,"LateUpdate");
        Check(!ownRenderer.forceRenderingOff&&ownRenderer.sharedMaterials[0]!=source,"owner immediately sees ghost again");
        CosmicNebulaState.OffensiveAction(target);Call(conceal,"LateUpdate");
        Check(ownRenderer.sharedMaterials[0]==source,"attacking reveals owner and restores material");
        Time.time+=2.1f;Time.unscaledTime+=1;Call(conceal,"LateUpdate");
        Check(ownRenderer.sharedMaterials[0]!=source,"hero fades again after reveal expires");
        target.transform.position=new Vector3(0,1,0);Call(conceal,"LateUpdate");
        Check(ownRenderer.sharedMaterials[0]==source,"leaving cloud restores original material");
        target.transform.position=new Vector3(cloud.X,1,cloud.Z);Time.unscaledTime+=1;Call(conceal,"LateUpdate");
        var external=new Material(new Shader());ownRenderer.sharedMaterials=new[]{external};Time.unscaledTime+=1;Call(conceal,"LateUpdate");
        Check(ownRenderer.sharedMaterials[0]!=external,"replacement skin is faded on next scan");
        Call(conceal,"OnDisable");Check(ownRenderer.sharedMaterials[0]==external,"restore preserves a material changed by another system");
        Call(conceal,"LateUpdate");var cloakState=target.AddComponent<VoidKnightState>();var cloak=target.AddComponent<VoidKnightStealth>();
        cloakState.Cloaked=true;Call(conceal,"LateUpdate");Check(ownRenderer.sharedMaterials[0]==external,"nebula releases materials before Void Knight cloak captures them");
        cloak.Applied=true;cloakState.Cloaked=false;Call(conceal,"LateUpdate");Check(ownRenderer.sharedMaterials[0]==external,"nebula waits until cloak has restored its materials");
        cloak.Applied=false;Call(conceal,"LateUpdate");Check(ownRenderer.sharedMaterials[0]!=external,"nebula fades again after cloak restoration");
        Call(conceal,"OnDisable");Check(ownRenderer.sharedMaterials[0]==external,"final restore recovers replacement skin");
        Application.isBatchMode=true;
        // Actual message writer/reader code, with server identity and time-aging checks.
        GameObject.All.Clear();CoopAuthority.Active=true;CoopAuthority.ActivePlan=new MatchPlan{MatchId="wire"};
        var server=new NetworkManager{IsServer=true};server.ServerTime.Time=100;NetworkManager.Singleton=server;
        var serverField=new GameObject().AddComponent<CosmicNebulaField>();
        var sender=Actor(1,new Vector3(),3);CosmicNebulaState.OffensiveAction(sender);
        Check(server.CustomMessagingManager.Sent.Count==1,"authoritative reveal sent");
        var packet=server.CustomMessagingManager.Sent[0];
        GameObject.All.Clear();var client=new NetworkManager();client.ServerTime.Time=100.25;NetworkManager.Singleton=client;
        var clientField=new GameObject().AddComponent<CosmicNebulaField>();var replica=Actor(1,new Vector3(),3);
        var handler=client.CustomMessagingManager.Handlers["DF_NEBULA_REVEAL_V1"];
        handler(7,new FastBufferReader(packet));Check(replica.GetComponent<CosmicNebulaState>()==null,"client cannot forge reveal");
        handler(0,new FastBufferReader(packet));
        var state=replica.GetComponent<CosmicNebulaState>();Check(Math.Abs(state.RevealRemaining-1.75f)<.001,"network delay ages reveal");
        handler(0,new FastBufferReader(3,99d,2f));Check(Math.Abs(state.RevealRemaining-1.75f)<.001,"old packet cannot restart reveal");
        handler(0,new FastBufferReader(3,101d,float.NaN));Check(Math.Abs(state.RevealRemaining-1.75f)<.001,"invalid duration ignored");
        Call(clientField,"OnDestroy");Check(!client.CustomMessagingManager.Handlers.ContainsKey("DF_NEBULA_REVEAL_V1"),"network handler removed");
        CoopAuthority.Active=false;
    }
    static async Task Voice()
    {
#if !UNITY_SERVER
        var sdk=Unity.Services.Vivox.VivoxService.Instance=new();
        var party=OnlinePartyService.Instance;party.VoiceRoomId="private-session-1";
        var voice=new GameObject().AddComponent<PartyVoiceService>();Call(voice,"Update");
        Check(voice.NeedsConsent&&sdk.Initializations==0,"party prompts without initializing microphone");
        voice.Decline();Check(!voice.NeedsConsent&&sdk.Initializations==0,"decline never initializes voice");
        voice.AskAgain();await voice.EnableAsync();
        Check(voice.MicrophoneOn&&!sdk.Muted&&sdk.Channel==PartyVoiceService.ChannelFor(party.VoiceRoomId),"explicit opt-in joins private party voice");
        var players=new System.Collections.Generic.List<PartyVoiceService.VoicePlayer>();
        string channel=sdk.Channel;
        var self=new Unity.Services.Vivox.VivoxParticipant{PlayerId="local-player",DisplayName="Same Name",IsSelf=true,ChannelName=channel};
        var remote=new Unity.Services.Vivox.VivoxParticipant{PlayerId="remote-a",DisplayName="Same Name",ChannelName=channel};
        var other=new Unity.Services.Vivox.VivoxParticipant{PlayerId="remote-b",DisplayName="Same Name",ChannelName=channel};
        var duplicate=new Unity.Services.Vivox.VivoxParticipant{PlayerId="remote-a",DisplayName="Same Name",ChannelName="second-channel"};
        sdk.Add(self);sdk.Add(remote);sdk.Add(other);sdk.Add(duplicate);voice.CopyVoicePlayers(players);
        Check(players.Count==2,"voice player list excludes self and unrelated channel entries");
        Check(!voice.SetPlayerMuted(null,true)&&!voice.SetPlayerMuted("local-player",true)&&!voice.SetPlayerMuted("unknown",true),"invalid, self and unknown mute targets rejected");
        int inputUnmutes=sdk.Unmutes;bool inputMuted=sdk.Muted;
        Check(voice.SetPlayerMuted("remote-a",true)&&remote.IsMuted&&duplicate.IsMuted&&!other.IsMuted&&!self.IsMuted,"mute selects identity across channels without muting a same-name player");
        Check(sdk.Unmutes==inputUnmutes&&sdk.Muted==inputMuted&&voice.MicrophoneOn,"remote mute never changes microphone state");
        voice.CopyVoicePlayers(players);Check(players[0].Muted&&!players[0].Pending,"UI reads confirmed native mute state");
        var rejoined=new Unity.Services.Vivox.VivoxParticipant{PlayerId="remote-a",DisplayName="Renamed",ChannelName=channel};
        sdk.ActiveChannels[channel].Remove(remote);sdk.Add(rejoined);
        Check(rejoined.IsMuted,"muted player stays muted after leaving/rejoining with a changed name");
        Check(voice.SetPlayerMuted("remote-a",false)&&!rejoined.IsMuted&&!duplicate.IsMuted,"unmute restores only the selected player across channels");
        // Exercise the real in-match menu route and click a particular duplicate-name player's row.
        var front=new GameObject().AddComponent<Alterverse.Match.DimensionsFront>();
        var options=new GameObject().AddComponent<Alterverse.UI.InMatchSettingsMenu>();
        Call(options,"OnGUI");CoopAuthority.Active=true;Call(options,"OpenOverlay");
        Alterverse.UI.FrontEndTheme.Click="VOICE / MUTE";Alterverse.UI.FrontEndTheme.Labels.Clear();
        Call(options,"DrawOverlay",new Alterverse.UI.CombatHudLayout(new Rect(0,0,960,640)));
        Check(Alterverse.UI.FrontEndTheme.Labels.Contains("VOICE PLAYERS")&&!Alterverse.Match.MatchClock.Paused,"settings voice tab opens without pausing online play");
        Alterverse.UI.FrontEndTheme.Click="MUTE PLAYER";Alterverse.UI.FrontEndTheme.ClickY=4;
        Call(options,"DrawVoicePlayers",new Rect(0,0,620,300));
        Check(other.IsMuted&&!rejoined.IsMuted,"per-player UI button mutes the exact row by identity, not display name");
        Alterverse.UI.FrontEndTheme.Click="UNMUTE PLAYER";Alterverse.UI.FrontEndTheme.ClickY=4;
        Call(options,"DrawVoicePlayers",new Rect(0,0,620,170));
        Check(!other.IsMuted&&GUI.ScrollContent.height==92&&GUI.ScrollViewport.height==70,"scrollable voice roster unmutes selected row on a short screen");
        Call(options,"CloseOverlay");CoopAuthority.Active=false;
        rejoined.DelayMute=true;voice.SetPlayerMuted("remote-a",true);voice.CopyVoicePlayers(players);
        Check(!players[1].Muted&&players[1].Pending,"pending native change is not falsely displayed as confirmed mute");
        float requestTime=Time.realtimeSinceStartup;Time.realtimeSinceStartup+=6;voice.CopyVoicePlayers(players);
        Check(!players[1].Pending&&voice.PlayerMuteStatus.Length>0,"unconfirmed native mute times out so controls never stay disabled forever");
        Time.realtimeSinceStartup=requestTime;
        rejoined.IsMuted=true;voice.CopyVoicePlayers(players);Check(players[1].Muted&&!players[1].Pending,"native confirmation releases pending state");
        rejoined.DelayMute=false;rejoined.Fail=true;
        Check(!voice.SetPlayerMuted("remote-a",false)&&voice.PlayerMuteStatus.Length>0,"native mute failure is contained and visible");rejoined.Fail=false;
        var ownerChange=new Unity.Services.Vivox.VivoxParticipant{PlayerId="remote-a",ChannelName=channel};
        Unity.Services.Authentication.AuthenticationService.Instance.PlayerId="different-local-player";sdk.Add(ownerChange);
        Check(!ownerChange.IsMuted,"local account switch cannot inherit another user's mute preferences");
        Unity.Services.Authentication.AuthenticationService.Instance.PlayerId="local-player";
        Call(voice,"OnApplicationFocus",false);Check(!voice.MicrophoneOn&&sdk.Muted,"loss of focus immediately mutes");
        Call(voice,"OnApplicationFocus",true);Check(!voice.MicrophoneOn,"focus return does not silently unmute");
        await voice.EnableAsync();Check(voice.MicrophoneOn,"explicit re-enable works");
        party.VoiceRoomId="";Call(voice,"Update");
        Check(sdk.Muted&&!sdk.IsLoggedIn&&!voice.NeedsConsent,"leaving party mutes and logs out");
        voice.CopyVoicePlayers(players);Check(players.Count==0&&!voice.SetPlayerMuted("remote-a",true),"leaving party removes voice controls and rejects stale mute targets");
        party.VoiceRoomId="private-session-2";Call(voice,"Update");
        sdk.JoinPending=new TaskCompletionSource<bool>();var pending=voice.EnableAsync();
        int unmuted=sdk.Unmutes;party.VoiceRoomId="";Call(voice,"Update");sdk.JoinPending.SetResult(true);await pending;
        Check(sdk.Unmutes==unmuted&&!voice.MicrophoneOn&&!sdk.IsLoggedIn,"late join completion cannot unmute after leaving");
        sdk.JoinPending=null;party.VoiceRoomId="private-session-3";Call(voice,"Update");
#if UNITY_ANDROID
        UnityEngine.Android.Permission.Granted=false;UnityEngine.Android.Permission.Answer=false;
        int initialized=sdk.Initializations;await voice.EnableAsync();
        Check(sdk.Initializations==initialized&&!voice.MicrophoneOn&&UnityEngine.Android.Permission.Requests==1,"permission denial keeps capture off");
        UnityEngine.Android.Permission.Answer=true;
#endif
        sdk.Fail=true;await voice.EnableAsync();Check(!voice.MicrophoneOn&&voice.Status=="VOICE UNAVAILABLE","service failure leaves match usable and mic off");
        Check(PartyVoiceService.ChannelFor("a")==PartyVoiceService.ChannelFor("a")&&PartyVoiceService.ChannelFor("a")!=PartyVoiceService.ChannelFor("b"),"voice channels isolated by party");
        Call(voice,"OnDestroy");
        Check(sdk.ParticipantHandlers==0,"voice participant handler removed on service destruction");
#else
        Check(PartyVoiceService.Instance==null,"dedicated server never initializes voice");
        await Task.CompletedTask;
#endif
    }
    static async Task Sharing()
    {
        Check(Alterverse.UI.GameInviteShare.BuildMessage(null).Contains("https://play.google.com/store/apps/details?id=com.lowperformence.dimensionsfront"),"correct public game link without authentication");
        Check(!Alterverse.UI.GameInviteShare.BuildMessage("bad\nhttps://evil.invalid").Contains("evil"),"invalid invitation text cannot inject another link");
        var party=OnlinePartyService.Instance;party.InParty=true;party.JoinCode="ABCD12";
        var operation=Alterverse.UI.GameInviteShare.ShareAsync();
#if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_SERVER
        Check(Alterverse.UI.GameInviteShare.Busy&&!AndroidJavaObject.Activity.Disposed,"JNI activity retained until UI dispatch");
        Check(!await Alterverse.UI.GameInviteShare.ShareAsync(),"repeated tap does not launch duplicate chooser");
        AndroidJavaObject.Ui.Dequeue()();Check(await operation,"Android chooser launched");
        Check(AndroidJavaObject.Started==1&&AndroidJavaObject.Shared.Contains("ABCD12"),"share includes party code");
        Check(AndroidJavaObject.Activity.Disposed&&!Alterverse.UI.GameInviteShare.Busy,"JNI references released after dispatch");
#else
        await operation;Check(GUIUtility.systemCopyBuffer.Contains("ABCD12"),"desktop invitation copied for WhatsApp");
#endif
        party.InParty=false;
    }
    static async Task Main(){Nebula();await Voice();await Sharing();Console.WriteLine("PASS "+checks+" nebula/voice lifecycle checks (controlled engine and SDK boundaries).");}
}
