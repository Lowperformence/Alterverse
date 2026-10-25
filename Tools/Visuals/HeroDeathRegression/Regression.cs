using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using Alterverse.Visuals;
class Regression {
 static int checks;
 static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
 static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.0001f,why+" actual="+a+" expected="+b);
 static HeroDeathBody[] Corpses=>GameObject.All.Where(g=>!g.Destroyed).Select(g=>g.GetComponent<HeroDeathBody>()).Where(b=>b!=null).ToArray();
 static HeroDeathBody Spawn(GameObject actor){var before=Corpses;HeroDeathBody.Spawn(actor);return Corpses.Single(c=>!before.Contains(c));}
 static void Validate(HeroDeathBody body,int count) {
  var limbs=body.GetComponentsInChildren<Rigidbody>(true);Check(limbs.Length==count,"expected independent limb count");Check(body.GetComponentsInChildren<CharacterJoint>(true).Length==count-1,"every non-root body has a passive joint");
  Check(limbs.All(b=>b.gameObject.scene==body.gameObject.scene)&&body.gameObject.scene.Id!=1,"all proxies stay in isolated scene");
  Check(limbs.Select(b=>b.gameObject.name).Distinct().Count()==count,"each mapped bone owns exactly one proxy");
  foreach(var joint in body.GetComponentsInChildren<CharacterJoint>(true))Check(joint.connectedBody!=null&&joint.connectedBody.gameObject.scene==body.gameObject.scene,"joint points to corpse body, never gameplay actor");
  SceneManager.Simulated.Clear();GameObject.Invoke(body,"FixedUpdate");Check(SceneManager.Simulated.SequenceEqual(new[]{body.gameObject.scene.Id}),"only isolated physics scene is simulated");
 }
 static void Main(string[] args){
  Resources.ResourceRoot=args[0];var actor=new GameObject("BOLT-7");actor.transform.position=new Vector3(3,1,7);actor.transform.rotation=Quaternion.Euler(0,35,0);Bolt7Visual.Create(actor);
  var live=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();var originalPositions=live.bones.Select(b=>b.position).ToArray();
  var corpse=Spawn(actor);Validate(corpse,13);Check(Debug.Errors==0,"BOLT death no longer reports an incomplete humanoid skeleton");
  var skin=corpse.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single();Check(ReferenceEquals(skin.sharedMesh,live.sharedMesh)&&skin.sharedMesh.vertices.Length==62113,"entire authored mesh retained");
  Check(skin.bones.Length==19&&skin.bones.All(b=>b!=null&&b.IsChildOf(corpse.transform)),"all 19 death-skin bones are private copies");
  Check(corpse.GetComponentsInChildren<MonoBehaviour>(true).Length==1,"live pose sampler and gameplay scripts are never copied");
  foreach(string name in new[]{"GatlingSpin","RamCollar","GrenadeDrum","socket_gatling_muzzle","socket_grenade_muzzle"})Check(corpse.GetComponentsInChildren<Transform>(true).Any(t=>t.name==name),"weapon detail retained: "+name);
  var leftWeapon=corpse.GetComponentsInChildren<Rigidbody>(true).Single(b=>b.gameObject.name=="DF_Ragdoll_Weapon.L");
  Vector3 weaponBefore=skin.bones.Single(b=>b.name=="Weapon.L").position;leftWeapon.position+=Vector3.right*.1f;GameObject.Invoke(corpse,"LateUpdate");
  Near(Vector3.Distance(skin.bones.Single(b=>b.name=="Weapon.L").position,weaponBefore+Vector3.right*.1f),0,"weapon bone follows its independent physics proxy");
  for(int i=0;i<live.bones.Length;i++)Near(Vector3.Distance(live.bones[i].position,originalPositions[i]),0,"ragdoll never changes live bone "+i);
  var elapsed=typeof(HeroDeathBody).GetField("elapsed",BindingFlags.NonPublic|BindingFlags.Instance);elapsed.SetValue(corpse,HeroDeathBody.CollapseSeconds+HeroDeathBody.HoldSeconds+HeroDeathBody.FadeSeconds*.5f);GameObject.Invoke(corpse,"LateUpdate");
  var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);Check(block.GetFloat(Shader.PropertyToID("_DFDeathFade"))>0,"corpse fades");live.GetPropertyBlock(block);Near(block.GetFloat(Shader.PropertyToID("_DFDeathFade")),0,"fade never changes living material instance");
  int isolated=corpse.gameObject.scene.Id;UnityEngine.Object.DestroyImmediate(corpse.gameObject);Check(!SceneManager.Loaded.Contains(isolated),"corpse cleanup unloads isolated physics scene");
  // Existing humanoid and procedural layouts keep their original 16-body path.
  foreach(bool legacy in new[]{false,true}){
   var human=new GameObject("human");var root=new GameObject("DF_VisualRoot");root.transform.SetParent(human.transform,false);root.AddComponent<SkinnedMeshRenderer>().sharedMesh=live.sharedMesh;root.GetComponent<SkinnedMeshRenderer>().sharedMaterials=live.sharedMaterials;root.GetComponent<SkinnedMeshRenderer>().bones=new Transform[0];
   string[] names=legacy?new[]{"DF_PelvisJoint","DF_SpineLower","DF_SpineUpper","DF_NeckJoint","DF_ArmL","DF_ElbowJointL","DF_WristJointL","DF_LegL","DF_KneeJointL","DF_AnkleJointL","DF_ArmR","DF_ElbowJointR","DF_WristJointR","DF_LegR","DF_KneeJointR","DF_AnkleJointR"}:new[]{"Hips","Spine","Chest","Head","UpperArmL","ForearmL","HandL","ThighL","ShinL","FootL","UpperArmR","ForearmR","HandR","ThighR","ShinR","FootR"};
   for(int i=0;i<names.Length;i++){var bone=new GameObject(names[i]);bone.transform.SetParent(root.transform,false);bone.transform.localPosition=new Vector3(i*.02f,(i<4?1.2f+i*.15f:.2f),0);}
   var humanCorpse=Spawn(human);Validate(humanCorpse,16);UnityEngine.Object.DestroyImmediate(humanCorpse.gameObject);UnityEngine.Object.DestroyImmediate(human);
  }
  for(int i=0;i<14;i++)Spawn(actor);Check(Corpses.Length==12,"simultaneous deaths remain bounded to 12 corpses");
  foreach(var c in Corpses)UnityEngine.Object.DestroyImmediate(c.gameObject);Check(SceneManager.Loaded.SetEquals(new[]{1}),"all corpse physics scenes released");
  Check(Debug.Errors==0,"all supported death layouts build without errors");
  Console.WriteLine("PASS "+checks+" production death-body/skin/weapon/joint/isolation/fade/lifetime checks; PhysX motion and rendering require Unity.");
 }
}
