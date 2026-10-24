using System;
using System.Linq;
using System.Reflection;
using Alterverse.Editor;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

static class Regression
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static GameObject Native(string name,Material material)
    {
        var root=new GameObject(name);root.AddComponent<ParticleSystem>();root.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{material};
        root.AddComponent<AudioSource>();root.AddComponent<Collider>();return root;
    }
    static int Hits(int from)=>GameObject.All.Skip(from).Count(g=>g.name=="DF_SmallBullet_Impact");
    static SmallBulletVfx Session(int from)=>GameObject.All.Skip(from).Single(g=>g.name=="DF_SmallBullet_Shot").GetComponent<SmallBulletVfx>();
    static void Tick(SmallBulletVfx shot,float time){Time.time=time;GameObject.Invoke(shot,"Update");}
    static void Main()
    {
        var solid=new Material(new Shader()){name="solid_AB"};solid.SetColor("_Color",new Color(1,.5f,.1f));
        AssetDatabase.Assets["Assets/Epic Toon FX/Materials/solid_AB.mat"]=solid;
        var bullet=Native("NativeBullet",solid);var muzzle=Native("NativeMuzzle",solid);var hit=Native("NativeHit",solid);
        const string folder="Assets/Epic Toon FX/Prefabs/Combat/Missiles/SmallBullet/";
        AssetDatabase.Assets[folder+"SmallBulletWhite.prefab"]=bullet;
        AssetDatabase.Assets[folder+"SmallBulletMuzzleWhite.prefab"]=muzzle;
        AssetDatabase.Assets[folder+"SmallBulletHitWhite.prefab"]=hit;
        // A similarly named 2D asset must never win the source lookup.
        AssetDatabase.Assets["Assets/Epic Toon FX/Prefabs/2D Alternate/SmallBulletWhite.prefab"]=Native("Wrong2D",solid);
        var controller=bullet.AddComponent<DemoController>();controller.References["muzzleParticle"]=muzzle;controller.References["impactParticle"]=hit;
        SmallBulletBuilder.Build();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/CombatVfx/SmallBullet/Library.prefab");
        Check(prefab!=null&&!prefab.activeSelf,"complete resource library is saved inactive");
        var library=prefab.GetComponent<SmallBulletVfx>();
        Check(library.muzzle!=null&&library.projectile!=null&&library.impact!=null,"all three native chain parts are required");
        Check(library.projectile.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name=="NativeBullet"),"3D projectile selected");
        Check(library.muzzle.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name=="NativeMuzzle"),"native controller muzzle reference followed");
        Check(library.impact.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name=="NativeHit"),"native controller impact reference followed");
        foreach(var part in new[]{library.muzzle,library.projectile,library.impact})
        {
            Check(part.GetComponentsInChildren<Collider>(true).Length==0&&part.GetComponentsInChildren<AudioSource>(true).Length==0,"no collision or doubled prefab audio");
            Check(part.GetComponentsInChildren<ParticleSystem>(true).Single().main.maxParticles==321,"native layer detail retained");
        }
        int saves=AssetDatabase.Saves;SmallBulletBuilder.Build();Check(AssetDatabase.Saves==saves,"preparation is idempotent");
        Check(bullet.GetComponent<AudioSource>()!=null&&bullet.GetComponent<DemoController>()!=null,"purchased sources untouched");
        Resources.Prefab=prefab;
        var actor=new GameObject("Sivra");var health=actor.AddComponent<Alterverse.Combat.Health>();
        Time.time=0;int first=GameObject.All.Count;
        Check(SmallBulletVfx.Launch(actor,new(0,1,0),new(4,1,0),.16f,deliveryId:101),"Sivra basic launches");
        Check(Hits(first)==0,"no premature impact at release");
        Check(SmallBulletVfx.ConfirmHit(actor,new(4,1,0)),"confirmed damage contact links to the flight");
        var session=Session(first);Tick(session,.08f);Check(Hits(first)==0,"native hit waits for flight arrival");
        var flight=GameObject.All.Skip(first).Single(g=>g.name=="DF_SmallBullet_Projectile");
        Check(Math.Abs(flight.transform.position.x-2)<.001f,"projectile travels rather than becoming a full-range beam");
        var clip=flight.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
        Check(clip.z==.10f&&clip.w==2,"small projectile and trail stay within the real shot segment");
        Tick(session,.17f);Check(Hits(first)==1,"one native impact at arrival");
        Check(!health.IsDead,"presentation never applies damage");
        Time.time=1;int miss=GameObject.All.Count;SmallBulletVfx.Launch(actor,new(),new(4,0,0),.16f,deliveryId:102);
        var missed=Session(miss);Tick(missed,1.2f);Check(Hits(miss)==0,"missed shot cannot invent a hit");SmallBulletVfx.CancelDelivery(102);
        Check(missed.gameObject.Destroyed,"delivery cancellation cleans up the shot");
        Time.time=2;int dead=GameObject.All.Count;SmallBulletVfx.Launch(actor,new(),new(4,0,0),.16f);health.IsDead=true;
        var dying=Session(dead);Tick(dying,2.05f);Check(dying.gameObject.Destroyed&&Hits(dead)==0,"owner death cancels the visual without a phantom hit");health.IsDead=false;
        foreach(var map in Enum.GetValues<Alterverse.Match.ArenaVariant>())
        {
            Alterverse.Match.ArenaVariantRuntime.Current=map;Time.time+=1;float start=Time.time;int before=GameObject.All.Count;
            var guard=new GameObject("Guard");guard.AddComponent<MechMachineGunVisual>();int sounds=Alterverse.Audio.DimensionAudio.Calls;
            Check(GuardBridge.SmallBulletGuard(guard,new(),new(3,0,0),0),"guard chain exists on "+map);
            var shots=GameObject.All.Skip(before).Where(g=>g.name=="DF_SmallBullet_Shot").Select(g=>g.GetComponent<SmallBulletVfx>()).ToArray();
            Check(shots.Length==3,"original mech three-round burst retained on "+map);
            foreach(var shot in shots)Tick(shot,start+.2f);
            Check(Hits(before)==3,"every burst round reaches its native impact on "+map);
            Check(Alterverse.Audio.DimensionAudio.Calls==sounds+1,"one original burst sound on "+map);
        }
        Time.time+=1;float concurrentAt=Time.time;int concurrent=GameObject.All.Count;
        SmallBulletVfx.Launch(actor,new(),new(4,0,0),.16f,deliveryId:201);var left=Session(concurrent);
        int next=GameObject.All.Count;SmallBulletVfx.Launch(actor,new(),new(-4,0,0),.16f,deliveryId:202);var right=Session(next);
        SmallBulletVfx.ConfirmHit(actor,new(4,0,0));Tick(right,concurrentAt+.2f);
        Check(Hits(concurrent)==0,"confirmation must not attach to the newer shot aimed elsewhere");
        Tick(left,concurrentAt+.2f);Check(Hits(concurrent)==1,"confirmation follows the matching endpoint");
        Tick(left,concurrentAt+.21f);Check(Hits(concurrent)==1,"finished shot cannot produce another impact");
        SmallBulletVfx.CancelOwner(actor);
        Check(right.gameObject.Destroyed,"owner cancellation releases remaining concurrent shots");
        Time.time+=1;float heldAt=Time.time;int heldStart=GameObject.All.Count;
        SmallBulletVfx.Launch(actor,new(),new(4,0,0),.5f,deliveryId:203);var heldSession=Session(heldStart);
        var heldBullet=GameObject.All.Skip(heldStart).Single(g=>g.name=="DF_SmallBullet_Projectile");
        foreach(var system in heldBullet.GetComponentsInChildren<ParticleSystem>(true))system.Playing=false;
        Time.time=heldAt+.2f;GameObject.Invoke(heldBullet.GetComponent<SmallBulletVfx>(),"Update");Tick(heldSession,heldAt+.21f);
        Check(!heldBullet.Destroyed,"short native one-shot layer cannot delete the travelling part early");
        Check(GameObject.All.Skip(heldStart).Count(g=>g.name=="DF_SmallBullet_Projectile")==1,"a finished native layer cannot retrigger muzzle/projectile spawning");
        SmallBulletVfx.CancelOwner(actor);
        var child=new GameObject("NativeOneShotSubEmitter");child.transform.SetParent(bullet.transform,false);
        var sub=child.AddComponent<ParticleSystem>();sub.main.loop=false;sub.main.playOnAwake=false;
        AssetDatabase.Dependency="one-shot-sub-emitter";SmallBulletBuilder.Build();
        var rebuilt=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/CombatVfx/SmallBullet/Library.prefab").GetComponent<SmallBulletVfx>();
        var preparedSystems=rebuilt.projectile.GetComponentsInChildren<ParticleSystem>(true);
        Check(!preparedSystems.Single(ps=>ps.gameObject.name=="NativeOneShotSubEmitter").main.loop,"native one-shot sub-emitter does not become an endless loop");
        Check(preparedSystems.Single(ps=>ps.gameObject.name=="NativeBullet").main.loop,"native looping projectile emitter is retained");
        Console.WriteLine($"PASS {checks} Small Bullet source-chain/flight/confirmed-hit/cancellation/guard/audio contracts. Controlled fixtures; licensed prefab rendering and native Unity remain unverified.");
    }
}
