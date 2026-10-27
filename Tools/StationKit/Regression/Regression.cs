using System;using System.Linq;using System.Reflection;using System.IO;using UnityEngine;using Alterverse.Visuals;using Alterverse.Combat;using Alterverse.Multiplayer;
static class Regression {
 static int checks;static void Check(bool c,string m){if(!c)throw new Exception(m);checks++;}
 static void Call(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
 static Transform Child(Transform p,string name){var g=new GameObject(name);g.transform.SetParent(p,false);return g.transform;}
 static Renderer Legacy(Transform p){var g=Child(p,"LegacyMesh").gameObject;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=new Material{Emission=true};return r;}
 static void Main(string[] args){
  Resources.Source=File.ReadAllBytes(args[0]);
  var world=new GameObject("DF_MatchWorld");var controller=new GameObject("Controller").AddComponent<StationBuildingRuntime>();
  var bunker=Child(world.transform,"Bunker").gameObject;bunker.AddComponent<DefenseBunker>();
  var old=Child(bunker.transform,"DF_DefenseBunkerVisual");var legacy=Legacy(old);var pivot=Child(old,"DF_Bunker_TurretHead");pivot.localPosition=new(0,2.38f,.22f);var muzzle=Child(pivot,"DF_Bunker_Muzzle");muzzle.localPosition=new(0,.17f,1.16f);
  var central=Child(world.transform,"Pickup").gameObject;central.transform.localPosition=new(0,.12f,0);central.AddComponent<CentralHealingPickup>();var oldPickup=Child(central.transform,"DF_HealStation");var pickupRenderer=Legacy(oldPickup);var plus=Child(oldPickup,"DF_HealingPlus");plus.localPosition=new(0,1.22f,0);plus.localScale=new(1.1f,1.1f,1.1f);
  var core=Child(world.transform,"Core").gameObject;core.AddComponent<RealityCoreShell>();core.AddComponent<ProductionBaseEnvironment>();core.AddComponent<TeamMember>().Team=1;
  var coreRenderer=Legacy(Child(core.transform,"DF_RealityCoreProduction"));var deckRenderer=Legacy(Child(core.transform,"DF_LumenProductionBase"));
  var pad=Child(world.transform,"Pad").gameObject;pad.transform.localPosition=new(0,.14f,0);pad.AddComponent<HealingZoneMarker>();var padRenderer=Legacy(Child(pad.transform,"DF_SpawnHealStationProduction"));
  var trench=Child(world.transform,"Trench").gameObject;trench.AddComponent<DefenseTrench>();var trenchRenderer=Legacy(Child(trench.transform,"DF_DefenseTrenchVisual"));int trenchComponents=trench.components.Count;
  int components=bunker.components.Count;Call(controller,"Update");
  Check(Debug.Warnings.Count==0,"Real Blender mesh library loads");
  Check(!legacy.enabled&&!pickupRenderer.enabled,"Old visual renderers are hidden after replacement succeeds");
  Check(!coreRenderer.enabled&&!deckRenderer.enabled&&!padRenderer.enabled&&!trenchRenderer.enabled,"Every requested structure type receives its new geometry");
  Check(core.transform.Find("DF_StationKit_core").gameObject.GetComponentsInChildren<MeshRenderer>().Any(r=>ReferenceEquals(r.sharedMaterial,coreRenderer.sharedMaterial)),"Core state writer still owns the visible emission material");
  Check(Math.Abs(pad.transform.Find("DF_StationKit_healpad").position.y-.015f)<.0001f,"Healing plate is aligned to actual ground rather than its elevated logical owner");
  Check(trench.components.Count==trenchComponents,"Decorative trench tower adds no attack or gameplay component");
  Check(bunker.components.Count==components,"Gameplay owner receives no new components");
  Check(pivot.localPosition.y==2.38f&&muzzle.localPosition.z==1.16f,"Existing pivot and muzzle stay unchanged");
  Check(pivot.gameObject.GetComponentsInChildren<MeshRenderer>().Length>0,"New turret meshes follow the original aim pivot");
  int count=GameObject.All.Count,loads=Resources.Loads,meshes=Mesh.Created;Time.unscaledTime=1;Call(controller,"Update");
  Check(GameObject.All.Count==count&&Resources.Loads==loads&&Mesh.Created==meshes,"Repeated scans neither duplicate models nor reload geometry");
  var newPickup=central.transform.Find("DF_StationKit_healnode");var owner=newPickup.GetComponent<StationBuildingVisual>();var symbol=plus.Find("DF_StationKit_Availability");
  Check(Math.Abs(symbol.position.y-.015f)<.0001f,"Pickup symbol stays at terrain surface");
  plus.localPosition=new(0,1.30f,0);plus.localScale=new(.9f,.9f,.9f);Call(owner,"LateUpdate");
  Check(Math.Abs(symbol.position.y-.015f)<.0001f&&Math.Abs(symbol.lossyScale.x-1)<.0001f,"Existing hover/pulse cannot lift the flat symbol or change its footprint");
  Check(plus.localPosition.y==1.30f&&plus.localScale.x==.9f,"Adapter never writes original animation or gameplay source");
  plus.gameObject.SetActive(false);Check(!symbol.gameObject.activeInHierarchy,"Collection hides the new symbol through the original availability owner");
  plus.gameObject.SetActive(true);Check(symbol.gameObject.activeInHierarchy,"Respawn restores the new symbol through the same owner");
  DedicatedServerNetworkRuntime.Requested=true;var server=new GameObject("ServerController").AddComponent<StationBuildingRuntime>();count=GameObject.All.Count;Call(server,"Update");
  Check(!server.enabled&&GameObject.All.Count==count,"Dedicated server does no visual work");DedicatedServerNetworkRuntime.Requested=false;
  Call(owner,"OnDestroy");Check(pickupRenderer.enabled&&symbol.gameObject.destroyed,"Teardown releases mounted visuals and restores original rendering");
  Console.WriteLine($"PASS: {checks} Station Kit lifecycle/mount/availability checks (managed engine fixtures).");
 }
}
