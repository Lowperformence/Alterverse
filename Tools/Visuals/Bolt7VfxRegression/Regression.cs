using System;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Alterverse.Visuals;
using Alterverse.Editor;
class Regression {
 static int checks;
 static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.0001f,why+" got "+a+" expected "+b);
 static object Runner=>typeof(Bolt7Vfx).GetField("runner",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
 static object[] Parts=>((IEnumerable)Runner.GetType().GetField("active",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Runner)).Cast<object>().ToArray();
 static GameObject Go(object p)=>(GameObject)p.GetType().GetField("go").GetValue(p);
 static GameObject Last(int i)=>Parts.Select(Go).Last(g=>g.name=="BOLT7_Native_"+i);
 static GameObject Native(string path,string name,Material mat,bool loop=false){var g=new GameObject(name);g.AddComponent<ParticleSystem>();g.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{mat};var a=g.AddComponent<AudioSource>();a.clip=new AudioClip{name=name+"Sound"};a.loop=loop;a.volume=.2f;AssetDatabase.Assets["Assets/Epic Toon FX/Sound/"+a.clip.name+".wav"]=a.clip;g.SetActive(false);AssetDatabase.Assets[path]=g;return g;}
 static void Tick(float time){Time.time=time;GameObject.Invoke(Runner,"Update");}
 static void Main(){
  var mat=new Material(new Shader()){name="SharedNativeMaterial"};AssetDatabase.Assets["Assets/Epic Toon FX/Materials/Shared.mat"]=mat;
  var muzzle=Native("Assets/Epic Toon FX/Prefabs/GrenadeMuzzle.prefab","Muzzle",mat);
  var projectile=Native("Assets/Epic Toon FX/Prefabs/GrenadeProjectile.prefab","Projectile",mat,true);
  var impact=Native("Assets/Epic Toon FX/Prefabs/GrenadeExplosion.prefab","Impact",mat);
  projectile.AddComponent<DemoController>().References["muzzle"]=muzzle;projectile.GetComponent<DemoController>().References["impact"]=impact;
  Native("Assets/Epic Toon FX/Prefabs/LightningOrbBlue.prefab","Orb",mat);Native("Assets/Epic Toon FX/Prefabs/StarFogPurple.prefab","Fog",mat);
  Native("Assets/Epic Toon FX/Prefabs/CartoonyPunchHeavyRed.prefab","Punch",mat);Native(UltimateLightningBuilder.IoneSource,"LightningNova",mat);
  var bulletMuzzle=Native("Assets/Epic Toon FX/Prefabs/BulletMuzzle.prefab","BulletMuzzle",mat);
  var bullet=Native("Assets/Epic Toon FX/Prefabs/Combat/Missiles/Bullet/BulletSmallFire.prefab","BulletFlight",mat,true);
  var bulletHit=Native("Assets/Epic Toon FX/Prefabs/BulletImpact.prefab","BulletHit",mat);
  bullet.AddComponent<DemoController>().References["muzzle"]=bulletMuzzle;bullet.GetComponent<DemoController>().References["impact"]=bulletHit;
  Bolt7Importer.Build();var lib=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/Bolt7/Vfx/Library.prefab").GetComponent<Bolt7VfxLibrary>();
  Check(lib.heavyPunch!=null&&lib.lightningNova!=null,"native punch and nova discovered");
  foreach(var stage in new[]{lib.muzzle,lib.grenade,lib.explosion,lib.lightningOrb,lib.starFog,lib.heavyPunch,lib.lightningNova}){
   var audio=stage.GetComponentsInChildren<AudioSource>(true).Single(a=>a.clip!=null);Check(audio.clip!=null&&!audio.playOnAwake,"original native audio retained without prewarm autoplay");
   Check(stage.GetComponentsInChildren<DemoController>(true).Length==0,"demo scripts removed");}
  var novaMaterial=lib.lightningNova.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials[0];var grenadeMaterial=lib.explosion.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials[0];
  Check(novaMaterial.shader.name=="DimensionsFront/BomberNukeParticles"&&grenadeMaterial.shader.name=="DimensionsFront/SelectedAbilityParticles","Nova clip shader does not overwrite shared materials of other stages");
  Resources.Prefabs["Bolt7/Vfx/Library"]=lib.gameObject;var actor=new GameObject("Bolt7");
  var mechShot=new AudioClip{name="MechGun",length=.3f};Resources.AudioClips["Audio/SFX/MechGun"]=mechShot;
  // An active source variant must never enable native particles/audio at the origin during warmup.
  lib.explosion.SetActive(true);int enabledClones=UnityEngine.Object.EnabledClones;
  Bolt7Vfx.Warm();Check(UnityEngine.Object.EnabledClones==enabledClones&&Parts.Length==0,"Warmup cannot activate an explosion or emit a gameplay effect");
  lib.explosion.SetActive(false);
  Bolt7Vfx.Grenade(actor,new Vector3(0,0,4),.45f);Near(Last(0).transform.localScale.x,1.75f,"muzzle reduced 30 percent from 2.5");Near(Last(1).transform.localScale.x,1.75f,"projectile reduced 30 percent");Tick(.02f);
  var flying=Last(1).GetComponentsInChildren<AudioSource>(true).Single(a=>a.clip!=null);Check(flying.isPlaying&&flying.loop,"native projectile flight audio starts");Near(flying.volume,.7f*.65f*.75f*.75f*.8f,"all Bolt audio is another 20 percent quieter");
  Alterverse.Profile.PlayerProfile.SfxVolume=0;Tick(.03f);Near(flying.volume,0,"SFX mute respected");Alterverse.Profile.PlayerProfile.SfxVolume=1;
  Bolt7Vfx.Area(actor,new Vector3(0,0,4),0);Near(Last(2).transform.localScale.x,Alterverse.Characters.Bolt7Balance.FxScale*.7f*Alterverse.Characters.Bolt7Balance.GrenadeRadius/1.4f,"Frag Arc blast matches the current damage radius");Tick(.04f);Check(Last(2).GetComponentsInChildren<AudioSource>(true).Single(a=>a.clip!=null).isPlaying,"detonation audio plays independently of hit count");
  Tick(.5f);Check(!flying.isPlaying,"flight loop stops at impact");
  Bolt7Vfx.Grenade(actor,Vector3.zero,.55f,2);Near(Last(1).transform.localScale.x,1.75f*Alterverse.Characters.Bolt7Balance.ImpulseRadius/Alterverse.Characters.Bolt7Balance.GrenadeRadius,"second grenade footprint follows the current radius ratio");Bolt7Vfx.Area(actor,Vector3.zero,2);Near(Last(2).transform.localScale.x,Alterverse.Characters.Bolt7Balance.FxScale*.7f*Alterverse.Characters.Bolt7Balance.ImpulseRadius/1.4f,"Impulse blast matches the current damage radius");
  int before=HeroVfxPool.Emits;Bolt7Vfx.Cast(actor,1);Bolt7Vfx.RamDash(actor,Vector3.zero,new Vector3(0,0,2),.15f);Bolt7Vfx.Area(actor,Vector3.zero,1);Check(HeroVfxPool.Emits-before>=9&&Last(5)!=null,"ram has charge, dash trails, native punch, pulse and sparks");
  Resources.AudioClips["Audio/Heroes/Bolt7/Nullfield_Selected"]=new AudioClip{name="UserNullfield",length=3.72f};
  Vector3 center=new Vector3(3,0,5);Bolt7Vfx.Area(actor,center,3);var nova=Last(6);var clip=nova.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
  Near(clip.x,3,"nova center x");Near(clip.y,5,"nova center z");Near(clip.z,3,"nova exactly clips to 3m AOE radius");Near(clip.w,1,"nova radial clip enabled");
  Near(Last(3).transform.localScale.x,2.5f,"Lightning Orb scale unchanged");Check(!Last(4).activeSelf,"Star Fog stays delayed after Orb/Nova");
  Check(Last(3).GetComponentsInChildren<AudioSource>(true).Count(a=>a.clip!=null)==1&&Last(3).GetComponentsInChildren<AudioSource>(true).Single(a=>a.clip!=null).clip.name=="UserNullfield","user Nullfeld sound replaces native Orb audio once");
  Check(Last(6).GetComponentsInChildren<AudioSource>(true).All(a=>a.clip==null)&&Last(4).GetComponentsInChildren<AudioSource>(true).All(a=>a.clip==null),"Nova and Fog do not duplicate the user's detonation sound");Tick(.7f);Check(Last(4).activeSelf,"Star Fog follows detonation");
  Check(lib.bulletMuzzle!=null&&lib.bulletProjectile!=null&&lib.bulletImpact!=null,"complete native Gatling bullet chain resolved");
  Bolt7Vfx.Basic(actor,new Vector3(0,0,4),.1f);Near(Last(8).transform.localScale.x,2.5f/3f,"Gatling projectile reduced to one third of previous size");Near(Last(7).transform.localScale.x,2.5f/3f,"Gatling muzzle reduced to one third");Tick(.72f);
  var shot=Last(7).GetComponentsInChildren<AudioSource>(true).Single(a=>a.clip!=null);Check(shot.isPlaying&&ReferenceEquals(shot.clip,mechShot),"Gatling plays the exact existing mech recording once");Near(shot.volume,.65f*.75f*.75f*.8f,"replacement shot includes the additional 20 percent reduction");Check(Last(8).GetComponentsInChildren<AudioSource>(true).All(a=>a.clip==null),"old projectile sound does not overlap the mech shot");
  Bolt7Vfx.BasicImpact(actor,new Vector3(0,0,4));Near(Last(9).transform.localScale.x,2.5f/3f,"Gatling impact reduced to one third");Tick(.73f);Check(Last(9).GetComponentsInChildren<AudioSource>(true).All(a=>a.clip==null),"confirmed basic hit does not duplicate the mech recording with old audio");Tick(.85f);Check(shot.Plays==1,"one recorded burst per basic attack");
  Near(HeroVfxPool.RamOrigin.z,0,"ram sweep stays anchored to original cone, not dash endpoint");Near(HeroVfxPool.RamSize.z,3,"ram sweep reaches exactly the melee cone range");
  foreach(int slot in new[]{0,2})foreach(float range in new[]{0f,.1f,4f}){
   actor.transform.position=new Vector3(5,1,7);Vector3 point=new Vector3(5,0,7+range);float start=Time.time,seconds=slot==0?.45f:.55f;
   Bolt7Vfx.Grenade(actor,point,seconds,slot);var grenade=Last(1);actor.transform.position+=Vector3.right*3;
   Tick(start+seconds*.999f);Check(Vector3.Distance(grenade.transform.position,point)<.01f,"grenade visual arrives at original near/self/far point while owner moves");
   Bolt7Vfx.Area(actor,point,slot);Check(Vector3.Distance(Last(2).transform.position,point)<.0001f,"detonation visual uses same grenade destination");Tick(start+seconds+.01f);
  }
  Console.WriteLine("PASS "+checks+" BOLT-7 native VFX/audio/scale/chain contracts. Controlled fixtures; native rendering/listening remain separate.");
 }
}
