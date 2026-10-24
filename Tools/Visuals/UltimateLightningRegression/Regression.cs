using System;
using System.Linq;
using System.Reflection;
using Alterverse.Editor;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

static class Regression
{
    const string Output="Assets/Alterverse/Resources/CombatVfx/UltimateLightning/Library.prefab";
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static GameObject Native(string name,Material material)
    {
        var root=new GameObject(name);root.AddComponent<ParticleSystem>();root.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{material};
        root.AddComponent<AudioSource>().clip=new AudioClip{name="MustNotPlay"};root.AddComponent<Collider>();root.AddComponent<DemoController>();return root;
    }
    static void Reset()=>typeof(UltimateLightningVfx).GetMethod("ResetStatic",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static UltimateLightningVfx Data()=>AssetDatabase.LoadAssetAtPath<GameObject>(Output).GetComponent<UltimateLightningVfx>();
    static void Main()
    {
        var purple=new Material(new Shader()){name="NativeLightningPurple"};purple.SetColor("_Color",new Color(.8f,.15f,1f,.7f));
        var yellow=new Material(new Shader()){name="NativeLightningYellow"};yellow.SetColor("_Color",new Color(1f,.85f,.1f,.8f));
        AssetDatabase.Assets["Assets/Epic Toon FX/Materials/NativeLightningPurple.mat"]=purple;
        AssetDatabase.Assets["Assets/Epic Toon FX/Materials/NativeLightningYellow.mat"]=yellow;
        var sourceIone=Native("NativeNovaLightningPurple",purple);var sourceTraveler=Native("NativeLightningExplosionYellow",yellow);
        sourceIone.SetActive(false);sourceTraveler.SetActive(false);
        var child=new GameObject("DelayedNativeOneShot");child.transform.SetParent(sourceIone.transform,false);
        var sub=child.AddComponent<ParticleSystem>();sub.main.loop=false;sub.main.playOnAwake=false;
        var ps=sourceIone.GetComponent<ParticleSystem>();ps.LongFuture=true;ps.main.duration=100000f;
        ps.emission.SetBursts(new[]{new ParticleSystem.Burst(0f,1)});
        AssetDatabase.Assets[UltimateLightningBuilder.IoneSource]=sourceIone;
        AssetDatabase.Assets[UltimateLightningBuilder.TravelerSource]=sourceTraveler;
        AssetDatabase.Assets["Assets/Epic Toon FX/Prefabs/2D Alternate/NovaLightningPurple.prefab"]=Native("Wrong2D",purple);
        AssetDatabase.Assets["Assets/Epic Toon FX/Prefabs/Combat/Nova/Lightning/NovaLightningYellow.prefab"]=Native("WrongNovaColour",yellow);
        UltimateLightningBuilder.Build();var data=Data();
        Check(!data.gameObject.activeSelf,"prepared resource library remains inactive");
        Check(data.ione.source==UltimateLightningBuilder.IoneSource&&data.ione.source.EndsWith("NovaLightningPurple.prefab"),"Ione uses the selected native purple Lightning Nova");
        Check(data.traveler.source==UltimateLightningBuilder.TravelerSource&&data.traveler.source.EndsWith("LightningExplosionYellow.prefab"),"Traveler uses the selected native yellow Lightning Explosion");
        Check(data.ione.prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name=="NativeNovaLightningPurple"),"complete Ione source composition kept");
        Check(data.traveler.prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name=="NativeLightningExplosionYellow"),"complete Traveler source composition kept");
        foreach(var entry in new[]{data.ione,data.traveler})
        {
            Check(!entry.prefab.activeSelf,"detonation stays inactive until positioned and clipped");
            Check(entry.nativeRadius==6f,"fit uses horizontal radius, not tall native height");
            Check(entry.prefab.GetComponentsInChildren<AudioSource>(true).Length==0,"source AudioSources are excluded");
            Check(entry.prefab.GetComponentsInChildren<Collider>(true).Length==0,"new visual has no gameplay collider");
            Check(entry.prefab.GetComponentsInChildren<DemoController>(true).Length==0,"demo controllers cannot alter gameplay");
            Check(entry.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>!p.main.loop&&p.main.stopAction==ParticleSystemStopAction.None),"native particles become one-shot detonations with owned cleanup");
            Check(entry.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.main.maxParticles==321),"native particle detail count remains intact");
            Check(entry.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.gameObject.activeInHierarchy==false),"saved prefab cannot emit before activation");
        }
        var purpleMaterial=data.ione.prefab.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials.Single();
        var yellowMaterial=data.traveler.prefab.GetComponentsInChildren<Renderer>(true).Single().sharedMaterials.Single();
        Check(purpleMaterial.GetColor("_Tint").r==.8f&&purpleMaterial.GetColor("_Tint").b==1f,"Ione native purple tint retained");
        Check(yellowMaterial.GetColor("_Tint").r==1f&&yellowMaterial.GetColor("_Tint").g==.85f,"Traveler native yellow tint retained");
        Check(data.ione.emissionSeconds<1f,"long burst-only scheduler does not prevent preparation or require a 60-second wait");
        Check(!sourceIone.activeSelf&&!sourceTraveler.activeSelf&&sourceIone.GetComponent<AudioSource>()!=null,"purchased source objects remain untouched");
        int saves=AssetDatabase.Saves;UltimateLightningBuilder.Build();Check(AssetDatabase.Saves==saves,"unchanged source import is idempotent");
        Resources.Prefab=data.gameObject;Reset();int sounds=Alterverse.Audio.DimensionAudio.Calls;
        foreach(int kind in new[]{UltimateLightningVfx.Ione,UltimateLightningVfx.Traveler})
            foreach(float radius in new[]{.5f,2f,3.24f,6f,8f})
            {
                Time.time+=10f;float time=Time.time;int before=GameObject.All.Count;Vector3 center=new(4,.2f,-9);
                Check(UltimateLightningVfx.TryPlay(kind,center,radius),"selected ultimate detonation starts");
                string name=kind==UltimateLightningVfx.Ione?"DF_Ione_LightningNova":"DF_Traveler_LightningExplosion";
                var instances=GameObject.All.Skip(before).Where(g=>g.name==name).ToArray();
                Check(instances.Length==1,"one Area cue creates one central detonation");var instance=instances.Single();
                Check(instance.transform.position.x==center.x&&instance.transform.position.y==.2f&&instance.transform.position.z==center.z,"supplied ground center is preserved without a new height offset");
                Check(Math.Abs(instance.transform.localScale.x-radius*2f/6f)<.0001f,"requested doubled visual composition uses the actual area radius as its basis");
                var clip=instance.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
                Check(clip.x==center.x&&clip.y==center.z&&clip.z==radius&&clip.w==1f,"shader footprint matches the actual circular area");
                Check(instance.GetComponentsInChildren<AudioSource>(true).Length==0&&Alterverse.Audio.DimensionAudio.Calls==sounds,"detonation neither duplicates nor replaces current sounds");
                var live=instance.GetComponent<UltimateLightningVfx>();Time.time=time+10f;GameObject.Invoke(live,"LateUpdate");
                var rootPs=instance.GetComponentsInChildren<ParticleSystem>(true).First(p=>p.main.playOnAwake);
                Check(rootPs.StoppedEmission&&!instance.Destroyed,"stopping future auto-emission does not truncate living smoke");
                var delayed=instance.GetComponentsInChildren<ParticleSystem>(true).FirstOrDefault(p=>!p.main.playOnAwake);
                if(delayed!=null)Check(!delayed.StoppedEmission,"late native sub-emitter is not stopped before its trigger");
                foreach(var system in instance.GetComponentsInChildren<ParticleSystem>(true))system.Playing=false;
                GameObject.Invoke(live,"LateUpdate");Check(instance.Destroyed,"finished native detonation is cleaned up");
            }
        int invalid=GameObject.All.Count;
        Check(!UltimateLightningVfx.TryPlay(-1,new(),2)&&!UltimateLightningVfx.TryPlay(2,new(),2),"unselected kinds cannot create a detonation");
        Check(!UltimateLightningVfx.TryPlay(0,new(),0)&&!UltimateLightningVfx.TryPlay(0,new(),-1)&&!UltimateLightningVfx.TryPlay(0,new(),float.NaN)&&!UltimateLightningVfx.TryPlay(0,new(),float.PositiveInfinity),"invalid area radii are rejected");
        Check(!UltimateLightningVfx.TryPlay(0,new(),2,-1)&&!UltimateLightningVfx.TryPlay(0,new(),2,float.NaN),"invalid network elapsed values are rejected");
        Check(GameObject.All.Count==invalid,"invalid inputs have no visual side effects");
        Check(typeof(UltimateLightningVfx).GetMethod("AudioFor")==null,"new helper has no audio replacement API");
        Resources.Prefab=null;Reset();Check(!UltimateLightningVfx.TryPlay(0,new(),3),"missing prepared resource allows the existing visual fallback");
        AssetDatabase.Assets.Remove(UltimateLightningBuilder.IoneSource);bool missing=false;AssetDatabase.Dependency="missing-original-purple";
        try{UltimateLightningBuilder.Build();}catch(System.IO.FileNotFoundException exception){missing=exception.Message.Contains(UltimateLightningBuilder.IoneSource);}
        Check(missing,"missing exact purple source is reported instead of silently choosing another colour or 2D variant");
        Check(ReferenceEquals(data,Data()),"failed import cannot replace a valid library with partial content");
        Console.WriteLine($"PASS {checks} ultimate lightning exact-source/radius/clip/audio-exclusion/lifecycle contracts. Controlled fixtures; native package rendering and device acceptance remain separate.");
    }
}
