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
    const string Prefix="Assets/Epic Toon FX/";
    const string SmallPath=Prefix+"Prefabs/Combat/Missiles/NovaSmall/NovaSmallBlue.prefab";
    const string LargePath=Prefix+"Prefabs/Combat/Missiles/Nova/NovaBlue.prefab";
    const string Library="Assets/Alterverse/Resources/CombatVfx/SivraNova/Library.prefab";
    static int checks;
    static void Check(bool condition,string message){checks++;if(!condition)throw new Exception(message);}
    static AudioClip Clip(string name){var clip=new AudioClip{name=name};AssetDatabase.Assets[Prefix+"Sound/"+name+".wav"]=clip;return clip;}
    static GameObject Native(string name,Material material,AudioClip clip=null)
    {
        var root=new GameObject(name);root.AddComponent<ParticleSystem>();root.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{material};
        root.AddComponent<Collider>();root.AddComponent<AudioSource>().clip=clip;return root;
    }
    static SivraNovaVfx Data()=>AssetDatabase.LoadAssetAtPath<GameObject>(Library).GetComponent<SivraNovaVfx>();
    static void Reset(){typeof(SivraNovaVfx).GetMethod("ResetStatic",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);}
    static void Tick(SivraNovaVfx component,float time){Time.time=time;GameObject.Invoke(component,"Update");}
    static SivraNovaVfx Shot(int first,int slot)=>GameObject.All.Skip(first).Single(g=>g.name==(slot==2?"DF_Sivra_NovaSmall_Shot":"DF_Sivra_Nova_Shot")).GetComponent<SivraNovaVfx>();
    static int Parts(int first,string suffix)=>GameObject.All.Skip(first).Count(g=>g.name=="DF_Sivra_"+suffix);
    static bool ContainsNative(SivraNovaVfx.Effect effect,string name)=>effect.prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name==name);
    static void Main()
    {
        var material=new Material(new Shader()){name="NativeBlueSolid"};material.SetColor("_Color",new Color(.15f,.5f,1f));
        AssetDatabase.Assets[Prefix+"Materials/NativeBlueSolid.mat"]=material;
        var smallCast=Clip("NovaSmall_shoot_blue");var smallHit=Clip("NovaSmall_hit_blue");var smallWindup=Clip("NovaSmall_charge");
        var largeCast=Clip("Nova_shoot_blue");var largeHit=Clip("Nova_hit_blue");var largeWindup=Clip("Nova_charge");
        var small=Native("NativeNovaSmallBlue",material);var large=Native("NativeNovaBlue",material);
        var smallMuzzle=Native("LinkedSmallMuzzleBlue",material,smallCast);var smallImpact=Native("LinkedSmallImpactBlue",material,smallHit);
        var largeMuzzle=Native("LinkedLargeMuzzleBlue",material,largeCast);var largeImpact=Native("LinkedLargeImpactBlue",material,largeHit);
        AssetDatabase.Assets[SmallPath]=small;AssetDatabase.Assets[LargePath]=large;
        foreach(var item in new[]{smallMuzzle,smallImpact,largeMuzzle,largeImpact})AssetDatabase.Assets[Prefix+"Prefabs/Shared/"+item.name+".prefab"]=item;
        var smallController=small.AddComponent<DemoController>();smallController.ObjectReferences["muzzleParticle"]=smallMuzzle.GetComponent<ParticleSystem>();
        smallController.References["impactParticle"]=smallImpact;smallController.ObjectReferences["windupClip"]=smallWindup;
        var largeController=large.AddComponent<DemoController>();largeController.References["muzzleParticle"]=largeMuzzle;
        largeController.ObjectReferences["impactParticle"]=largeImpact.GetComponent<ParticleSystem>();largeController.ObjectReferences["chargeClip"]=largeWindup;
        AssetDatabase.Assets[Prefix+"Prefabs/Combat/Missiles/NovaSmall/NovaSmallFire.prefab"]=Native("WrongFireSmall",material);
        AssetDatabase.Assets[Prefix+"Prefabs/Combat/Missiles/Nova/NovaWhite.prefab"]=Native("WrongWhiteNova",material);
        AssetDatabase.Assets[Prefix+"Prefabs/2D Alternate/NovaSmallBlue.prefab"]=Native("Wrong2DSmall",material);
        AssetDatabase.Assets[Prefix+"Prefabs/2D Alternate/NovaBlue.prefab"]=Native("Wrong2DNova",material);
        AssetDatabase.Assets[Prefix+"Prefabs/Combat/Muzzleflash/NovaSmall/NovaSmallMuzzleBlue.prefab"]=Native("DecoySmallMuzzleBlue",material);
        AssetDatabase.Assets[Prefix+"Prefabs/Combat/Explosions/NovaSmall/NovaSmallHitBlue.prefab"]=Native("DecoySmallImpactBlue",material);
        AssetDatabase.Assets[Prefix+"Prefabs/Combat/Muzzleflash/Nova/NovaMuzzleBlue.prefab"]=Native("DecoyLargeMuzzleBlue",material);
        AssetDatabase.Assets[Prefix+"Prefabs/Combat/Explosions/Nova/NovaHitBlue.prefab"]=Native("DecoyLargeImpactBlue",material);
        SivraNovaBuilder.Build();var data=Data();
        Check(!data.gameObject.activeSelf,"prepared resource library is inactive");
        Check(ContainsNative(data.novaSmall.projectile,"NativeNovaSmallBlue"),"Nova Small uses the distinct selected blue family");
        Check(ContainsNative(data.nova.projectile,"NativeNovaBlue"),"Nova uses the distinct normal blue family");
        Check(!data.novaSmall.sources.Contains("WrongFire")&&!data.novaSmall.sources.Contains("2D Alternate"),"non-blue/2D variants are excluded");
        Check(ContainsNative(data.novaSmall.muzzle,"LinkedSmallMuzzleBlue"),"native component muzzle reference wins over name heuristics");
        Check(ContainsNative(data.novaSmall.impact,"LinkedSmallImpactBlue"),"native GameObject impact reference wins over name heuristics");
        Check(ContainsNative(data.nova.muzzle,"LinkedLargeMuzzleBlue"),"normal native GameObject muzzle reference preserved");
        Check(ContainsNative(data.nova.impact,"LinkedLargeImpactBlue"),"normal native component impact reference preserved");
        foreach(var chain in new[]{data.novaSmall,data.nova})foreach(var effect in new[]{chain.muzzle,chain.projectile,chain.impact})
        {
            Check(effect.prefab.GetComponentsInChildren<AudioSource>(true).Length==0,"native AudioSources stripped to prevent doubled sound");
            Check(effect.prefab.GetComponentsInChildren<Collider>(true).Length==0,"cosmetic parts have no gameplay colliders");
            Check(effect.prefab.GetComponentsInChildren<ParticleSystem>(true).Single().main.maxParticles==321,"native detail count retained");
        }
        Check(data.novaSmall.castClip==smallCast&&data.novaSmall.impactClip==smallHit,"small native AudioSource clips kept as asset references");
        Check(data.nova.castClip==largeCast&&data.nova.impactClip==largeHit,"normal native AudioSource clips kept as asset references");
        Check(data.novaSmall.windupClip==smallWindup&&data.nova.windupClip==largeWindup,"controller AudioClip windup references resolved");
        Check(small.GetComponent<DemoController>()!=null&&small.GetComponent<AudioSource>()!=null,"purchased source prefab is not rewritten");
        int saves=AssetDatabase.Saves;SivraNovaBuilder.Build();Check(AssetDatabase.Saves==saves,"repeat preparation is idempotent");
        // Missing blue Small must not silently become Fire or the larger normal Nova.
        AssetDatabase.Assets.Remove(SmallPath);AssetDatabase.Dependency="missing-small-blue";bool missing=false;
        try{SivraNovaBuilder.Build();}catch(InvalidOperationException error){missing=error.Message.Contains("Nova Small")&&error.Message.Contains("projectile")&&error.Message.Contains("NovaSmallFire.prefab");}
        Check(missing,"missing exact blue small family reports actual found variants");
        Check(ReferenceEquals(data,Data()),"a failed import cannot publish a partial replacement library");
        AssetDatabase.Assets[SmallPath]=small;AssetDatabase.Dependency="restored-small-blue";SivraNovaBuilder.Build();data=Data();
        Resources.Prefab=data.gameObject;Reset();
        Check(SivraNovaVfx.AudioFor(2,0)==smallCast&&SivraNovaVfx.AudioFor(2,1)==smallHit,"small cast/impact audio exposed without copying or trimming");
        Check(SivraNovaVfx.AudioFor(3,0)==largeCast&&SivraNovaVfx.AudioFor(3,2)==largeWindup,"ultimate cast/windup audio exposed");
        Check(SivraNovaVfx.AudioFor(0,0)==null&&SivraNovaVfx.AudioFor(1,0)==null,"other Sivra skills retain their existing audio routing");
        var actor=new GameObject("Sivra");var ownerHealth=actor.AddComponent<Health>();
        var victim=new GameObject("Victim");victim.transform.position=new(4,0,0);var hp=victim.AddComponent<Health>();
        Time.time=0;int first=GameObject.All.Count;
        Check(SivraNovaVfx.Cast(actor,2,new(0,1,0),.2f),"small cast starts");
        Check(SivraNovaVfx.Launch(actor,2,new(0,1,0),new(4,.25f,0),.16f,deliveryId:201),"small flight launches");
        Check(Parts(first,"NovaSmall_Impact")==0,"release cannot invent an early hit");
        var snareShot=Shot(first,2);Check(SivraNovaVfx.ConfirmHit(actor,hp,2),"small impact is confirmed by gameplay");
        int sounds=0;Check(SivraNovaVfx.ScheduleImpactAudio(actor,2,victim.transform.position,()=>sounds++),"impact audio waits for the confirmed flight");
        Tick(snareShot,.08f);Check(Parts(first,"NovaSmall_Impact")==0&&sounds==0,"native impact and sound remain pending during flight");
        var flying=GameObject.All.Skip(first).Single(g=>g.name=="DF_Sivra_NovaSmall_Projectile");
        Check(Math.Abs(flying.transform.position.x-2)<.001f,"small nova visibly travels rather than becoming a full-length laser");
        var clip=flying.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
        Check(clip.w==2&&clip.z==.625f,"Nova Small projectile/trail is 150 percent larger and constrained to the shot segment");
        Tick(snareShot,.17f);Check(Parts(first,"NovaSmall_Impact")==1&&sounds==1,"one native hit and one audio callback at arrival");
        Tick(snareShot,.18f);Check(Parts(first,"NovaSmall_Impact")==1&&sounds==1,"finished delivery cannot repeat its impact/audio");
        Time.time=1;int missFirst=GameObject.All.Count;SivraNovaVfx.Launch(actor,2,new(),new(4,0,0),.16f,deliveryId:202);
        var miss=Shot(missFirst,2);Tick(miss,1.2f);Check(Parts(missFirst,"NovaSmall_Impact")==0,"missed shot cannot create a native contact");
        Check(!SivraNovaVfx.ScheduleImpactAudio(actor,2,new(),()=>sounds++),"unconfirmed miss cannot schedule a hit sound");SivraNovaVfx.CancelDelivery(202);
        Check(miss.gameObject.Destroyed,"delivery cancellation removes the pending flight");
        Time.time=2;int cancelFirst=GameObject.All.Count;SivraNovaVfx.Launch(actor,2,new(),new(4,0,0),.16f,deliveryId:203);
        var cancelled=Shot(cancelFirst,2);SivraNovaVfx.ConfirmHit(actor,hp,2);int cancelledAudio=0;
        Check(SivraNovaVfx.ScheduleImpactAudio(actor,2,new(4,0,0),()=>cancelledAudio++),"confirmed audio can be queued before cancellation");
        SivraNovaVfx.CancelDelivery(203);Tick(cancelled,2.3f);
        Check(Parts(cancelFirst,"NovaSmall_Impact")==0&&cancelledAudio==0,"cancelled flight cannot produce a phantom hit/audio");
        // Three existing gameplay releases, never a second visual-only three-shot generator.
        Time.time=3;int ultimateFirst=GameObject.All.Count;Check(SivraNovaVfx.Cast(actor,3,new(),.3f),"ultimate charge starts once");
        int ultimateAudio=0;
        for(int i=0;i<3;i++)
        {
            Time.time=3+i*.18f;int begin=GameObject.All.Count;
            Check(SivraNovaVfx.Launch(actor,3,new(),new(4,.25f,0),.12f,deliveryId:300+i),"one ultimate release "+i);
            var shot=Shot(begin,3);Check(SivraNovaVfx.ConfirmHit(actor,hp,3),"one ultimate confirmed contact "+i);
            Check(SivraNovaVfx.ScheduleImpactAudio(actor,3,victim.transform.position,()=>ultimateAudio++),"one ultimate impact sound scheduled "+i);
            Tick(shot,3+i*.18f+.13f);
        }
        Check(GameObject.All.Skip(ultimateFirst).Count(g=>g.name=="DF_Sivra_Nova_Shot")==3,"three actual cues make exactly three ultimate projectiles");
        Check(Parts(ultimateFirst,"Nova_Impact")==3&&ultimateAudio==3,"three committed shots make three native hits/audio callbacks");
        Check(Parts(ultimateFirst,"Nova_Muzzle")==4,"one cast charge and three actual muzzle releases");
        Check(!hp.IsDead&&!ownerHealth.IsDead,"presenter never applies damage");
        // Status starts only after real gameplay slow; it does not root a building.
        Time.time=5;int statusFirst=GameObject.All.Count;Check(SivraNovaVfx.SnareStatus(actor,hp),"status hook consumed when no valid slow exists");
        Check(GameObject.All.Count==statusFirst,"no bands are invented on an unslowed target");
        var status=victim.AddComponent<HeroCombatStatus>();status.MoveMultiplier=.7f;
        Check(SivraNovaVfx.SnareStatus(actor,hp),"confirmed slow starts native status bands");
        var bands=GameObject.All.Skip(statusFirst).Where(g=>g.name=="DF_Sivra_NovaSmall_Muzzle").ToArray();
        Check(bands.Length==2,"snare has two distinct compact native planes");
        Time.time=5.1f;int refreshFirst=GameObject.All.Count;SivraNovaVfx.SnareStatus(actor,hp,.1f);
        Check(GameObject.All.Count==refreshFirst,"refresh reuses the existing status instead of stacking more bands");
        victim.transform.position=new(5,0,0);Tick(bands[0].GetComponent<SivraNovaVfx>(),5.2f);
        Check(bands[0].transform.position.x==5,"status follows the victim rather than marking a ground AoE");
        Tick(bands[0].GetComponent<SivraNovaVfx>(),6.81f);Tick(bands[1].GetComponent<SivraNovaVfx>(),6.81f);
        Check(bands.All(g=>g.Destroyed),"status ends at the genuine 1.8-second lifetime");
        Time.time=7;int earlyFirst=GameObject.All.Count;SivraNovaVfx.SnareStatus(actor,hp);var early=GameObject.All.Skip(earlyFirst).Where(g=>g.name=="DF_Sivra_NovaSmall_Muzzle").ToArray();
        status.MoveMultiplier=1;foreach(var band in early)Tick(band.GetComponent<SivraNovaVfx>(),7.1f);
        Check(early.All(g=>g.Destroyed),"removing the slow removes the bands early");
        status.MoveMultiplier=.7f;Time.time=8;int deathFirst=GameObject.All.Count;SivraNovaVfx.SnareStatus(actor,hp);var death=GameObject.All.Skip(deathFirst).Where(g=>g.name=="DF_Sivra_NovaSmall_Muzzle").ToArray();
        hp.IsDead=true;foreach(var band in death)Tick(band.GetComponent<SivraNovaVfx>(),8.1f);
        Check(death.All(g=>g.Destroyed),"victim death removes the status");hp.IsDead=false;
        int stale=GameObject.All.Count;Check(SivraNovaVfx.SnareStatus(actor,hp,1.8f),"expired network status is consumed");Check(GameObject.All.Count==stale,"expired status cannot replay bands");
        // A short native layer must not delete the travelling composition and replay its muzzle.
        Time.time=9;int heldFirst=GameObject.All.Count;SivraNovaVfx.Launch(actor,2,new(),new(4,0,0),.5f,deliveryId:500);var held=Shot(heldFirst,2);
        var heldPart=GameObject.All.Skip(heldFirst).Single(g=>g.name=="DF_Sivra_NovaSmall_Projectile");
        foreach(var system in heldPart.GetComponentsInChildren<ParticleSystem>(true))system.Playing=false;
        Tick(heldPart.GetComponent<SivraNovaVfx>(),9.2f);Tick(held,9.21f);
        Check(!heldPart.Destroyed&&Parts(heldFirst,"NovaSmall_Projectile")==1,"finished native layer cannot retrigger the entire projectile");SivraNovaVfx.CancelOwner(actor);
        Check(held.gameObject.Destroyed,"owner cancellation releases pending native skills");
        // Deliberate purchased magic/mystic shot fallback and exact-family phase discovery.
        smallMuzzle.GetComponent<AudioSource>().clip=null;largeMuzzle.GetComponent<AudioSource>().clip=null;
        foreach(string key in AssetDatabase.Assets.Keys.Where(p=>p.Contains("Nova")&&p.Contains("_shoot_")).ToArray())AssetDatabase.Assets.Remove(key);
        var magic=Clip("etfx_shoot_magic");var mystic=Clip("etfx_shoot_mystic");
        AssetDatabase.Dependency="verified-purchased-shot-fallback";SivraNovaBuilder.Build();
        Check(Data().novaSmall.castClip==magic&&Data().nova.castClip==mystic,"known purchased magic/mystic clips are selected deliberately when native cast clips are absent");
        AssetDatabase.Assets.Remove(Prefix+"Sound/etfx_shoot_magic.wav");AssetDatabase.Assets.Remove(Prefix+"Sound/etfx_shoot_mystic.wav");
        AssetDatabase.Dependency="no-fabricated-shot-clips";SivraNovaBuilder.Build();
        Check(Data().novaSmall.castClip==null&&Data().nova.castClip==null,"missing purchased cast audio remains null instead of fabricated or relabelled original audio");
        Console.WriteLine($"PASS {checks} Sivra blue Nova Small/Nova source-chain, native-audio, flight, confirmed-hit, cancellation and slow-status contracts. Controlled fixtures; actual package rendering/device acceptance remains separate.");
    }
}
