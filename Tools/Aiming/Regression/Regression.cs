using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.UI;
class Regression
{
 static int checks;
 static void Check(bool yes,string reason){checks++;if(!yes)throw new Exception(reason);}
 static void Near(float a,float b,string reason)=>Check(Math.Abs(a-b)<.01f,reason+" actual="+a+" expected="+b);
 static void Call(object o,string method)=>GameObject.Invoke(o,method);
 static void Reset()
 {
  foreach(var g in GameObject.All.ToArray())g.SetActive(false);
  GameObject.All.Clear();Health.Active.Clear();Clock.Reset();Camera.main=null;Mouse.current=null;Keyboard.current=null;
  Touch.activeTouches.Clear();CoopAuthority.Active=false;CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.LocalIsHost=true;
  Unity.Netcode.NetworkManager.Singleton=null;
  AimingSettings.Current.overrides=new AimingSettings.AttackOverride[0];
 }
 static GameObject Hero(HeroId id,int team,Vector3 p)
 {
  var g=new GameObject(id.ToString());g.transform.position=p;
  g.AddComponent<TeamMember>().Initialize(team);g.AddComponent<Health>().Configure(1000);
  g.AddComponent<HeroIdentity>().Configure(id);g.AddComponent<HeroAbilityKit>().Configure(id);g.AddComponent<HeroCombatProfile>().Configure(id);
  g.AddComponent<HeroMotor>();g.AddComponent<HeroLocomotion>();g.AddComponent<HeroAbilityExecution>();g.AddComponent<PlayerHeroAbilities>();g.AddComponent<BasicAttack>();
  Unlock(g.GetComponent<HeroAbilityExecution>());return g;
 }
 static void Unlock(HeroAbilityExecution e)=>Array.Clear((float[])typeof(HeroAbilityExecution).GetField("ready",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(e),0,4);
 static float Total(GameObject g)=>g.GetComponent<Health>().Current+g.GetComponent<Health>().Shield;
 static void Step(float dt=.01f)
 {
  foreach(var g in GameObject.All.Where(g=>g.activeInHierarchy)) {var l=g.GetComponent<HeroLocomotion>();if(l!=null)l.FrameStartPosition=g.transform.position;}
  Clock.Advance(dt);
  foreach(var g in GameObject.All.ToArray().Where(g=>g.activeInHierarchy)) {var e=g.GetComponent<HeroAbilityExecution>();if(e!=null)Call(e,"Update");}
  foreach(var g in GameObject.All.ToArray().Where(g=>g.activeInHierarchy)) {var d=g.GetComponent<HeroAttackDelivery>();if(d!=null)Call(d,"Update");}
 }
 static void Run(float seconds){for(float t=0;t<seconds-.000001f;t+=.01f)Step();}
 static AimPose Forward(GameObject a,float distance=4)=>new AimPose(Vector3.forward,a.transform.position+Vector3.forward*distance);
 static Vector3 Angle(float angle,float range)=>new Vector3(Mathf.Sin(angle*Mathf.Deg2Rad)*range,0,Mathf.Cos(angle*Mathf.Deg2Rad)*range);
 static void Geometry()
 {
  Reset();
  Check(AimingSettings.Current.For(HeroId.Nyra,0).aimType==AimType.Beam,"Prism Shot is a laser");
  Check(AimingSettings.Current.For(HeroId.Traveler,0).aimType==AimType.Projectile,"Temporal Beam retains existing orb flight");
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
  {
   var entry=AimingSettings.Current.For(hero,slot);var baseline=HeroAbilityKit.LoadoutFor(hero).Spec(slot);
   Near(entry.castRange,baseline.Range,"cast range comes from catalog");Near(entry.aoeRadius,baseline.Radius,"AOE radius comes from catalog");
   Near(entry.coneAngle,HeroAttackDelivery.IsRanged(hero)?45:80,"role-specific aim cone");
   Near(entry.aimAssistAngle,8,"central assist default");
  }
  Check(AimGeometry.Cone(Vector3.zero,Vector3.forward,Angle(39,2),3,80),"80-degree melee interior");
  Check(!AimGeometry.Cone(Vector3.zero,Vector3.forward,Angle(41,2),3,80),"melee exterior");
  Check(!AimGeometry.Cone(Vector3.zero,Vector3.forward,Angle(24,2),3,45),"narrow ranged cone");
  Check(!AimGeometry.Cone(Vector3.zero,Vector3.forward,Vector3.forward*3.01f,3,80),"range exact");
  Check(AimGeometry.Circle(Vector3.zero,new Vector3(1,99,1),2),"area is planar");
  float fraction;
  Check(AimGeometry.Sweep(Vector3.zero,Vector3.forward*20,Vector3.forward*7,Vector3.forward*7,.55f,out fraction),"fast projectile cannot tunnel");
  Check(fraction>0&&fraction<1,"sweep contact time");
  Check(AimGeometry.Sweep(Vector3.zero,Vector3.forward*5,new Vector3(-2,0,2.5f),new Vector3(2,0,2.5f),.55f,out fraction),"crossing moving target");
  Check(!AimGeometry.Sweep(Vector3.zero,Vector3.forward*5,new Vector3(2,0,2.5f),new Vector3(4,0,2.5f),.55f,out fraction),"moving target miss");
  Near(AimGeometry.ClampPoint(Vector3.zero,new Vector3(20,100,20),5).magnitude,5,"ground clamp");
  var actor=Hero(HeroId.Nyra,0,Vector3.zero);var near=Hero(HeroId.Kaelon,1,Angle(7,4));var behind=Hero(HeroId.Kaelon,1,Angle(170,1));
  var spec=AimingSettings.Current.For(HeroId.Nyra,-1);Health target;
  Vector3 assist=AimGeometry.Assist(actor,Vector3.forward,spec,out target);
  Check(target==near.GetComponent<Health>(),"assist ignores closer target behind caster");
  Check(Vector3.Angle(assist,Vector3.forward)<=8.01f,"assist at most configured 8 degrees");
  near.transform.position=Angle(9,4);assist=AimGeometry.Assist(actor,Vector3.forward,spec,out target);
  Check(target==null,"outside 8 degrees no lock");Near(Vector3.Angle(assist,Vector3.forward),0,"no correction outside limit");
 }
 static void AttackCases()
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
  {
   Reset();var a=Hero(hero,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,Vector3.forward*1.5f);float before=Total(b);
   Check(a.GetComponent<BasicAttack>().TryAttackAimed(Forward(a)),hero+" basic cast");Near(Total(b),before,"windup has no damage");Run(.65f);
   Near(before-Total(b),HeroIdentity.AttackDamage(hero)*(hero==HeroId.Rook?1.05f:1f),hero+" basic damage preserved");Run(.3f);Near(before-Total(b),HeroIdentity.AttackDamage(hero)*(hero==HeroId.Rook?1.05f:1f),"single application");
  }
  Reset();var source=Hero(HeroId.Veyr,0,Vector3.zero);var outside=Hero(HeroId.Kaelon,1,Angle(50,1.5f));
  Check(source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source)),"empty cone still casts");Run(.5f);Near(Total(outside),1000,"melee outside cone misses");
  Check(source.GetComponent<BasicAttack>().RemainingCooldown>0,"miss commits cooldown");
  Reset();source=Hero(HeroId.Veyr,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Vector3.forward*1.5f);
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));outside.transform.position=Vector3.right*1.5f;Run(.5f);Near(Total(outside),1000,"melee dodge during windup");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Angle(20,4));
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));Run(.5f);Near(Total(outside),1000,"cone guide is not projectile damage fan");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));Run(.09f);outside.transform.position+=Vector3.right*2;Run(.4f);Near(Total(outside),1000,"released projectile does not home");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);var first=Hero(HeroId.Kaelon,1,Vector3.forward*2);var second=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));Run(.4f);Near(Total(first),978,"nearest collision gets hit");Near(Total(second),1000,"projectile does not hit selected further target");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));source.AddComponent<HeroCombatStatus>().Stun(.5f);Run(.7f);Near(Total(outside),1000,"stun interrupts windup");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));Run(.09f);source.AddComponent<HeroCombatStatus>().Stun(.5f);Run(.4f);Near(Total(outside),978,"stun does not erase released projectile");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));Run(.09f);source.GetComponent<Health>().TakeDamage(2000);Run(.4f);Near(Total(outside),1000,"death cancels flight");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,.5f,2);wall.transform.localScale=new Vector3(2,2,.2f);
  source.GetComponent<BasicAttack>().TryAttackAimed(Forward(source));Run(.5f);Near(Total(outside),1000,"wall stops projectile");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);outside=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  source.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(source));outside.transform.position=Vector3.right*3;Run(.3f);Near(Total(outside),1000,"Prism beam can be dodged during windup");
 }
 static void Kits()
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
  {
   Reset();var a=Hero(hero,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*2);var ally=Hero(HeroId.Mira,0,Vector3.forward*1.5f);
   a.GetComponent<Health>().TakeDamage(200);ally.GetComponent<Health>().TakeDamage(300);
   var execution=a.GetComponent<HeroAbilityExecution>();var spec=AimingSettings.Current.For(hero,slot);
   float before=Total(enemy),allyBefore=Total(ally),selfBefore=Total(a);
   var pose=new AimPose(Vector3.forward,enemy.transform.position,spec.aimType==AimType.Ally?ally.GetComponent<Health>():null);
   Check(execution.TryCastAimed(slot,pose),hero+" slot "+slot+" cast");Check(execution.Remaining(slot)>0,"cooldown committed");
   Check(!execution.TryCastAimed(slot,pose),"cooldown rejects repeat");Run(hero==HeroId.Nerok&&slot==3?3.9f:1.1f);
   var numbers=HeroAbilityKit.LoadoutFor(hero).Spec(slot);
   if(spec.aimType==AimType.Dash)Near(a.transform.position.magnitude,spec.castRange,"dash length preserved");
   else if(spec.aimType==AimType.Self)Check(Total(a)>selfBefore,"self utility works");
   else if(spec.aimType==AimType.Ally||hero==HeroId.Mira&&slot==3)Check(Total(ally)>allyBefore,"support works");
   else Near(before-Total(enemy),numbers.Power*(hero==HeroId.Rook?1.05f:1f),hero+" slot "+slot+" damage preserved");
  }
  Reset();var caster=Hero(HeroId.Elyra,0,Vector3.zero);var escape=Hero(HeroId.Kaelon,1,Vector3.forward*4);var enter=Hero(HeroId.Kaelon,1,Vector3.right*4);
  Check(caster.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(caster)),"ground cast has no enemy requirement");
  escape.transform.position=Vector3.right*6;enter.transform.position=Vector3.forward*4;Run(.5f);
  Near(Total(escape),1000,"AOE escaped before impact");Near(Total(enter),945,"AOE hits enemy entering fixed circle");
  Reset();caster=Hero(HeroId.Nerok,0,Vector3.zero);escape=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  caster.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(caster));Run(.32f);escape.transform.position+=Vector3.right*3;Run(.6f);
  Near(Total(escape),1000,"hook misses sideways dodge");Check(!escape.GetComponent<HeroLocomotion>().IsForcedMoving,"missed hook has no pull");
  Reset();caster=Hero(HeroId.Nyra,0,Vector3.zero);escape=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  caster.GetComponent<PlayerHeroAbilities>().CastAimed(3,Forward(caster));Run(.34f);float firstDamage=1000-Total(escape);escape.transform.position+=Vector3.right*3;Run(.8f);
  Near(firstDamage,64f/3,"first volley shot");Near(1000-Total(escape),firstDamage,"later volley shots keep chosen direction");
  Reset();caster=Hero(HeroId.Mira,0,Vector3.zero);var invalid=Hero(HeroId.Kaelon,1,Vector3.forward*2);
  Check(!caster.GetComponent<HeroAbilityExecution>().TryCastAimed(1,new AimPose(Vector3.forward,invalid.transform.position,invalid.GetComponent<Health>())),"support rejects enemy");
 }
 static void Gestures()
 {
  Reset();var a=Hero(HeroId.Elyra,0,Vector3.zero);var aim=ManualAiming.For(a);
  Check(aim.Begin(0,new Vector2(700,100)),"hold begins");Near(a.GetComponent<PlayerHeroAbilities>().RemainingFor(0),0,"holding spends no cooldown");
  aim.Drag(new Vector2(796,100),1,false);Near(Vector3.Angle(aim.Pose.Direction,Vector3.right),0,"drag direction");Near(aim.Pose.Point.magnitude,7.3f,"dragged AOE clamped to range");
  a.GetComponent<HeroMotor>().SetMoveInput(new Vector2(0,1));a.transform.position=Vector3.forward;Call(aim,"LateUpdate");Near(aim.Pose.Point.z,1,"aim follows moving caster");
  Check(aim.Release(),"release casts");Check(!aim.Active,"preview clears after release");Check(a.GetComponent<HeroMotor>().HasMoveInput,"aim does not clear movement");
  Check(!aim.Begin(0,new Vector2(700,100)),"cooldown blocks gesture");
  Check(aim.Begin(1,new Vector2(700,100)),"next skill begins without waiting");aim.Drag(new Vector2(750,140),1,true);Check(!aim.Release(),"cancel zone discards cast");Near(a.GetComponent<PlayerHeroAbilities>().RemainingFor(1),0,"cancel does not spend cooldown");
  Check(aim.Begin(1,new Vector2(700,100)),"recovered after cancel");a.AddComponent<HeroCombatStatus>().Stun(.1f);Call(aim,"LateUpdate");Check(!aim.Active,"stun clears held gesture");a.GetComponent<HeroCombatStatus>().Clear();
  Check(aim.Begin(1,new Vector2(700,100)),"begin after stun");Time.timeScale=0;Call(aim,"LateUpdate");Check(!aim.Active,"pause clears gesture");Time.timeScale=1;
  Check(aim.Begin(1,new Vector2(700,100)),"tap begins");Check(aim.Release(),"short tap can cast empty ground");
  Reset();a=Hero(HeroId.Veyr,0,Vector3.zero);aim=ManualAiming.For(a);Check(aim.Begin(4,new Vector2(800,100)),"basic has same gesture");Near(a.GetComponent<BasicAttack>().RemainingCooldown,0,"basic waits for release");Check(aim.Release(),"basic tap can miss");
  foreach(var dimensions in new[]{new Vector2(960,540),new Vector2(2340,1080),new Vector2(540,960)})
  {
   var l=new CombatHudLayout(new Rect(20,30,dimensions.x,dimensions.y));var button=l.Action(0);
   Vector2 screen=new Vector2(l.Safe.x+button.center.x*l.Scale,l.Safe.yMax-button.center.y*l.Scale);
   Check(l.Hit(l.Point(screen))==0,"safe-area hit test roundtrip");Check(l.CancelZone.x>=0&&l.CancelZone.xMax<=l.Width&&l.CancelZone.yMax<=l.Height,"cancel zone within viewport");
  }
 }
 static void TouchHud()
 {
  Reset();var a=Hero(HeroId.Elyra,0,Vector3.zero);var hud=new GameObject("HUD").AddComponent<PrototypeHUD>();hud.Initialize(a.GetComponent<Health>(),null,null);
  var layout=new CombatHudLayout(Screen.safeArea);Vector2 button=new Vector2(layout.Action(0).center.x,Screen.height-layout.Action(0).center.y);
  Touch.activeTouches.Add(new Touch{touchId=1,phase=TouchPhase.Began,screenPosition=new Vector2(140,140)});
  Touch.activeTouches.Add(new Touch{touchId=2,phase=TouchPhase.Began,screenPosition=button});Call(hud,"Update");
  Check(a.GetComponent<ManualAiming>().Active,"HUD begins aim without casting");Near(a.GetComponent<PlayerHeroAbilities>().QRemaining,0,"HUD hold defers cast");
  Touch.activeTouches[0]=new Touch{touchId=1,phase=TouchPhase.Moved,screenPosition=new Vector2(195,140)};
  Touch.activeTouches[1]=new Touch{touchId=2,phase=TouchPhase.Moved,screenPosition=button+new Vector2(96,0)};Call(hud,"Update");
  Check(a.GetComponent<HeroMotor>().HasMoveInput,"left thumb still moves during aim");Check(a.GetComponent<ManualAiming>().Active,"drag outside attack button stays captured");
  Touch.activeTouches[1]=new Touch{touchId=2,phase=TouchPhase.Ended,screenPosition=button+new Vector2(96,0)};Call(hud,"Update");
  Check(a.GetComponent<PlayerHeroAbilities>().QRemaining>0,"HUD casts on release");Check(!a.GetComponent<ManualAiming>().Active,"HUD clears gesture");Check(a.GetComponent<HeroMotor>().HasMoveInput,"release preserves left thumb");
 }
 static void Network()
 {
  Reset();var a=Hero(HeroId.Elyra,0,Vector3.zero);a.AddComponent<CoopHeroSlot>().Slot=1;
  var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
  var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
  var pose=new AimPose(Vector3.right,new Vector3(5,0,1));Check(CoopGameplayCommands.TrySendAbility(a,0,pose),"network send aiming intent");
  Check(manager.CustomMessagingManager.LastMessage=="DF_COOP_ABILITY_V4","protocol revision updated");
  var reader=new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket);reader.ReadValueSafe(out string round);reader.ReadValueSafe(out int roster);reader.ReadValueSafe(out Unity.Collections.FixedString128Bytes id);reader.ReadValueSafe(out bool target);reader.ReadValueSafe(out int slot);
  reader.ReadValueSafe(out float dx);reader.ReadValueSafe(out float dy);reader.ReadValueSafe(out float dz);reader.ReadValueSafe(out float px);reader.ReadValueSafe(out float py);reader.ReadValueSafe(out float pz);
  Check(!target,"normal spell has no soft target reference");Near(dx,1,"network preserves chosen direction");Near(px,5,"network preserves ground x");Near(pz,1,"network preserves ground z");
  CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V4"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));Run(.5f);
  Near(Alterverse.Visuals.HeroSkillVfx.LastPoint.x,5,"server executes transmitted AOE point");Near(Alterverse.Visuals.HeroSkillVfx.LastPoint.z,1,"server does not reacquire enemy");
  Unlock(a.GetComponent<HeroAbilityExecution>());
  Check(a.GetComponent<PlayerHeroAbilities>().CastNetworkAuthoritative(0,new AimPose(Vector3.right,new Vector3(100,0,0))),"server accepts clamped ground intent");
  Near(Alterverse.Visuals.HeroSkillVfx.LastPoint.magnitude,7.3f,"server clamps forged point to cast range");
 }
 static void InterruptionsAndCapacity()
 {
  Reset();var a=Hero(HeroId.Elyra,0,Vector3.zero);var b=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  a.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(a));a.AddComponent<HeroCombatStatus>().Stun(.1f);Run(.6f);
  Near(Total(b),1000,"brief stun cancels longer AOE windup");
  Reset();a=Hero(HeroId.Elyra,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  a.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(a));a.GetComponent<Health>().TakeDamage(2000);Run(.6f);
  Near(Total(b),1000,"death cancels AOE");
  Reset();a=Hero(HeroId.Nyra,0,Vector3.zero);var delivery=HeroAttackDelivery.For(a);
  for(int i=0;i<AimingSettings.Current.deliveryCapacity;i++)Check(delivery.BasicAimed(Forward(a),a.GetComponent<BasicAttack>().Aim,1),"pool slot available");
  Check(!a.GetComponent<BasicAttack>().TryAttackAimed(Forward(a)),"full pool rejects cast");Near(a.GetComponent<BasicAttack>().RemainingCooldown,0,"pool failure does not spend cooldown");
  Check(!a.GetComponent<PlayerHeroAbilities>().CastAimed(3,Forward(a)),"volley reserves all three slots before cooldown");Near(a.GetComponent<PlayerHeroAbilities>().UltRemaining,0,"rejected volley no cooldown");
  Reset();a=Hero(HeroId.Kaelon,0,Vector3.zero);delivery=HeroAttackDelivery.For(a);
  for(int i=0;i<AimingSettings.Current.deliveryCapacity;i++)delivery.BasicAimed(Forward(a),a.GetComponent<BasicAttack>().Aim,1);
  Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(1,Forward(a)),"full projectile pool does not block self shield");
  Reset();a=Hero(HeroId.Elyra,0,Vector3.zero);var aim=ManualAiming.For(a);
  aim.Begin(0,new Vector2(700,100));aim.Drag(new Vector2(780,100),1,false);aim.Drag(new Vector2(700,100),1,false);
  Near(aim.Pose.Point.magnitude,0,"drag back allows AOE at caster");
  var area=AimingSettings.Current.For(HeroId.Elyra,0);area.aimType=AimType.AreaCone;
  AimingSettings.Current.overrides=new[]{new AimingSettings.AttackOverride{hero=HeroId.Elyra,slot=0,spec=area}};
  b=Hero(HeroId.Kaelon,1,Angle(10,4));var outside=Hero(HeroId.Kaelon,1,Angle(50,4));
  a.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(a));Run(.6f);Near(Total(b),945,"explicit AOE cone damages inside");Near(Total(outside),1000,"explicit AOE cone misses outside");
  Reset();a=Hero(HeroId.Nyra,0,Vector3.zero);aim=ManualAiming.For(a);aim.Begin(0,new Vector2(700,100));a.GetComponent<Health>().TakeDamage(2000);Call(aim,"LateUpdate");Check(!aim.Active,"death clears held aim");
  Reset();a=Hero(HeroId.Nyra,0,Vector3.zero);a.GetComponent<PlayerHeroAbilities>().enabled=false;Check(!a.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(a)),"disabled player component rejects cast");
  Check(!ManualAiming.For(a).Begin(0,new Vector2(700,100)),"disabled attack cannot begin a preview");
  Reset();a=Hero(HeroId.Nerok,0,Vector3.zero);
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,.5f,2);wall.transform.localScale=new Vector3(2,2,.2f);
  Check(!a.GetComponent<PlayerHeroAbilities>().CastAimed(3,Forward(a)),"Nerok ground placement retains wall restriction");
  Near(a.GetComponent<PlayerHeroAbilities>().UltRemaining,0,"blocked vortex no cooldown");
  Reset();a=Hero(HeroId.Nyra,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,new Vector3(-.4f,0,1.6f));
  b.GetComponent<HeroLocomotion>().FrameStartPosition=new Vector3(2,0,1.6f);float contact;
  Check(AimGeometry.HitBody(b.GetComponent<Health>(),Vector3.zero,Vector3.forward*2,.3f,true,out contact),"full frame target crosses trajectory");
  Check(!AimGeometry.HitBody(b.GetComponent<Health>(),Vector3.zero,Vector3.forward*2,.3f,true,out contact,.5f),"expired flight excludes later target motion");
  var basicSpec=a.GetComponent<BasicAttack>().Aim;basicSpec.castRange=4f;
  AimingSettings.Current.overrides=new[]{new AimingSettings.AttackOverride{hero=HeroId.Nyra,slot=-1,spec=basicSpec}};
  Near(a.GetComponent<BasicAttack>().Aim.castRange,4f,"central basic range override is respected");
  Reset();a=Hero(HeroId.Nerok,0,Vector3.zero);b=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  var homing=AimingSettings.Current.For(HeroId.Nerok,0);homing.aimType=AimType.Homing;homing.targetingMode=TargetingMode.Homing;
  AimingSettings.Current.overrides=new[]{new AimingSettings.AttackOverride{hero=HeroId.Nerok,slot=0,spec=homing}};
  Check(!a.GetComponent<PlayerHeroAbilities>().CastAimed(0,Forward(a)),"explicit homing requires a captured enemy");
  Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(0,new AimPose(Vector3.forward,b.transform.position,b.GetComponent<Health>())),"explicit homing can acquire a target");
  Run(.32f);b.transform.position=new Vector3(2,0,4);Run(.4f);Near(Total(b),972,"only explicit homing follows the moving target");
 }
 static void Main()
 {
  Geometry();AttackCases();Kits();Gestures();TouchHud();Network();InterruptionsAndCapacity();
  Reset();Alterverse.EditorChecks.BasicAttackDeliveryRegression.Run();
  Reset();Alterverse.EditorChecks.AttackDeliveryRegression.Run();
  Console.WriteLine("PASS "+checks+" assertions: geometry, all 8 basics / 32 skills, dodges, AOE, gestures, multitouch, network direction/point, interrupts.");
 }
}
