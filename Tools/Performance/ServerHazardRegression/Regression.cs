using System;
using System.Linq;
using UnityEngine;
using Alterverse.Combat;
using Alterverse.Match;
using Alterverse.Multiplayer;

static class Regression
{
 static int checks;static GameObject world;static DimensionsFront front;
 static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
 static void Reset()
 {
  GameObject.All.Clear();Time.frameCount=0;Time.time=0;Time.timeScale=1;CoopAuthority.CanSimulateHostGameplay=true;DedicatedServerNetworkRuntime.Requested=true;
  world=new("DF_MatchWorld");front=world.AddComponent<DimensionsFront>();HotZoneTank.Active.Clear();Physics.Hits=Array.Empty<Collider>();
  MeteorStrike.VisualBuilds=NoxaraMeteorStrike.VisualBuilds=SpaceBombStrike.VisualBuilds=SpaceBomberDirector.VisualBuilds=0;
 }
 static Health Victim(int team=1){var g=new GameObject();var h=g.AddComponent<Health>();g.AddComponent<TeamMember>().Team=team;g.AddComponent<Collider>();return h;}
 static T Last<T>()where T:class=>GameObject.All.Select(g=>g.GetComponent<T>()).Last(x=>x!=null);
 static void Main()
 {
  foreach(bool dedicated in new[]{true,false})TestHazards(dedicated);
  Console.WriteLine($"PASS: {checks} hazard timing, damage and presentation checks (engine fixtures; no native Unity profiling).");
 }
 static void TestHazards(bool dedicated)
 {
  Reset();DedicatedServerNetworkRuntime.Requested=dedicated;Time.time=10;
  var planet=new GameObject().AddComponent<DimensionPlanet>();var victim=Victim();Physics.Hits=new[]{victim.GetComponent<Collider>(),victim.GetComponent<Collider>()};
  MeteorStrike.Create(new(1,0,2),1.55f,14,.32f,planet);var meteor=Last<MeteorStrike>();Check(Math.Abs(meteor.Deadline-11.87f)<.0001f,"planet meteor deadline unchanged");Check(MeteorStrike.VisualBuilds==(dedicated?0:1),"meteor visuals only on visible client");
  Time.time=11.86f;meteor.Tick();Check(victim.HitCount==0,"meteor cannot hit before warning finishes");Time.time=11.88f;meteor.Tick();Check(victim.HitCount==1&&victim.Damage==14,"planet meteor damages multi-collider target once");
  Reset();DedicatedServerNetworkRuntime.Requested=dedicated;Time.time=20;var spider=new GameObject().AddComponent<HotZoneTank>();spider.gameObject.AddComponent<Health>();spider.Team=0;var enemy=Victim(1);var ally=Victim(0);Physics.Hits=new[]{enemy.GetComponent<Collider>(),enemy.GetComponent<Collider>(),ally.GetComponent<Collider>()};
  NoxaraMeteorStrike.Create(new(0,0,0),1.55f,14,.16f,spider);var nox=Last<NoxaraMeteorStrike>();Check(Math.Abs(nox.Deadline-21.71f)<.0001f,"Noxara meteor deadline unchanged");Check(NoxaraMeteorStrike.VisualBuilds==(dedicated?0:1),"Noxara visuals only on visible client");
  Time.time=21.70f;nox.Tick();Check(enemy.HitCount==0,"Noxara warning intact");Time.time=21.72f;Time.timeScale=0;nox.Tick();Check(enemy.HitCount==0,"Noxara respects pause");Time.timeScale=1;nox.Tick();nox.Tick();Check(enemy.HitCount==1&&enemy.Damage==14&&ally.HitCount==0,"Noxara damage, friendly exclusion and one-shot resolution intact");
  Reset();DedicatedServerNetworkRuntime.Requested=dedicated;Time.time=30;var target=Victim();var tank=new GameObject().AddComponent<HotZoneTank>();tank.gameObject.AddComponent<Health>();tank.gameObject.AddComponent<Collider>();HotZoneTank.Active.Add(tank);Physics.Hits=new[]{target.GetComponent<Collider>(),target.GetComponent<Collider>(),tank.GetComponent<Collider>()};
  var director=new GameObject().AddComponent<SpaceBomberDirector>();Check(director.Prepare(),"headless bomber still ready without shaders");Check(SpaceBomberDirector.VisualBuilds==(dedicated?0:1),"headless bomber skips material setup");
  SpaceBombStrike.Create(new(0,0,0),10,1.5f,42,null,null,null,front);var bomb=Last<SpaceBombStrike>();Check(bomb.Deadline==31.5f,"bomb deadline unchanged");Check(SpaceBombStrike.VisualBuilds==(dedicated?0:1),"bomb visuals only on visible client");
  Time.time=31.49f;bomb.Tick();Check(target.HitCount==0,"bomb warning intact");Time.time=31.51f;bomb.Tick();bomb.Tick();Check(target.HitCount==1&&target.Damage==42&&tank.TankHealth.HitCount==1,"bomb multi-collider and monster fallback deduplicate damage");
 }
}
