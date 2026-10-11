using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
partial class Regression
{
 // Supplied displacement paths, not native CharacterController/Android measurements.
 static void WalkingStep(float dt,GameObject actor,Vector3 actorVelocity,GameObject target=null,Vector3 targetVelocity=default)
 {
  Step(dt,()=>
  {
   actor.transform.position+=actorVelocity*dt;actor.GetComponent<HeroLocomotion>().Velocity=actorVelocity;
   if(target!=null){target.transform.position+=targetVelocity*dt;target.GetComponent<HeroLocomotion>().Velocity=targetVelocity;}
  });
 }
 static void MovingConnectionCases()
 {
  // Every loadout must still execute and affect the appropriate body while its caster walks.
  int stationaryMisses=0;
  foreach(int fps in timingFps)foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=-1;slot<4;slot++)
  {
   if(hero==HeroId.Timmy&&slot==3)continue; // Fixed-end contact/channel has separate tests.
   Reset();var actor=Hero(hero,0,Vector3.zero);float contact=hero==HeroId.Timmy&&(slot<0||slot==0)?1f:2f;var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*contact);
   var ally=Hero(HeroId.Mira,0,Vector3.forward*1.5f);actor.GetComponent<Health>().TakeDamage(200);ally.GetComponent<Health>().TakeDamage(300);
   var spec=AimingSettings.Current.For(hero,slot);
   bool utility=spec.aimType==AimType.Dash||spec.aimType==AimType.Self||spec.aimType==AimType.Ally||hero==HeroId.Mira&&slot==3;
   var observed=spec.aimType==AimType.Ally||hero==HeroId.Mira&&slot==3?ally:utility?actor:enemy;
   float before=Total(observed);Vector3 start=actor.transform.position;
   var pose=new AimPose(Vector3.forward,enemy.transform.position,spec.aimType==AimType.Ally?ally.GetComponent<Health>():null,true);
   bool cast=slot<0?actor.GetComponent<BasicAttack>().TryAttack():actor.GetComponent<PlayerHeroAbilities>().CastAimed(slot,pose);
   Check(cast,hero+" moving cast "+slot+" at "+fps+" FPS");
   float windup=slot<0?HeroAttackDelivery.Windup(hero):HeroAttackDelivery.AbilityWindup(hero,slot,HeroAbilityKit.LoadoutFor(hero).Spec(slot));
   for(int frame=0;frame<Mathf.CeilToInt(1.6f*fps);frame++)
    WalkingStep(1f/fps,actor,Time.time<windup?Vector3.right*actor.GetComponent<HeroCombatProfile>().MoveSpeed*.88f:Vector3.zero);
   if(spec.aimType==AimType.Dash)Check(Vector3.Distance(start,actor.transform.position)>=spec.castRange-.01f,"moving dash executes "+hero);
   else if(Math.Abs(Total(observed)-before)<=.01f){stationaryMisses++;Console.WriteLine("MOVING_CASTER_MISS,"+fps+","+hero+","+slot);}
  }
  // Keep relative distance constant: walking with a moving foe is not a range escape.
  // No sudden direction change; the automatic basic should aim at their actual flight path.
  int misses=0;
  foreach(int fps in timingFps)foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
  {
   Reset();var actor=Hero(hero,0,Vector3.zero);float distance=HeroAttackDelivery.IsRanged(hero)?4f:Mathf.Min(1.8f,HeroCombatProfile.StatsFor(hero).AttackRange-.1f);
   var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*distance);
   var basic=actor.GetComponent<BasicAttack>();var hud=HudFor(actor);Vector2 point=ButtonScreen(4);
   Vector3 velocity=Vector3.right*actor.GetComponent<HeroCombatProfile>().MoveSpeed*.88f;
   enemy.GetComponent<HeroLocomotion>().Velocity=velocity;
   Touch.activeTouches.Add(new Touch{touchId=49,phase=TouchPhase.Began,screenPosition=new Vector2(140,140)});
   Touch.activeTouches.Add(new Touch{touchId=50,phase=TouchPhase.Began,screenPosition=point});Call(hud,"Update");
   Touch.activeTouches[0]=new Touch{touchId=49,phase=TouchPhase.Moved,screenPosition=new Vector2(195,140)};
   Touch.activeTouches[1]=new Touch{touchId=50,phase=TouchPhase.Stationary,screenPosition=point};
   for(int frame=0;frame<Mathf.CeilToInt((basic.AttackCooldown*2+.45f)*fps);frame++)
   {
    WalkingStep(1f/fps,actor,velocity,enemy,velocity);Call(hud,"Update");
   }
   float damage=1000-Total(enemy);float expected=HeroIdentity.AttackDamage(hero)*3*(hero==HeroId.Rook?1.05f:1f);
   Console.WriteLine("MOVING_BASIC,"+fps+","+hero+",held,co-moving-"+distance+"m,"+Number(damage)+","+Number(expected));
   if(Math.Abs(damage-expected)>.01f)misses++;
   Check(actor.GetComponent<HeroMotor>().HasMoveInput,"held moving basic keeps input "+hero);
  }
  Check(misses==0,"all automatic basics connect during sustained co-moving hold; failing cases="+misses);
  Check(stationaryMisses==0,"all stationary in-range effects connect after caster strafe; failing cases="+stationaryMisses);
  CrowdedProjectilePath();
  AutomaticBasicBoundaries();
  MovingBasicNetwork();
 }
 static void AutomaticBasicBoundaries()
 {
  foreach(HeroId hero in new[]{HeroId.Nyra,HeroId.Elyra,HeroId.Mira,HeroId.Traveler})
  {
   Reset();var actor=Hero(hero,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*4);
   Check(actor.GetComponent<BasicAttack>().TryAttack(),"auto basic starts for launch-time dodge check "+hero);
   Run(HeroAttackDelivery.Windup(hero)+.02f);enemy.transform.position+=Vector3.right*3;Run(.5f);
   Near(Total(enemy),1000,"auto basic never follows target after launch "+hero);
   Reset();actor=Hero(hero,0,Vector3.zero);enemy=Hero(HeroId.Kaelon,1,Vector3.forward*4);
   Check(actor.GetComponent<BasicAttack>().TryAttackAimed(new AimPose(Vector3.forward,enemy.transform.position,null,true)),"explicit fixed-point basic remains possible "+hero);
   enemy.transform.position+=Vector3.right*2;Run(.6f);Near(Total(enemy),1000,"fixed-point basic never silently reacquires "+hero);
   Reset();actor=Hero(hero,0,Vector3.zero);enemy=Hero(HeroId.Kaelon,1,Vector3.forward*(actor.GetComponent<BasicAttack>().Aim.castRange+2));
   Check(!actor.GetComponent<BasicAttack>().TryAttackNetworkAuthoritative(enemy.GetComponent<Health>()),"host rejects out-of-range automatic target "+hero);
   Near(actor.GetComponent<BasicAttack>().RemainingCooldown,0,"rejected automatic target spends no cooldown "+hero);
   enemy.GetComponent<TeamMember>().Initialize(0);enemy.transform.position=Vector3.forward*2;
   Check(!actor.GetComponent<BasicAttack>().TryAttackNetworkAuthoritative(enemy.GetComponent<Health>()),"host rejects allied automatic target "+hero);
  }
  Reset();var caster=Hero(HeroId.Elyra,0,Vector3.zero);var foe=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  AimingSettings.Current.basicLeadQuality=0;Vector3 movement=Vector3.right*4.5f;
  Check(caster.GetComponent<BasicAttack>().TryAttack(),"lead can be disabled centrally");
  for(int i=0;i<60;i++)WalkingStep(1f/60,caster,movement,foe,movement);
  Near(Total(foe),1000,"disabled lead keeps a straight launch-time snapshot");
 }
 static void MovingBasicNetwork()
 {
  foreach(int fps in timingFps)
  {
   Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;
   var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*4);enemy.AddComponent<CoopHeroSlot>().Slot=2;
   var decoy=Hero(HeroId.Kaelon,1,new Vector3(-3,0,4));decoy.AddComponent<CoopHeroSlot>().Slot=3;
   var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
   var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
   CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
   Check(CoopGameplayCommands.TrySendBasic(actor,enemy.GetComponent<Health>()),"automatic basic helper sends selected target");
   byte[] packet=manager.CustomMessagingManager.LastPacket;
   CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
   // A target's server position has advanced since the client's click; roster identity stays stable.
   enemy.transform.position+=Vector3.right;enemy.GetComponent<HeroLocomotion>().Velocity=Vector3.right*4;
   var truncated=new byte[packet.Length-1];Array.Copy(packet,truncated,truncated.Length);
   manager.CustomMessagingManager.Handlers["DF_COOP_BASIC_V5"](1,new Unity.Netcode.FastBufferReader(truncated));
   Near(actor.GetComponent<BasicAttack>().RemainingCooldown,0,"truncated basic command rejected before cooldown");
   manager.CustomMessagingManager.Handlers["DF_COOP_BASIC_V5"](1,new Unity.Netcode.FastBufferReader(packet));
   Check(actor.GetComponent<BasicAttack>().RemainingCooldown>0,"host accepts automatic basic target identity");
   for(int i=0;i<Mathf.CeilToInt(.5f*fps);i++)WalkingStep(1f/fps,actor,Vector3.zero,enemy,Vector3.right*4);
   Near(Total(enemy),978,"host launch-time basic connects to moving intended body at "+fps+" FPS");
   Near(Total(decoy),1000,"host does not redirect automatic basic to another hero");
  }
 }
 static void CrowdedProjectilePath()
 {
  foreach(bool wall in new[]{false,true})
  {
   Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*4);
   // The normal 32-hit sight buffer fills with units that must be ignored as walls.
   for(int i=0;i<40;i++){var ally=Hero(HeroId.Kaelon,0,Vector3.forward*1.5f);ally.layer=0;ally.AddComponent<Collider>();}
   if(wall){var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=new Vector3(0,.5f,3);obstacle.transform.localScale=new Vector3(2,2,.2f);}
   Check(actor.GetComponent<BasicAttack>().TryAttack(),"crowded basic begins");Run(.5f);
   Near(Total(enemy),wall?1000:978,"saturated query distinguishes ignored units from real wall");
  }
 }
}
