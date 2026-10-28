using System;
using System.Linq;
using UnityEngine;
using Alterverse.Visuals;
class Regression {
 static int checks;
 static void Assert(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static void Main(string[] args){
  Resources.ResourceRoot=args[0];
  var actor=new GameObject("BOLT test");var model=Bolt7Model.Create(actor.transform);
  Assert(model!=null,"real gzip model resource loads");Assert(model.Clips.Length==15&&model.MaterialNames.Length==6,"all authored clips and material slots preserved");
  var skin=model.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();var mesh=skin.sharedMesh;
  Assert(skin.bones.Length==19&&mesh.vertices.Length==62113,"exact runtime vertex and skeleton counts");Assert(mesh.Submeshes.Sum(a=>a.Length)/3==76642,"all model triangles retained");
  Assert(skin.updateWhenOffscreen,"skinned model never disappears from stale imported bounds");
  Assert(model.Root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="socket_gatling_muzzle")&&model.Root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="socket_grenade_muzzle"),"both weapon sockets present");
  foreach(var clip in model.Clips){model.Sample(clip,0);model.Sample(clip,clip.length*.5f);model.Sample(clip,clip.length);foreach(var bone in skin.bones){Assert(float.IsFinite(bone.localPosition.x)&&float.IsFinite(bone.localRotation.w),"finite local poses for "+clip.name);}}
  foreach(int slot in new[]{0,1,2,3,4}){var icon=Bolt7Assets.Icon(slot);Assert(icon!=null&&icon.width>=512&&icon.height==icon.width,"BOLT-7 icon loaded from its PNG bytes for slot "+slot);Assert(ReferenceEquals(icon,Bolt7Assets.Icon(slot)),"icon cache "+slot);}
  Assert(Bolt7Assets.Icon(2)!=null,"second grenade has its own icon");Assert(Bolt7Assets.Texture("Portrait")!=null,"hero portrait loads");Assert(Bolt7Assets.Texture("BOLT7_BaseColor_2K").width==2048&&Bolt7Assets.Texture("BOLT7_Normal_2K",true).width==2048,"two 2K maps resolve");
  var visualActor=new GameObject("menu actor");Bolt7Visual.Create(visualActor);Assert(visualActor.GetComponent<Bolt7Visual>().enabled,"production visual builds without any FBX resource");GameObject.Invoke(visualActor.GetComponent<Bolt7Visual>(),"LateUpdate");
  Assert(visualActor.GetComponent<HeadwearPresentation>()?.Root?.name=="DF_VisualRoot","production BOLT visual initializes the shared headwear renderer");
  model.Sample(model.Clips.Single(c=>c.name=="Idle"),0);float groundedRoot=skin.rootBone.localPosition.y;
  var matchActor=new GameObject("match actor");var health=matchActor.AddComponent<Alterverse.Combat.Health>();Bolt7Visual.Create(matchActor);var matchVisual=matchActor.GetComponent<Bolt7Visual>();
  var matchSkin=matchActor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();
  Assert(Math.Abs(matchSkin.rootBone.localPosition.y-groundedRoot)<.001f,"initial spawn starts in grounded idle immediately");
  GameObject.Invoke(matchVisual,"LateUpdate");Assert(Math.Abs(matchSkin.rootBone.localPosition.y-groundedRoot)<.001f,"first update never samples airborne Spawn clip");
  matchVisual.Shot();health.Die();Time.time+=.4f;GameObject.Invoke(matchVisual,"LateUpdate");health.Restore();
  Assert(Math.Abs(matchSkin.rootBone.localPosition.y-groundedRoot)<.001f,"respawn resets grounded pose before next render");GameObject.Invoke(matchVisual,"LateUpdate");
  Assert(Math.Abs(matchSkin.rootBone.localPosition.y-groundedRoot)<.001f,"respawn clears death and fire actions");
  Console.WriteLine("PASS "+checks+" BOLT-7 model/pose/texture-binding checks; native Unity rendering and native PNG decoding are not emulated.");
 }
}
