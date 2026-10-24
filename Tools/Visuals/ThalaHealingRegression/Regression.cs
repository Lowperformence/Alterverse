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
    const string Output="Assets/Alterverse/Resources/CombatVfx/ThalaHealing/Library.prefab";
    static int checks;
    static void Check(bool valid,string reason){checks++;if(!valid)throw new Exception(reason);}
    static GameObject Native(string name,Material material)
    {
        var root=new GameObject(name);root.AddComponent<ParticleSystem>();root.AddComponent<ParticleSystemRenderer>().sharedMaterials=new[]{material};
        root.AddComponent<AudioSource>().clip=new AudioClip{name="OriginalRecordingMustRemain"};root.AddComponent<Collider>();root.AddComponent<DemoController>();
        root.SetActive(false);return root;
    }
    static void Reset()=>typeof(ThalaHealingVfx).GetMethod("ResetStatic",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
    static ThalaHealingVfx Data()=>AssetDatabase.LoadAssetAtPath<GameObject>(Output).GetComponent<ThalaHealingVfx>();
    static GameObject Latest(string name,int since)=>GameObject.All.Skip(since).Single(g=>g.name==name);
    static Vector4 Clip(GameObject instance)=>instance.GetComponentsInChildren<Renderer>(true).Single().Block.Vectors["_AttackClip"];
    static void Finish(GameObject instance)
    {
        var live=instance.GetComponent<ThalaHealingVfx>();Time.time+=20f;GameObject.Invoke(live,"LateUpdate");
        Check(instance.GetComponentsInChildren<ParticleSystem>(true).First().StoppedEmission&&!instance.Destroyed,"future emission ends without destroying live native particles");
        foreach(var ps in instance.GetComponentsInChildren<ParticleSystem>(true))ps.Playing=false;
        GameObject.Invoke(live,"LateUpdate");Check(instance.Destroyed,"finished healing composition is cleaned up");
    }
    static void Main()
    {
        var material=new Material(new Shader()){name="NativeHealingGreen"};material.SetColor("_Color",new Color(.2f,1f,.4f,.8f));
        AssetDatabase.Assets["Assets/Epic Toon FX/Materials/HealingGreen.mat"]=material;
        var small=Native("NativeSmallHeal",material);var big=Native("NativeLargeHealNova",material);
        var subObject=new GameObject("NativeDelayedSubEmitter");subObject.transform.SetParent(small.transform,false);
        var sub=subObject.AddComponent<ParticleSystem>();sub.main.playOnAwake=false;
        small.GetComponent<ParticleSystem>().main.duration=100000f;
        small.GetComponent<ParticleSystem>().emission.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
        AssetDatabase.Assets[ThalaHealingBuilder.TargetSource]=small;AssetDatabase.Assets[ThalaHealingBuilder.AreaSource]=big;
        AssetDatabase.Assets["Assets/Epic Toon FX/Prefabs/2D Alternate/HealOnceBurst.prefab"]=Native("Wrong2D",material);
        ThalaHealingBuilder.Build();var data=Data();
        Check(data.target.source=="Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealOnceBurst.prefab","Growth Veil uses exact native small healing burst");
        Check(data.area.source=="Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealNova.prefab","Wildsurge uses exact native large healing nova");
        foreach(var entry in new[]{data.target,data.area})
        {
            Check(!entry.prefab.activeSelf,"prepared healing is inactive before correct positioning");
            Check(entry.nativeRadius==6f,"native horizontal footprint is measured independently of tall particles");
            Check(entry.prefab.GetComponentsInChildren<AudioSource>(true).Length==0,"native source audio cannot replace or duplicate existing sounds");
            Check(entry.prefab.GetComponentsInChildren<Collider>(true).Length==0&&entry.prefab.GetComponentsInChildren<DemoController>(true).Length==0,"native prefab adds no collider or demo gameplay script");
            Check(entry.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.main.maxParticles==321),"native particle budgets retain full source detail");
            Check(entry.prefab.GetComponentsInChildren<ParticleSystem>(true).All(p=>!p.main.loop&&p.main.stopAction==ParticleSystemStopAction.None),"heal is an owned one-shot with natural tail cleanup");
        }
        Check(data.target.emissionSeconds<1,"large burst-only duration does not become a long fake emission window");
        Check(!small.activeSelf&&!big.activeSelf&&small.GetComponent<AudioSource>()!=null,"purchased source prefabs remain unmodified");
        int saves=AssetDatabase.Saves;ThalaHealingBuilder.Build();Check(AssetDatabase.Saves==saves,"same native import does not rebuild repeatedly");
        Resources.Prefab=data.gameObject;Reset();int audio=Alterverse.Audio.DimensionAudio.Calls;
        var patient=new GameObject("HealedAlly");patient.AddComponent<Health>();patient.transform.position=new Vector3(3,0,4);
        Time.time=30;int before=GameObject.All.Count;Check(ThalaHealingVfx.TryTarget(patient.transform),"small healing starts on actual recipient");
        var target=Latest("DF_Thala_GrowthVeil_Heal",before);
        Check(Math.Abs(target.transform.localScale.x-.45f*3.5f/6f)<.0001f,"small healing receives requested +250 percent linear scale");
        Check(Math.Abs(Clip(target).z-1.575f)<.0001f,"small heal retains a distinct compact target footprint");
        var block=target.GetComponentsInChildren<Renderer>(true).Single().Block;
        patient.transform.position=new Vector3(-2,0,7);Time.time+=.15f;GameObject.Invoke(target.GetComponent<ThalaHealingVfx>(),"LateUpdate");
        Check(target.transform.position.x==-2&&target.transform.position.z==7,"single-target heal follows a moving recipient");
        Check(Clip(target).x==-2&&Clip(target).y==7,"clip follows the target instead of cutting the moved heal away");
        Check(ReferenceEquals(block,target.GetComponentsInChildren<Renderer>(true).Single().Block),"moving target reuses its property block");
        Finish(target);
        foreach(float radius in new[]{.5f,2f,5f,8f})
        {
            Time.time+=20;before=GameObject.All.Count;var center=new Vector3(7,.2f,-4);
            Check(ThalaHealingVfx.TryArea(center,radius),"large Wildsurge healing starts");var area=Latest("DF_Thala_Wildsurge_Heal",before);
            Check(Math.Abs(area.transform.localScale.x-radius*3.5f/6f)<.0001f,"large native healing uses +250 percent geometry at live AoE scale");
            var clip=Clip(area);Check(clip.x==7&&clip.y==-4&&clip.z==radius&&clip.w==1,"Wildsurge visual footprint stays within actual ability radius");
            Check(area.GetComponentsInChildren<Transform>(true).Any(t=>t.gameObject.name=="NativeLargeHealNova"),"large native composition is retained");
            Finish(area);
        }
        Time.time+=20;before=GameObject.All.Count;Check(ThalaHealingVfx.TryTarget(patient.transform),"second recipient heal starts independently");
        var interrupted=Latest("DF_Thala_GrowthVeil_Heal",before);patient.SetActive(false);Time.time+=.12f;
        GameObject.Invoke(interrupted.GetComponent<ThalaHealingVfx>(),"LateUpdate");
        Check(interrupted.GetComponentsInChildren<ParticleSystem>(true).First(p=>p.main.playOnAwake).StoppedEmission&&!interrupted.Destroyed,"recipient disappearing stops emission without cutting live particles");
        Check(!interrupted.GetComponentsInChildren<ParticleSystem>(true).First(p=>!p.main.playOnAwake).StoppedEmission,"recipient disappearing preserves non-autoplay native sub-emitters");
        patient.SetActive(true);before=GameObject.All.Count;
        Check(ThalaHealingVfx.TryArea(new Vector3(2,.2f,3),5,10),"delayed network area cue is consumed");
        var late=Latest("DF_Thala_Wildsurge_Heal",before);GameObject.Invoke(late.GetComponent<ThalaHealingVfx>(),"LateUpdate");
        Check(late.Destroyed,"completed late healing does not restart as a new full-bright nova");
        before=GameObject.All.Count;
        Check(!ThalaHealingVfx.TryArea(new(),5,-1)&&!ThalaHealingVfx.TryArea(new(),5,float.NaN)&&!ThalaHealingVfx.TryTarget(patient.transform,float.PositiveInfinity),"invalid replay elapsed time cannot create a new heal");
        Check(GameObject.All.Count==before,"invalid replay time has no visual side effects");
        before=GameObject.All.Count;patient.GetComponent<Health>().IsDead=true;
        ThalaHealingVfx.TryTarget(patient.transform);Check(GameObject.All.Count==before,"dead recipient cannot start a heal effect");
        patient.GetComponent<Health>().IsDead=false;patient.SetActive(false);ThalaHealingVfx.TryTarget(patient.transform);
        Check(GameObject.All.Count==before,"inactive recipient cannot start a heal effect");patient.SetActive(true);
        Check(!ThalaHealingVfx.TryArea(new(),0)&&!ThalaHealingVfx.TryArea(new(),float.NaN)&&!ThalaHealingVfx.TryArea(new(),float.PositiveInfinity),"invalid area sizes cannot create visuals");
        Check(GameObject.All.Count==before,"invalid area sizes have no side effects");
        Check(Alterverse.Audio.DimensionAudio.Calls==audio,"both heals preserve existing audio routing");
        Resources.Prefab=null;Reset();Check(!ThalaHealingVfx.TryTarget(patient.transform)&&!ThalaHealingVfx.TryArea(new(),5),"missing resources retain original target and area fallback");
        AssetDatabase.Assets.Remove(ThalaHealingBuilder.AreaSource);AssetDatabase.Dependency="missing-large";bool missing=false;
        try{ThalaHealingBuilder.Build();}catch(System.IO.FileNotFoundException ex){missing=ex.Message.Contains(ThalaHealingBuilder.AreaSource);}
        Check(missing&&ReferenceEquals(data,Data()),"missing exact large source reports path and cannot replace valid library partially");
        Console.WriteLine($"PASS {checks} Thala healing exact-source/scale/follow/clip/audio/lifecycle checks. Controlled fixtures; native Unity rendering and Android performance remain separate.");
    }
}
