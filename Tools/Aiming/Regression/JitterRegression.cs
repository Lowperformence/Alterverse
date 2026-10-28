using System;
using System.Reflection;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;

class JitterEntry { static void Main() => Regression.RunJitterCases(); }

partial class Regression
{
 static bool Facing(HeroMotor motor,out Vector3 direction)
 {
  object[] args={Vector3.zero};
  bool result=(bool)typeof(HeroMotor).GetMethod("TryGetAttackFacing",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motor,args);
  direction=(Vector3)args[0];return result;
 }
 static void HeldFocusCases()
 {
  foreach(int fps in new[]{30,60,120})foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
  {
   if(!HeroAttackDelivery.IsRanged(hero))continue;
   Reset();var actor=Hero(hero,0,Vector3.zero);var basic=actor.GetComponent<BasicAttack>();
   float range=basic.Aim.castRange;
   var first=Hero(HeroId.Kaelon,1,Vector3.forward*(range*.7f)).GetComponent<Health>();
   var second=Hero(HeroId.Rook,1,Vector3.right*(range*.8f)).GetComponent<Health>();
   first.Current=500;second.Current=800;
   Check(basic.TryAttack(),"held focus starts "+hero);
   var soft=actor.GetComponent<SoftTargeting>();Check(soft.Current==first,"initial focus priority "+hero);
   for(int frame=0;frame<fps;frame++)
   {
    Clock.Advance(1f/fps);
    // Only target selection is exercised; keep cooldown active to avoid fixture damage.
    typeof(BasicAttack).GetField("nextAttackTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(basic,Time.time+10);first.Current=frame%2==0?800:400;second.Current=frame%2==0?300:900;
    first.transform.position=Vector3.forward*(range*(frame%2==0?.85f:.65f));
    second.transform.position=Vector3.right*(range*(frame%2==0?.60f:.95f));
    // Manual selection can share SoftTargeting but must not steal a held basic's focus.
    soft.AcquireAssisted(range,Vector3.right,20);basic.TryAttack();
    Check(soft.Current==first,"held basic ignores HP/distance and manual preview churn "+hero);
   }
   first.transform.position=Vector3.forward*(range+.1f);second.Current=1;
   Clock.Advance(1f/fps);Check(soft.AcquireBasic(range,Vector3.forward)==null,"edge band never extends attack range "+hero);
   Check(soft.Current==first,"edge band preserves held focus "+hero);
   first.transform.position=Vector3.forward*(range*.7f);
   Clock.Advance(.3f);
   Check(soft.AcquireBasic(range,Vector3.forward)==second,"release permits fresh priority "+hero);
   second.IsDead=true;
   Check(soft.AcquireBasic(range,Vector3.forward)==first,"dead focus immediately replaced "+hero);
   first.transform.position=Vector3.forward*(range+1);
   Check(soft.AcquireBasic(range,Vector3.forward)==null,"distant focus cleared "+hero);
   Reset();actor=Hero(hero,0,Vector3.zero);soft=actor.GetComponent<SoftTargeting>();
   var soldier=new GameObject("focus soldier");soldier.transform.position=Vector3.forward;
   soldier.AddComponent<TeamMember>().Initialize(1);var soldierHp=soldier.AddComponent<Health>();
   Check(soft.AcquireBasic(5,Vector3.forward)==soldierHp,"nonhero fallback "+hero);
   var priorityHero=Hero(HeroId.Rook,1,Vector3.right*2).GetComponent<Health>();
   Check(soft.AcquireBasic(5,Vector3.forward)==priorityHero,"hero still replaces nonhero during hold "+hero);
  }
 }
 static void OnlineSnapshotEdgeCases()
 {
  foreach(int fps in new[]{30,60,120})foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
  {
   Reset();var actor=Hero(hero,0,Vector3.zero);actor.AddComponent<Alterverse.Multiplayer.CoopHeroSlot>();
   var basic=actor.GetComponent<BasicAttack>();var motor=actor.GetComponent<HeroMotor>();var soft=actor.GetComponent<SoftTargeting>();
   float range=basic.Aim.castRange;
   var target=Hero(HeroId.Kaelon,1,Vector3.forward*(range-.05f)).GetComponent<Health>();
   Alterverse.Multiplayer.CoopAuthority.Active=true;Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=false;
   Check(soft.AcquireBasic(range,Vector3.forward)==target,"online held focus acquired "+hero);
   motor.TrackBasicTarget(target,.08f);
   var decoy=Hero(HeroId.Rook,1,Vector3.right*(range*.7f)).GetComponent<Health>();decoy.Current=1;
   for(int frame=0;frame<fps;frame++)
   {
    Clock.Advance(1f/fps);
    // Alternate 20Hz poses across the exact range by up to a full relative movement step.
    int snapshot=(int)(frame/(float)fps*20);
    target.transform.position=Vector3.forward*(range+(snapshot%2==0?.65f:-.05f));
    typeof(BasicAttack).GetField("nextAttackTime",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(basic,Time.time+10);
    Check(!basic.TryAttack(),"online hold never bypasses cooldown or range "+hero);
    Check(soft.Current==target,"20Hz edge snapshots do not swap the held target "+hero);
    Check(Facing(motor,out var direction),"online edge keeps combat facing "+hero);
    Near(Vector3.Angle(direction,Vector3.forward),0,"online edge never turns toward movement/decoy "+hero);
   }
   Near(target.Current,1000,"focus tolerance never deals out-of-range damage "+hero);
   target.transform.position=Vector3.forward*(range+1f);
   Check(soft.AcquireBasic(range,Vector3.forward)==decoy,"genuinely distant focus can be replaced "+hero);
   Clock.Advance(.3f);typeof(HeroMotor).GetMethod("UpdateState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motor,new object[]{Vector3.right});
   Check(!Facing(motor,out _),"release expires online facing "+hero);
  }
  Reset();var owner=Hero(HeroId.Nyra,0,Vector3.zero);owner.AddComponent<Alterverse.Multiplayer.CoopHeroSlot>();
  var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*3).GetComponent<Health>();
  Alterverse.Multiplayer.CoopAuthority.Active=true;Alterverse.Multiplayer.CoopAuthority.LocalHasServerAuthority=false;
  var attack=owner.GetComponent<BasicAttack>();
  attack.ApplyNetworkWindup(Vector3.right*3,enemy,.2f,.1f);
  Near(Vector3.Angle(owner.transform.forward,Vector3.forward),0,"delayed acceptance faces the live target instead of its old cast point");
  enemy.transform.position=Vector3.left*3;attack.ApplyNetworkWindup(Vector3.right*3,enemy,.2f,.1f);
  Near(Vector3.Angle(owner.transform.forward,Vector3.left),0,"subsequent acceptance follows the target's new direction");
  attack.ApplyNetworkWindup(Vector3.right*3,null,.2f,.1f);
  Near(Vector3.Angle(owner.transform.forward,Vector3.right),0,"fixed-point windup retains its captured aim");
  attack.ApplyNetworkWindup(Vector3.forward*3,enemy,.2f,.4f);
  Near(Vector3.Angle(owner.transform.forward,Vector3.right),0,"expired acceptance never changes facing");
 }
 public static void RunJitterCases()
 {
  HeldFocusCases();
  OnlineSnapshotEdgeCases();
  foreach(int fps in new[]{30,60,120})
  {
   foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))for(int slot=0;slot<4;slot++)
   {
    Reset();var a=Hero(hero,0,Vector3.zero);var aim=ManualAiming.For(a);
    if(a.GetComponent<PlayerHeroAbilities>().AimFor(slot).aimType!=AimType.Area)continue;
    Check(aim.Begin(slot,Vector2.zero),"area gesture starts");
    var spec=aim.Spec;
    var enemy=Hero(HeroId.Kaelon,1,Angle(6,spec.castRange*.8f));
    // Tap assist is preserved before the first drag.
    Call(aim,"LateUpdate");Near(Vector3.Distance(aim.Pose.Point,enemy.transform.position),0,"tap area still assists");
    for(int frame=0;frame<fps;frame++)
    {
     Clock.Advance(1f/fps);a.transform.position=Vector3.right*(frame*.01f);
     // Cross both angular and range eligibility boundaries at the outer drag radius.
     enemy.transform.position=a.transform.position+Angle(frame%2==0?6:30,spec.castRange+(frame%2==0?-.1f:.7f));
     aim.Drag(new Vector2(0,96+(frame%2)),1,false);Call(aim,"LateUpdate");
     Near(Vector3.Angle(aim.Pose.Direction,Vector3.forward),0,"drag ignores alternating assist");
     Near(Vector3.Distance(aim.Pose.Point,a.transform.position+Vector3.forward*spec.castRange),0,"moving outer-radius area stays at thumb point");
    }
    aim.Drag(Vector2.zero,1,false);Near(Vector3.Distance(aim.Pose.Point,a.transform.position),0,"drag returns to caster");
    aim.Cancel();Check(!aim.Active,"cancel clears area");
   }
   foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))
   {
    Reset();var a=Hero(hero,0,Vector3.zero);var basic=a.GetComponent<BasicAttack>();var motor=a.GetComponent<HeroMotor>();
    float range=basic.Aim.castRange;var enemy=Hero(HeroId.Kaelon,1,Vector3.forward*(range-.01f));
    Check(basic.TryAttack(),"edge attack starts "+hero);
    // A hold during cooldown must preserve a longer accepted attack-facing deadline.
    motor.TrackBasicTarget(enemy.GetComponent<Health>(),.7f);Clock.Advance(.02f);basic.TryAttack();
    float until=(float)typeof(HeroMotor).GetField("basicFacingUntil",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(motor);
    Check(until>=.7f,"hold does not shorten accepted facing "+hero);
    Run(2);motor.SetMoveInput(new Vector2(1,0));
    for(int frame=0;frame<fps;frame++)
    {
     Clock.Advance(1f/fps);a.transform.position=Vector3.right*(frame*.01f);
     enemy.transform.position=a.transform.position+Vector3.forward*(range+.1f);
     Check(!basic.TryAttack(),"outside edge never starts damage "+hero);
     Near(basic.RemainingCooldown,0,"outside edge spends no cooldown "+hero);
     typeof(HeroMotor).GetMethod("UpdateState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motor,new object[]{Vector3.right});
     Check(Facing(motor,out var direction),"held edge retains facing "+hero);
     Near(Vector3.Angle(direction,Vector3.forward),0,"edge faces enemy instead of movement "+hero);
    }
    Clock.Advance(.2f);
    // Let the attack state expire as the real UpdateState would, without native movement.
    typeof(HeroMotor).GetMethod("UpdateState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motor,new object[]{Vector3.right});
    Check(!Facing(motor,out _),"release lets facing expire "+hero);
    motor.TrackBasicTarget(enemy.GetComponent<Health>(),.02f);enemy.transform.position=a.transform.position+Vector3.forward*(range+1);
    motor.KeepBasicTargetFacing(range);Clock.Advance(.04f);
    Check(!Facing(motor,out _),"far target does not refresh facing "+hero);
    enemy.transform.position=a.transform.position+Vector3.forward*(range+.1f);enemy.GetComponent<Health>().IsDead=true;
    motor.KeepBasicTargetFacing(range);Check(!Facing(motor,out _),"dead target is not held "+hero);
   }
  }
  Console.WriteLine("PASS "+checks+" AOE/facing edge assertions at 30/60/120 FPS; actual ManualAiming, BasicAttack and HeroMotor. Native movement/rotation/rendering are fixtures.");
 }
}
