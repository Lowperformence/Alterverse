using System;
using System.Linq;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Visuals;

partial class Regression
{
 static void TemporalBeamCases()
 {
  foreach(int fps in new[]{30,60,120})
  {
   // A beam must resolve on release, before a victim can leave on the next frame.
   Reset();var actor=Hero(HeroId.Traveler,0,Vector3.zero);
   var victim=Hero(HeroId.Kaelon,1,Vector3.forward*4);
   Check(actor.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(actor)),"Temporal Beam starts");
   float release=HeroAttackDelivery.AbilityWindup(HeroId.Traveler,0,actor.GetComponent<HeroAbilityKit>().Q);
   while(Time.time+1f/fps<release)Step(1f/fps);
   Step(1f/fps);
   Near(Total(victim),965,"Temporal Beam hits on release at "+fps+" FPS");
   victim.transform.position=Vector3.right*20;Run(.5f);
   Near(Total(victim),965,"beam applies damage once before next-frame dodge");

   Reset();actor=Hero(HeroId.Traveler,0,Vector3.zero);
   victim=Hero(HeroId.Kaelon,1,Vector3.right*8);
   Check(actor.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(actor)),"empty beam starts");
   while(Time.time+1f/fps<release)Step(1f/fps);
   Step(1f/fps);Near(Total(victim),1000,"empty beam cannot hit off-cone enemy");
   Step(1f/fps,()=>victim.transform.position=Vector3.forward*4);
   Near(Total(victim),965,"late entrant is hit during visible beam interval at "+fps);
   Run(.5f);Near(Total(victim),965,"late entrant cannot take duplicate damage");

   foreach(int scenario in new[]{0,1,2,3,4,5})
   {
    Reset();actor=Hero(HeroId.Traveler,0,Vector3.zero);
    victim=Hero(HeroId.Kaelon,scenario==0?0:1,Vector3.forward*4);
    if(scenario==1)victim.transform.position=Vector3.forward*9;
    if(scenario==2)victim.transform.position=Vector3.right*4;
    if(scenario==3){var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,.5f,2);wall.transform.localScale=new Vector3(4,2,.2f);}
    if(scenario==4)victim.GetComponent<Health>().DamageGate=()=>false;
    Check(actor.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(actor)),"beam negative case starts");
    if(scenario==5)(actor.GetComponent<HeroCombatStatus>()??actor.AddComponent<HeroCombatStatus>()).Stun(1);
    for(int i=0;i<fps;i++)Step(1f/fps);
    Near(Total(victim),1000,"beam rejects ally/range/direction/wall/gate/stun case "+scenario+" at "+fps);
   }

   Reset();actor=Hero(HeroId.Traveler,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;
   victim=Hero(HeroId.Kaelon,1,Vector3.forward*4);victim.AddComponent<CoopHeroSlot>().Slot=2;
   var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
   var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
   CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
   Check(CoopGameplayCommands.TrySendAbility(actor,0,Forward(actor)),"client sends Temporal Beam");
   Near(Total(victim),1000,"client cannot apply damage");
   CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
   manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V6"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));
   for(int i=0;i<fps;i++)Step(1f/fps);
   Near(Total(victim),965,"host resolves one beam hit from client command at "+fps);
  }
  TemporalLaserVfx();
 }

 static void TemporalLaserVfx()
 {
  Reset();var actor=Hero(HeroId.Traveler,0,Vector3.zero);
  Alterverse.Performance.AdaptiveVfxBudget.AllowDecorative=false;
  try
  {
   HeroSkillVfx.Spell(actor,Vector3.forward*7.2f,0,.12f);
   var lasers=VisibleFx(HeroFxShape.Laser);
   Check(lasers.Length==2,"white laser core and glow remain on low VFX budget");
   Check(VisibleFx(HeroFxShape.Orb).Length==0&&VisibleFx(HeroFxShape.Bolt).Length==0,"Temporal Beam has no travelling orb or bolt");
   foreach(var laser in lasers)
   {
    var color=laser.GetComponent<MeshRenderer>().Block.Colors[Shader.PropertyToID("_Tint")];
    Check(color.r>=1,"laser retains bright white intensity");Near(color.g,color.r,"laser has no colored tint");Near(color.b,color.r,"laser has no colored tint");
    Near(laser.transform.localScale.z,7.2f,"entire laser is visible immediately to exact range");
   }
   actor.transform.position=Vector3.right*2;actor.transform.rotation=Quaternion.Euler(0,90,0);Step(.06f);
   Check(lasers.All(f=>Math.Abs(f.transform.position.x)<.01f),"released laser cannot follow subsequent caster movement");
   Check(lasers.All(f=>Math.Abs(f.transform.localScale.z-7.2f)<.01f),"laser does not grow like a projectile");
   Step(.07f);Check(VisibleFx(HeroFxShape.Laser).Length==0,"laser clears at end of beam interval");
   HeroSkillVfx.Spell(actor,actor.transform.position+Vector3.forward*20,0,.12f);
   Check(VisibleFx(HeroFxShape.Laser).All(f=>Math.Abs(f.transform.localScale.z-7.2f)<.01f),"laser mesh cannot extend past gameplay range");
  }
  finally { Alterverse.Performance.AdaptiveVfxBudget.AllowDecorative=true; }
 }
}
