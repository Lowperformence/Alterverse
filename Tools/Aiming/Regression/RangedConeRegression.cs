using System;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;

partial class Regression
{
 public static void RunRangedConeCases()
 {
  int attacks=0;
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
   for(int slot=-1;slot<4;slot++)
   {
    AimSpec spec=AimingSettings.Current.For(hero,slot);
    if(spec.aimType!=AimType.Projectile&&spec.aimType!=AimType.Beam&&spec.aimType!=AimType.Homing)continue;
    attacks++;
    foreach(int fps in new[]{30,60,120})
    {
     // The body at the wedge edge is deliberately clear of the old projectile line.
     Reset();var actor=Hero(hero,0,Vector3.zero);
     float distance=spec.castRange*.85f;
     var enemy=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.5f-1,distance));
     float before=Total(enemy);
     Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"ranged cone accepted "+hero+slot);
     float windup=slot<0?HeroAttackDelivery.Windup(hero):
      HeroAttackDelivery.AbilityWindup(hero,slot,actor.GetComponent<HeroAbilityKit>().Spec(slot));
     for(int i=0;i<Mathf.CeilToInt(windup*fps)+1;i++)Step(1f/fps);
     Check(Total(enemy)<before,"wedge-edge hit at release without projectile travel "+hero+slot+" FPS "+fps);
     Run(1);
     float expected=slot<0?actor.GetComponent<HeroCombatProfile>().Attack:
      HeroAbilityKit.LoadoutFor(hero).Spec(slot).Power;
     Near(before-Total(enemy),expected,"one complete attack, including volley "+hero+slot);

