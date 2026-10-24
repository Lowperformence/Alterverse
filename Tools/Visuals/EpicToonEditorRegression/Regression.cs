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
    static void Main()
    {
        Setup();Accept("all converted primary materials");
        var library=Setup();Smoke(library);Accept("Smoke's unused empty trail slot with trails disabled");
        library=Setup();Smoke(library).sharedMaterials[0]=null;Reject("Missing primary","missing visible primary material still blocks setup");
        library=Setup();Smoke(library,true);Reject("Smoke / SmokeBurstWhiteSoft [1]","enabled trail requires material and identifies slot");
        library=Setup();Smoke(library,true).sharedMaterials[1]=Valid();Accept("enabled converted trail");
        library=Setup();Smoke(library).sharedMaterials=new[]{Valid(),null,null};Reject("[2]","other empty slots are not silently ignored");
        library=Setup();Smoke(library).sharedMaterials[0].shader=new Shader{name="Legacy/Particles"};Reject("[0]","legacy shader remains blocked");
        library=Setup();Smoke(library).sharedMaterials[0].shader=null;Reject("[0]","missing shader remains blocked");
        library=Setup();library.Find("Smoke").prefab.GetComponent<Renderer>().sharedMaterials=new[]{Valid(),null};Reject("[1]","ordinary mesh renderer cannot claim optional particle trail slot");
        library=Setup();library.Find("Smoke").prefab=null;Reject("Missing Epic VFX","missing effect remains blocked");
        Console.WriteLine("PASS "+checks+" production editor-validator checks (controlled Unity adapters; no native prefab import)");
    }
}
