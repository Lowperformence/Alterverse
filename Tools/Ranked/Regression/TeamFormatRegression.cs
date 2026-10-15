using System;
using System.Collections.Generic;
using System.Linq;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Multiplayer;

partial class Regression
{
 static void FullRoster(CoopMatchPlan plan, int humans)
 {
  Check(plan.Session.IsFull && plan.Session.Slots.Length == 8, "complete eight-slot roster");
  Check(plan.HumanCount == humans, "human count preserved");
  Check(plan.Session.Slots.Count(s => !s.IsBot) == humans, "remaining slots filled with bots");
  for (int team = 0; team < 2; team++)
  {
   Check(plan.Session.Slots.Count(s => s.Team == team) == 4, "exactly four heroes per team");
   for (int row = 0; row < MatchSession.TeamSize; row++)
    Check(plan.Session.Slots[team * MatchSession.TeamSize + row].Team == team, "slot/team boundary");
  }
 }

 static void TeamFormatCases()
 {
  // Execute the production offline roster, including its actual spawn loops.
  foreach (HeroId hero in Enum.GetValues(typeof(HeroId)))
  {
   UnityEngine.GameObject.All.Clear();
   var player = new UnityEngine.GameObject("AV_Player");
   player.AddComponent<Alterverse.Combat.Health>();
   PlayerHeroSelection.Selected = hero;
   var roster = new Alterverse.Prototype.FiveVsFiveTestRoster();
   var start = typeof(Alterverse.Prototype.FiveVsFiveTestRoster).GetMethod("Start", Flags);
   start.Invoke(roster, null);
   var actors = UnityEngine.GameObject.All.Where(g => g.GetComponent<HeroIdentity>() != null).ToList();
   Check(actors.Count == 8, "offline starts exactly eight heroes");
   Check(actors.Count(g => g.GetComponent<Alterverse.Combat.TeamMember>().Team == 0) == 4, "local player and three allies");
   Check(actors.Count(g => g.GetComponent<Alterverse.Combat.TeamMember>().Team == 1) == 4, "four offline opponents");
   Check(actors.Where(g => g.GetComponent<Alterverse.Combat.TeamMember>().Team == 0).Select(g => g.GetComponent<HeroIdentity>().Hero).Distinct().Count() == 4, "offline allies exclude selected hero");
   start.Invoke(roster, null);
   Check(UnityEngine.GameObject.All.Count(g => g.GetComponent<HeroIdentity>() != null) == 8, "offline roster cannot spawn twice");
  }
  Check(MatchSession.TeamSize == 4 && MatchSession.MaxPlayers == 8, "4v4 is the shared format");
  var members = new List<CoopLobbyService.MemberView>();
  for (int count = 1; count <= 4; count++)
  {
   members.Add(new() { PlayerId = "p" + count, DisplayName = "Player " + count,
    Hero = (HeroId)(count - 1), IsHost = count == 1, IsLocal = count == 1, Ready = true });
   var plan = CoopMatchPlan.Build("coop-" + count, ArenaVariant.Moon, BotDifficulty.Hard, members);
   FullRoster(plan, count);
   Check(plan.Session.Slots.Take(4).Select(s => s.Hero).Distinct().Count() == 4, "unique allied co-op heroes");
   Check(plan.Session.Slots.Skip(4).All(s => s.IsBot), "co-op opponents remain bots");
   var reversed = members.AsEnumerable().Reverse().ToList();
   FullRoster(CoopMatchPlan.Build("coop-" + count, ArenaVariant.Moon, BotDifficulty.Hard, reversed), count);
   Check(CoopPlanHandshake.Fingerprint(plan) == CoopPlanHandshake.Fingerprint(
    CoopMatchPlan.Build("coop-" + count, ArenaVariant.Moon, BotDifficulty.Hard, reversed)), "stable invite roster ordering");
   var next = CoopMatchPlan.NextRound(plan, "next-" + count, (1 << count) - 1);
   FullRoster(next, count);
   Check(next.Session.Slots[0].IsHost && next.Session.Slots[0].IsLocal, "host/local player survives rematch");
   var disconnected = CoopMatchPlan.NextRound(next, "disconnect-" + count, 1);
   FullRoster(disconnected, 1);
  }
  members.Add(new() { PlayerId = "fifth", Hero = HeroId.Mira });
  Rejected(() => CoopMatchPlan.Build("overflow", ArenaVariant.Moon, BotDifficulty.Hard, members), "fifth co-op human rejected");

  for (int count = 1; count <= 8; count++)
   for (int partyCount = 1; partyCount <= Math.Min(count, 4); partyCount++)
   {
    var online = new List<OnlinePartyService.MemberView>();
    for (int i = 0; i < count; i++) online.Add(new() { PlayerId = "p" + i,
     DisplayName = "Player " + i, Hero = (HeroId)(i % 4), IsPartyMember = i < partyCount,
     IsHost = i == 0, IsLocal = i == 0, Ready = true });
    var plan = OnlineMatchPlan.Build("casual", ArenaVariant.Moon, BotDifficulty.Hard, online);
    FullRoster(plan, count);
    Check(plan.Session.Slots.Take(4).Count(s => !s.IsBot && int.Parse(s.PlayerId.Substring(1)) < partyCount) == partyCount,
     "invited friends stay together");
   }
  var oversizedParty = Enumerable.Range(0, 5).Select(i => new OnlinePartyService.MemberView {
   PlayerId = "party-" + i, IsPartyMember = true, IsHost = i == 0 }).ToList();
  Rejected(() => OnlineMatchPlan.Build("bad-party", ArenaVariant.Moon, BotDifficulty.Hard, oversizedParty), "fifth invited party member rejected");

  for (int red = 0; red <= 4; red++) for (int blue = 0; blue <= 4; blue++)
  {
   if (red + blue == 0) continue;
   var players = new List<OnlineMatchPlan.DedicatedMember>();
   for (int i = 0; i < red + blue; i++) players.Add(new() { PlayerId = "ticket-" + i,
    TeamId = i < red ? "red" : "blue", Hero = HeroId.Veyr });
   var plan = OnlineMatchPlan.BuildDedicated("dedicated", ArenaVariant.Moon, BotDifficulty.Hard, players);
   FullRoster(plan, red + blue);
   var client = OnlineMatchPlan.FromAuthoritativeSlots(plan.MatchId, plan.Arena, plan.Difficulty, plan.Session.Slots, "ticket-0");
   FullRoster(client, red + blue);
   Check(CoopPlanHandshake.Fingerprint(plan) == CoopPlanHandshake.Fingerprint(client), "dedicated/client plan agreement");
  }
  var fiveOnOneTeam = Enumerable.Range(0, 5).Select(i => new OnlineMatchPlan.DedicatedMember {
   PlayerId = "ticket-" + i, TeamId = "red" }).ToList();
  Rejected(() => OnlineMatchPlan.BuildDedicated("bad", ArenaVariant.Moon, BotDifficulty.Hard, fiveOnOneTeam), "fifth dedicated teammate rejected");
  var legacy = new MatchPlayerSlot[10];
  Rejected(() => OnlineMatchPlan.FromAuthoritativeSlots("legacy", ArenaVariant.Moon, BotDifficulty.Hard, legacy, "p0"), "legacy ten-slot wire roster rejected");
  var session = new MatchSession("slots", SessionMode.Private);
  for (int i = 0; i < 8; i++) Check(session.TryAssign(i, new MatchPlayerSlot { PlayerId = "p" + i, Team = i / 4 }), "eight slots accepted");
  Check(!session.TryAssign(4, new MatchPlayerSlot { Team = 0 }), "fifth teammate rejected when replacing enemy slot");
  Check(!session.TryAssign(8, new MatchPlayerSlot { Team = 1 }), "ninth player rejected");
 }
}
