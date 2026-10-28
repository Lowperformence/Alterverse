using System;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;

partial class Bolt7Regression
{
 static void SamePoint(Vector3 a,Vector3 b,string why)=>Near(AimGeometry.Flat(a-b).magnitude,0,why);
 static void AreaAimCases()
 {
  foreach(int fps in new[]{30,60,120})foreach(int slot in new[]{0,2})foreach(float fraction in new[]{0f,.04f,.3f,1f})
  {
   Clock.Reset();foreach(var old in GameObject.All.ToArray())old.SetActive(false);
   var actor=Hero(HeroId.Bolt7,0,new Vector3(7,1,9));
   var kit=actor.gameObject.AddComponent<HeroAbilityKit>();kit.Configure(HeroId.Bolt7);
   actor.gameObject.AddComponent<HeroAbilityExecution>();var abilities=actor.gameObject.AddComponent<PlayerHeroAbilities>();
   var spec=kit.Spec(slot);var aim=ManualAiming.For(actor.gameObject);
   Check(aim.Begin(slot,Vector2.zero),"grenade gesture starts");
   // Drag out of the dead zone first, then return all the way to the caster.
   aim.Drag(new Vector2(0,96),1,false);aim.Drag(new Vector2(0,96*fraction),1,false);
   Vector3 point=aim.Pose.Point;Check(aim.Pose.HasAimPoint,"AOE retains its explicit preview point, including zero distance");
   var center=Hero(HeroId.Nyra,1,point);var inside=Hero(HeroId.Nyra,1,point+Vector3.right*(spec.Radius*.5f));
   var edge=Hero(HeroId.Nyra,1,point+Vector3.back*spec.Radius);var outside=Hero(HeroId.Nyra,1,point+Vector3.left*(spec.Radius+.02f));
   var ally=Hero(HeroId.Nyra,0,point);var entrant=Hero(HeroId.Nyra,1,point+Vector3.forward*(spec.Radius+1));
   var leaver=Hero(HeroId.Nyra,1,point);
   foreach(var target in new[]{center,inside,edge,outside,ally,entrant,leaver})target.RemoveAllShields(actor.gameObject);
   float hp=center.Current,own=actor.Current+actor.Shield;
   Check(aim.Release(),"manual release accepted");SamePoint(abilities.LastPose.Point,point,"release point matches preview");
   SamePoint(HeroSkillVfx.Warning,point,"warning matches preview");SamePoint(HeroSkillVfx.Flight,point,"grenade flight endpoint matches preview");
   Near(center.Current,hp,"no premature windup damage");
   // Point stays fixed when the caster moves; damage samples occupants at detonation.
   actor.transform.position+=Vector3.right*2;entrant.transform.position=point+Vector3.forward*.2f;leaver.transform.position=point+Vector3.forward*(spec.Radius+1);
   int detonations=HeroSkillVfx.Detonations;
   for(int f=0;f<(int)Math.Ceiling((spec.Windup+.1f)*fps);f++)Clock.Advance(1f/fps);
   SamePoint(HeroSkillVfx.Detonation,point,"damage/explosion remain at release point while caster moves");
   Check(HeroSkillVfx.Detonations==detonations+1,"one detonation per cast");
   Near(center.Current,hp-spec.Power,"center takes actual HP damage");
   Near(inside.Current,hp-(slot==0?Bolt7Combat.GrenadeDamage(spec.Radius*.5f):spec.Power),"inner AOE enemy damaged");
   Near(edge.Current,hp-(slot==0?Bolt7Balance.GrenadeEdgeDamage:spec.Power),"AOE boundary enemy damaged");
   Near(entrant.Current,hp-spec.Power,"enemy entering before explosion damaged");
   Near(outside.Current,hp,"enemy outside AOE untouched");Near(leaver.Current,hp,"enemy leaving before explosion untouched");
   Near(ally.Current,hp,"ally in AOE untouched");Near(actor.Current+actor.Shield,own,"caster never takes own grenade damage");
   float after=center.Current;Clock.Advance(1);Near(center.Current,after,"no repeated explosion damage");
  }
  foreach(int slot in new[]{0,2,3})
  {
   Clock.Reset();foreach(var old in GameObject.All.ToArray())old.SetActive(false);
   var actor=Hero(HeroId.Bolt7,0,new Vector3(4,1,8));var spec=AimingSettings.Current.For(HeroId.Bolt7,slot);
   var pose=AimGeometry.TapPose(actor.gameObject,spec);Check(pose.HasAimPoint,"tap AOE fallback records explicit point");
   SamePoint(pose.Point,actor.transform.position+Vector3.forward*spec.castRange*.65f,"tap AOE keeps standard 65 percent range");
   var target=Hero(HeroId.Nyra,1,actor.transform.position+Vector3.forward*.1f);
   pose=AimGeometry.TapPose(actor.gameObject,spec);SamePoint(pose.Point,target.transform.position,"tap targets close enemy instead of maximum range");
  }
  foreach(var old in GameObject.All.ToArray())old.SetActive(false);Clock.Reset();
  var ram=AimingSettings.Current.For(HeroId.Bolt7,1);Check(ram.aimType==AimType.Melee&&ram.targetingMode==TargetingMode.Cone,"ram uses normal melee cone preview");
  Near(ram.castRange,Bolt7Balance.RamRange,"preview range matches ram damage");Near(ram.coneAngle,Bolt7Balance.RamAngle,"preview angle matches ram damage");
  var mech=Hero(HeroId.Bolt7,0,Vector3.zero);var ramKit=mech.gameObject.AddComponent<HeroAbilityKit>();ramKit.Configure(HeroId.Bolt7);
  var execution=mech.gameObject.AddComponent<HeroAbilityExecution>();var left=Hero(HeroId.Nyra,1,new Vector3(-1,0,2));var right=Hero(HeroId.Nyra,1,new Vector3(1,0,2));
  Check(execution.CastForTest(1,new AimPose(Vector3.forward,right.transform.position,right,true)),"ram accepts captured enemy on side of preview cone");
  Clock.Advance(Bolt7Balance.RamWindup);Clock.Advance(.15f);
  Near(left.Shield,20,"full cone hits left enemy even when capture points right");Near(right.Shield,20,"full cone also hits right enemy");
  foreach(var old in GameObject.All.ToArray())old.SetActive(false);Clock.Reset();
 }
}
