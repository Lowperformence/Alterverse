// Deterministic gameplay-clock measurements, not CPU benchmarks or mobile/network latency.
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Alterverse.Characters;
using Alterverse.Combat;

partial class Regression
{
 static readonly int[] timingFps={30,60,120};
 static string Number(float n)=>n.ToString("0.###",CultureInfo.InvariantCulture);
 static float FirstEffect(GameObject observed,float before,float started,float dt,GameObject moving=null,Vector3? originalPosition=null)
 {
  // Immediate heals/shields/dashes are effects, not damaging hits.
  for(int frame=0;frame<Mathf.CeilToInt(2f/dt);frame++)
  {
   if(Math.Abs(Total(observed)-before)>.001f || moving!=null && Vector3.Distance(moving.transform.position,originalPosition.Value)>.001f)
    return Time.time-started;
   Step(dt);
  }
  return -1;
 }
 static float MeasureAttack(HeroId hero,int slot,int fps,float distance)
 {
  Reset();var actor=Hero(hero,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*distance);
  var ally=Hero(HeroId.Mira,0,Vector3.forward*1.5f);
  actor.GetComponent<Health>().TakeDamage(200);ally.GetComponent<Health>().TakeDamage(300);
  AimSpec aim=AimingSettings.Current.For(hero,slot);
  bool utility=aim.aimType==AimType.Dash||aim.aimType==AimType.Self||aim.aimType==AimType.Ally||hero==HeroId.Mira&&slot==3;
  var observed=aim.aimType==AimType.Ally||hero==HeroId.Mira&&slot==3?ally:utility?actor:enemy;
  float before=Total(observed),started=Time.time;Vector3 from=actor.transform.position;
  var pose=new AimPose(Vector3.forward,enemy.transform.position,aim.aimType==AimType.Ally?ally.GetComponent<Health>():null);
  bool cast=slot<0?actor.GetComponent<BasicAttack>().TryAttack():actor.GetComponent<PlayerHeroAbilities>().CastAimed(slot,pose);
  Check(cast,hero+" timing cast "+slot+" at "+fps+" FPS");
  float result=FirstEffect(observed,before,started,1f/fps,aim.aimType==AimType.Dash?actor:null,from);
  Check(result>=0,hero+" timing effect reaches intended body "+slot);
  float windup=slot<0?HeroAttackDelivery.Windup(hero):HeroAttackDelivery.AbilityWindup(hero,slot,HeroAbilityKit.LoadoutFor(hero).Spec(slot));
  float expected=utility&&aim.aimType!=AimType.SelfArea?0:windup;
  if(aim.aimType==AimType.Projectile||aim.aimType==AimType.Homing)
   expected+=Mathf.Max(0,distance-(.4f+aim.projectileWidth*.5f))/aim.projectileSpeed;
  if(hero==HeroId.Nerok&&slot==3)expected+=.5f;
  // Windup and contact each round to a gameplay frame; coroutine fixture has 1 us tolerance.
  Check(result+1.1e-6f>=expected,hero+" no damage before windup / body contact "+slot);
  Check(result<=expected+2f/fps+1.1e-5f,hero+" no unexplained acquisition delay "+slot);
  return result;
 }
 static void FastReleaseTiming(bool print)
 {
  foreach(int fps in timingFps)foreach(float hold in new[]{0f,.05f,.15f,.75f,1f})
  {
   Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Angle(60,4));
   var hud=HudFor(actor);Vector2 button=ButtonScreen(2);
   Touch.activeTouches.Add(new Touch{touchId=21,phase=TouchPhase.Began,screenPosition=button});Call(hud,"Update");
   Check(ManualAiming.For(actor).Active,"fast gesture begins with no eligible forward target");
   Clock.Advance(hold);
   // Enemy and final thumb position become valid only in Ended. No Moved or LateUpdate is sent.
   enemy.transform.position=Angle(7,4);
   Touch.activeTouches[0]=new Touch{touchId=21,phase=TouchPhase.Ended,screenPosition=button+new Vector2(0,96)};
   float released=Time.time;Call(hud,"Update");
   Check(actor.GetComponent<SoftTargeting>().Current==enemy.GetComponent<Health>(),"release acquires enemy immediately without visible-marker frame");
   Check(actor.GetComponent<PlayerHeroAbilities>().ERemaining>0,"final release position commits fast skill");
   Check(!ManualAiming.For(actor).Active,"release clears preview immediately");
   Near(Vector3.Angle(actor.transform.forward,enemy.transform.position),0,"ended-only drag uses final assisted direction");
   float delay=FirstEffect(enemy,1000,released,1f/fps);
   float expected=.12f+(4f-.55f)/38f;
   Check(delay>=expected-1e-5f&&delay<=expected+2f/fps+1e-5f,"fast drag hits with ordinary windup plus flight, independent of hold duration");
   if(print)Console.WriteLine("GESTURE,"+fps+","+Number(hold*1000)+",0,"+Number(delay*1000));
  }
 }
 static void ConnectionMissDiagnosis(bool print)
 {
  foreach(float hold in new[]{0f,1f})
  {
   Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Angle(20,4));
   var aim=ManualAiming.For(actor);aim.Begin(2,new Vector2(700,100));Clock.Advance(hold);aim.Drag(new Vector2(700,196),1,false);
   Check(AimGeometry.Cone(actor.transform.position,aim.Pose.Direction,enemy.transform.position,aim.Spec.castRange,aim.Spec.coneAngle),"enemy inside displayed aiming corridor");
   Check(actor.GetComponent<SoftTargeting>().Current==null,"enemy beyond 8 degrees intentionally not captured");
   Check(aim.Release(),"corridor release still casts");Run(1);Near(Total(enemy),1000,"wide guide is not projectile damage; slow hold cannot fix miss");
  }
  foreach(bool strafe in new[]{false,true})
  {
   Reset();var actor=Hero(HeroId.Mira,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*4);
   var aim=ManualAiming.For(actor);aim.Begin(0,new Vector2(700,100));aim.Drag(new Vector2(700,196),1,false);
   Check(actor.GetComponent<SoftTargeting>().Current==enemy.GetComponent<Health>(),"straight target captured on drag");Check(aim.Release(),"strafe diagnosis cast");
   // Model an already walking caster during the existing 160 ms preparation.
   // HeroMotor's default attack movement multiplier is .88; this is a supplied
   // displacement path, not a measurement of native HeroMotor/CharacterController.
   float speed=actor.GetComponent<HeroCombatProfile>().MoveSpeed*.88f;
   for(int frame=1;frame<=60;frame++)
   {
    if(strafe)actor.transform.position=Vector3.right*speed*Mathf.Min(frame,10)/60f;
    Step(1f/60);
   }
   Near(Total(enemy),strafe?1000:982,"stationary target "+(strafe?"missed by shifted launch origin":"connected normally"));
  }
  if(print)Console.WriteLine("DIAGNOSIS,20-deg stationary target inside 45-deg guide misses after both 0 and 1000 ms holds; caster 0.765-m strafe during Mira Q windup also misses; no target-acquisition wait.");
 }
 static void ConnectionTiming(bool print)
 {
  if(print)Console.WriteLine("TIMING,fps,hero,slot,attack,aimType,distance_m,first_effect_ms");
  foreach(int fps in timingFps)foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=-1;slot<4;slot++)
  {
   float distance=2f;float seconds=MeasureAttack(hero,slot,fps,distance);
   if(print)Console.WriteLine("TIMING,"+fps+","+hero+","+slot+","+(slot<0?"Basic":HeroAbilityKit.LoadoutFor(hero).Spec(slot).Name)+","+AimingSettings.Current.For(hero,slot).aimType+","+Number(distance)+","+Number(seconds*1000));
  }
  // An extra 4 m sample exposes the distance-dependent portion of ranged basics.
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))if(HeroAttackDelivery.IsRanged(hero))
  {
   float seconds=MeasureAttack(hero,-1,60,4f);
   if(print)Console.WriteLine("TIMING,60,"+hero+",-1,Basic,Projectile,4,"+Number(seconds*1000));
  }
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=-1;slot<4;slot++)
  {
   AimSpec spec=AimingSettings.Current.For(hero,slot);
   if(spec.aimType!=AimType.Projectile)continue;
   float distance=spec.castRange*.95f;float seconds=MeasureAttack(hero,slot,60,distance);
   if(print)Console.WriteLine("TIMING,60,"+hero+","+slot+","+(slot<0?"Basic":HeroAbilityKit.LoadoutFor(hero).Spec(slot).Name)+",Projectile,"+Number(distance)+","+Number(seconds*1000));
  }
  if(print)Console.WriteLine("GESTURE,fps,hold_ms,acquisition_gameplay_delay_ms,first_damage_ms");
  FastReleaseTiming(print);ConnectionMissDiagnosis(print);
 }
}
