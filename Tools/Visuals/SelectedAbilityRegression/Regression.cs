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
    static int checks;static void Check(bool v,string message){checks++;if(!v)throw new Exception(message);}
    static GameObject Native(string path,Material material)
    {var g=new GameObject(System.IO.Path.GetFileNameWithoutExtension(path));g.AddComponent<ParticleSystem>();g.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{material};g.AddComponent<Collider>();g.AddComponent<AudioSource>();g.AddComponent<DemoController>();g.SetActive(false);AssetDatabase.Assets[path]=g;return g;}
    static void Reset()=>typeof(SelectedAbilityVfx).GetMethod("ResetStatic",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static GameObject Last(int kind,int before)=>GameObject.All.Last(g=>!g.Destroyed&&g.activeSelf&&g.name=="DF_Selected_"+kind&&g.GetComponent<SelectedAbilityVfx>()!=null);
    static Vector4 Clip(GameObject g)=>g.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
    static void Finish(GameObject g)
    {Time.time+=20;GameObject.Invoke(g.GetComponent<SelectedAbilityVfx>(),"LateUpdate");foreach(var ps in g.GetComponentsInChildren<ParticleSystem>(true))ps.Playing=false;GameObject.Invoke(g.GetComponent<SelectedAbilityVfx>(),"LateUpdate");Check(g.Destroyed||!g.activeSelf,"completed native composition is released");}
    static void Main()
    {
        var m=new Material(new Shader()){name="NativeDetail"};m.SetColor("_Color",new Color(.3f,.8f,.2f));AssetDatabase.Assets["Assets/Epic Toon FX/Materials/NativeDetail.mat"]=m;
        var paths=new[]{SelectedAbilityVfxBuilder.SmokeSource,"Assets/Epic Toon FX/Prefabs/Combat/Explosions/Spiky/ExplosionSpikyBlood.prefab","Assets/Epic Toon FX/Prefabs/Combat/Lightning/LightningStrikePurple.prefab","Assets/Epic Toon FX/Prefabs/Combat/Magic/Circle Simple/MagicCircleSimpleYellow.prefab",SelectedAbilityVfxBuilder.FireSource,"Assets/Epic Toon FX/Prefabs/Status/StunningCirclingStars.prefab"};
        var sources=paths.Select(p=>Native(p,m)).ToArray();
        sources[0].GetComponent<ParticleSystemRenderer>().sharedMaterials=new Material[]{m,null};Native("Assets/Epic Toon FX/Prefabs/2D Alternate/LightningStrikePurple.prefab",m);
        var sound=new AudioClip{name="NativeStunningSound"};AssetDatabase.Assets["Assets/Epic Toon FX/Sound/etfx_stun.wav"]=sound;sources[5].GetComponent<DemoController>().ObjectReferences["stunningSound"]=sound;
        SelectedAbilityVfxBuilder.Build();var library=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/CombatVfx/SelectedAbilities/Library.prefab").GetComponent<SelectedAbilityVfx>();
        var entries=new[]{library.smoke,library.blood,library.strike,library.circle,library.fire,library.stars};
        for(int i=0;i<6;i++)
        {
            var e=entries[i];Check(e.source==paths[i],"selected native family and colour retained "+i);Check(e.nativeRadius==6,"horizontal footprint ignores tall particle height "+i);
            Check(e.prefab.GetComponentsInChildren<Collider>(true).Length==0&&e.prefab.GetComponentsInChildren<AudioSource>(true).Length==0&&e.prefab.GetComponentsInChildren<DemoController>(true).Length==0,"native demo/audio/physics excluded "+i);
            Check(e.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.main.maxParticles==321),"native particle detail retained "+i);
            Check(!sources[i].activeSelf&&sources[i].GetComponent<Collider>()!=null,"purchased source is unmodified "+i);
        }
        Check(library.smoke.prefab.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials[1]==null,"unused empty native material slot is preserved instead of aborting import");
        var driver=new GameObject("NonRenderingDriver").AddComponent<ParticleSystemRenderer>();driver.renderMode=ParticleSystemRenderMode.None;driver.sharedMaterials=new Material[]{null};
        BomberNukeBuilder.ConvertRendererMaterials(driver,new Shader(),new System.Collections.Generic.Dictionary<Material,Material>(),"Assets/Prepared","Assets/Epic Toon FX/NativeDriver.prefab");
        Check(driver.sharedMaterials[0]==null,"RenderMode.None driver does not require a particle surface");
        var visible=new GameObject("VisibleLayer").AddComponent<ParticleSystemRenderer>();visible.sharedMaterials=new Material[]{null};bool diagnosed=false;
        try{BomberNukeBuilder.ConvertRendererMaterials(visible,new Shader(),new System.Collections.Generic.Dictionary<Material,Material>(),"Assets/Prepared","Assets/Epic Toon FX/RealVisible.prefab");}
        catch(InvalidOperationException ex){diagnosed=ex.Message.Contains("RealVisible.prefab")&&ex.Message.Contains("VisibleLayer")&&ex.Message.Contains("material 0")&&!ex.Message.Contains("Nuke Cone");}
        Check(diagnosed,"genuine missing visible material reports actual source, renderer and slot");
        int saves=AssetDatabase.Saves;SelectedAbilityVfxBuilder.Build();Check(AssetDatabase.Saves==saves,"native preparation does not repeat without changes");
        Resources.Prefab=library.gameObject;Reset();Check(ReferenceEquals(SelectedAbilityVfx.StarsClip(),sound),"full native stunning sound reference retained");
        var actor=new GameObject("Timmy");actor.AddComponent<Health>();var victim=new GameObject("Victim");var hp=victim.AddComponent<Health>();var state=victim.AddComponent<HeroCombatStatus>();state.Rooted=true;
        Time.time=10;int before=GameObject.All.Count;Check(SelectedAbilityVfx.TimmyHold(actor,hp,1.8f),"confirmed hold starts stars");var stars=Last(5,before);
        Check(Math.Abs(stars.transform.localScale.x-.25f*3.5f/6f)<.0001,"stars enlarged by +250 percent");
        int count=GameObject.All.Count;SelectedAbilityVfx.TimmyHold(actor,hp,1.5f);Check(GameObject.All.Count==count,"hold refresh cannot stack another stars effect");
        victim.transform.position=new Vector3(3,0,4);Time.time+=.2f;GameObject.Invoke(stars.GetComponent<SelectedAbilityVfx>(),"LateUpdate");Check(stars.transform.position.x==3&&stars.transform.position.z==4&&stars.transform.position.y>1,"stars follow target overhead");
        SelectedAbilityVfx.TimmyHold(actor,hp,0);Check(stars.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),"confirmed hold end immediately clears status indication");Finish(stars);
        before=GameObject.All.Count;SelectedAbilityVfx.TimmyHold(actor,hp,1);var next=Last(5,before);Check(ReferenceEquals(next,stars)&&GameObject.All.Count==before,"stars reuse the existing composition without allocations");state.Rooted=false;state.Stunned=true;Time.time+=.1f;GameObject.Invoke(next.GetComponent<SelectedAbilityVfx>(),"LateUpdate");Check(next.GetComponentsInChildren<Renderer>(true).All(r=>r.enabled),"stunned network snapshot retains stars during confirmed control");state.Stunned=false;Time.time+=.2f;GameObject.Invoke(next.GetComponent<SelectedAbilityVfx>(),"LateUpdate");Check(next.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),"observed root ending removes stars early");Finish(next);
        before=GameObject.All.Count;Check(SelectedAbilityVfx.TimmyHold(actor,hp,1,2)&&GameObject.All.Count==before,"late expired stars do not restart");
        hp.IsDead=true;SelectedAbilityVfx.TimmyHold(actor,hp,1);Check(GameObject.All.Count==before,"dead target cannot gain stars");hp.IsDead=false;
        foreach(int kind in new[]{SelectedAbilityVfx.Circle,SelectedAbilityVfx.Fire})foreach(float radius in new[]{.5f,3.2f,3.8f,6})
        {
            Time.time+=30;before=GameObject.All.Count;Check(SelectedAbilityVfx.TryArea(kind,new Vector3(2,.2f,4),radius),"selected area starts");var area=Last(kind,before);
            Check(Math.Abs(area.transform.localScale.x-radius*(kind==3?1:3.5f)/6f)<.0001,"ring fits whole area; fire interior uses requested scale");
            Check(Clip(area).z==radius&&Clip(area).x==2&&Clip(area).y==4,"area can never advertise a larger gameplay radius");Finish(area);
        }
        Time.time+=30;before=GameObject.All.Count;SelectedAbilityVfx.JetSmoke(actor,actor.transform,new(),Quaternion.identity,new Vector3(0,0,3),.4f);var smoke=Last(0,before);
        actor.transform.position=new Vector3(0,1,2);Time.time+=.1f;GameObject.Invoke(smoke.GetComponent<SelectedAbilityVfx>(),"LateUpdate");Check(smoke.transform.position.z==2&&smoke.transform.position.y==1,"smoke follows jetpack nozzle");
        Check(Math.Abs(smoke.transform.localScale.x-.16f*3.5f/6f)<.0001,"smoke receives default requested scale");SelectedAbilityVfx.CancelSmoke(actor);
        Check(smoke.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.StoppedEmission),"leap cancellation stops future smoke emission");Finish(smoke);
        Time.time+=30;before=GameObject.All.Count;SelectedAbilityVfx.TryRiftScar(actor,new(),new Vector3(0,0,3),.4f,.45f,42);var strike=Last(2,before);
        Time.time+=.2f;GameObject.Invoke(strike.GetComponent<SelectedAbilityVfx>(),"LateUpdate");Check(Math.Abs(strike.transform.position.z-1.5f)<.001,"Lightning Strike follows original delivery timing");
        Check(Clip(strike).w==2&&Clip(strike).z==.45f,"Lightning Strike is clipped to actual shot width and flat path ends");SelectedAbilityVfx.CancelDelivery(42);Finish(strike);
        SelectedAbilityVfx.RememberEcho(actor,new(),new Vector3(0,0,1),3.5f,70);before=GameObject.All.Count;
        Check(SelectedAbilityVfx.TryEchoImpact(actor,new Vector3(0,0,2)),"confirmed Vacant Echo creates native blood explosion");var blood=Last(1,before);
        Check(Clip(blood).w==3&&Clip(blood).z==3.5f,"blood explosion is bounded to actual release cone");
        var cone=blood.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackCone"];Check(Math.Abs(cone.x-(float)Math.Cos(35*Math.PI/180))<.0001,"blood uses actual half cone angle");Finish(blood);
        // Repeat casts after warm-up without creating/destroying native hierarchies.
        int warmCount=GameObject.All.Count;
        for(int i=0;i<200;i++)
        {
            SelectedAbilityVfx.TryArea(SelectedAbilityVfx.Fire,new Vector3(i,0,2),1+i%4);
            var fx=Last(SelectedAbilityVfx.Fire,0);
            Check(Clip(fx).x==i&&Clip(fx).z==1+i%4,"reused clipping is fresh");Finish(fx);
        }
        Check(GameObject.All.Count==warmCount,"200 sequential casts allocate no new effect hierarchy");
        // Saturation must not steal a running effect or drop a confirmed cast.
        var burst=new System.Collections.Generic.List<GameObject>();
        for(int i=0;i<25;i++){SelectedAbilityVfx.TryArea(SelectedAbilityVfx.Fire,new Vector3(i,0,0),2);burst.Add(Last(SelectedAbilityVfx.Fire,0));}
        Check(burst.Distinct().Count()==25&&burst.All(g=>g.activeSelf),"all 25 simultaneous casts retain independent visuals");
        foreach(var fx in burst)Finish(fx);
        Check(burst.Count(g=>!g.Destroyed)==16,"idle retention remains bounded to 16 per kind");
        int retained=GameObject.All.Count;
        for(int i=0;i<16;i++)SelectedAbilityVfx.TryArea(SelectedAbilityVfx.Fire,new(),2);
        Check(GameObject.All.Count==retained,"next burst uses all retained compositions");
        AssetDatabase.Assets.Remove(paths[1]);AssetDatabase.Assets.Remove(paths[5]);
        string folderBlood="Assets/Epic Toon FX/Prefabs/Combat/Explosions/SpikyBlood/Blood.prefab",folderStars="Assets/Epic Toon FX/Prefabs/Status/Stunning/CirclingStars.prefab";
        Native(folderBlood,m);Native(folderStars,m);AssetDatabase.Dependency="folder-categories";SelectedAbilityVfxBuilder.Build();
        var category=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Alterverse/Resources/CombatVfx/SelectedAbilities/Library.prefab").GetComponent<SelectedAbilityVfx>();
        Check(category.blood.source==folderBlood&&category.stars.source==folderStars,"native families are discoverable when part of their label is a directory name");
        Resources.Prefab=null;Reset();Check(!SelectedAbilityVfx.TryArea(3,new(),3.8f),"missing native library preserves old visible fallback");
        Console.WriteLine($"PASS {checks} selected native ability source/scale/clip/hold/smoke/delivery/lifecycle checks. Controlled fixtures; Unity import, GPU appearance and Android acceptance remain separate.");
    }
}

