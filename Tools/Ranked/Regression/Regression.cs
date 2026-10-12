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
 static void Main(){
  IdentityCases();
  var players=new List<OnlineMatchPlan.DedicatedMember>();
  for(int i=0;i<10;i++) players.Add(new(){PlayerId="t"+i,DisplayName="Hero"+i,TeamId=i<5?"red":"blue",Hero=HeroId.Veyr});
  var plan=OnlineMatchPlan.BuildDedicated("match",ArenaVariant.Moon,BotDifficulty.Hard,players,SessionMode.OnlineRanked);
  Check(plan.Session.Mode==SessionMode.OnlineRanked,"mode propagated");
  Check(plan.HumanCount==10,"ten humans");
  foreach(var slot in plan.Session.Slots){Check(!slot.IsBot,"no bot fill");Check(slot.Hero==HeroId.Veyr,"chosen heroes preserved");}
  var client=OnlineMatchPlan.FromAuthoritativeSlots("match",ArenaVariant.Moon,BotDifficulty.Hard,plan.Session.Slots,"t0",SessionMode.OnlineRanked);
  Check(CoopPlanHandshake.Fingerprint(plan)==CoopPlanHandshake.Fingerprint(client),"server/client fingerprint");
  var casual=OnlineMatchPlan.FromAuthoritativeSlots("match",ArenaVariant.Moon,BotDifficulty.Hard,plan.Session.Slots,"t0");
  Check(CoopPlanHandshake.Fingerprint(plan)!=CoopPlanHandshake.Fingerprint(casual),"mode protected by fingerprint");
  Rejected(()=>OnlineMatchPlan.BuildDedicated("bad",ArenaVariant.Moon,BotDifficulty.Hard,players.GetRange(0,9),SessionMode.OnlineRanked),"missing human rejected");
  var wrong=new List<OnlineMatchPlan.DedicatedMember>(players);wrong[9]=new(){PlayerId="t9",TeamId="red"};
  Rejected(()=>OnlineMatchPlan.BuildDedicated("bad",ArenaVariant.Moon,BotDifficulty.Hard,wrong,SessionMode.OnlineRanked),"uneven teams rejected");
  var dup=new List<OnlineMatchPlan.DedicatedMember>(players);dup[9]=dup[0];
  Rejected(()=>OnlineMatchPlan.BuildDedicated("bad",ArenaVariant.Moon,BotDifficulty.Hard,dup,SessionMode.OnlineRanked),"duplicate identity rejected");
  plan.Session.Slots[0].IsBot=true;
  Rejected(()=>OnlineMatchPlan.FromAuthoritativeSlots("bad",ArenaVariant.Moon,BotDifficulty.Hard,plan.Session.Slots,"t1",SessionMode.OnlineRanked),"client rejects bot plan");
  var solo=OnlineMatchPlan.BuildDedicated("casual",ArenaVariant.Moon,BotDifficulty.Hard,players.GetRange(0,1));
  Check(solo.HumanCount==1&&solo.Session.IsFull,"casual bot fill preserved");
  FailureCases().GetAwaiter().GetResult();
  Console.WriteLine(checks+" C# identity and ranked checks passed; engine/network substitutes, not a Unity build.");
 }
}
