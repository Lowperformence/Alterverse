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
 static void ConeCaptureCases()
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))if(!HeroAttackDelivery.IsRanged(hero))
   Near(AimingSettings.Current.For(hero,-1).coneAngle,80,"basic melee geometry preserves pre-existing width");
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
  {
   AimSpec spec=AimingSettings.Current.For(hero,slot);
   bool single=spec.aimType==AimType.Melee||spec.aimType==AimType.Projectile||spec.aimType==AimType.Beam;
   Check((spec.targetingMode==TargetingMode.Cone)==(single||hero==HeroId.Timmy&&slot==3),hero+" slot "+slot+" cone capture only for directed single-target skills");
   if(!single)continue;
   Reset();var actor=Hero(hero,0,Vector3.zero);var enemy=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.5f-1,2));
   var aim=ManualAiming.For(actor);Check(aim.Begin(slot,new Vector2(700,100)),"cone begins "+hero+slot);
   aim.Drag(new Vector2(700,196),1,false);
   Check(actor.GetComponent<SoftTargeting>().Current==enemy.GetComponent<Health>(),"cone edge captures immediately "+hero+slot);
   Check(aim.Pose.HasAimPoint,"cone records release point "+hero+slot);
   Check(aim.Pose.Target==null,"ordinary cone has no homing reference "+hero+slot);
   Near(Vector3.Angle(aim.Pose.Direction,Vector3.forward),0,"target does not rotate thumb-selected cone "+hero+slot);
   Near(Vector3.Distance(aim.Pose.Point,enemy.transform.position),0,"captured point matches marked enemy "+hero+slot);
   Resources.PreviewShaderAvailable=true;var overlay=actor.AddComponent<AbilityRangePreviewOverlay>();Call(overlay,"LateUpdate");
   var surfaces=(GameObject[])typeof(AbilityRangePreviewOverlay).GetField("surfaces",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(overlay);
   Check(surfaces[0]!=null&&surfaces[0].activeSelf,"cone surface visible "+hero+slot);
   Check(surfaces[1]==null||!surfaces[1].activeSelf,"no projectile or beam line "+hero+slot);
   var material=surfaces[0].GetComponent<MeshRenderer>().sharedMaterial;
   Near(material.Floats[Shader.PropertyToID("_Mode")],1,"single cone presentation mode "+hero+slot);
   Near(material.Floats[Shader.PropertyToID("_ConeCos")],Mathf.Cos(spec.coneAngle*.5f*Mathf.Deg2Rad),"preview matches capture angle "+hero+slot);
   Near(material.Floats[Shader.PropertyToID("_Opacity")],.5625f,"requested transparency preserved "+hero+slot);
   Check(aim.Release(),"edge release casts "+hero+slot);Call(overlay,"LateUpdate");Check(!surfaces[0].activeSelf,"release removes cone "+hero+slot);
   Run(1);Near(1000-Total(enemy),HeroAbilityKit.LoadoutFor(hero).Spec(slot).Power*(hero==HeroId.Rook?1.05f:1f),"stationary cone edge connects "+hero+slot);
   Reset();actor=Hero(hero,0,Vector3.zero);enemy=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.5f+2,2));
   aim=ManualAiming.For(actor);aim.Begin(slot,new Vector2(700,100));aim.Drag(new Vector2(700,196),1,false);
   Check(actor.GetComponent<SoftTargeting>().Current==null&&!aim.Pose.HasAimPoint,"outside cone never captured "+hero+slot);
   Check(aim.Release(),"empty cone permits free cast "+hero+slot);Run(1);Near(Total(enemy),1000,"outside cone misses "+hero+slot);
   Reset();actor=Hero(hero,0,Vector3.zero);enemy=Hero(HeroId.Kaelon,1,Angle(spec.coneAngle*.5f-1,2));
   AimPose tap=AimGeometry.TapPose(actor,spec);
   Check(tap.HasAimPoint&&actor.GetComponent<SoftTargeting>().Current==enemy.GetComponent<Health>(),"short tap also captures whole cone "+hero+slot);
   var abilities=actor.GetComponent<PlayerHeroAbilities>();
   Check(slot==0?abilities.CastQ():slot==1?abilities.CastW():slot==2?abilities.CastE():abilities.CastUltimate(),"keyboard/button tap casts "+hero+slot);
   Run(1);Near(1000-Total(enemy),HeroAbilityKit.LoadoutFor(hero).Spec(slot).Power*(hero==HeroId.Rook?1.05f:1f),"tap uses captured position "+hero+slot);
  }
  foreach(HeroId hero in new[]{HeroId.Veyr,HeroId.Nyra})
  {
   Reset();var actor=Hero(hero,0,Vector3.zero);int slot=hero==HeroId.Nyra?2:0;
   var chosen=Hero(HeroId.Kaelon,1,Angle(10,2));var other=Hero(HeroId.Kaelon,1,Angle(hero==HeroId.Nyra?-16:30,1.6f));
   var aim=ManualAiming.For(actor);aim.Begin(slot,new Vector2(700,100));aim.Drag(new Vector2(700,196),1,false);
   Check(actor.GetComponent<SoftTargeting>().Current==chosen.GetComponent<Health>(),"choose one cone target by direction "+hero);
   Check(aim.Release(),"two-enemy cone casts "+hero);Run(.6f);
   Check(Total(chosen)<1000,"marked cone target gets geometric hit "+hero);Near(Total(other),1000,"single-target cone never damages second enemy "+hero);
  }
  Reset();var source=Hero(HeroId.Nyra,0,Vector3.zero);var dodge=Hero(HeroId.Kaelon,1,Angle(16,4));
  var gesture=ManualAiming.For(source);gesture.Begin(2,new Vector2(700,100));gesture.Drag(new Vector2(700,196),1,false);
  Vector3 snapshot=gesture.Pose.Point;Check(gesture.Release(),"capture dodge casts");dodge.transform.position+=Vector3.right*2;
  source.transform.position+=Vector3.right*.7f;Run(.6f);Near(Total(dodge),1000,"snapshot never tracks enemy during preparation or flight");
  Near(snapshot.magnitude,4,"release snapshot stays fixed");
  Reset();source=Hero(HeroId.Elyra,0,Vector3.zero);var first=Hero(HeroId.Kaelon,1,Vector3.forward*4);var second=Hero(HeroId.Kaelon,1,new Vector3(.8f,0,4));
  gesture=ManualAiming.For(source);gesture.Begin(0,new Vector2(700,100));gesture.Drag(new Vector2(700,100+96f*4/7.3f),1,false);
  Check(!gesture.Pose.HasAimPoint,"AOE placement is not a single-target capture");Check(gesture.Release(),"AOE gesture casts");Run(.5f);
  Near(Total(first),945,"AOE retains first area hit");Near(Total(second),945,"AOE retains second area hit");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);first=Hero(HeroId.Kaelon,1,Vector3.left*4);second=Hero(HeroId.Kaelon,1,Vector3.right*4);
  gesture=ManualAiming.For(source);gesture.Begin(0,new Vector2(700,100));gesture.Drag(new Vector2(604,100),1,false);Check(gesture.Release(),"first rapid skill releases left");
  gesture.Begin(2,new Vector2(700,100));gesture.Drag(new Vector2(796,100),1,false);Check(gesture.Release(),"second rapid skill releases right without waiting");Run(.6f);
  Near(Total(first),968,"queued beam retains its own captured point");Near(Total(second),980,"queued projectile retains its own captured point");
  Reset();source=Hero(HeroId.Nyra,0,Vector3.zero);first=Hero(HeroId.Kaelon,1,Vector3.forward*4);
  var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,.5f,2);wall.transform.localScale=new Vector3(2,2,.2f);
  gesture=ManualAiming.For(source);gesture.Begin(2,new Vector2(700,100));Check(gesture.Pose.HasAimPoint,"cone may capture point behind wall");Check(gesture.Release(),"captured wall shot still casts");Run(.6f);
  Near(Total(first),1000,"capturing point never bypasses real wall collision");
  IndependentConeDirection();EndedOnlyReverseDirection();CapturedPointNetwork();
 }
 static void EndedOnlyReverseDirection()
 {
  foreach(int fps in timingFps)
  {
   Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);var ahead=Hero(HeroId.Kaelon,1,Vector3.forward*4);var behind=Hero(HeroId.Kaelon,1,Vector3.forward*-4);
   var hud=HudFor(actor);Vector2 point=ButtonScreen(2);
   Touch.activeTouches.Add(new Touch{touchId=1,phase=TouchPhase.Began,screenPosition=new Vector2(140,140)});
   Touch.activeTouches.Add(new Touch{touchId=2,phase=TouchPhase.Began,screenPosition=point});Call(hud,"Update");
   Check(actor.GetComponent<SoftTargeting>().Current==ahead.GetComponent<Health>(),"press starts from facing before reverse sample");Clock.Advance(.05f);
   Touch.activeTouches[0]=new Touch{touchId=1,phase=TouchPhase.Moved,screenPosition=new Vector2(180,140)};
   Touch.activeTouches[1]=new Touch{touchId=2,phase=TouchPhase.Ended,screenPosition=point+new Vector2(0,-96)};Call(hud,"Update");
   Check(actor.GetComponent<SoftTargeting>().Current==behind.GetComponent<Health>(),"Ended-only reverse flick reacquires behind immediately at "+fps+" FPS");
   Check(actor.GetComponent<PlayerHeroAbilities>().ERemaining>0&&!ManualAiming.For(actor).Active,"fast reverse commits and clears pose");
   for(int i=0;i<fps;i++)Step(1f/fps);
   Near(Total(behind),980,"50 ms reverse flick connects at "+fps+" FPS");Near(Total(ahead),1000,"initial forward marker never controls reverse release");
   Check(actor.GetComponent<HeroMotor>().HasMoveInput,"reverse flick keeps left-thumb movement");
  }
 }
 static void IndependentConeDirection()
 {
  foreach(var dimensions in new[]{new Vector2(960,540),new Vector2(2340,1080),new Vector2(540,960)})
  {
   Reset();Screen.width=(int)dimensions.x;Screen.height=(int)dimensions.y;
   var actor=Hero(HeroId.Nyra,0,Vector3.zero);var behind=Hero(HeroId.Kaelon,1,Vector3.forward*-4);var ahead=Hero(HeroId.Kaelon,1,Vector3.forward*4);
   var hud=HudFor(actor);var point=ButtonScreen(2);var layout=new CombatHudLayout(Screen.safeArea);
   Touch.activeTouches.Add(new Touch{touchId=1,phase=TouchPhase.Began,screenPosition=new Vector2(140,140)});
   Touch.activeTouches.Add(new Touch{touchId=2,phase=TouchPhase.Began,screenPosition=point});Call(hud,"Update");
   Touch.activeTouches[0]=new Touch{touchId=1,phase=TouchPhase.Moved,screenPosition=new Vector2(180,140)};
   Touch.activeTouches[1]=new Touch{touchId=2,phase=TouchPhase.Moved,screenPosition=point+new Vector2(0,-3*layout.Scale)};Call(hud,"Update");
   var aim=ManualAiming.For(actor);Near(Vector3.Angle(aim.Pose.Direction,-Vector3.forward),0,"first tiny drag aims behind without turning hero");
   Near(Vector3.Angle(actor.transform.forward,Vector3.forward),0,"holding reverse aim does not turn body");
   Check(actor.GetComponent<HeroMotor>().HasMoveInput,"left thumb keeps moving with backward cone");
   actor.transform.forward=Vector3.right;Call(aim,"LateUpdate");Near(Vector3.Angle(aim.Pose.Direction,-Vector3.forward),0,"walking rotation cannot reset held cone");
   Check(actor.GetComponent<SoftTargeting>().Current==behind.GetComponent<Health>(),"behind target captured independently of facing");
   Touch.activeTouches[1]=new Touch{touchId=2,phase=TouchPhase.Ended,screenPosition=point+new Vector2(0,-3*layout.Scale)};Call(hud,"Update");Run(.5f);
   Near(Total(behind),980,"backward release connects");Near(Total(ahead),1000,"forward enemy excluded from backward cone");
   Check(actor.GetComponent<HeroMotor>().HasMoveInput,"backward release preserves movement");
  }
  Reset();var hero=Hero(HeroId.Nyra,0,Vector3.zero);var manual=ManualAiming.For(hero);
  manual.Begin(2,new Vector2(700,100));manual.Drag(new Vector2(700,97),1,false);manual.Cancel();hero.transform.forward=Vector3.right;
  manual.Begin(0,new Vector2(700,100));Near(Vector3.Angle(manual.Pose.Direction,Vector3.right),0,"default starts in current facing for quick taps");manual.Cancel();
  AimingSettings.Current.rememberAimDirection=true;
  manual.Begin(2,new Vector2(700,100));manual.Drag(new Vector2(700,97),1,false);manual.Cancel();hero.transform.forward=Vector3.forward;
  manual.Begin(0,new Vector2(700,100));Near(Vector3.Angle(manual.Pose.Direction,-Vector3.forward),0,"optional last aim direction independent of movement");manual.Cancel();
  var camera=new GameObject("camera").AddComponent<Camera>();Camera.main=camera;camera.transform.forward=new Vector3(1,-1,0);camera.transform.right=new Vector3(0,0,-1);
  manual.Begin(2,new Vector2(700,100));manual.Drag(new Vector2(700,97),1,false);
  Near(Vector3.Angle(manual.Pose.Direction,Vector3.left),0,"reverse drag uses camera ground axes");
 }
 static void CapturedPointNetwork()
 {
  Reset();var actor=Hero(HeroId.Nyra,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;
  var enemy=Hero(HeroId.Kaelon,1,Angle(16,4));var aim=ManualAiming.For(actor);
  aim.Begin(2,new Vector2(700,100));aim.Drag(new Vector2(700,196),1,false);AimPose captured=aim.Pose;aim.Cancel();
  var service=new GameObject("commands").AddComponent<CoopGameplayCommands>();
  var manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");
  Check(CoopGameplayCommands.TrySendAbility(actor,2,captured),"client sends captured cone point");
  var reader=new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket);
  reader.ReadValueSafe(out string round);reader.ReadValueSafe(out int roster);reader.ReadValueSafe(out Unity.Collections.FixedString128Bytes id);reader.ReadValueSafe(out bool target);reader.ReadValueSafe(out int slot);
  reader.ReadValueSafe(out float dx);reader.ReadValueSafe(out float dy);reader.ReadValueSafe(out float dz);reader.ReadValueSafe(out float px);reader.ReadValueSafe(out float py);reader.ReadValueSafe(out float pz);reader.ReadValueSafe(out bool anchored);
  Check(!target&&anchored,"V5 carries frozen point flag without normal target reference");Near(px,captured.Point.x,"V5 captured x");Near(pz,captured.Point.z,"V5 captured z");Near(dx,0,"V5 preserves raw cone direction");
  CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;CoopAuthority.ActivePlan.Session.Slots[1]=new MatchPlayerSlot{PlayerId="test"};
  actor.transform.position=Vector3.right*.8f;
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V5"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));Run(.6f);
  Near(Total(enemy),980,"server preserves frozen capture through receipt and windup movement");
  Reset();actor=Hero(HeroId.Nyra,0,Vector3.zero);actor.AddComponent<CoopHeroSlot>().Slot=1;enemy=Hero(HeroId.Kaelon,1,Angle(16,4));
  service=new GameObject("commands").AddComponent<CoopGameplayCommands>();manager=new Unity.Netcode.NetworkManager{IsListening=true,IsConnectedClient=true,IsServer=false};Unity.Netcode.NetworkManager.Singleton=manager;
  CoopAuthority.Active=true;CoopAuthority.LocalHasServerAuthority=false;Call(service,"Update");Check(CoopGameplayCommands.TrySendAbility(actor,2,captured),"client snapshot can arrive after target moves");
  enemy.transform.position+=Vector3.right*2;CoopAuthority.LocalHasServerAuthority=true;manager.IsServer=true;
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V5"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket.Take(manager.CustomMessagingManager.LastPacket.Length-1).ToArray()));
  Near(actor.GetComponent<PlayerHeroAbilities>().ERemaining,0,"truncated V5 capture flag rejects before cooldown or queue");
  manager.CustomMessagingManager.Handlers["DF_COOP_ABILITY_V5"](1,new Unity.Netcode.FastBufferReader(manager.CustomMessagingManager.LastPacket));Run(.6f);
  Near(Total(enemy),1000,"server does not reacquire or follow moved captured enemy");
 }
}
