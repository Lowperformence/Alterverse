// Snapshot the real production geometry and material intent, instead of redrawing the old models.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using Alterverse.Visuals;
using Alterverse.Characters;
class LegacyIconExport
{
 static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
 static float[] C(Color c)=>new[]{c.r,c.g,c.b};
 static object Material(Material m)=>new {name=m.name,color=C(m.Colors.GetValueOrDefault("_BaseColor",Color.white)),
  emission=C(m.Colors.GetValueOrDefault("_EmissionColor",new Color(0,0,0))),
  metallic=m.Floats.GetValueOrDefault("_Metallic",0),smoothness=m.Floats.GetValueOrDefault("_Smoothness",.5f)};
 static object Node(Transform t)
 {
  var mesh=t.gameObject.GetComponent<MeshFilter>()?.sharedMesh;
  var renderer=t.gameObject.GetComponent<MeshRenderer>();
  var line=t.gameObject.GetComponent<LineRenderer>();
  return new {name=t.gameObject.name,position=V(t.localPosition),scale=V(t.localScale),
   vertices=mesh?.vertices.Select(V).ToArray(),triangles=mesh?.Triangles.OrderBy(x=>x.Key).Select(x=>x.Value).ToArray(),
   materials=renderer==null?null:(renderer.sharedMaterials??new[]{renderer.sharedMaterial}).Select(Material).ToArray(),
   sphere=t.gameObject.name=="DF_PlagueToxinNode",
   line=line==null?null:new {points=line.Positions.OrderBy(x=>x.Key).Select(x=>V(x.Value)).ToArray(),width=line.widthMultiplier,material=Material(line.sharedMaterial)},
   children=t.Children.Select(Node).ToArray()};
 }
 static void Main(string[] args)
 {
  var designs=new Dictionary<string,object>();
  foreach(string key in new[]{"Voidfall","Plague","BetaSupporter","Deluxe"})
  {
   var owner=new GameObject(key);var visual=new GameObject(key);var p=owner.AddComponent<VoidWingPresentation>();
   switch(key){case "Voidfall":p.Build(visual.transform,true);break;case "Plague":p.BuildPlague(visual.transform,true,HeroId.Veyr);break;case "BetaSupporter":p.BuildBetaSupporter(visual.transform,true,HeroId.Veyr);break;default:p.BuildDeluxe(visual.transform,true,HeroId.Veyr);break;}
   if(visual.transform.Children.Count!=2)throw new Exception("Legacy builder must create both sides: "+key);
   designs[key]=visual.transform.Children.Select(Node).ToArray();
  }
  File.WriteAllText(args[0],JsonSerializer.Serialize(designs));
  Console.WriteLine("Exported four unchanged production wing models to "+args[0]);
 }
}
