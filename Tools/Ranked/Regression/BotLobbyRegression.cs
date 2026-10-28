using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.AI;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;

partial class Regression
{
 static void BotRosterChecks(IReadOnlyList<MatchPlayerSlot> slots, int humans)
 {
  Check(slots.Count == 8 && slots.Count(p => !p.IsBot) == humans, "lobby has eight roster slots and the exact human count");
  Check(slots.Select(p => p.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 8, "bot aliases do not collide with humans or each other");
  foreach (var bot in slots.Where(p => p.IsBot))
  {
   Check(SafeIdentityCatalog.IsBotName(bot.DisplayName) && !bot.DisplayName.Any(char.IsDigit), "bot handles contain no generated number suffixes");
   Check(SafeIdentityCatalog.IsDisplayName(bot.DisplayName) && bot.Ready, "bot aliases fit the network and bots are ready");
   Check(bot.AvatarId >= 0 && bot.AvatarId < 6, "bots use public starter avatars");
  }
 }
 static async Task BotLobbyCases()
 {
  foreach (bool coop in new[] { false, true })
  {
   ResetLobby();
   var room = Room(1, coop ? 4 : 8);
   room.CurrentPlayer.Properties[SafeIdentityCatalog.DisplayNameFormatKey] = new PlayerProperty(SafeIdentityCatalog.DisplayNameFormat, VisibilityPropertyOptions.Member);
   room.CurrentPlayer.Properties["displayName"].Value = SafeIdentityCatalog.BotName(room.Id, 1);
   room.CurrentPlayer.Properties["avatar"] = new PlayerProperty("17", VisibilityPropertyOptions.Member);
   object service = coop ? (object)CoopLobbyService.Instance : OnlinePartyService.Instance;
   Put(service, "session", room); Invoke(service, "AttachSession", room);
   Func<IReadOnlyList<MatchPlayerSlot>> roster = () => coop ? CoopLobbyService.Instance.LobbySlots : OnlinePartyService.Instance.LobbySlots;
   var initial = roster(); BotRosterChecks(initial, 1);
   Check(initial[0].AvatarId == 17, "remote lobby avatar is independent of this client's inventory");
   Check(object.ReferenceEquals(initial, roster()), "painting reuses the roster snapshot");
   for (int count = 2; count <= 4; count++)
   {
    var guest = LobbyPlayer("p" + count, count - 1);
    guest.Properties[SafeIdentityCatalog.DisplayNameFormatKey] = new PlayerProperty(SafeIdentityCatalog.DisplayNameFormat, VisibilityPropertyOptions.Member);
    guest.Properties["displayName"].Value = "Guest" + count;
    guest.Properties["avatar"] = new PlayerProperty((10 + count).ToString(), VisibilityPropertyOptions.Member);
    room.Add(guest);
    var current = roster(); BotRosterChecks(current, count);
    Check(current[count - 1].PlayerId == guest.Id && !current[count - 1].IsBot && current[count - 1].AvatarId == 10 + count,
     "a real join replaces an existing bot card with the correct name, hero and avatar");
    Check(initial.Count == 8 && initial.Count(p => !p.IsBot) == 1, "joins do not mutate older roster snapshots");
    Check(room.PlayerCount == count && !room.IsLocked, "bots consume no service membership or invitation capacity");
    bool ready = coop ? CoopLobbyService.Instance.AllPresentPlayersReady : OnlinePartyService.Instance.AllPresentPlayersReady;
    Check(ready, "only actual human readiness is required");
   }
   room.Remove("p4");
   var afterLeave = roster(); BotRosterChecks(afterLeave, 3);
   Check(afterLeave[3].IsBot, "leaving restores a ready bot to the free team slot");
   string remainingBot = afterLeave[3].DisplayName;
   room.Players[1].Properties["avatar"].Value = "-5"; room.Notify();
   Check(roster()[1].AvatarId == 0 && roster()[3].DisplayName == remainingBot, "invalid avatar falls back without rerolling bot names");
   if (coop)
   {
    Check(CoopLobbyService.Instance.TryBuildMatchPlan(out var staged), "co-op staging has a complete roster");
    Check(staged.Session.Slots.Select(p => p.DisplayName).SequenceEqual(roster().Select(p => p.DisplayName)), "lobby and match use the same bot aliases");
    room.Players[1].Properties["hero"].Value = room.CurrentPlayer.Properties["hero"].Value; room.Notify();
    Check(roster().Count == 8 && !CoopLobbyService.Instance.CanStageMatch, "hero conflicts keep lobby cards visible while blocking staging");
   }
   else
   {
    var staged = OnlineMatchPlan.Build("new-match", ArenaVariant.Moon, BotDifficulty.Hard, OnlinePartyService.Instance.Members, room.Id);
    Check(staged.Session.Slots.Select(p => p.DisplayName).SequenceEqual(roster().Select(p => p.DisplayName)), "online relay staging preserves lobby aliases");
   }
   // Avatar changes are display-only and never reset a player's Ready choice.
   room.CurrentPlayer.Properties["avatar"].Value = "17";
   Time.unscaledTime = 2; Invoke(service, "Update");
   await Until(() => coop ? !CoopLobbyService.Instance.Busy : !OnlinePartyService.Instance.Busy, "avatar sync");
   Check(room.CurrentPlayer.Properties["avatar"].Value == PlayerProfile.Avatar.ToString() && room.CurrentPlayer.Properties["ready"].Value == "1",
    "equipped avatar refreshes in the lobby without resetting Ready");
   room.CurrentPlayer.Properties["avatar"].Value = "17";
   room.Save = () => Task.FromException(new Exception("fixture outage"));
   Time.unscaledTime = 4; Invoke(service, "Update");
   await Until(() => coop ? !CoopLobbyService.Instance.Busy : !OnlinePartyService.Instance.Busy, "failed avatar sync");
   Check(room.CurrentPlayer.Properties["avatar"].Value == "17", "failed avatar save restores the pending value for retry");
   room.Save = null; Time.unscaledTime = 6; Invoke(service, "Update");
   await Until(() => coop ? !CoopLobbyService.Instance.Busy : !OnlinePartyService.Instance.Busy, "avatar retry");
   Check(room.CurrentPlayer.Properties["avatar"].Value == PlayerProfile.Avatar.ToString(), "avatar sync recovers after a temporary service failure");
  }
  foreach (Type service in new[] { typeof(CoopLobbyService), typeof(OnlinePartyService) })
  {
   var props = (Dictionary<string, PlayerProperty>)service.GetMethod("BuildLocalPlayerProperties", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { false });
   Check(props["avatar"].Value == PlayerProfile.Avatar.ToString(), "create and join publish the equipped avatar");
  }
  // Exercise the real dedicated hello and plan readers/writers, with optional metadata.
  ResetFailures(); Environment.SetEnvironmentVariable("DF_RANKED_SERVER", "0");
  EdgegapInjectedMatchContext.AssignedTicketCount = 1;
  var handshake = Component<DedicatedOnlineMatchHandshake>();
  var manager = new NetworkManager { IsServer = true, IsListening = true }; NetworkManager.Singleton = manager;
  manager.ConnectedClientsIds.Add(1); Put(handshake, "messaging", manager.CustomMessagingManager);
  Invoke(handshake, "OnClientHello", (ulong)1, new FastBufferReader(7,
   new FixedString128Bytes("avatar-ticket"), new FixedString128Bytes("team"), new FixedString128Bytes("match"),
   new FixedString128Bytes("avatar-ticket"), new FixedString64Bytes("Guest"), (int)HeroId.Veyr,
   (int)BattleItemType.None, new FixedString4096Bytes(""), (uint)0x44464E31, (uint)0,
   new FixedString128Bytes("shared-lobby"), 17));
  Invoke(handshake, "UpdateDedicatedServer", manager);
  var plan = (OnlineMatchPlan)CoopAuthority.PreparedPlan;
  BotRosterChecks(plan.Session.Slots, 1);
  Check(plan.Session.Slots[0].AvatarId == 17, "dedicated hello retains the selected avatar");
  var preview = OnlineMatchPlan.Build("shared-lobby", ArenaVariant.Moon, BotDifficulty.Hard,
   new[] { new OnlinePartyService.MemberView { PlayerId = "guest", DisplayName = "Guest", Hero = HeroId.Veyr, IsHost = true, IsPartyMember = true } }, "shared-lobby");
  Check(plan.Session.Slots.Take(4).Select(p => p.DisplayName).SequenceEqual(preview.Session.Slots.Take(4).Select(p => p.DisplayName)), "dedicated teammates retain lobby bot identities");
  var payload = manager.CustomMessagingManager.Sent.First(p => p.Name == "DF_DEDICATED_PLAN_V1").Values;
  CoopAuthority.PreparedPlan = null; manager.IsServer = false; manager.IsClient = true;
  Invoke(handshake, "OnAuthoritativePlan", (ulong)0, new FastBufferReader(payload));
  var received = CoopAuthority.PreparedPlan;
  Check(received != null && received.Session.Slots.Select(p => p.AvatarId).SequenceEqual(plan.Session.Slots.Select(p => p.AvatarId)), "authoritative avatar metadata round-trips");
  Check(CoopPlanHandshake.Fingerprint(received) == CoopPlanHandshake.Fingerprint(plan), "bot aliases survive server/client fingerprint verification");
  CoopAuthority.PreparedPlan = null;
  Invoke(handshake, "OnAuthoritativePlan", (ulong)0, new FastBufferReader(payload.Take(payload.Length - 8).ToArray()));
  Check(CoopAuthority.PreparedPlan != null && CoopAuthority.PreparedPlan.Session.Slots.All(p => p.AvatarId == 0), "old plan without avatars still launches with defaults");
  ResetFailures(); ResetLobby();
 }
}
