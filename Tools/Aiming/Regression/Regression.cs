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
partial class Regression
{
 static int checks;
 static void Check(bool yes,string reason){checks++;if(!yes)throw new Exception(reason);}
 static void Near(float a,float b,string reason)=>Check(Math.Abs(a-b)<.01f,reason+" actual="+a+" expected="+b);
 static void Call(object o,string method)=>GameObject.Invoke(o,method);
 static void Reset()
 {
  foreach(var g in GameObject.All.ToArray())
  {
   var commands=g.GetComponent<CoopGameplayCommands>();if(commands!=null)Call(commands,"OnDestroy");
   g.SetActive(false);
  }
  foreach(var g in GameObject.All.ToArray())if(g.GetComponent<Alterverse.Visuals.HeroVfxPool>()!=null)Call(g.GetComponent<Alterverse.Visuals.HeroVfxPool>(),"OnDestroy");
  GameObject.All.Clear();Health.Active.Clear();Clock.Reset();Camera.main=null;Mouse.current=null;Keyboard.current=null;
  Touch.activeTouches.Clear();CoopAuthority.Active=false;CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.LocalIsHost=true;
  Unity.Netcode.NetworkManager.Singleton=null;Alterverse.Visuals.CombatMotion.Starts=0;
  AimingSettings.Current.overrides=new AimingSettings.AttackOverride[0];
  AimingSettings.Current.rememberAimDirection=false;
  AimingSettings.Current.basicLeadQuality=1f;
  Resources.PreviewShaderAvailable=false;Screen.width=960;Screen.height=540;
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
 static void Step(float dt=.01f,Action movement=null)
 {
  foreach(var g in GameObject.All.Where(g=>g.activeInHierarchy)) {var l=g.GetComponent<HeroLocomotion>();if(l!=null){l.FrameStartPosition=g.transform.position;l.Advance(Time.time+dt);}}
  Clock.Advance(dt);
  movement?.Invoke();
  foreach(var g in GameObject.All.ToArray().Where(g=>g.activeInHierarchy)) {var e=g.GetComponent<HeroAbilityExecution>();if(e!=null)Call(e,"Update");}
  foreach(var g in GameObject.All.ToArray().Where(g=>g.activeInHierarchy)) {var d=g.GetComponent<HeroAttackDelivery>();if(d!=null)Call(d,"Update");var fx=g.GetComponent<Alterverse.Visuals.HeroVfxPool>();if(fx!=null){Call(fx,"Update");Call(fx,"LateUpdate");}}
 }
 static void Run(float seconds){for(float t=0;t<seconds-.000001f;t+=.01f)Step();}
 static AimPose Forward(GameObject a,float distance=4)=>new AimPose(Vector3.forward,a.transform.position+Vector3.forward*distance);
 static Vector3 Angle(float angle,float range)=>new Vector3(Mathf.Sin(angle*Mathf.Deg2Rad)*range,0,Mathf.Cos(angle*Mathf.Deg2Rad)*range);
 static void Geometry()
 {
  Reset();
  Check(AimingSettings.Current.For(HeroId.Nyra,0).aimType==AimType.Beam,"Prism Shot is a laser");
  Check(AimingSettings.Current.For(HeroId.Traveler,0).aimType==AimType.Beam,"Temporal Beam resolves as a laser on release");
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
  {
   var entry=AimingSettings.Current.For(hero,slot);var baseline=HeroAbilityKit.LoadoutFor(hero).Spec(slot);
   Near(entry.castRange,baseline.Range,"cast range comes from catalog");Near(entry.aoeRadius,baseline.Radius,"AOE radius comes from catalog");
   Near(entry.coneAngle,HeroAttackDelivery.IsRanged(hero)?35:70,"role-specific aim cone");
   Near(entry.aimAssistAngle,8,"central assist default");
  }
  Check(AimGeometry.Cone(Vector3.zero,Vector3.forward,Angle(34,2),3,70),"70-degree melee interior");
  Check(!AimGeometry.Cone(Vector3.zero,Vector3.forward,Angle(36,2),3,70),"melee exterior");
  Check(!AimGeometry.Cone(Vector3.zero,Vector3.forward,Angle(18,2),3,35),"narrow ranged cone");
  Check(!AimGeometry.Cone(Vector3.zero,Vector3.forward,Vector3.forward*3.01f,3,70),"range exact");
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
   if(hero==HeroId.Timmy)continue; // Targeted leap/channel is covered by TimmyCases.
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
  Reset();a=Hero(HeroId.Veyr,0,Vector3.zero);aim=ManualAiming.For(a);Check(!aim.Begin(4,new Vector2(800,100)),"basic cannot enter skill aiming");Check(!aim.Active,"basic has no preview pose");Check(!a.GetComponent<BasicAttack>().TryAttack(),"basic waits for a valid soft target");
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
 static Vector2 ButtonScreen(int slot)
 {
  var layout=new CombatHudLayout(Screen.safeArea);var button=layout.Action(slot);
  return new Vector2(layout.Safe.x+button.center.x*layout.Scale,layout.Safe.yMax-button.center.y*layout.Scale);
 }
 static PrototypeHUD HudFor(GameObject a)
 {
  var hud=new GameObject("HUD").AddComponent<PrototypeHUD>();hud.Initialize(a.GetComponent<Health>(),null,null);return hud;
 }
 static void BasicHud()
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
  {
   Reset();var a=Hero(hero,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Vector3.forward);
   var hud=HudFor(a);var basic=a.GetComponent<BasicAttack>();float interval=basic.AttackCooldown;
   var point=ButtonScreen(4);
   Touch.activeTouches.Add(new Touch{touchId=1,phase=TouchPhase.Began,screenPosition=new Vector2(140,140)});
   Touch.activeTouches.Add(new Touch{touchId=2,phase=TouchPhase.Began,screenPosition=point});Call(hud,"Update");
   Check(basic.RemainingCooldown>0,hero+" basic fires on press");Check(!a.GetComponent<ManualAiming>().Active,hero+" basic never opens preview");
   float pressedCooldown=basic.RemainingCooldown;
   Touch.activeTouches[0]=new Touch{touchId=1,phase=TouchPhase.Moved,screenPosition=new Vector2(195,140)};
   Touch.activeTouches[1]=new Touch{touchId=2,phase=TouchPhase.Stationary,screenPosition=point};Call(hud,"Update");
   Near(basic.RemainingCooldown,pressedCooldown,hero+" hold respects cooldown");Check(a.GetComponent<HeroMotor>().HasMoveInput,hero+" movement continues during basic hold");
   Run(interval+.02f);Call(hud,"Update");Check(basic.RemainingCooldown>0,hero+" held basic repeats when ready");
   // Release exactly when another attack could fire: it must stop, not add a third shot.
   Run(interval+.02f);Touch.activeTouches[1]=new Touch{touchId=2,phase=TouchPhase.Ended,screenPosition=point};Call(hud,"Update");
   Near(basic.RemainingCooldown,0,hero+" release never fires an extra attack");Check(a.GetComponent<HeroMotor>().HasMoveInput,hero+" release keeps movement");
   float damage=a.GetComponent<HeroCombatProfile>().Attack*(hero==HeroId.Rook?1.05f:1f);
   Run(.4f);Near(1000-Total(enemy),damage*2,hero+" press plus hold delivers exactly two geometric hits");
  }
  Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);Hero(HeroId.Kaelon,1,Vector3.forward);var controller=HudFor(actor);var attack=actor.GetComponent<BasicAttack>();var buttonPoint=ButtonScreen(4);
  attack.TryAttack();float initial=attack.RemainingCooldown;
  Touch.activeTouches.Add(new Touch{touchId=7,phase=TouchPhase.Began,screenPosition=buttonPoint});Call(controller,"Update");Near(attack.RemainingCooldown,initial,"press during cooldown does not duplicate cast");
  Touch.activeTouches[0]=new Touch{touchId=7,phase=TouchPhase.Stationary,screenPosition=buttonPoint};Run(attack.AttackCooldown+.02f);Call(controller,"Update");Check(attack.RemainingCooldown>0,"hold started on cooldown resumes when ready");
  Touch.activeTouches[0]=new Touch{touchId=7,phase=TouchPhase.Moved,screenPosition=buttonPoint+new Vector2(-150,0)};Run(attack.AttackCooldown+.02f);Call(controller,"Update");
  Near(attack.RemainingCooldown,0,"leaving basic button stops repeats");Check(!actor.GetComponent<ManualAiming>().Active,"dragging basic does not create aim");Near(Vector3.Angle(actor.transform.forward,Vector3.forward),0,"basic drag cannot steer attack");
  Touch.activeTouches[0]=new Touch{touchId=7,phase=TouchPhase.Moved,screenPosition=buttonPoint};Call(controller,"Update");Check(attack.RemainingCooldown>0,"returning to held button resumes");
  Touch.activeTouches.Clear();Run(attack.AttackCooldown+.02f);Call(controller,"Update");Near(attack.RemainingCooldown,0,"lost pointer stops repeats");
  Touch.activeTouches.Add(new Touch{touchId=8,phase=TouchPhase.Began,screenPosition=ButtonScreen(0)});Call(controller,"Update");Check(actor.GetComponent<ManualAiming>().Active,"skill still begins aim after basic release");
  Reset();actor=Hero(HeroId.Veyr,0,Vector3.zero);Hero(HeroId.Kaelon,1,Vector3.forward);controller=HudFor(actor);attack=actor.GetComponent<BasicAttack>();buttonPoint=ButtonScreen(4);
  Touch.activeTouches.Add(new Touch{touchId=7,phase=TouchPhase.Began,screenPosition=buttonPoint});Call(controller,"Update");
  Touch.activeTouches[0]=new Touch{touchId=7,phase=TouchPhase.Stationary,screenPosition=buttonPoint};actor.AddComponent<HeroCombatStatus>().Stun(.1f);Call(controller,"Update");Run(attack.AttackCooldown+.2f);Call(controller,"Update");Near(attack.RemainingCooldown,0,"stun interrupts held basic until a new press");
  Reset();actor=Hero(HeroId.Nyra,0,Vector3.zero);Hero(HeroId.Kaelon,1,Vector3.forward);controller=HudFor(actor);attack=actor.GetComponent<BasicAttack>();Mouse.current=new Mouse();Mouse.current.position.Value=ButtonScreen(4);Mouse.current.leftButton.isPressed=true;Mouse.current.leftButton.wasPressedThisFrame=true;Call(controller,"Update");
  Check(attack.RemainingCooldown>0,"mouse basic fires on press");Check(!actor.GetComponent<ManualAiming>().Active,"mouse basic has no aim preview");
  Mouse.current.leftButton.wasPressedThisFrame=false;Run(attack.AttackCooldown+.02f);Call(controller,"Update");Check(attack.RemainingCooldown>0,"mouse hold repeats");
  Run(attack.AttackCooldown+.02f);Mouse.current.leftButton.isPressed=false;Mouse.current.leftButton.wasReleasedThisFrame=true;Call(controller,"Update");Near(attack.RemainingCooldown,0,"mouse release cannot double cast");
 }
 static void BasicSoftTargets()
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
  {
   Reset();var actor=Hero(hero,0,Vector3.zero);var chosen=Hero(HeroId.Kaelon,1,Vector3.right*1.6f);var ally=Hero(HeroId.Mira,0,Vector3.forward*.6f);
   var soldier=new GameObject("enemy soldier");soldier.transform.position=Vector3.forward*.9f;soldier.AddComponent<TeamMember>().Initialize(1);soldier.AddComponent<Health>().Configure(1000);soldier.AddComponent<LaneMinion>().Team=1;
   var basic=actor.GetComponent<BasicAttack>();var soft=actor.GetComponent<SoftTargeting>();
   Check(basic.TryAttack(),hero+" basic automatically acquires sideways hero");Check(soft.Current==chosen.GetComponent<Health>(),hero+" basic restores hero priority over closer soldier");
   Near(Vector3.Angle(actor.transform.forward,Vector3.right),0,hero+" basic faces selected target beyond skill assist window");Check(!ManualAiming.For(actor).Active,hero+" soft target does not enter manual aiming");
   Run(basic.AttackCooldown+.02f);Check(Total(chosen)<1000,hero+" auto-selected hero receives geometric hit");Near(Total(ally),1000,hero+" auto selection excludes ally");Near(soldier.GetComponent<Health>().Current,1000,hero+" off-axis soldier not hit");
   var closer=Hero(HeroId.Kaelon,1,Vector3.left*1.2f);
   Check(basic.TryAttack(),hero+" second basic starts");Check(soft.Current==chosen.GetComponent<Health>(),hero+" sticky selection keeps valid hero");Run(basic.AttackCooldown+.02f);
   chosen.GetComponent<Health>().TakeDamage(2000);Check(basic.TryAttack(),hero+" reacquires after target death");Check(soft.Current==closer.GetComponent<Health>(),hero+" dead soft target replaced by valid hero");
  }
  Reset();var a=Hero(HeroId.Veyr,0,Vector3.zero);Hero(HeroId.Mira,0,Vector3.forward);var far=Hero(HeroId.Kaelon,1,Vector3.forward*5);
  Check(!a.GetComponent<BasicAttack>().TryAttack(),"out-of-range enemies and allies cannot start basic");Near(a.GetComponent<BasicAttack>().RemainingCooldown,0,"no eligible soft target spends no cooldown");
  far.transform.position=Vector3.forward*-1.6f;Check(a.GetComponent<BasicAttack>().TryAttack(),"basic acquires target behind player");far.transform.position=Vector3.forward*1.6f;Run(.5f);Near(Total(far),1000-HeroIdentity.AttackDamage(HeroId.Veyr),"accepted melee basic retains its target during windup");
  Reset();a=Hero(HeroId.Nyra,0,Vector3.zero);far=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  Check(a.GetComponent<BasicAttack>().TryAttack(),"ranged basic starts with restored target");Run(.09f);far.transform.position=new Vector3(3,0,4);Run(.4f);Near(Total(far),978,"accepted ranged basic follows its selected target after release");
  Reset();a=Hero(HeroId.Nyra,0,Vector3.zero);var hud=HudFor(a);var button=ButtonScreen(4);
  Touch.activeTouches.Add(new Touch{touchId=9,phase=TouchPhase.Began,screenPosition=button});Call(hud,"Update");Near(a.GetComponent<BasicAttack>().RemainingCooldown,0,"held basic waits without eligible target");
  far=Hero(HeroId.Kaelon,1,Vector3.left*2);Touch.activeTouches[0]=new Touch{touchId=9,phase=TouchPhase.Stationary,screenPosition=button};Call(hud,"Update");
  Check(a.GetComponent<BasicAttack>().RemainingCooldown>0,"held basic starts when a target enters range");Check(a.GetComponent<SoftTargeting>().Current==far.GetComponent<Health>(),"hold updates the visible soft target");
 }
 static void RejectedBasicWindups()
 {
  Reset();var actor=Hero(HeroId.Veyr,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;
  var target=Hero(HeroId.Kaelon,1,Vector3.forward).GetComponent<Health>();var basic=actor.GetComponent<BasicAttack>();
  var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
  var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;CoopAuthority.LocalIsHost=false;Call(service,"Update");
  int attempts=0;
  for(int i=0;i<20;i++)
  {
   if(basic.TryAttack())attempts++;basic.ApplyNetworkCooldown(0,10000);Clock.Advance(.02f);
  }
  Check(attempts>1&&attempts<=4,"zero cooldown rejection retries are bounded independently of server cooldown");
  Check(Alterverse.Visuals.CombatMotion.Starts==0,"rejected held basic never predicts phantom windups");
  float hp=Total(target.gameObject);
  basic.ApplyNetworkWindup(target.transform.position,target,.16f,.04f);
  Check(Alterverse.Visuals.CombatMotion.Starts==1,"accepted server windup starts owner presentation once");
  Near(Alterverse.Visuals.CombatMotion.Elapsed,.04f,"confirmed presentation keeps network age");
  basic.ApplyNetworkWindup(target.transform.position,target,.16f,.4f);
  Check(Alterverse.Visuals.CombatMotion.Starts==1,"late acceptance cannot replay expired windup");
  Near(Total(target.gameObject),hp,"confirmation never deals client damage");
  Clock.Advance(1);target.transform.position=Vector3.forward*(basic.Aim.castRange+1);
  Check(!basic.TryAttackAimed(new AimPose(Vector3.forward,target.transform.position,target,true)),"explicit out-of-range client target rejected before sending");
  CoopAuthority.LocalHasServerAuthority=true;CoopAuthority.LocalIsHost=true;manager.IsServer=true;
  int confirmations=0;
  Action<Alterverse.Visuals.HeroVfxEvent> onCue=e=>{if(e.Cue==Alterverse.Visuals.HeroVfxCue.BasicWindup)confirmations++;};
  Alterverse.Visuals.HeroVfxEvents.Emitted+=onCue;
  try
  {
   Check(!basic.TryAttackNetworkAuthoritative(target),"host rejects distant target");
   Check(confirmations==0,"host range rejection emits no acceptance");
   target.transform.position=Vector3.forward;
   Check(basic.TryAttackNetworkAuthoritative(target),"host still accepts valid basic");
   Check(confirmations==1,"only queued authoritative strike emits acceptance");
   Check(!basic.TryAttackNetworkAuthoritative(target),"cooldown rejects repeated accepted strike");
   Check(confirmations==1,"cooldown rejection emits no duplicate acceptance");
  }
  finally { Alterverse.Visuals.HeroVfxEvents.Emitted-=onCue; }

 }
 static void Network()
 {
  Reset();var a=Hero(HeroId.Elyra,0,Vector3.zero);a.AddComponent<CoopHeroSlot>().Slot=1;
  var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
  var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
  var pose=new AimPose(Vector3.right,new Vector3(5,0,1));Check(CoopGameplayCommands.TrySendAbility(a,0,pose),"network send aiming intent");
  Check(manager.CustomMessagingManager.LastMessage=="DF_COOP_ABILITY_V7","protocol revision updated");
  var reader=new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket);reader.ReadValueSafe(out string round);reader.ReadValueSafe(out int roster);reader.ReadValueSafe(out Unity.Collections.FixedString128Bytes id);reader.ReadValueSafe(out uint sequence);reader.ReadValueSafe(out bool target);reader.ReadValueSafe(out int slot);
  reader.ReadValueSafe(out float dx);reader.ReadValueSafe(out float dy);reader.ReadValueSafe(out float dz);reader.ReadValueSafe(out float px);reader.ReadValueSafe(out float py);reader.ReadValueSafe(out float pz);
  Check(!target,"normal spell has no soft target reference");Near(dx,1,"network preserves chosen direction");Near(px,5,"network preserves ground x");Near(pz,1,"network preserves ground z");
  CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V7"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));Run(.5f);
  Near(Alterverse.Visuals.HeroSkillVfx.LastPoint.x,5,"server executes transmitted AOE point");Near(Alterverse.Visuals.HeroSkillVfx.LastPoint.z,1,"server does not reacquire enemy");
  Unlock(a.GetComponent<HeroAbilityExecution>());
  Check(a.GetComponent<PlayerHeroAbilities>().CastNetworkAuthoritative(0,new AimPose(Vector3.right,new Vector3(100,0,0))),"server accepts clamped ground intent");
  Near(Alterverse.Visuals.HeroSkillVfx.LastPoint.magnitude,7.3f,"server clamps forged point to cast range");
  CoopAuthority.LocalHasServerAuthority=false;manager.IsServer=false;Hero(HeroId.Kaelon,1,Vector3.left*2);
  Check(a.GetComponent<BasicAttack>().TryAttack(),"client basic restores automatic target selection");Check(manager.CustomMessagingManager.LastMessage=="DF_COOP_BASIC_V7","auto-target basic uses the keyed V7 route");
  reader=new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket);reader.ReadValueSafe(out round);reader.ReadValueSafe(out roster);reader.ReadValueSafe(out id);reader.ReadValueSafe(out sequence);reader.ReadValueSafe(out target);
  Check(target,"automatic ranged basic sends selected target for launch-time refresh");
  reader.ReadValueSafe(out int kind);reader.ReadValueSafe(out int targetSlot);reader.ReadValueSafe(out int targetTeam);reader.ReadValueSafe(out float tx);reader.ReadValueSafe(out float ty);reader.ReadValueSafe(out float tz);
  Check(kind==1&&targetTeam==1,"automatic basic identifies an enemy hero");Near(tx,-2,"automatic basic transmits target lookup point");
  reader.ReadValueSafe(out dx);reader.ReadValueSafe(out dy);reader.ReadValueSafe(out dz);Near(dx,-1,"network basic preserves automatic soft-target direction");
  Unlock(a.GetComponent<HeroAbilityExecution>());
  Check(a.GetComponent<PlayerHeroAbilities>().CastAimed(2,new AimPose(Vector3.right,new Vector3(100,0,0))),"client sends Phase Fold through normal ability command");
  var phasePacket=manager.CustomMessagingManager.LastPacket;
  Near(a.GetComponent<Health>().TemporaryShield,0,"client cooldown prediction does not grant shield");
  CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;Unlock(a.GetComponent<HeroAbilityExecution>());
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V7"](1,new Unity.Netcode.FastBufferReader(phasePacket));
  Near(a.transform.position.x,2.3f,"host clamps Phase Fold to 2.3 meters");
  Near(a.GetComponent<Health>().TemporaryShield,20,"host applies Phase Fold shield once");
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
 static void SupportTargeting()
 {
  foreach(int slot in new[]{1,2})
  {
   Reset();var actor=Hero(HeroId.Mira,0,Vector3.zero);var ally=Hero(HeroId.Nyra,0,Vector3.forward*3);
   var self=actor.GetComponent<Health>();var friend=ally.GetComponent<Health>();self.TakeDamage(400);friend.TakeDamage(200);
   var abilities=actor.GetComponent<PlayerHeroAbilities>();var spec=abilities.AimFor(slot);float power=abilities.SpecFor(slot).Power;
   AimPose tap=AimGeometry.TapPose(actor,spec);
   Check(tap.Target==friend,"Tahla targets teammate before wounded self, slot "+slot);
   float selfBefore=Total(actor),allyBefore=Total(ally);
   Check(slot==1?abilities.CastW():abilities.CastE(),"Tahla tap supports aimed teammate");
   Near(Total(ally)-allyBefore,power,"support amount unchanged");Near(Total(actor),selfBefore,"aimed teammate receives support exclusively");
   Check(abilities.RemainingFor(slot)>0,"support starts cooldown");
   Check(!(slot==1?abilities.CastW():abilities.CastE()),"support cooldown prevents repeat");

   foreach(bool explicitSelf in new[]{false,true})
   {
    Reset();actor=Hero(HeroId.Mira,0,Vector3.zero);actor.GetComponent<Health>().TakeDamage(400);
    ally=Hero(HeroId.Nyra,0,Vector3.forward*3);ally.GetComponent<Health>().TakeDamage(200);selfBefore=Total(actor);allyBefore=Total(ally);
    Check(actor.GetComponent<PlayerHeroAbilities>().CastNetworkAuthoritative(slot,new AimPose(Vector3.forward,ally.transform.position,
        explicitSelf?actor.GetComponent<Health>():null)),"authoritative support checks aimed teammate before self");
    Near(Total(ally)-allyBefore,power,"direction-only or self-reference support reaches aimed teammate");
    Near(Total(actor),selfBefore,"authoritative support cannot self-cast over an aimed teammate");
   }

   Reset();actor=Hero(HeroId.Mira,0,Vector3.zero);ally=Hero(HeroId.Nyra,0,Vector3.right*3);
   self=actor.GetComponent<Health>();friend=ally.GetComponent<Health>();self.TakeDamage(400);friend.TakeDamage(200);
   var behind=Hero(HeroId.Rook,0,Vector3.left);behind.GetComponent<Health>().TakeDamage(600);
   var aiming=ManualAiming.For(actor);Check(aiming.Begin(slot,new Vector2(700,100)),"Tahla support gesture begins");
   aiming.Drag(new Vector2(796,100),1,false);Check(aiming.Pose.Target==friend,"drag selects teammate in chosen direction");
   selfBefore=Total(actor);allyBefore=Total(ally);float behindBefore=Total(behind);
   Check(aiming.Release(),"drag releases support");Near(Total(ally)-allyBefore,power,"dragged teammate gets support");
   Near(Total(actor),selfBefore,"drag does not also support self");Near(Total(behind),behindBefore,"wounded teammate outside aim is untouched");

   Reset();actor=Hero(HeroId.Mira,0,Vector3.zero);self=actor.GetComponent<Health>();self.TakeDamage(400);
   Hero(HeroId.Nyra,0,(-Vector3.forward)*2).GetComponent<Health>().TakeDamage(500);Hero(HeroId.Kaelon,1,Vector3.forward);
   abilities=actor.GetComponent<PlayerHeroAbilities>();tap=AimGeometry.TapPose(actor,abilities.AimFor(slot));
   Check(tap.Target==null,"no aimed teammate leaves target empty");selfBefore=Total(actor);
   Check(abilities.CastAimed(slot,tap),"no teammate applies support to self");Near(Total(actor)-selfBefore,power,"self fallback amount unchanged");

   Reset();actor=Hero(HeroId.Mira,0,Vector3.zero);self=actor.GetComponent<Health>();self.TakeDamage(400);
   ally=Hero(HeroId.Nyra,0,Vector3.forward*3);friend=ally.GetComponent<Health>();
   if(slot==2)friend.AddShield(50,4,false);
   abilities=actor.GetComponent<PlayerHeroAbilities>();tap=AimGeometry.TapPose(actor,abilities.AimFor(slot));
   Check(tap.Target==friend,"full or shielded teammate remains the aimed recipient");selfBefore=Total(actor);allyBefore=Total(ally);
   Check(!abilities.CastAimed(slot,tap),"support without benefit rejects cast");Near(Total(actor),selfBefore,"unneeded ally support never redirects to self");
   Near(Total(ally),allyBefore,"rejected support does not alter recipient");Near(abilities.RemainingFor(slot),0,"rejected support spends no cooldown");

   Reset();actor=Hero(HeroId.Mira,0,Vector3.zero);self=actor.GetComponent<Health>();self.TakeDamage(400);
   var dead=Hero(HeroId.Nyra,0,Vector3.forward);dead.GetComponent<Health>().TakeDamage(2000);
   var inactive=Hero(HeroId.Nyra,0,Vector3.forward*2);inactive.SetActive(false);
   var far=Hero(HeroId.Nyra,0,Vector3.forward*8);var minion=new GameObject("allied minion");minion.transform.position=Vector3.forward*2;
   minion.AddComponent<Health>();minion.AddComponent<TeamMember>().Initialize(0);
   abilities=actor.GetComponent<PlayerHeroAbilities>();tap=AimGeometry.TapPose(actor,abilities.AimFor(slot));
   Check(tap.Target==null,"dead, inactive, out-of-range and nonhero allies do not capture support");
   Check(abilities.CastAimed(slot,tap),"no valid teammate still permits self fallback");

   foreach(var position in new[]{Vector3.forward*8,(-Vector3.forward)*2})
   {
    Reset();actor=Hero(HeroId.Mira,0,Vector3.zero);actor.GetComponent<Health>().TakeDamage(400);
    ally=Hero(HeroId.Nyra,0,position);ally.GetComponent<Health>().TakeDamage(200);abilities=actor.GetComponent<PlayerHeroAbilities>();selfBefore=Total(actor);
    Check(!abilities.CastAimed(slot,new AimPose(Vector3.forward,position,ally.GetComponent<Health>())),"explicit ally outside range or cone is rejected");
    Near(Total(actor),selfBefore,"invalid explicit teammate cannot redirect support to self");Near(abilities.RemainingFor(slot),0,"invalid ally spends no cooldown");
   }

   foreach(bool aimAtAlly in new[]{true,false})
   {
    Reset();actor=Hero(HeroId.Mira,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;self=actor.GetComponent<Health>();self.TakeDamage(400);
    ally=Hero(HeroId.Nyra,0,aimAtAlly?Vector3.forward*3:(-Vector3.forward)*3);ally.AddComponent<CoopHeroSlot>().Slot=2;
    friend=ally.GetComponent<Health>();friend.TakeDamage(200);selfBefore=Total(actor);allyBefore=Total(ally);
    var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
    var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
    CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
    abilities=actor.GetComponent<PlayerHeroAbilities>();tap=AimGeometry.TapPose(actor,abilities.AimFor(slot));
    Check(abilities.CastAimed(slot,tap),"client routes support intent");Near(Total(actor),selfBefore,"client does not mutate self");Near(Total(ally),allyBefore,"client does not mutate ally");
    Unlock(actor.GetComponent<HeroAbilityExecution>());CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;
    CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
    manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V7"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));
    Near(Total(actor)-selfBefore,aimAtAlly?0:power,"host self fallback matches transmitted aim");
    Near(Total(ally)-allyBefore,aimAtAlly?power:0,"host teammate support matches transmitted aim");
    Check(abilities.RemainingFor(slot)>0,"host support commits cooldown");
   }
  }
 }
 static void SelfShieldPreviews()
 {
  foreach(HeroId hero in new[]{HeroId.Rook,HeroId.Kaelon,HeroId.Nerok})
  {
   Reset();Resources.PreviewShaderAvailable=true;var actor=Hero(hero,0,Vector3.zero);actor.GetComponent<Health>().TakeDamage(200);
   var enemy=Hero(HeroId.Nyra,1,Vector3.forward*2);var ally=Hero(HeroId.Mira,0,Vector3.right*2);
   var abilities=actor.GetComponent<PlayerHeroAbilities>();var spec=abilities.AimFor(1);
   Check(spec.aimType==AimType.Self,"defensive shield is self-only: "+hero);Check(spec.targetingMode==TargetingMode.Geometry,"self shield has no target capture");
   var aiming=ManualAiming.For(actor);var overlay=actor.AddComponent<AbilityRangePreviewOverlay>();
   Check(aiming.Begin(0,new Vector2(700,100)),"directed skill preview begins before self shield");Call(overlay,"LateUpdate");aiming.Cancel();
   Check(aiming.Begin(1,new Vector2(700,100)),"self shield button begins");aiming.Drag(new Vector2(796,100),1,false);Call(overlay,"LateUpdate");
   Check(aiming.Pose.Target==null,"self shield never selects another hero");
   var surfaces=(GameObject[])typeof(AbilityRangePreviewOverlay).GetField("surfaces",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(overlay);
   Check(surfaces.All(s=>s==null||!s.activeSelf),"self shield has no ground aim preview: "+hero);
   float before=Total(actor);Check(aiming.Release(),"self shield works without a target");Check(Total(actor)>before,"self shield reaches actor");
   Near(Total(enemy),1000,"self shield does not affect enemy");Near(Total(ally),1000,"self shield does not affect ally");
   var hud=new GameObject("HUD").AddComponent<PrototypeHUD>();hud.Initialize(actor.GetComponent<Health>(),null,null);
   var details=(string[])typeof(PrototypeHUD).GetField("details",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);
   Check(details[1]=="SELF  ·  TAP TO CAST","self shield HUD does not ask player to aim");
  }
  foreach(var entry in new[]{(HeroId.Mira,1),(HeroId.Rook,3),(HeroId.Kaelon,3)})
  {
   Reset();Resources.PreviewShaderAvailable=true;var actor=Hero(entry.Item1,0,Vector3.zero);var aiming=ManualAiming.For(actor);
   var overlay=actor.AddComponent<AbilityRangePreviewOverlay>();Check(aiming.Begin(entry.Item2,new Vector2(700,100)),"targeted or area ability still begins");Call(overlay,"LateUpdate");
   var surfaces=(GameObject[])typeof(AbilityRangePreviewOverlay).GetField("surfaces",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(overlay);
   Check(surfaces[0]!=null&&surfaces[0].activeSelf,"teammate and area skills retain preview");
   var material=surfaces[0].GetComponent<MeshRenderer>().sharedMaterial;
   Near(material.Floats[Shader.PropertyToID("_Mode")],entry.Item1==HeroId.Mira?1:0,"only teammate aiming displays a cone; areas display a circle");
  }
 }
 static void MovementDashCases()
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
  {
   Reset();var actor=Hero(hero,0,Vector3.zero);var abilities=actor.GetComponent<PlayerHeroAbilities>();
   if(abilities.AimFor(slot).aimType!=AimType.Dash)continue;
   var motor=actor.GetComponent<HeroMotor>();actor.transform.forward=-Vector3.forward;
   motor.SetMoveInput(new Vector2(1,0));
   var enemy=Hero(HeroId.Kaelon,1,new Vector3(3,0,1));
   var tap=AimGeometry.TapPose(actor,abilities.AimFor(slot));
   Near(Vector3.Angle(tap.Direction,Vector3.right),0,"dash tap uses current input, not attack-facing "+hero+slot);
   Check(tap.Target==null&&!tap.HasAimPoint&&!tap.AllowDashTargetCapture,"movement tap cannot snap to enemy "+hero+slot);
   Near(Vector3.Angle(abilities.PreviewDirection(slot),Vector3.right),0,"dash preview uses same movement direction "+hero+slot);
   var gesture=ManualAiming.For(actor);Check(gesture.Begin(slot,new Vector2(700,100)),"movement dash gesture begins "+hero+slot);
   motor.SetMoveInput(new Vector2(0,-1));
   Check(gesture.Release(),"movement dash tap releases "+hero+slot);Run(1);
   Near(Vector3.Angle(actor.transform.position,-Vector3.forward),0,"tap resolves movement at release "+hero+slot);
   Near(Total(enemy),1000,"tap leap leaves off-axis enemy untouched "+hero+slot);
   Reset();actor=Hero(hero,0,Vector3.zero);abilities=actor.GetComponent<PlayerHeroAbilities>();motor=actor.GetComponent<HeroMotor>();
   motor.SetMoveInput(new Vector2(1,0));motor.SetMoveInput(Vector2.zero);actor.transform.forward=-Vector3.forward;
   tap=AimGeometry.TapPose(actor,abilities.AimFor(slot));Near(Vector3.Angle(tap.Direction,Vector3.right),0,"standing tap retains last movement direction "+hero+slot);
   motor.SetMoveInput(new Vector2(1,0));
   gesture=ManualAiming.For(actor);gesture.Begin(slot,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);
   Near(Vector3.Angle(gesture.Pose.Direction,Vector3.forward),0,"manual dash aim overrides movement "+hero+slot);
   Check(gesture.Release(),"manual dash releases "+hero+slot);Run(1);
   Near(Vector3.Angle(actor.transform.position,Vector3.forward),0,"manual dash follows dragged direction "+hero+slot);
   Unlock(actor.GetComponent<HeroAbilityExecution>());AimingSettings.Current.rememberAimDirection=true;
   gesture.Begin(slot,new Vector2(700,100));Check(gesture.Release(),"next tap ignores remembered manual direction "+hero+slot);Run(1);
   Check(actor.transform.position.x>1,"tap returns to movement after manual aim "+hero+slot);
  }
  Reset();var client=Hero(HeroId.Timmy,0,Vector3.zero);client.AddComponent<CoopHeroSlot>().Slot=1;
  client.GetComponent<HeroMotor>().SetMoveInput(new Vector2(1,0));
  var victim=Hero(HeroId.Kaelon,1,new Vector3(3,0,.5f));
  var service=new GameObject("dash commands").AddComponent<CoopGameplayCommands>();
  var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
  Check(client.GetComponent<PlayerHeroAbilities>().CastUltimate(),"client sends movement tap");
  byte[] packet=manager.CustomMessagingManager.LastPacket;
  Check(packet[packet.Length-1]==0,"network retains no-capture movement intent");
  CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
  Unlock(client.GetComponent<HeroAbilityExecution>());
  client.GetComponent<HeroMotor>().SetMoveInput(new Vector2(-1,0));
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V7"](1,new Unity.Netcode.FastBufferReader(packet));Run(1);
  Near(Vector3.Angle(client.transform.position,Vector3.right),0,"host preserves remote movement direction");
  Near(Total(victim),1000,"host does not recapture an enemy for a movement tap");
 }
 static void Main(string[] args)
 {
  if(args.Contains("--movement-dashes"))
  {
   MovementDashCases();Gestures();TouchHud();Network();TimmyCases();ConeCaptureCases();
   Console.WriteLine("PASS "+checks+" assertions: movement taps, manual override, last direction, multitouch, Timmy leaps and host routing.");
   return;
  }
  MovementDashCases();SupportTargeting();SelfShieldPreviews();Geometry();AttackCases();Kits();Gestures();TouchHud();BasicHud();BasicSoftTargets();RejectedBasicWindups();Network();InterruptionsAndCapacity();ConeCaptureCases();MovingConnectionCases();TimmyCases();TimmyVfxCases();
  Reset();Alterverse.EditorChecks.BasicAttackDeliveryRegression.Run();
  Reset();Alterverse.EditorChecks.AttackDeliveryRegression.Run();
  ConnectionTiming(args.Contains("--connection-timing"));
  Console.WriteLine("PASS "+checks+" assertions: existing 8 heroes, Timmy contact/root/channel, geometry, dodges, AOE, gestures, multitouch, network direction/point, interrupts and connection timing.");
 }
}


