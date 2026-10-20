using System;using UnityEngine;using Alterverse.Characters;using Alterverse.Combat;using Alterverse.Match;using Alterverse.Multiplayer;using Unity.Collections;
static class AuditRegression
{
 static int checks;
 static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
 static Health Hero(string name,int slot,int team)
 {var g=new GameObject(name);g.AddComponent<HeroIdentity>();g.AddComponent<TeamMember>().Team=team;g.AddComponent<CoopHeroSlot>().Slot=slot;return g.AddComponent<Health>();}
 static Health Minion(string name,float x,int team)
 {var g=new GameObject(name);g.transform.position=new Vector3(x,0,0);g.AddComponent<LaneMinion>().Team=team;g.AddComponent<CoopWorldEntityKey>().Key=name;var h=g.AddComponent<Health>();Health.Active.Add(h);return h;}
 static void Main()
 {
  var telemetry=new GameObject().AddComponent<MatchTelemetry>();typeof(MatchTelemetry).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(telemetry,null);
  Health healer=Hero("healer",0,0),recipient=Hero("recipient",1,0);
  recipient.Heal(40,false,healer.gameObject);
  Check(telemetry.GetStats(healer).Healing==40&&telemetry.GetStats(recipient).Healing==0,"Ally healing credits the healer, not the recipient");
  recipient.Heal(100,false,healer.gameObject);Check(telemetry.GetStats(healer).Healing==50,"Only effective healing is credited, excluding overheal");
  recipient.Heal(40,false,healer.gameObject);Check(telemetry.GetStats(healer).Healing==50,"Healing a full target gives no extra contribution");
  healer.Heal(10,false,null);Check(telemetry.GetStats(healer).Healing==60,"Self healing keeps its source credit");
  var board=new MatchScoreboard();board.Add(healer,"first",0,0,0);board.Add(recipient,"best",10,0,0);
  Check(board.Winner()=="best"&&board.Score(recipient)>0,"Live client scoreboard uses authoritative KDA instead of empty local telemetry");
  CoopMatchResultSync.Results[0]=new(){DamageDealt=30000};CoopMatchResultSync.Results[1]=new(){Kills=10};
  Check(board.Winner()=="first"&&board.Score(healer)==1650,"Final MVP includes complete authoritative damage and support statistics");
  CoopAuthority.LocalHasServerAuthority=true;telemetry.GetStats(healer).Kills=100;telemetry.GetStats(healer).Healing=0;
  Check(board.Winner()=="best","Nonhero quest kills cannot inflate the server MVP score");
  Health intended=Minion("MINION:A",5.8f,1),nearby=Minion("MINION:B",5.05f,1);
  object[] packet={true,2,-1,1,5f,0f,0f,new FixedString128Bytes("MINION:A")};
  Check(CoopGameplayCommands.Probe(packet)==intended,"A moving target retains its identity even when another unit is closer to its old pose");
  intended.IsDead=true;Check(CoopGameplayCommands.Probe(packet)==null,"A dead target cannot silently retarget the nearby live unit");intended.IsDead=false;
  intended.GetComponent<LaneMinion>().Team=0;Check(CoopGameplayCommands.Probe(packet)==null,"A stable key cannot bypass the target team check");intended.GetComponent<LaneMinion>().Team=1;
  packet[7]=new FixedString128Bytes("missing");Check(CoopGameplayCommands.Probe(packet)==null,"Unknown identities reject the target intention");
  var monster=new GameObject().AddComponent<HotZoneTank>();monster.team=0;
  float before=nearby.Current;monster.FireTest(nearby);Check(before-nearby.Current==10,"Hotzone rewards damage selected enemy lane soldiers");
  nearby.GetComponent<LaneMinion>().Team=0;before=nearby.Current;monster.FireTest(nearby);Check(nearby.Current==before,"Friendly soldiers remain immune");
  Health enemy=Hero("enemy",2,1);before=enemy.Current;monster.FireTest(enemy);Check(before-enemy.Current==10,"Enemy hero hit remains unchanged");
  enemy.transform.position=new Vector3(20,0,0);before=enemy.Current;monster.FireTest(enemy);Check(enemy.Current==before,"Hotzone attacks still enforce contact range");
  Console.WriteLine("PASS: "+checks+" audited combat/target/healing/MVP scenarios using production methods; native Unity is a fixture.");
 }
}
