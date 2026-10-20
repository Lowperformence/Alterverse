using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Alterverse.Visuals;
using Alterverse.Characters;
class VisualRegression
{
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static void Call(object o,string name)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,null);
 static void Main()
 {
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))foreach(bool preview in new[]{false,true})foreach(bool rgb in new[]{false,true})
  {
   Time.time=Time.unscaledTime=0;
   var owner=new GameObject();var visual=new GameObject();var p=owner.AddComponent<VoidWingPresentation>();
   if(rgb)p.BuildRgbRift(visual.transform,preview,hero);else p.BuildAngel(visual.transform,preview,hero);
   Check(visual.transform.Children.Count==3,"two wing sides and static mount");
   var renderers=visual.transform.Children.Select(t=>t.gameObject.GetComponent<MeshRenderer>()).ToArray();
   var meshes=visual.transform.Children.Select(t=>t.gameObject.GetComponent<MeshFilter>().sharedMesh).Distinct().ToArray();
   Check(meshes.Length==2,"left and right share mesh");
   foreach(var mesh in meshes)
   {
    Check(mesh.vertices.Length<65536&&mesh.vertices.Length>0,"mobile 16-bit mesh budget");
    foreach(var v in mesh.vertices)Check(float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z),"finite model coordinates");
    foreach(var indices in mesh.Triangles.Values)foreach(int index in indices)Check(index>=0&&index<mesh.vertices.Length,"valid indices");
    Check(mesh.colors.Length==mesh.vertices.Length,"vertex spectrum covers whole mesh");
   }
   Check(visual.transform.Children[0].localScale.x<0&&visual.transform.Children[1].localScale.x>0,"mirrored pair");
   Check(visual.transform.Children.All(t=>t.gameObject.GetComponent<Collider>()==null),"cosmetics add no colliders");
   var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
   Check(materials.Length==3,"three owned material slots");
   Check(renderers.All(r=>!r.receiveShadows),"no per-cosmetic shadow reception");
   var face=materials.Single(m=>m.name.Contains(rgb?"Gunmetal":"Ivory"));
   if(rgb)Check(!face.shader.name.Contains("RGB"),"anthracite metal does not cycle with lights");
   if(rgb)p.BuildRgbRift(visual.transform,preview,hero);else p.BuildAngel(visual.transform,preview,hero);
   Check(visual.transform.Children.Count==3,"repeated build cannot duplicate model");
   Time.time=Time.unscaledTime=3;Call(p,"LateUpdate");
   if(rgb)
   {
    var edge=materials.Single(m=>m.name.Contains("Spectrum"));Check(Math.Abs(edge.Floats["_HueShift"]-.5f)<.001f,"six-second hue cycle");
    Time.time=Time.unscaledTime=6;Call(p,"LateUpdate");Check(edge.Floats["_HueShift"]==0,"cycle returns seamlessly");
   }
   Call(p,"OnDestroy");
   Check(meshes.All(m=>m.Destroyed)&&materials.All(m=>m.Destroyed),"runtime meshes and materials released");
  }
  foreach(HeroId hero in Enum.GetValues(typeof(HeroId)))foreach(bool preview in new[]{false,true})
  {
   var owner=new GameObject();var visual=new GameObject();var p=owner.AddComponent<VoidWingPresentation>();
   p.BuildDeluxe(visual.transform,preview,hero);
   Check(visual.transform.Children.Count==2,"Deluxe paired wings");
   var renderers=visual.transform.Children.Select(t=>t.gameObject.GetComponent<MeshRenderer>()).ToArray();
   var materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
   Check(materials.Length==3,"Deluxe shared silver, crystal and white energy materials");
   var energy=materials.Single(m=>m.name=="DF_DeluxeWhiteFractures");
   Check(energy.shader.name==RuntimeShaderLibrary.Unlit.name,"Deluxe white elements are independent of scene light");
   var white=energy.Colors["_BaseColor"];
   Check(white.r==1&&white.g==1&&white.b==1,"Deluxe base is neutral white rather than cyan");
   var mesh=visual.transform.Children[0].gameObject.GetComponent<MeshFilter>().sharedMesh;
   Check(mesh.Triangles[2].Length>0&&mesh.Triangles[0].Length>0&&mesh.Triangles[1].Length>0,"white elements coexist with both non-emissive surfaces");
   foreach(float time in new[]{0f,.7f,3f,6f})
   {
    Time.time=Time.unscaledTime=time;Call(p,"LateUpdate");
    var emission=energy.Colors["_EmissionColor"];
    Check(emission.r>1&&emission.r==emission.g&&emission.g==emission.b,"animated emission stays luminous neutral white");
   }
   p.BuildDeluxe(visual.transform,preview,hero);
   Check(visual.transform.Children.Count==2,"Deluxe rebuild cannot duplicate wings");
   Call(p,"OnDestroy");
   Check(mesh.Destroyed&&materials.All(m=>m.Destroyed),"Deluxe resources released");
  }
  Console.WriteLine("PASS "+checks+" production wing construction/index/material/cycle/cleanup assertions for all ten heroes, game and preview, including white Deluxe emission. GPU/native transforms remain fixtures.");
 }
}

