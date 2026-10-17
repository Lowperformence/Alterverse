using System;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;

class EntryPoint { static void Main() => Regression.RunEntryCases(); }

partial class Regression
{
 public static void RunEntryCases()
 {
  foreach(int fps in new[]{30,60,120})
  {
   EmptyDirectedSkills(fps);
   EmptyAreaSkills(fps);
   EmptyScratch(fps,false);
   EmptyScratch(fps,true);
   ScratchMisses(fps);
   EmptyScratchNetwork(fps);
   EmptyConeCommitRules(fps);
   EmptyConeNetwork(fps);
   ScratchWarningEdgeAndTap(fps);
  }
  FirstScratchContact();
  LiveConeCases();
  TimmyCases();TimmyVfxCases();IndependentConeDirection();EndedOnlyReverseDirection();CapturedPointNetwork();
  SupportTargeting();SelfShieldPreviews();
  Reset();Alterverse.EditorChecks.BasicAttackDeliveryRegression.Run();
  Reset();Alterverse.EditorChecks.AttackDeliveryRegression.Run();
  Console.WriteLine("PASS "+checks+" skill-entry and existing combat assertions. Deterministic engine fixtures; no native Unity/Android/latency test.");
 }

 static void LiveConeCases()
 {
  // Ranged and melee direct skill cones share the same authoritative hit region.
  foreach(HeroId hero in new[]{HeroId.Nyra,HeroId.Veyr})
  {
   int slot=hero==HeroId.Nyra?2:0;
   Reset();var a=Hero(hero,0,Vector3.zero);
   var victim=Hero(HeroId.Kaelon,1,Vector3.forward*3);
   var fallback=new GameObject("HotZone-style fallback");
   fallback.transform.position=Vector3.forward*1.7f;
   fallback.AddComponent<TeamMember>().Initialize(1);
   fallback.AddComponent<Health>().Configure(1000);
   Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(slot,Forward(a)),
    "live cone casts "+hero);
   Run(.8f);
   Check(Total(victim)<1000,"hero beats closer fallback "+hero);
   Near(Total(fallback),1000,"one hit never split with monster fallback "+hero);

   Reset();a=Hero(hero,0,Vector3.zero);
   fallback=new GameObject("HotZone-style fallback");
   fallback.transform.position=Vector3.forward*1.7f;
   fallback.AddComponent<TeamMember>().Initialize(1);
   fallback.AddComponent<Health>().Configure(1000);
   Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(slot,Forward(a)),
    "fallback cone cast "+hero);
   Run(.8f);
   Check(Total(fallback)<1000,"monster fallback if no enemy hero "+hero);
  }
  foreach(int fps in new[]{30,60,120})
  {
   Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);
   var opponent=Hero(HeroId.Kaelon,1,new Vector3(5,0,4));
   Check(actor.GetComponent<PlayerHeroAbilities>().CastAimed(2,Forward(actor)),
    "late entry cast starts");
   float windup=HeroAttackDelivery.AbilityWindup(HeroId.Nyra,2,
       actor.GetComponent<HeroAbilityKit>().Spec(2));
   for(int i=0;i<Mathf.CeilToInt((windup+.02f)*fps);i++)Step(1f/fps);
   HeroAttackDelivery delivery=HeroAttackDelivery.For(actor);
   Check(delivery.TryActiveCone(out _,out Vector3 dir,out AimSpec active) &&
        Vector3.Angle(dir,Vector3.forward)<.001f &&
        Mathf.Approximately(active.coneAngle,AimingSettings.Current.For(HeroId.Nyra,2).coneAngle),
        "shown active cone must equal server hitbox "+fps);
   Step(1f/fps,()=>opponent.transform.position=Vector3.forward*4);
   float hit=Total(opponent);
   Check(hit<1000,"opponent crossing active cone must be hit "+fps);
   Run(.8f);Near(Total(opponent),hit,"no repeated cone damage "+fps);

   Reset();actor=Hero(HeroId.Nyra,0,Vector3.zero);
   opponent=Hero(HeroId.Kaelon,1,new Vector3(5,0,4));
   Check(actor.GetComponent<PlayerHeroAbilities>().CastAimed(2,Forward(actor)),
    "expired cone cast starts");
   Run(1f);
   opponent.transform.position=Vector3.forward*4;
   Run(.2f);
   Near(Total(opponent),1000,"expired cone does not hit "+fps);
  }
  Check(AimGeometry.SweptConeCircle(Vector3.zero,Vector3.forward,
   new Vector3(-5,0,4),new Vector3(5,0,4),6f,35f,.35f),
   "a frame-spanning dash crosses the active hitbox");
 }

 static void EmptyDirectedSkills(int fps)
 {
  foreach(bool edge in new[]{false,true})foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
  {
   var spec=AimingSettings.Current.For(hero,slot);
   if(spec.aimType!=AimType.Melee&&spec.aimType!=AimType.Projectile&&spec.aimType!=AimType.Beam)continue;
   Reset();var a=Hero(hero,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,Vector3.right*8);
   var gesture=ManualAiming.For(a);gesture.Begin(slot,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);
   Check(!gesture.Pose.HasAimPoint,"empty directed cone has no preselected enemy "+hero+slot);
   Check(gesture.Release(),"empty directed cast starts "+hero+slot);
   float windup=HeroAttackDelivery.AbilityWindup(hero,slot,a.GetComponent<HeroAbilityKit>().Spec(slot));
   Vector3 destination=Angle(edge?spec.coneAngle*.5f-1:0,Mathf.Min(4,spec.castRange*.7f));
   for(int frame=0;frame<Mathf.CeilToInt(1.2f*fps);frame++)
   {
    float next=(frame+1f)/fps;
    // Walk into the path during preparation, then remain there until contact.
    Step(1f/fps,()=>b.transform.position=Vector3.Lerp(new Vector3(8,0,destination.z),destination,next/Mathf.Max(.02f,windup*.5f)));
   }
   float expected=a.GetComponent<HeroAbilityKit>().Spec(slot).Power*(hero==HeroId.Rook?1.05f:1f);
   Near(1000-Total(b),expected,"late entrant takes directed damage exactly once "+hero+slot+" edge="+edge+" at "+fps);
  }
 }

 static void EmptyAreaSkills(int fps)
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
  {
   var spec=AimingSettings.Current.For(hero,slot);
   if(spec.aimType!=AimType.Area&&spec.aimType!=AimType.SelfArea&&spec.aimType!=AimType.AreaCone)continue;
   if(hero==HeroId.Mira)continue; // This area heals allies rather than damaging enemies.
   Reset();var a=Hero(hero,0,Vector3.zero);
   Vector3 center=spec.aimType==AimType.Area?Vector3.forward*spec.castRange*.65f:
    spec.aimType==AimType.AreaCone?Vector3.forward*2:Vector3.zero;
   var b=Hero(HeroId.Kaelon,1,center+Vector3.right*(spec.aoeRadius+5));
   var pose=new AimPose(Vector3.forward,center);
   Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(slot,pose),"empty area cast starts "+hero+slot);
   float windup=a.GetComponent<HeroAbilityKit>().Spec(slot).Windup;
   for(int frame=0;frame<Mathf.CeilToInt((windup+3.5f)*fps);frame++)
    Step(1f/fps,()=>b.transform.position=center+Vector3.right*Mathf.Max(0,1-Time.time/Mathf.Max(.02f,windup*.5f))*(spec.aoeRadius+5));
   float expected=a.GetComponent<HeroAbilityKit>().Spec(slot).Power*(hero==HeroId.Rook?1.05f:1f);
   if(hero==HeroId.VoidKnight&&slot==3)expected+=VoidKnightBalance.UltimateLifeDrain;
   Near(1000-Total(b),expected,"area checks late entrant at impact with unchanged damage "+hero+slot+" at "+fps);
  }
 }

 static void EmptyScratch(int fps,bool duringLeap)
 {
  Reset();var a=Hero(HeroId.Timmy,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,new Vector3(5,0,4.5f));
  var gesture=ManualAiming.For(a);gesture.Begin(3,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);
  Check(gesture.Pose.Target==null,"Scratch begins without an enemy in its field");
  Check(gesture.Release(),"empty Scratch starts its directed leap");
  float entry= duringLeap?.32f:.08f;
  for(int frame=0;frame<Mathf.CeilToInt(3f*fps);frame++)
   Step(1f/fps,()=>{if(b.GetComponent<HeroCombatStatus>()?.Rooted!=true)b.transform.position=new Vector3(5*Mathf.Clamp01((entry-Time.time)/.08f),0,4.5f);});
  Near(Total(b),1000-TimmyBalance.ScratchDamage,"Scratch hits a late path entrant "+(duringLeap?"during leap":"during warning")+" at "+fps);
  Check(b.GetComponent<HeroCombatStatus>()?.Rooted!=true,"late entrant's hold expires normally");
 }

 static void ScratchMisses(int fps)
 {
  for(int scenario=0;scenario<8;scenario++)
  {
   Reset();var a=Hero(HeroId.Timmy,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,new Vector3(5,0,4.5f));
   if(scenario==1)b.GetComponent<TeamMember>().Initialize(0);
   if(scenario==2){b.GetComponent<HeroIdentity>().enabled=false;b.SetActive(false);b=new GameObject("minion");b.transform.position=new Vector3(5,0,4.5f);b.AddComponent<TeamMember>().Initialize(1);b.AddComponent<Health>();b.AddComponent<LaneMinion>().Team=1;}
   if(scenario==4)b.GetComponent<Health>().DamageGate=()=>false;
   var gesture=ManualAiming.For(a);gesture.Begin(3,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);
   Check(gesture.Release(),"negative empty Scratch starts "+scenario);
   // A parallel wall separates the leap from a body within contact radius.
   // The shared locomotion fixture does not itself simulate native dash/wall collision.
   if(scenario==3){var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(.5f,.5f,4.5f);wall.transform.localScale=new Vector3(.1f,2,20);}
   for(int frame=0;frame<Mathf.CeilToInt(3f*fps);frame++)
    Step(1f/fps,()=>{
     if(scenario==0){if(Time.time>.25f)b.transform.position=Angle(30,4.5f);} // After launch: in the cone, clear of the actual leap path.
     else if(scenario==3)b.transform.position=new Vector3(1,0,4.5f);
     else if(scenario==5){if(Time.time>.8f)b.transform.position=Vector3.forward*9;}
     else if(scenario==6){b.transform.position=Vector3.forward*10.5f;}
     else b.transform.position=Vector3.forward*4.5f;
     if(scenario==7&&Time.time<.1f)(a.GetComponent<HeroCombatStatus>()??a.AddComponent<HeroCombatStatus>()).Stun(1);
    });
   Near(Total(b),1000,"Scratch rejects off-path/ally/minion/wall/invulnerable/expired/out-of-range/interrupted target "+scenario+" at "+fps);
   Check(b.GetComponent<HeroCombatStatus>()?.Rooted!=true,"rejected entrant is not held "+scenario);
  }
  // Once a marked victim dodges, another entrant cannot replace that victim.
  Reset();var caster=Hero(HeroId.Timmy,0,Vector3.zero);var victim=Hero(HeroId.Kaelon,1,Vector3.forward*6);
  var decoy=Hero(HeroId.Kaelon,1,new Vector3(5,0,3));
  Check(caster.GetComponent<PlayerHeroAbilities>().CastAimed(3,new AimPose(Vector3.forward,victim.transform.position,victim.GetComponent<Health>(),true)),"selected Scratch starts");
  for(int frame=0;frame<Mathf.CeilToInt(3f*fps);frame++)Step(1f/fps,()=>{victim.transform.position=Vector3.right*12;decoy.transform.position=Vector3.forward*3;});
  Near(Total(victim),1000,"marked victim can still dodge");Near(Total(decoy),1000,"marked cast does not swap to a late entrant");
 }

 static void EmptyScratchNetwork(int fps)
 {
  Reset();var a=Hero(HeroId.Timmy,0,Vector3.zero);a.AddComponent<CoopHeroSlot>().Slot=1;
  var b=Hero(HeroId.Kaelon,1,new Vector3(5,0,4.5f));b.AddComponent<CoopHeroSlot>().Slot=2;
  var gesture=ManualAiming.For(a);gesture.Begin(3,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);var pose=gesture.Pose;gesture.Cancel();
  Check(pose.Target==null,"network Scratch is sent without a selected enemy");
  var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
  var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
  Check(CoopGameplayCommands.TrySendAbility(a,3,pose),"empty Scratch packet sent");
  CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V6"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));
  for(int frame=0;frame<Mathf.CeilToInt(3f*fps);frame++)Step(1f/fps,()=>{if(Time.time>.1f)b.transform.position=Vector3.forward*4.5f;});
  Near(Total(b),1000-TimmyBalance.ScratchDamage,"host resolves late entry after empty V5 cast at "+fps);
 }

 static void FirstScratchContact()
 {
  Reset();var a=Hero(HeroId.Timmy,0,Vector3.zero);
  var far=Hero(HeroId.Kaelon,1,new Vector3(5,0,6));var near=Hero(HeroId.Kaelon,1,new Vector3(5,0,3));
  Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(3,Forward(a,9)),"empty cast before long leap frame");
  Run(.22f);far.transform.position=Vector3.forward*6;near.transform.position=Vector3.forward*3;
  Step(.4f);Run(2.2f);
  Near(Total(near),1000-TimmyBalance.ScratchDamage,"swept earliest contact wins on a long landing frame");
  Near(Total(far),1000,"later body on the same frame is not hit");
 }

 static void EmptyConeCommitRules(int fps)
 {
  for(int scenario=0;scenario<4;scenario++)
  {
   Reset();var a=Hero(HeroId.Nyra,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,new Vector3(5,0,4));
   if(scenario==2)
   {
    var spec=AimingSettings.Current.For(HeroId.Nyra,2);spec.targetingMode=TargetingMode.Geometry;
    AimingSettings.Current.overrides=new[]{new AimingSettings.AttackOverride{hero=HeroId.Nyra,slot=2,spec=spec}};
   }
   if(scenario==3)b.transform.position=Angle(16,4);
   var gesture=ManualAiming.For(a);gesture.Begin(2,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);
   Check(gesture.Pose.HasAimPoint==(scenario==3),"only pre-existing cone victim supplies a release snapshot");
   Check(gesture.Release(),"commit-rule cast starts "+scenario);
   for(int frame=0;frame<fps;frame++)Step(1f/fps,()=>{
    if(scenario==0){a.transform.position=Vector3.right*2;b.transform.position=new Vector3(3,0,4);}
    if(scenario==1&&Time.time>.2f)b.transform.position=Angle(16,4);
    if(scenario==2)b.transform.position=Angle(16,4);
    if(scenario==3)b.transform.position=new Vector3(-5,0,4);
   });
   if(scenario==0 || scenario==1)
    Near(1000-Total(b),a.GetComponent<HeroAbilityKit>().Spec(2).Power,
     "live cone accepts moving entrant after launch "+scenario+" at "+fps);
   else Near(Total(b),1000,
     "non-cone geometry and true off-field dodge stay safe "+scenario+" at "+fps);
  }
 }

 static void EmptyConeNetwork(int fps)
 {
  foreach(int slot in new[]{0,2})
  {
   Reset();var a=Hero(HeroId.Nyra,0,Vector3.zero);a.AddComponent<CoopHeroSlot>().Slot=1;
   var b=Hero(HeroId.Kaelon,1,new Vector3(5,0,4));b.AddComponent<CoopHeroSlot>().Slot=2;
   var gesture=ManualAiming.For(a);gesture.Begin(slot,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);var pose=gesture.Pose;gesture.Cancel();
   Check(!pose.HasAimPoint&&pose.Target==null,"empty V5 cone has no selected point or enemy");
   var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
   var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
   CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
   Check(CoopGameplayCommands.TrySendAbility(a,slot,pose),"empty V5 directed cast sent");
   CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
   manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V6"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));
   for(int frame=0;frame<fps;frame++)Step(1f/fps,()=>b.transform.position=Angle(16,4));
   Near(1000-Total(b),a.GetComponent<HeroAbilityKit>().Spec(slot).Power,"host captures late cone entrant before beam/projectile launch at "+fps);
  }
 }

 static void ScratchWarningEdgeAndTap(int fps)
 {
  foreach(bool tap in new[]{false,true})
  {
   Reset();var a=Hero(HeroId.Timmy,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,new Vector3(5,0,4.5f));
   var gesture=ManualAiming.For(a);gesture.Begin(3,new Vector2(700,100));if(!tap)gesture.Drag(new Vector2(700,196),1,false);
   Check(gesture.Release(),"empty aimed/tap leap starts");
   for(int frame=0;frame<3*fps;frame++)Step(1f/fps,()=>{if(Time.time>.05f&&b.GetComponent<HeroCombatStatus>()?.Rooted!=true)b.transform.position=tap?Vector3.forward*4.5f:Angle(30,6);});
   Near(Total(b),1000-TimmyBalance.ScratchDamage,"warning accepts full-cone entrant; movement tap accepts physical path contact at "+fps+" tap="+tap);
  }
 }
}
