using System;
using System.Linq;
using System.Reflection;
using Alterverse.Editor;
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
static class Regression
{
    static int checks;
    static Material Valid()=>new(){shader=new Shader{name="DimensionsFront/EpicParticles"},mainTexture=new object()};
    static EpicVfxLibrary Setup()
    {
        var sources=((string key,string path,float scale)[])typeof(EpicToonReplacementBuilder).GetField("Sources",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        var library=new EpicVfxLibrary{sourceVersion=EpicVfxLibrary.CurrentVersion,beamMaterial=Valid(),entries=sources.Select(s=>new EpicVfxLibrary.Entry{key=s.key,prefab=new GameObject()}).ToArray()};
        foreach(var entry in library.entries)entry.prefab.Add<MeshRenderer>().sharedMaterials=new[]{Valid()};
        AssetDatabase.Assets["Assets/Alterverse/Resources/CombatVfx/EpicToon/Library.asset"]=library;
        return library;
    }
    static ParticleSystemRenderer Smoke(EpicVfxLibrary library,bool trails=false)
    {
        var go=library.Find("Smoke").prefab;go.Components.RemoveAll(c=>c is Renderer);
        go.Add<ParticleSystem>().trails.enabled=trails;
        var renderer=go.Add<ParticleSystemRenderer>();renderer.name="SmokeBurstWhiteSoft";
        renderer.sharedMaterials=new[]{Valid(),null};return renderer;
    }
    static void Accept(string reason){EpicToonReplacementBuilder.Validate();checks++;Console.WriteLine("PASS "+reason);}
    static void Reject(string expected,string reason)
    {
        try{EpicToonReplacementBuilder.Validate();throw new Exception("Unexpected acceptance: "+reason);}
        catch(BuildFailedException error){if(!error.Message.Contains(expected))throw;checks++;Console.WriteLine("PASS "+reason);}
    }
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;Console.WriteLine("PASS "+reason);}
    static void Convert(string key,ParticleSystem system)=>typeof(EpicToonReplacementBuilder)
        .GetMethod("PrepareManualEmission",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{key,system});
    static void Orient(string key,GameObject source)=>typeof(EpicToonReplacementBuilder)
        .GetMethod("PreparePortalGeometry",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{key,source});
    static GameObject MeshDisc()
    {
        var source=new GameObject();source.transform.localRotation=Quaternion.Euler(0,0,90);
        var ps=source.Add<ParticleSystem>();ps.main.startRotation3D=true;
        ps.main.startRotationX=MathF.PI*.5f;ps.main.startRotationY=2;ps.main.startRotationZ=3;
        var renderer=source.Add<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Mesh;
        renderer.alignment=ParticleSystemRenderSpace.Local;return source;
    }
    static void OrientationCases()
    {
        foreach(string key in new[]{"Portal","Vortex"})
        {
            var source=MeshDisc();Orient(key,source);var renderer=source.GetComponent<ParticleSystemRenderer>();
            var main=source.GetComponent<ParticleSystem>().main;
            Check(renderer.renderMode==(key=="Portal"?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.HorizontalBillboard)&&
                renderer.alignment==(key=="Portal"?ParticleSystemRenderSpace.View:ParticleSystemRenderSpace.World)&&
                !main.startRotation3D&&main.startRotationX.constantMax==0&&main.startRotationY.constantMax==0&&
                main.startRotationZ.constantMax==3,key+" disc has stable facing and retains authored Z spin");
            var frame=source.transform.localRotation;
            Check(MathF.Abs(frame.z)<.0001f&&MathF.Abs(frame.y)<.0001f&&
                MathF.Abs(frame.x-(key=="Portal"?-MathF.Sqrt(.5f):0))<.0001f,
                key+" emitter plane removes the source's sideways Z frame");
            source=MeshDisc();source.GetComponent<ParticleSystemRenderer>().renderMode=ParticleSystemRenderMode.Billboard;
            Orient(key,source);main=source.GetComponent<ParticleSystem>().main;
            Check(source.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.Billboard&&
                main.startRotation3D&&main.startRotationX.constantMax==MathF.PI*.5f&&main.startRotationZ.constantMax==3,
                key+" authored sparkle billboards are not rewritten as discs");
        }
        var sources=((string key,string path,float scale)[])typeof(EpicToonReplacementBuilder).GetField("Sources",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        foreach(var entry in sources.Where(s=>s.key!="Portal"&&s.key!="Vortex"))
        {
            var source=MeshDisc();Orient(entry.key,source);
            Check(source.GetComponent<ParticleSystemRenderer>().renderMode==ParticleSystemRenderMode.Mesh&&
                source.GetComponent<ParticleSystem>().main.startRotation3D&&MathF.Abs(source.transform.localRotation.z-MathF.Sqrt(.5f))<.0001f,
                entry.key+" authored geometry and orientation unchanged");
        }
    }
    static void EmissionCases()
    {
        // Exact distance-only rates of all eight systems in the two uploaded sources.
        foreach(var item in new[]{("Fire",2f),("Fire",2f),("Fire",6f),("Fire",4f),
            ("ShadowMissile",2f),("ShadowMissile",4f),("ShadowMissile",3f),("ShadowMissile",7f)})
        {
            var ps=new ParticleSystem();ps.emission.rateOverDistance=item.Item2;Convert(item.Item1,ps);
            Check(ps.emission.rateOverTime.constantMax>0&&ps.emission.rateOverTime.constantMax<=48&&
                ps.emission.rateOverDistance.constantMax==0&&ps.emission.burstCount==1&&
                ps.emission.bursts[0].time==0&&ps.emission.bursts[0].minCount==1,
                item.Item1+" distance-only source starts without travel and cannot emit twice with movement");
            Convert(item.Item1,ps);Check(ps.emission.burstCount==1,"reapplying conversion does not stack a birth burst");
        }
        foreach(string key in new[]{"Portal","Vortex"})
        {
            var ps=new ParticleSystem();ps.emission.rateOverTime=2;Convert(key,ps);
            Check(ps.emission.burstCount==1&&ps.emission.bursts[0].time==0&&
                ps.emission.rateOverTime.constantMax==2,key+" slow disc starts at birth without changing steady rate");
            ps=new ParticleSystem();ps.emission.rateOverTime=35;Convert(key,ps);
            Check(ps.emission.burstCount==0,key+" fine sparks retain authored emission");
        }
        var sources=((string key,string path,float scale)[])typeof(EpicToonReplacementBuilder).GetField("Sources",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
        foreach(var source in sources.Where(s=>s.key!="Fire"&&s.key!="ShadowMissile"&&s.key!="Portal"&&s.key!="Vortex"))
        {
            var ps=new ParticleSystem();ps.emission.rateOverTime=2;ps.emission.rateOverDistance=4;Convert(source.key,ps);
            Check(ps.emission.rateOverTime.constantMax==2&&ps.emission.rateOverDistance.constantMax==4&&
                ps.emission.burstCount==0,source.key+" emission untouched");
        }
        var disabled=new ParticleSystem();disabled.emission.enabled=false;disabled.emission.rateOverDistance=6;Convert("Fire",disabled);
        Check(!disabled.emission.enabled&&disabled.emission.burstCount==0&&disabled.emission.rateOverTime.constantMax==0,
            "conversion does not enable authored disabled emission");
        var authored=new ParticleSystem();authored.emission.rateOverDistance=6;
        authored.emission.SetBursts(new[]{new ParticleSystem.Burst(.1f,3)});Convert("Fire",authored);
        Check(authored.emission.burstCount==1&&authored.emission.bursts[0].time==.1f&&authored.emission.bursts[0].minCount==3,
            "authored bursts are retained");
    }
    static void Main()
    {
        Setup();Accept("all converted primary materials");
        foreach(string version in new[]{"2","3"})
        {var stale=Setup();stale.sourceVersion="Epic Toon FX user import; DF replacement "+version;
        Reject("Build the Epic Toon","version "+version+" must rebuild before visibility review");}
        var library=Setup();Smoke(library);Accept("Smoke's unused empty trail slot with trails disabled");
        library=Setup();Smoke(library).sharedMaterials[0]=null;Reject("Missing primary","missing visible primary material still blocks setup");
        library=Setup();Smoke(library,true);Reject("Smoke / SmokeBurstWhiteSoft [1]","enabled trail requires material and identifies slot");
        library=Setup();Smoke(library,true).sharedMaterials[1]=Valid();Accept("enabled converted trail");
        library=Setup();Smoke(library).sharedMaterials=new[]{Valid(),null,null};Reject("[2]","other empty slots are not silently ignored");
        library=Setup();Smoke(library).sharedMaterials[0].shader=new Shader{name="Legacy/Particles"};Reject("[0]","legacy shader remains blocked");
        library=Setup();Smoke(library).sharedMaterials[0].shader=null;Reject("[0]","missing shader remains blocked");
        library=Setup();library.Find("Smoke").prefab.GetComponent<Renderer>().sharedMaterials=new[]{Valid(),null};Reject("[1]","ordinary mesh renderer cannot claim optional particle trail slot");
        library=Setup();library.Find("Smoke").prefab=null;Reject("Missing Epic VFX","missing effect remains blocked");
        EmissionCases();
        OrientationCases();
        Console.WriteLine("PASS "+checks+" production editor checks (controlled Unity adapters; no native prefab import)");
    }
}
