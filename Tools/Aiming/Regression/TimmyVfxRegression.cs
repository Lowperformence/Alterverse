using System;
using System.Linq;
using UnityEngine;
using Alterverse.Characters;
using Alterverse.Combat;
using Alterverse.Visuals;

partial class Regression
{
 static MeshFilter[] VisibleFx(HeroFxShape shape)=>GameObject.All
  .Select(g=>g.GetComponent<MeshFilter>())
  .Where(f=>f!=null&&f.sharedMesh?.name=="DF_AuthoredFx_"+shape&&f.GetComponent<MeshRenderer>().enabled).ToArray();
 static float FxAlpha(MeshFilter f)=>f.GetComponent<MeshRenderer>().Block.Colors[Shader.PropertyToID("_Tint")].a;
 static void TimmyVfxCases()
 {
  // Compile and exercise the real pool scheduler/mesh builder and Timmy VFX partial.
  // Common hero facade, GPU, transform hierarchy and transport remain engine fixtures.
  Reset();var actor=Hero(HeroId.Timmy,0,Vector3.zero);
  var victim=Hero(HeroId.Kaelon,1,new Vector3(1,0,4));
  Check(actor.GetComponent<HeroAbilityExecution>().TryCastAimed(1,new AimPose(Vector3.forward,victim.transform.position)),"grenade cast at selected ground point");
  var grenade=VisibleFx(HeroFxShape.TimmyGrenade).Single();
  var warning=VisibleFx(HeroFxShape.Ring).Single();
  Near(warning.transform.localScale.x,2,"Stardust warning radius matches damage radius");
  Near(grenade.transform.localScale.x,.23f,"grenade keeps its authored physical size");
  Check(grenade.sharedMesh.Vertices.Count>100,"visible grenade has a closed 3D canister mesh");
  for(int i=0;i<grenade.sharedMesh.Triangles.Count;i+=3)
  {
   var v=grenade.sharedMesh.Vertices;var t=grenade.sharedMesh.Triangles;
   Check(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).sqrMagnitude>1e-8f,"grenade mesh triangle is nondegenerate");
  }
  Step(.15f);Near(grenade.transform.position.z,2,"grenade halfway to captured point");
  Near(grenade.transform.position.x,.5f,"grenade horizontal flight");
  Check(grenade.transform.position.y>.6f,"grenade visibly follows a lob arc");
  Near(Total(victim),1000,"grenade flight cannot deal early damage");
  actor.transform.position+=Vector3.right*3;
  Step(.15f);Near(Total(victim),1000-TimmyBalance.DustDamage,"Stardust damage resolves at grenade landing");
  Check(VisibleFx(HeroFxShape.TimmyGrenade).Length==0&&VisibleFx(HeroFxShape.Ring).Length==0,"grenade and warning clear at impact");
  var clouds=VisibleFx(HeroFxShape.Mist);Check(clouds.Length==2,"two sustained mist layers appear on landing");
  Near(clouds[0].transform.position.x,1,"caster movement cannot relocate the mist");
  Near(clouds[0].transform.localScale.x,2,"mist radius matches warning and gameplay");
  float opacity=FxAlpha(clouds[0]);Run(1.75f);
  Check(VisibleFx(HeroFxShape.Mist).Length==2,"mist remains visible well past the original flash");
  Near(FxAlpha(clouds[0]),opacity,"mist retains opacity through most of its two seconds");
  Near(Total(victim),1000-TimmyBalance.DustDamage,"lingering mist does not add damage ticks");
  Run(.15f);Check(FxAlpha(clouds[0])>0&&FxAlpha(clouds[0])<opacity,"mist fades only at the end");
  Run(.11f);Check(VisibleFx(HeroFxShape.Mist).Length==0,"mist clears after two seconds");

  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);
  var motion=actor.AddComponent<HeroVisualMotion>();
  var left=new GameObject("DF_TimmyJetL").transform;left.SetParent(actor.transform,true);left.position=new Vector3(-.12f,.35f,-.20f);
  var right=new GameObject("DF_TimmyJetR").transform;right.SetParent(actor.transform,true);right.position=new Vector3(.12f,.35f,-.20f);
  HeroSkillVfx.TimmyEffect(actor,2,Vector3.zero,Vector3.forward*3.4f,0,TimmyBalance.JetDashSeconds);
  var jets=VisibleFx(HeroFxShape.Streak);Check(jets.Length==4,"both backpack nozzles have visible core and exhaust");
  Near(motion.DashSeconds,.16f,"jet animation uses actual dash duration");
  Vector3 delta=new Vector3(2,0,1);left.position+=delta;right.position+=delta;
  left.rotation=right.rotation=Quaternion.Euler(0,90,0);Step(.08f);
  Check(jets.All(f=>Vector3.Distance(f.transform.position,left.position)<.01f||Vector3.Distance(f.transform.position,right.position)<.01f),"exhaust follows moving real nozzles");
  Check(jets.All(f=>Math.Abs((f.transform.rotation*Vector3.back).x)>.8f),"exhaust turns with backpack sockets");
  Run(.13f);Check(VisibleFx(HeroFxShape.Streak).Length==4,"jet exhaust retains a short afterglow");
  Run(.08f);Check(VisibleFx(HeroFxShape.Streak).Length==0,"jet exhaust ends after dash and afterglow");

  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);
  HeroSkillVfx.TimmyEffect(actor,3,Vector3.zero,Vector3.right*5,0,TimmyBalance.ScratchLeapSeconds);
  jets=VisibleFx(HeroFxShape.Streak);Check(jets.Length==4,"Scratch activates the same two nozzle cores and exhausts as Jet Pounce");
  var claws=VisibleFx(HeroFxShape.Claw);Check(claws.Length==3,"Scratch launches exactly three claws");
  Check(claws.All(f=>f.transform.position.x>.4f),"Scratch claws begin ahead of Timmy");
  var clawStarts=claws.Select(f=>f.transform.position).ToArray();
  actor.transform.position+=new Vector3(2,0,1);Step(.2f);
  for(int i=0;i<claws.Length;i++)Check(Vector3.Distance(claws[i].transform.position,clawStarts[i]+new Vector3(2,0,1))<.01f,"Scratch claws follow the actor's actual curved leap");
  actor.transform.position+=Vector3.forward*3;Step(.1f);
  Check(VisibleFx(HeroFxShape.Streak).Length==4,"Scratch jets stay active through the leap");
  Check(jets.All(f=>f.transform.position.z>2.7f),"Scratch exhaust follows Timmy's actual leap");
  Check(claws.All(f=>Math.Abs(f.transform.position.z-4)<=.21f),"Scratch claws stay with Timmy when the leap changes direction");
  HeroVfxPool.Emit(HeroFxShape.Claw,Vector3.zero,Vector3.zero,Vector3.one,Color.white,.16f,HeroFxMotion.Sweep,owner:actor);
  HeroSkillVfx.CancelTimmyDash(actor);
  Check(VisibleFx(HeroFxShape.Claw).Length==1&&VisibleFx(HeroFxShape.Streak).Length==0,"interruption removes airborne claws and jets without deleting the final contact sweep");

  // A first cue before the authored model is attached must not permanently
  // cache missing nozzle transforms. Verify recovery on the following leap.
  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);
  HeroSkillVfx.TimmyEffect(actor,2,Vector3.zero,Vector3.forward*3,0,.16f);
  HeroSkillVfx.CancelTimmyDash(actor);
  left=new GameObject("DF_TimmyJetL").transform;left.SetParent(actor.transform,true);left.position=new Vector3(-.2f,.5f,-.4f);
  right=new GameObject("DF_TimmyJetR").transform;right.SetParent(actor.transform,true);right.position=new Vector3(.2f,.5f,-.4f);
  HeroSkillVfx.TimmyEffect(actor,3,Vector3.zero,Vector3.forward*5,0,.4f);
  jets=VisibleFx(HeroFxShape.Streak);Check(jets.Length==4,"Scratch recovers both late-imported nozzle sockets");
  Check(jets.All(f=>Vector3.Distance(f.transform.position,left.position)<.01f||Vector3.Distance(f.transform.position,right.position)<.01f),"recovered exhaust begins at the actual backpack sockets");
  Step(.39f);Check(VisibleFx(HeroFxShape.Streak).Length==4,"both Scratch nozzles remain active until landing");
  Step(.02f);Check(VisibleFx(HeroFxShape.Claw).Length==0&&VisibleFx(HeroFxShape.Streak).Length==4,"Scratch claws end on landing while nozzle afterglow remains briefly");
  Step(.12f);Check(VisibleFx(HeroFxShape.Streak).Length==0,"Scratch exhaust does not continue into the two-second grip");

  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);
  HeroSkillVfx.TimmyEffect(actor,3,Vector3.zero,Vector3.forward*5,0,.4f,false,.30f);
  Check(VisibleFx(HeroFxShape.Streak).Length==4,"late Scratch cue still shows both jets for the remaining flight");
  Step(.23f);Check(VisibleFx(HeroFxShape.Streak).Length==0,"late Scratch jets respect the original landing and expiry");

  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);
  HeroSkillVfx.TimmyEffect(actor,5,Vector3.up*.5f,Vector3.forward*4,0,.3f);
  HeroSkillVfx.TimmyEffect(actor,3,Vector3.zero,Vector3.forward*5,0,.4f);
  HeroSkillVfx.EndTimmyLeap(actor);
  Check(VisibleFx(HeroFxShape.Claw).Length==0&&VisibleFx(HeroFxShape.Streak).Length==0,"Scratch landing clears its own airborne effects");
  Check(VisibleFx(HeroFxShape.TimmyGrenade).Length==1,"Scratch landing does not cancel a concurrently thrown Stardust grenade");

  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);motion=actor.AddComponent<HeroVisualMotion>();
  HeroSkillVfx.TimmyEffect(actor,2,Vector3.zero,Vector3.forward*3,0,.16f,false,.22f);
  Near(motion.DashSeconds,0,"late jet afterglow cannot restart a remote dash animation");

  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);
  HeroSkillVfx.TimmyEffect(actor,5,Vector3.up*.5f,Vector3.forward*4,0,.3f,false,.15f);
  grenade=VisibleFx(HeroFxShape.TimmyGrenade).Single();Near(grenade.transform.position.z,2,"remote cue catches up instead of restarting its flight");
  actor.GetComponent<Health>().TakeDamage(2000);Step();Check(VisibleFx(HeroFxShape.TimmyGrenade).Length==0,"death clears grenade flight");
  Reset();actor=Hero(HeroId.Timmy,0,Vector3.zero);
  int relays=Alterverse.Multiplayer.CoopHeroStateSync.TimmyRelays;
  HeroSkillVfx.TimmyEffect(actor,1,Vector3.zero,Vector3.forward*4,2,2,false,1);
  Check(Alterverse.Multiplayer.CoopHeroStateSync.TimmyRelays==relays,"remote VFX playback never rebroadcasts");
  Run(1.01f);Check(VisibleFx(HeroFxShape.Mist).Length==0,"remote mist preserves its original expiry");
  HeroSkillVfx.TimmyEffect(actor,1,Vector3.zero,Vector3.forward*4,2,2,false,2.1f);
  Check(VisibleFx(HeroFxShape.Mist).Length==0,"expired remote effect is not resurrected");
 }
}
