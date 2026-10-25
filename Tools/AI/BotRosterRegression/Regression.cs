using System;
using System.Linq;
using System.Collections.Generic;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Prototype;
class Regression
{
 static int checks;
 static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
 static void Valid(CoopMatchPlan plan,int humans)
 {
  Check(plan.Session.IsFull&&plan.HumanCount==humans,"full roster and expected humans");
  Check(plan.MatchId==plan.Session.SessionId,"match id is canonical random seed");
  for(int team=0;team<2;team++)Check(plan.Session.Slots.Where(s=>s.Team==team).Select(s=>s.Hero).Distinct().Count()==MatchSession.TeamSize,"no same-team duplicates");
 }
 static void Main()
 {
  var coopSeen=new HashSet<HeroId>();var onlineSeen=new HashSet<HeroId>();var dedicatedSeen=new HashSet<HeroId>();var offlineSeen=new HashSet<HeroId>();var nextSeen=new HashSet<HeroId>();
  string first=null;bool varied=false,roundVaried=false;
  for(int match=0;match<100;match++)
  {
   string id="match-"+match;var order=BotHeroPool.Shuffled(id,0);
   Check(order.Distinct().Count()==Enum.GetValues(typeof(HeroId)).Length,"every enum hero is in bot catalog");
   Check(order.SequenceEqual(BotHeroPool.Shuffled(id,0)),"deterministic across peers");
   var used=new HashSet<HeroId>{HeroId.Bolt7};int cursor=0;
   for(int i=0;i<order.Length-1;i++){var hero=BotHeroPool.Next(order,used,ref cursor);Check(used.Add(hero),"draw excludes existing teammate");}
   var allies=OfflineRosterFixture.BuildAllies(HeroId.Bolt7,id);Check(allies.Length==3&&allies.Distinct().Count()==3&&!allies.Contains(HeroId.Bolt7),"offline allies exclude human and duplicates");foreach(var hero in allies)offlineSeen.Add(hero);
   var coop=CoopMatchPlan.Build(id,ArenaVariant.Moon,BotDifficulty.Normal,new[]{new CoopLobbyService.MemberView{PlayerId="host",Hero=HeroId.Bolt7,IsHost=true,IsLocal=true},new CoopLobbyService.MemberView{PlayerId="friend",Hero=HeroId.Nyra}});
   Valid(coop,2);Check(coop.Host.Hero==HeroId.Bolt7&&coop.Friend.Hero==HeroId.Nyra,"co-op human choices preserved");foreach(var s in coop.Session.Slots.Where(s=>s.IsBot))coopSeen.Add(s.Hero);
   var online=OnlineMatchPlan.Build(id,ArenaVariant.Moon,BotDifficulty.Easy,new[]{new OnlinePartyService.MemberView{PlayerId="host",Hero=HeroId.Bolt7,IsHost=true,IsPartyMember=true,IsLocal=true}});
   Valid(online,1);foreach(var s in online.Session.Slots.Where(s=>s.IsBot))onlineSeen.Add(s.Hero);
   var dedicated=OnlineMatchPlan.BuildDedicated(id,ArenaVariant.Moon,BotDifficulty.Normal,new[]{new OnlineMatchPlan.DedicatedMember{PlayerId="one",TeamId="a",Hero=HeroId.Bolt7},new OnlineMatchPlan.DedicatedMember{PlayerId="two",TeamId="b",Hero=HeroId.Nyra}});
   Valid(dedicated,2);foreach(var s in dedicated.Session.Slots.Where(s=>s.IsBot))dedicatedSeen.Add(s.Hero);
   var copy=OnlineMatchPlan.FromAuthoritativeSlots(id,ArenaVariant.Moon,BotDifficulty.Normal,dedicated.Session.Slots,"one");Check(copy.Session.Slots.Select(s=>s.Hero).SequenceEqual(dedicated.Session.Slots.Select(s=>s.Hero)),"clients preserve exact server draw including premium heroes");
   var next=CoopMatchPlan.NextRound(coop,id+"-next",3);Valid(next,2);Check(next.Host.Hero==coop.Host.Hero&&next.Friend.Hero==coop.Friend.Hero,"next round preserves human heroes");foreach(var s in next.Session.Slots.Where(s=>s.IsBot))nextSeen.Add(s.Hero);
   roundVaried|=!next.Session.Slots.Select(s=>s.Hero).SequenceEqual(coop.Session.Slots.Select(s=>s.Hero));
   var afterLeave=CoopMatchPlan.NextRound(coop,id+"-leave",1);Valid(afterLeave,1);Check(afterLeave.Session.Slots[1].IsBot,"leaver replaced by full-catalog bot");
   string roster=string.Join(",",online.Session.Slots.Select(s=>s.Hero));if(first==null)first=roster;else varied|=roster!=first;
  }
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId))){Check(coopSeen.Contains(hero)&&onlineSeen.Contains(hero)&&dedicatedSeen.Contains(hero)&&nextSeen.Contains(hero),"all match paths can draw "+hero);if(hero!=HeroId.Bolt7)Check(offlineSeen.Contains(hero),"offline can draw "+hero);}
  Check(varied&&roundVaried,"new matches and rounds produce varied rosters");
  bool rejected=false;try{OnlineMatchPlan.BuildDedicated("ranked",ArenaVariant.Moon,BotDifficulty.Normal,new[]{new OnlineMatchPlan.DedicatedMember{PlayerId="one",TeamId="a",Hero=HeroId.Bolt7}},SessionMode.OnlineRanked);}catch(InvalidOperationException){rejected=true;}Check(rejected,"ranked still requires full human lobby");
  Console.WriteLine("PASS "+checks+" full-catalog bot draw/offline/co-op/online/dedicated/next-round checks; service/network transport is a fixture.");
 }
}
