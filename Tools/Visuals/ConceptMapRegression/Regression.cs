using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Alterverse.Match;
using Alterverse.Visuals;
using UO=UnityEngine.Object;
static class Regression {
 static int checks;
 static void Require(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
 static string Fingerprint()=>string.Join(";",SceneryBuildingClearance.Probes.Select(p=>{
  var m=p.Placement.Native;return $"{m.M11:R},{m.M12:R},{m.M13:R},{m.M21:R},{m.M22:R},{m.M23:R},{m.M31:R},{m.M32:R},{m.M33:R},{m.M41:R},{m.M42:R},{m.M43:R}";
 }));
 static PlanetLandscapeDetail Build(ArenaVariant variant,bool reject=false){
  UO.All.Clear();SceneryBuildingClearance.Probes.Clear();SceneryBuildingClearance.RejectAll=reject;
  ArenaVariantRuntime.Current=variant;var builder=new GameObject("World").AddComponent<PlanetLandscapeDetail>();builder.Build();return builder;
 }
 static void Tick(PlanetArenaThemeRuntime runtime)=>typeof(PlanetArenaThemeRuntime).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(runtime,null);
 static void Teardown(PlanetArenaThemeRuntime runtime)=>typeof(PlanetArenaThemeRuntime).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(runtime,null);
 static PlanetArenaThemeRuntime Theme(ArenaVariant variant){
  UO.All.Clear();Debug.Errors.Clear();SceneryBuildingClearance.RejectAll=false;
  RuntimeShaderLibrary.FailAfter=-1;RuntimeShaderLibrary.Creates=0;
  ArenaVariantRuntime.Current=variant;new GameObject("DF_MatchWorld");
  var runtime=new GameObject("ThemeHost").AddComponent<PlanetArenaThemeRuntime>();Tick(runtime);return runtime;
 }
 static void CheckThemes(){
  foreach(var variant in new[]{ArenaVariant.Pyraxis,ArenaVariant.Neptune}){
   var runtime=Theme(variant);
   Require(Debug.Errors.Count==0,$"{variant}: theme builds without fallback");
   var meshes=UO.All.OfType<Mesh>().Where(m=>m!=null).ToArray();
   Require(meshes.Length==(variant==ArenaVariant.Pyraxis?2:1),$"{variant}: bounded reusable mesh ownership");
   Require(!UO.All.OfType<Collider>().Any(c=>c!=null),$"{variant}: no live primitive colliders");
   var parts=UO.All.OfType<GameObject>().Where(g=>g!=null&&(g.name=="BasaltMass"||g.name=="BasaltSpire"||g.name=="CoralBranch")).ToArray();
   Require(parts.Length==(variant==ArenaVariant.Pyraxis?12:24),$"{variant}: expected shaped prop count");
   Require(parts.All(g=>g.GetComponent<MeshFilter>()?.sharedMesh!=null&&g.GetComponent<MeshRenderer>()?.sharedMaterial!=null),$"{variant}: every shaped prop has geometry and material");
   Require(parts.Select(g=>g.GetComponent<MeshFilter>().sharedMesh).Distinct().Count()==meshes.Length,$"{variant}: props share regional source geometry");
   if(variant==ArenaVariant.Pyraxis)
    Require(meshes.All(m=>m.vertices.All(v=>MathF.Abs(v.x)<=.50001f&&MathF.Abs(v.y)<=.50001f&&MathF.Abs(v.z)<=.50001f)),"Fractured basalt retains original primitive bounds");
   if(variant==ArenaVariant.Neptune){
    foreach(var branch in parts){
     var siblings=UO.All.OfType<GameObject>().Where(g=>g!=null&&g.name=="BioTip"&&ReferenceEquals(g.transform.Parent,branch.transform.Parent)).ToArray();
     var expected=branch.transform.localPosition+branch.transform.localRotation*(Vector3.up*branch.transform.localScale.y);
     Require(siblings.Any(t=>(t.transform.localPosition-expected).magnitude<.00001f),"Coral glow follows the actual tilted endpoint");
    }
   }
   int before=parts.Length;Tick(runtime);
   Require(UO.All.OfType<GameObject>().Count(g=>g!=null&&(g.name=="BasaltMass"||g.name=="BasaltSpire"||g.name=="CoralBranch"))==before,$"{variant}: no rebuild on repeated tick");
   var materials=UO.All.OfType<Material>().Where(m=>m!=null).ToArray();
   ArenaVariantRuntime.Current=ArenaVariant.Moon;Tick(runtime);
   Require(meshes.All(m=>m.Destroyed)&&materials.All(m=>m.Destroyed),$"{variant}: variant exit releases mesh/material ownership");
   Require(parts.All(g=>g.Destroyed),$"{variant}: variant exit removes all shaped props");
   Teardown(runtime);
  }
  // Force material setup to fail after a partial build; optional dressing must clean up
  // and avoid allocating/retrying every Update for the same failed arena generation.
  var failed=Theme(ArenaVariant.Moon);
  ArenaVariantRuntime.Current=ArenaVariant.Pyraxis;RuntimeShaderLibrary.FailAfter=2;Tick(failed);
  Require(Debug.Errors.Count==2,"Optional theme failure is reported once with its exception");
  Require(UO.All.OfType<Mesh>().All(m=>m.Destroyed)&&UO.All.OfType<Material>().All(m=>m.Destroyed),"Partial failed theme releases mesh/material resources");
  Require(!UO.All.OfType<GameObject>().Any(g=>g!=null&&g.name=="DF_PlanetTheme_Pyraxis"),"Partial failed theme root is removed");
  int objects=UO.All.Count;Tick(failed);Require(UO.All.Count==objects,"Failed generation does not allocate repeatedly");
  UO.Destroy(GameObject.Find("DF_MatchWorld"));Tick(failed);
  RuntimeShaderLibrary.FailAfter=-1;new GameObject("DF_MatchWorld");Tick(failed);
  Require(UO.All.OfType<GameObject>().Count(g=>g!=null&&g.name=="DF_PlanetTheme_Pyraxis")==1,"New world can recover after failed optional theme");
  var liveMeshes=UO.All.OfType<Mesh>().Where(m=>m!=null).ToArray();var liveMaterials=UO.All.OfType<Material>().Where(m=>m!=null).ToArray();
  Teardown(failed);
  Require(liveMeshes.All(m=>m.Destroyed)&&liveMaterials.All(m=>m.Destroyed),"Runtime destruction releases recovered ownership");
  var removed=Theme(ArenaVariant.Neptune);
  liveMeshes=UO.All.OfType<Mesh>().Where(m=>m!=null).ToArray();liveMaterials=UO.All.OfType<Material>().Where(m=>m!=null).ToArray();
  UO.Destroy(GameObject.Find("DF_MatchWorld"));Tick(removed);
  Require(liveMeshes.All(m=>m.Destroyed)&&liveMaterials.All(m=>m.Destroyed),"World destruction releases owned theme resources");
 }
 static int Main(){
  var cases=new List<string>();
  foreach(ArenaVariant variant in Enum.GetValues<ArenaVariant>()){
   var builder=Build(variant);
   var renderers=UO.All.OfType<MeshRenderer>().ToArray();var lods=UO.All.OfType<LODGroup>().ToArray();
   Require(lods.Length==8,$"{variant}: exactly eight regional pockets");
   Require(renderers.Length<=32&&renderers.Length>=8,$"{variant}: bounded existing batch count");
   Require(renderers.All(r=>r.receiveShadows&&r.sharedMaterial!=null),$"{variant}: valid shadow-receiving materials");
   int count=SceneryBuildingClearance.Probes.Count;
   Require(count>200&&count<1100,$"{variant}: bounded richer detail count ({count})");
   Require(count==UO.All.OfType<Mesh>().Where(m=>!m.Destroyed).Sum(m=>m.Instances.Count),$"{variant}: every cleared placement is batched");
   Require(SceneryBuildingClearance.Probes.All(p=>{
    var m=p.Placement.Native;return float.IsFinite(m.M41)&&float.IsFinite(m.M42)&&float.IsFinite(m.M43)&&MathF.Abs(m.M41)<35&&MathF.Abs(m.M43)<=35;
   }),$"{variant}: placement stays in peripheral pockets");
   int[] batchSizes=UO.All.OfType<Mesh>().Where(m=>!m.Destroyed).Select(m=>m.Instances.Count).ToArray();
   Require(UO.All.OfType<Mesh>().Where(m=>!m.Destroyed).All(m=>m.vertexCount<65535),$"{variant}: combined fixture geometry fits 16-bit index budget");
   int probesBefore=count;builder.Build();
   Require(SceneryBuildingClearance.Probes.Count==probesBefore,$"{variant}: repeated Build does not duplicate props");
   string expected=Fingerprint();
   var liveMeshes=UO.All.OfType<Mesh>().Where(m=>!m.Destroyed).ToArray();var materials=UO.All.OfType<Material>().ToArray();
   typeof(PlanetLandscapeDetail).GetMethod("OnDestroy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(builder,null);
   Require(liveMeshes.All(m=>m.Destroyed)&&materials.All(m=>m.Destroyed),$"{variant}: resources released on teardown");
   Build(variant);Require(Fingerprint()==expected,$"{variant}: deterministic recreation");
   Build(variant,true);
   Require(!UO.All.OfType<MeshRenderer>().Any(),$"{variant}: all-blocked clearance emits no geometry");
   Require(SceneryBuildingClearance.Probes.Count==count,$"{variant}: clearance evaluates every candidate");
   cases.Add($"{variant}:{count} placements,{renderers.Length} batches,max {batchSizes.Max()} placements/batch");
  }
  Require(PlanetLandscapeDetail.DetailSites.Take(4).Zip(PlanetLandscapeDetail.DetailSites.Skip(4)).All(p=>p.First.x==-p.Second.x&&p.First.y==-p.Second.y),"Symmetric team pocket centers");
  CheckThemes();
  Console.WriteLine($"{checks} checks passed across {cases.Count} map variants and theme lifecycles. Production landscape and theme C# against engine substitutes; native Unity visuals/performance not tested.");
  cases.ForEach(Console.WriteLine);return 0;
 }
}
