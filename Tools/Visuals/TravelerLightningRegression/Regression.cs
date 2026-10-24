using System;
using System.Linq;
using System.Reflection;
using Alterverse.Combat;
using Alterverse.Editor;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
static class Regression
{
    static int checks;static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static GameObject Native(string path,Material material)
    {var g=new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));g.AddComponent<ParticleSystem>();g.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{material};g.AddComponent<Collider>();g.AddComponent<AudioSource>();g.AddComponent<DemoController>();g.SetActive(false);AssetDatabase.Assets[path]=g;return g;}
    static void Reset()=>typeof(TravelerLightningVfx).GetMethod("ResetStatic",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static GameObject[] Parts(string source)=>GameObject.All.Where(g=>!g.Destroyed&&g.GetComponent<TravelerLightningVfx>()!=null&&g.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name==source)).ToArray();
    static GameObject Shot(int since,int slot)=>GameObject.All.Skip(since).Single(g=>g.name==(slot==0?"DF_Traveler_TemporalBeam_Lightning":"DF_Traveler_ChronoSnare_Lightning"));
    static void Update(GameObject g)=>GameObject.Invoke(g.GetComponent<TravelerLightningVfx>(),"Update");
    static void Main()
    {
        var m=new Material(new Shader()){name="NativeLightningYellow"};m.SetColor("_Color",new Color(1,.8f,.1f));AssetDatabase.Assets["Assets/Epic Toon FX/Materials/NativeLightningYellow.mat"]=m;
        var missile=Native(TravelerLightningBuilder.ProjectileSource,m);
        var muzzle=Native("Assets/Epic Toon FX/Prefabs/Combat/LightningReferencedMuzzleYellow.prefab",m);
        var hit=Native("Assets/Epic Toon FX/Prefabs/Combat/LightningReferencedHitYellow.prefab",m);
        var nova=Native(TravelerLightningBuilder.NovaSource,m);
        Native(TravelerLightningBuilder.MuzzleSource,m);Native(TravelerLightningBuilder.ImpactSource,m);Native("Assets/Epic Toon FX/Prefabs/2D Alternate/LightningMissileYellow.prefab",m);
        var cast=new AudioClip{name="CompleteNativeCast"};var impact=new AudioClip{name="CompleteNativeImpact"};var windup=new AudioClip{name="CompleteNativeCharge"};
        AssetDatabase.Assets[TravelerLightningBuilder.CastSound]=cast;AssetDatabase.Assets["Assets/Epic Toon FX/Sound/etfx_electric_impact.wav"]=impact;AssetDatabase.Assets["Assets/Epic Toon FX/Sound/etfx_lightning_charge.wav"]=windup;
        var controller=missile.GetComponent<DemoController>();controller.ObjectReferences["muzzleParticle"]=muzzle.GetComponent<ParticleSystem>();controller.References["impactParticle"]=hit;
        controller.ObjectReferences["castSound"]=cast;controller.ObjectReferences["hitSound"]=impact;controller.ObjectReferences["chargeSound"]=windup;
        TravelerLightningBuilder.Build();var data=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/CombatVfx/TravelerLightning/Library.prefab").GetComponent<TravelerLightningVfx>();
        Check(data.lightning.sources.Contains("LightningReferencedMuzzleYellow")&&data.lightning.sources.Contains("LightningReferencedHitYellow"),"real controller references win over generic fallback stages");
        foreach(var part in new[]{data.lightning.muzzle,data.lightning.projectile,data.lightning.impact,data.lightning.snareNova})
        {
            Check(part.radius==6,"horizontal radius is measured independently of native height");
            Check(!part.prefab.activeSelf,"native copy waits for position and footprint before activation");
            Check(part.prefab.GetComponentsInChildren<AudioSource>(true).Length==0&&part.prefab.GetComponentsInChildren<Collider>(true).Length==0&&part.prefab.GetComponentsInChildren<DemoController>(true).Length==0,"visual cannot play duplicate audio or change gameplay");
            Check(part.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.main.maxParticles==321),"full native particle detail retained");
        }
        Check(data.lightning.projectile.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.main.loop),"native flight loops are retained");
        Check(ReferenceEquals(data.lightning.castClip,cast)&&ReferenceEquals(data.lightning.impactClip,impact),"complete native clip references are used without copies/trimming");
        int saves=AssetDatabase.Saves;TravelerLightningBuilder.Build();Check(AssetDatabase.Saves==saves,"source preparation is idempotent");
        Resources.Prefab=data.gameObject;Reset();Check(TravelerLightningVfx.Ready(0)&&TravelerLightningVfx.Ready(2)&&!TravelerLightningVfx.Ready(1)&&!TravelerLightningVfx.Ready(3),"only the two authorized Traveler skills use this chain");
        var actor=new GameObject("Traveler");actor.AddComponent<Health>();var victim=new GameObject("Target");var hp=victim.AddComponent<Health>();victim.transform.position=new Vector3(0,0,4);
        int before=GameObject.All.Count;Time.time=10;
        Check(TravelerLightningVfx.Cast(actor,0),"native preparation handles the selected cast");Check(GameObject.All.Count==before,"cast preparation cannot duplicate the release muzzle");
        Check(TravelerLightningVfx.Launch(actor,0,new(),new Vector3(0,.35f,4),.25f,.3f,1),"Temporal Beam launches full native lightning chain");var shot=Shot(before,0);
        Check(Parts("LightningReferencedMuzzleYellow").Length>=1,"native muzzle exists at release");
        var flight=GameObject.All.Last(g=>g.name=="DF_Traveler_Lightning_Flight");Check(Math.Abs(flight.transform.localScale.x-.18f*3.5f/6f)<.0001,"flight enlarged by +250 percent");
        var block=flight.GetComponentsInChildren<Renderer>(true).Single().Block;Check(block.Vectors["_AttackClip"].z==.3f&&block.Vectors["_AttackClip"].w==2,"flight is bounded to real shot width and flat endpoints");
        int bursts=Parts("LightningReferencedHitYellow").Length;victim.transform.position=new Vector3(.5f,0,4);TravelerLightningVfx.ConfirmHit(actor,hp,0);int sounds=0;
        Check(TravelerLightningVfx.ScheduleImpactAudio(actor,0,victim.transform.position,()=>sounds++),"confirmed early damage can defer contact audio");
        Check(Parts("LightningReferencedHitYellow").Length==bursts&&sounds==0,"visual impact and sound wait for lightning arrival");
        Time.time+=.25f;Update(shot);Check(block.Vectors["_AttackTrail"].z>0,"confirmed endpoint correction also updates cached clip corridor");Check(Parts("LightningReferencedHitYellow").Length==bursts+1&&sounds==1,"confirmed arrival creates one impact and one sound");
        Check(shot.Destroyed,"finished direct lightning delivery is cleaned up");
        before=GameObject.All.Count;Time.time+=10;TravelerLightningVfx.Launch(actor,0,new(),new Vector3(0,0,5),.2f,.3f,2);var miss=Shot(before,0);bursts=Parts("LightningReferencedHitYellow").Length;
        Time.time+=1;Update(miss);Check(miss.Destroyed&&Parts("LightningReferencedHitYellow").Length==bursts,"missed direct shot never invents an impact");
        before=GameObject.All.Count;Time.time+=10;TravelerLightningVfx.Launch(actor,0,new(),new Vector3(0,0,5),.2f,.3f,3);var cancelled=Shot(before,0);TravelerLightningVfx.CancelDelivery(3);Check(cancelled.Destroyed,"delivery cancellation removes pending lightning shot");
        before=GameObject.All.Count;Time.time+=10;var center=new Vector3(0,.2f,4);TravelerLightningVfx.Launch(actor,2,new(),center,.38f,.3f,4);var snare=Shot(before,2);bursts=Parts("LightningReferencedHitYellow").Length;
        int novas=Parts("MagicNovaYellow").Length;
        TravelerLightningVfx.ConfirmHit(actor,hp,2);TravelerLightningVfx.ConfirmHit(actor,hp,2);
        Check(Parts("LightningReferencedHitYellow").Length==bursts,"Chrono victims cannot each create a full central explosion");
        int areaAudio=0;TravelerLightningVfx.ResolveArea(actor,center,2.2f,()=>areaAudio++);
        Check(areaAudio==0&&Parts("MagicNovaYellow").Length==novas,"Nova and audio wait for flight arrival; early area resolution still waits for existing flight deadline");Time.time+=.38f;Update(snare);
        Check(areaAudio==1&&Parts("MagicNovaYellow").Length==novas+1,"Chrono produces one central native Nova and package sound only after arrival");
        Check(Parts("LightningReferencedHitYellow").Length==bursts,"Chrono cannot also spawn its former lightning-explosion impact");
        var explosion=GameObject.All.Last(g=>g.name=="DF_Traveler_Lightning_Burst");var clip=explosion.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
        Check(explosion.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name=="MagicNovaYellow"),"Chrono ground detonation is the selected full Nova composition");
        Check(clip.z==2.2f&&clip.x==center.x&&clip.y==center.z,"Chrono detonation stays at actual center and AoE radius");
        Check(Math.Abs(explosion.transform.localScale.x-2.2f*3.5f/6f)<.0001,"Chrono interior receives requested geometry enlargement");
        areaAudio=0;TravelerLightningVfx.ResolveArea(actor,center,2.2f,()=>areaAudio++);Check(areaAudio==1,"ground-only cast still has its legitimate ground contact sound");
        Resources.Prefab=null;Reset();Check(!TravelerLightningVfx.Ready(0)&&!TravelerLightningVfx.Launch(actor,0,new(),new(),.2f,.3f),"unprepared complete chain preserves old visual/audio fallback");
        Console.WriteLine($"PASS {checks} Traveler native chain/controller/audio-reference/scale/radius/confirmed-impact/miss/cancellation/AoE contracts. Controlled fixtures; native rendering and Android acceptance remain separate.");
    }
}
