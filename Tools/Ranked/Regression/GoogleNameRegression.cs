using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Profile;
using Alterverse.Multiplayer;
using Alterverse.Characters;
using Unity.Netcode;
using Unity.Collections;
using Unity.Services.Multiplayer;
partial class Regression
{
 static void GoogleNameCases()
 {
  var initial=new[]{new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="SolarFox0000001"}};
  AccountProfilePrefs.Attach("google-display",2,initial);
  string alias="Chris Ä龍🎮";
  Check(!PlayerProfile.TryUseGooglePlayName("another-account",alias),"late callback cannot rename another account");
  Check(PlayerProfile.TryUseGooglePlayName("google-display",alias),"Google name saved on matching profile");
  Check(PlayerProfile.PlayerName==alias&&AccountProfilePrefs.GetString("DF_PlayerName")==initial[0].text,"display name preserves canonical account name");
  Check(!PlayerProfile.TryUseGooglePlayName("google-display",alias),"unchanged alias does not resave profile");
  Check(!PlayerProfile.TryUseGooglePlayName("google-display",null)&&PlayerProfile.PlayerName==alias,"empty Google reply keeps previous display");
  var snapshot=new SocialService.Snapshot{me=new SocialService.Player{playerId="google-display",name=alias}};
  SocialService.CleanSnapshot(snapshot);
  Check(snapshot.me.name==alias,"social sanitizer preserves Google name");
  foreach(string bad in new[]{"<color=red>Chris</color>","Chris\nName","Chris\u202eName","\ud800","\udc00"," "})
   Check(SafeIdentityCatalog.NormalizePlayGamesName(bad)==null,"invalid display name rejected");
  Check(SafeIdentityCatalog.NormalizePlayGamesName("  Cafe\u0301  ")=="Café","Google name trims and normalizes Unicode");
  string bounded=SafeIdentityCatalog.NormalizePlayGamesName(new string('龍',40));
  Check(bounded.Length==20&&Encoding.UTF8.GetByteCount(bounded)==60,"multibyte name fits network string");
  string emojis=SafeIdentityCatalog.NormalizePlayGamesName(string.Concat(Enumerable.Repeat("🎮",40)));
  Check(emojis.Length==30&&Encoding.UTF8.GetByteCount(emojis)==60,"emoji truncation preserves surrogate pairs");
  Check(SafeIdentityCatalog.DisplayPlayerName(alias)==alias,"repeated network sanitization preserves alias");
  AccountProfilePrefs.Attach("google-other",2,initial);
  Check(PlayerProfile.PlayerName==initial[0].text,"Google alias cannot leak between cloud accounts");
  AccountProfilePrefs.Attach("google-display",2,initial);
  Check(PlayerProfile.PlayerName==alias,"pending alias restored from same-account cache");
  AccountProfilePrefs.Detach();
  Check(!PlayerProfile.TryUseGooglePlayName("google-display",alias),"detached profile never saves a Google alias");
  GoogleNameNetworkCases(alias);
 }
 static void GoogleNameNetworkCases(string alias)
 {
  ResetLobby();var room=Room(2);Put(OnlinePartyService.Instance,"session",room);Put(CoopLobbyService.Instance,"session",room);
  foreach(Player player in room.Players)
  {
   player.Properties["displayName"]=new PlayerProperty(alias,VisibilityPropertyOptions.Member);
   player.Properties[SafeIdentityCatalog.DisplayNameFormatKey]=new PlayerProperty(SafeIdentityCatalog.DisplayNameFormat,VisibilityPropertyOptions.Member);
  }
  Check(OnlinePartyService.Instance.Members.All(p=>p.DisplayName==alias),"current online lobby preserves Google aliases");
  Check(CoopLobbyService.Instance.Members.All(p=>p.DisplayName==alias),"current co-op lobby preserves Google aliases");
  room.Players[1].Properties.Remove(SafeIdentityCatalog.DisplayNameFormatKey);
  Check(OnlinePartyService.Instance.Members.All(p=>SafeIdentityCatalog.IsPlayerName(p.DisplayName)),"mixed online lobby uses legacy-compatible names");
  Check(CoopLobbyService.Instance.Members.All(p=>SafeIdentityCatalog.IsPlayerName(p.DisplayName)),"mixed co-op lobby uses legacy-compatible names");
  foreach(Type service in new[]{typeof(CoopLobbyService),typeof(OnlinePartyService)})
  {
   var props=(Dictionary<string,PlayerProperty>)service.GetMethod("BuildLocalPlayerProperties",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});
   Check(props[SafeIdentityCatalog.DisplayNameFormatKey].Value==SafeIdentityCatalog.DisplayNameFormat,"new lobby clients advertise name format");
  }
  foreach(bool mixed in new[]{false,true})
  {
   ResetFailures();Environment.SetEnvironmentVariable("DF_RANKED_SERVER","0");
   EdgegapInjectedMatchContext.AssignedTicketCount=2;
   var handshake=Component<DedicatedOnlineMatchHandshake>();
   var manager=new NetworkManager{IsServer=true,IsListening=true};NetworkManager.Singleton=manager;
   Put(handshake,"messaging",manager.CustomMessagingManager);
   for(ulong id=1;id<=2;id++)
   {
    string ticket="google-ticket-"+id;manager.ConnectedClientsIds.Add(id);
    var values=new List<object>{6,new FixedString128Bytes(ticket),new FixedString128Bytes("team"),
     new FixedString128Bytes("fixture-match"),new FixedString128Bytes(ticket),new FixedString64Bytes(alias),
     (int)HeroId.Veyr,(int)BattleItemType.None,new FixedString4096Bytes("")};
    if(!mixed||id==1)values.Add((uint)0x44464E31);
    Invoke(handshake,"OnClientHello",id,new FastBufferReader(values.ToArray()));
   }
   Invoke(handshake,"UpdateDedicatedServer",manager);
   var plan=(OnlineMatchPlan)CoopAuthority.PreparedPlan;
   Check(plan!=null&&plan.HumanCount==2,"both new and legacy hello shapes join dedicated roster");
   Check(plan.Session.Slots.Where(s=>!s.IsBot).All(s=>mixed?SafeIdentityCatalog.IsPlayerName(s.DisplayName):s.DisplayName==alias),"dedicated roster negotiates display format for all clients");
   Check(plan.Session.Slots.Where(s=>s.IsBot).All(s=>SafeIdentityCatalog.IsPlayerName(s.DisplayName)),"bot labels keep legacy-compatible format");
   if(mixed)
   {
    uint before=CoopPlanHandshake.Fingerprint(plan);
    foreach(var slot in plan.Session.Slots)slot.DisplayName=SafeIdentityCatalog.DisplayPlayerName(slot.DisplayName,null,false);
    Check(CoopPlanHandshake.Fingerprint(plan)==before,"legacy name filtering cannot change mixed-match fingerprint");
   }
   var payload=manager.CustomMessagingManager.Sent.First(s=>s.Name=="DF_DEDICATED_PLAN_V1").Values;
   CoopAuthority.PreparedPlan=null;manager.IsServer=false;manager.IsClient=true;
   Invoke(handshake,"OnAuthoritativePlan",(ulong)0,new FastBufferReader(payload));
   var received=(OnlineMatchPlan)CoopAuthority.PreparedPlan;
   Check(received!=null&&CoopPlanHandshake.Fingerprint(received)==CoopPlanHandshake.Fingerprint(plan),"Google-name plan round-trip verifies the exact server fingerprint");
  }
  Put(OnlinePartyService.Instance,"session",null);Put(CoopLobbyService.Instance,"session",null);
  ResetFailures();Environment.SetEnvironmentVariable("DF_RANKED_SERVER","0");
 }
}
