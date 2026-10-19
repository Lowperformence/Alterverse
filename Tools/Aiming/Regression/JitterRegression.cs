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
 public static void RunJitterCases()
 {
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