     // An empty cone stays active for 0.2s even with a very high cosmetic speed.
     Reset();actor=Hero(hero,0,Vector3.zero);
     var fast=spec;fast.projectileSpeed=10000;
     AimingSettings.Current.overrides=new[]{new AimingSettings.AttackOverride{hero=hero,slot=slot,spec=fast}};
     enemy=Hero(HeroId.Kaelon,1,new Vector3(20,0,distance));
     Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"empty fast visual casts "+hero+slot);
     for(int i=0;i<Mathf.CeilToInt(windup*fps)+1;i++)Step(1f/fps);
     Check(HeroAttackDelivery.For(actor).TryActiveCone(out _,out _,out _),"cone survives cosmetic endpoint "+hero+slot);
     Step(1f/fps,()=>enemy.transform.position=Angle(spec.coneAngle*.5f-1,distance));
     Check(Total(enemy)<1000,"late entry hits independently of flight speed "+hero+slot);

     // A centre-line wall may not erase a clear side of the wedge.
     Reset();actor=Hero(hero,0,Vector3.zero);
     enemy=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.5f-1,distance));
     var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=GameplayCollision.Structures;
     wall.transform.position=new Vector3(0,.5f,distance*.55f);wall.transform.localScale=new Vector3(.12f,2,.15f);
     Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"off-axis cone casts");
     Run(1);Check(Total(enemy)<1000,"central visual obstacle does not block clear cone edge "+hero+slot);

     // Cover on the target ray, an ally and an out-of-range body remain protected.
     Reset();actor=Hero(hero,0,Vector3.zero);enemy=Hero(HeroId.Kaelon,1,Vector3.forward*distance);
     wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.layer=GameplayCollision.Structures;
     wall.transform.position=new Vector3(0,.5f,distance*.5f);wall.transform.localScale=new Vector3(2,2,.3f);
     Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"covered cast accepted");
     Run(1);Near(Total(enemy),1000,"target cover blocks cone "+hero+slot);
     Reset();actor=Hero(hero,0,Vector3.zero);enemy=Hero(HeroId.Kaelon,0,Vector3.forward*distance);
     Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"ally-only cone casts");
     Run(1);Near(Total(enemy),1000,"allies excluded "+hero+slot);
     Reset();actor=Hero(hero,0,Vector3.zero);enemy=Hero(HeroId.Kaelon,1,Vector3.forward*(spec.castRange+2));
     Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"out-of-range cone casts");
     Run(1);Near(Total(enemy),1000,"range retained "+hero+slot);
    }
    PriorityCone(hero,slot,spec);
    NetworkRangedCone(hero,slot,spec);
   }
  // Old asset overrides cannot re-enable projectile collision.
  foreach(var mode in new[]{TargetingMode.Geometry,TargetingMode.Homing})
  {
   Reset();var spec=AimingSettings.Current.For(HeroId.Traveler,0);spec.targetingMode=mode;
   AimingSettings.Current.overrides=new[]{new AimingSettings.AttackOverride{hero=HeroId.Traveler,slot=0,spec=spec}};
   Check(AimingSettings.Current.For(HeroId.Traveler,0).targetingMode==TargetingMode.Cone,"legacy override normalized "+mode);
  }
  foreach(HeroId hero in new[]{HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Traveler})
  {
   Reset();var a=Hero(hero,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,Vector3.forward*4);
   Check(a.GetComponent<BasicAttack>().TryAttack(),"automatic basic starts "+hero);
   Run(HeroAttackDelivery.Windup(hero)+.02f);
   Check(Total(b)<1000,"automatic ranged basic uses release cone "+hero);
  }
  Reset();var shooter=Hero(HeroId.Nyra,0,Vector3.zero);
  var weak=Hero(HeroId.Kaelon,1,Vector3.forward*4);weak.GetComponent<Health>().TakeDamage(970);
  var other=Hero(HeroId.Kaelon,1,Angle(10,4));
  Check(RangedCast(shooter,3,Forward(shooter,7)),"single-target volley starts");
  Run(1);Check(weak.GetComponent<Health>().IsDead,"volley finishes weak victim");
  Near(Total(other),1000,"remaining volley shots cannot switch to a second victim");
  Check(RangedCast(shooter,0,Forward(shooter,7)),"next independent attack can reacquire");
  Run(1);Check(Total(other)<1000,"next attack acquires surviving enemy");

  Reset();var mage=Hero(HeroId.Elyra,0,Vector3.zero);
  var aoeLeft=Hero(HeroId.Kaelon,1,new Vector3(-.4f,0,4));
  var aoeRight=Hero(HeroId.Kaelon,1,new Vector3(.4f,0,4));
  Check(RangedCast(mage,0,new AimPose(Vector3.forward,Vector3.forward*4,null,true)),"AOE casts");
  Run(1);Check(Total(aoeLeft)<1000&&Total(aoeRight)<1000,"actual AOE retains multiple victims");
  Reset();EmptyConeNetwork(30);EmptyConeNetwork(60);EmptyConeNetwork(120);
  Console.WriteLine("PASS "+checks+" assertions across "+attacks+" ranged attack classes, 30/60/120 FPS, cone edges, flight independence, cover, ranges, overrides and server commands.");
 }
 static void PriorityCone(HeroId hero,int slot,AimSpec spec)
 {
  Reset();var actor=Hero(hero,0,Vector3.zero);
  var close=Hero(HeroId.Kaelon,1,Vector3.forward*(spec.castRange*.45f));
  var wounded=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.4f,spec.castRange*.75f));
  wounded.GetComponent<Health>().TakeDamage(800);
  var minion=new GameObject("nearly dead fallback");minion.transform.position=Vector3.forward;
  minion.AddComponent<TeamMember>().Initialize(1);minion.AddComponent<Health>().Configure(1);
  var selected=AimGeometry.CaptureConeTarget(actor,Vector3.forward,spec);
  Check(selected==wounded.GetComponent<Health>(),"preview selects low-HP hero above closer hero/minion "+hero+slot);
  Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"priority attack starts");
  Run(1);
  Check(Total(wounded)<200,"single attack chooses lowest-life hero "+hero+slot);
  Near(Total(close),1000,"second hero receives no split/splash damage "+hero+slot);
  Near(Total(minion),1,"hero priority beats critically wounded fallback "+hero+slot);
  var hit=Total(wounded);Run(.5f);Near(Total(wounded),hit,"cone resolves only once "+hero+slot);

  Reset();actor=Hero(hero,0,Vector3.zero);
  close=Hero(HeroId.Kaelon,1,Vector3.forward*(spec.castRange*.45f));
  wounded=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.4f,spec.castRange*.75f));
  Check(RangedCast(actor,slot,Forward(actor,spec.castRange)),"tie attack starts");
  Run(1);Check(Total(close)<1000,"equal-priority equal-HP uses nearest "+hero+slot);
  Near(Total(wounded),1000,"equal-HP distant hero is not also damaged "+hero+slot);
  if(slot<0)
  {
   Reset();actor=Hero(hero,0,Vector3.zero);
   close=Hero(HeroId.Kaelon,1,Vector3.forward*2);
   wounded=Hero(HeroId.Kaelon,1,Vector3.forward*(spec.castRange*.8f));
   wounded.GetComponent<Health>().TakeDamage(800);
   Check(actor.GetComponent<BasicAttack>().TryAttack(),"automatic lowest-HP acquisition starts");
   Check(actor.GetComponent<SoftTargeting>().Current==wounded.GetComponent<Health>(),"automatic target matches priority preview "+hero);
   Run(1);Check(Total(wounded)<200,"automatic basic hits chosen weak hero "+hero);
   Near(Total(close),1000,"automatic basic never splits to closer hero "+hero);
  }
 }
 static bool RangedCast(GameObject actor,int slot,AimPose pose)
  =>slot<0?actor.GetComponent<BasicAttack>().TryAttackAimed(pose):
   actor.GetComponent<PlayerHeroAbilities>().CastAimed(slot,pose);

 static void NetworkRangedCone(HeroId hero,int slot,AimSpec spec)
 {
  Reset();var actor=Hero(hero,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;
  var close=Hero(HeroId.Kaelon,1,Vector3.forward*(spec.castRange*.45f));close.AddComponent<CoopHeroSlot>().Slot=2;
  var weak=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.4f,spec.castRange*.75f));weak.AddComponent<CoopHeroSlot>().Slot=3;
  weak.GetComponent<Health>().TakeDamage(800);
  var service=new GameObject("ranged cone commands").AddComponent<CoopGameplayCommands>();
  var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};
  Unity.Netcode.NetworkManager.Singleton=manager;CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;
  Call(service,"Update");
  var pose=Forward(actor,spec.castRange);
  Check(slot<0?CoopGameplayCommands.TrySendBasic(actor,pose):CoopGameplayCommands.TrySendAbility(actor,slot,pose),
   "ranged command sent "+hero+slot);
  CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;
  CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
  manager.CustomMessagingManager.Handlers[slot<0?"DF_COOP_BASIC_V6":"DF_COOP_ABILITY_V6"](
   1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));
  Run(1);
  Check(Total(weak)<200,"server chooses low-HP cone victim "+hero+slot);
  Near(Total(close),1000,"server attack damages only one target "+hero+slot);
 }
}
