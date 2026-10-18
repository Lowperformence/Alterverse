using System;
using System.Collections.Generic;
using Alterverse.Multiplayer;
using Alterverse.Characters;
using Alterverse.AI;
using Alterverse.Match;
partial class Regression {
 static int checks;
 static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static void Rejected(Action a,string message){bool rejected=false;try{a();}catch(InvalidOperationException){rejected=true;}Check(rejected,message);}
 static void ArenaPoolCases(){
  const uint moon=1u<<(int)ArenaVariant.Moon, mars=1u<<(int)ArenaVariant.Mars, earth=1u<<(int)ArenaVariant.Earth;
  uint free=OnlineArenaPool.Normalize(0u);
  Check((free&moon)!=0u&&(free&mars)!=0u,"free maps always in online pool");
  Check((free&earth)==0u,"premium map not included without purchase");
  Check(OnlineArenaPool.Normalize(1u<<31)==free,"unknown arena bits rejected");
  Alterverse.Shop.ArenaUnlockInventory.Unlocked.Clear();
  Check((OnlineArenaPool.LocalOwnedMask()&earth)==0u,"locked local map omitted");
  Alterverse.Shop.ArenaUnlockInventory.Unlocked.Add(ArenaVariant.Earth);
  uint buyerMask=OnlineArenaPool.LocalOwnedMask();
  Check((buyerMask&earth)!=0u,"purchased arena reported");
  uint otherPlayerMask=free;
  uint union=buyerMask|otherPlayerMask;
  Check((union&earth)!=0u,"one owner's purchased map enters collective match pool");
  UnityEngine.Random.TestIndex=2;
  Check(OnlineArenaPool.Draw(union)==ArenaVariant.Earth,"server can draw premium pooled arena");
  UnityEngine.Random.TestIndex=0;
  Check(OnlineArenaPool.Draw(0u)==ArenaVariant.Moon,"empty reported pool still draws a free map");
  Alterverse.Shop.ArenaUnlockInventory.Unlocked.Clear();
 }
 static void Main(){
  ArenaPoolCases();
  LobbyCases().GetAwaiter().GetResult();
  LifecycleCases().GetAwaiter().GetResult();
  IdentityCases();
  GoogleNameCases();
  TeamFormatCases();
  FingerprintCases();
  var players=new List<OnlineMatchPlan.DedicatedMember>();
  for(int i=0;i<MatchSession.MaxPlayers;i++) players.Add(new(){PlayerId="t"+i,DisplayName="Hero"+i,TeamId=i<MatchSession.TeamSize?"red":"blue",Hero=HeroId.Veyr});
  var plan=OnlineMatchPlan.BuildDedicated("match",ArenaVariant.Moon,BotDifficulty.Hard,players,SessionMode.OnlineRanked);
  Check(plan.Session.Mode==SessionMode.OnlineRanked,"mode propagated");
  Check(plan.HumanCount==8,"eight humans");
  foreach(var slot in plan.Session.Slots){Check(!slot.IsBot,"no bot fill");Check(slot.Hero==HeroId.Veyr,"chosen heroes preserved");}
  var client=OnlineMatchPlan.FromAuthoritativeSlots("match",ArenaVariant.Moon,BotDifficulty.Hard,plan.Session.Slots,"t0",SessionMode.OnlineRanked);
  Check(CoopPlanHandshake.Fingerprint(plan)==CoopPlanHandshake.Fingerprint(client),"server/client fingerprint");
  var casual=OnlineMatchPlan.FromAuthoritativeSlots("match",ArenaVariant.Moon,BotDifficulty.Hard,plan.Session.Slots,"t0");
  Check(CoopPlanHandshake.Fingerprint(plan)!=CoopPlanHandshake.Fingerprint(casual),"mode protected by fingerprint");
  Rejected(()=>OnlineMatchPlan.BuildDedicated("bad",ArenaVariant.Moon,BotDifficulty.Hard,players.GetRange(0,MatchSession.MaxPlayers-1),SessionMode.OnlineRanked),"missing human rejected");
  var wrong=new List<OnlineMatchPlan.DedicatedMember>(players);wrong[MatchSession.MaxPlayers-1]=new(){PlayerId="t9",TeamId="red"};
  Rejected(()=>OnlineMatchPlan.BuildDedicated("bad",ArenaVariant.Moon,BotDifficulty.Hard,wrong,SessionMode.OnlineRanked),"uneven teams rejected");
  var dup=new List<OnlineMatchPlan.DedicatedMember>(players);dup[MatchSession.MaxPlayers-1]=dup[0];
  Rejected(()=>OnlineMatchPlan.BuildDedicated("bad",ArenaVariant.Moon,BotDifficulty.Hard,dup,SessionMode.OnlineRanked),"duplicate identity rejected");
  plan.Session.Slots[0].IsBot=true;
  Rejected(()=>OnlineMatchPlan.FromAuthoritativeSlots("bad",ArenaVariant.Moon,BotDifficulty.Hard,plan.Session.Slots,"t1",SessionMode.OnlineRanked),"client rejects bot plan");
  var solo=OnlineMatchPlan.BuildDedicated("casual",ArenaVariant.Moon,BotDifficulty.Hard,players.GetRange(0,1));
  Check(solo.HumanCount==1&&solo.Session.IsFull,"casual bot fill preserved");
  FailureCases().GetAwaiter().GetResult();
  Console.WriteLine(checks+" C# lobby, identity and ranked checks passed; engine/network substitutes, not a Unity build.");
 }
}

