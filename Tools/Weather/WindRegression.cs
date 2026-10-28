using System;using System.Linq;using System.Reflection;using UnityEngine;using Alterverse.Visuals;using Alterverse.Match;using Alterverse.Performance;using Alterverse.Multiplayer;
static class WindRegression
{
 static int checks;
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
 static void Update(WindVfxRuntime target)=>typeof(WindVfxRuntime).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
 static void Main()
 {
  foreach(ArenaVariant map in Enum.GetValues(typeof(ArenaVariant)))Check(WindVfxRuntime.Speed(map)>=.55f&&WindVfxRuntime.Speed(map)<=1.5f,"Positive bounded wind on every map: "+map);
  Check(WindVfxRuntime.UseLeaves(ArenaVariant.Earth)&&WindVfxRuntime.UseLeaves(ArenaVariant.GreenPlanet),"Vegetated maps select leaves");
  Check(!WindVfxRuntime.UseLeaves(ArenaVariant.Pyraxis)&&!WindVfxRuntime.UseLeaves(ArenaVariant.Moon),"Barren maps select dust instead of leaves");
  var catalog=new PlanetWindCatalog{flows=new GameObject("Flow prefab"),dust=new GameObject("Dust prefab"),leaves=new GameObject("Leaf prefab")};Resources.Assets["PlanetWindCatalog"]=catalog;
  int copies=0;
  UnityEngine.Object.InstantiateHook=(prefab,parent)=>
  {
   copies++;var instance=new GameObject(prefab.name+" copy");instance.transform.SetParent(parent,false);instance.AddComponent<ParticleSystem>();
   var child=new GameObject("Visible particles");child.transform.SetParent(instance.transform,false);child.AddComponent<ParticleSystem>();child.GetComponent<ParticleSystemRenderer>().sharedMaterial=new Material(new Shader());return instance;
  };
  var world=new GameObject("DF_MatchWorld");var camera=new GameObject("Camera").AddComponent<UnityEngine.Camera>();UnityEngine.Camera.main=camera;camera.transform.position=new Vector3(0,12,-9);camera.transform.forward=new Vector3(0,-.8f,.6f);
  ArenaVariantRuntime.Current=ArenaVariant.Moon;var runtime=new GameObject("Wind controller").AddComponent<WindVfxRuntime>();Update(runtime);
  Check(Debug.Warnings.Count==0&&copies==2,"Original flow and dust prefabs instantiate on the Moon: copies="+copies+" "+string.Join(";",Debug.Warnings));
  var root=GameObject.Find("DF_AuthoredWind_Moon");var particles=root.GetComponentsInChildren<ParticleSystem>(true);
  var visible=particles.Where(p=>p.GetComponent<ParticleSystemRenderer>()?.sharedMaterial!=null).ToArray();
  Check(particles.Length==4&&visible.Length==2,"Hidden vendor container systems are distinguished from rendered layers");
  Check(particles.Except(visible).All(p=>!p.emission.enabled&&!p.isPlaying),"Hidden prefab containers never emit");
  Check(visible.All(p=>p.main.maxParticles<=36&&p.shape.scale.y<=.65f&&!p.collision.enabled&&!p.trigger.enabled&&!p.subEmitters.enabled),"Visible weather is bounded, near ground and has no particle physics callbacks");
  Check(visible[0].main.startColor.a<=.24f&&visible[0].main.useUnscaledTime==false,"Flow opacity is limited and pauses with gameplay");
  int loads=Resources.Loads;camera.transform.position=new Vector3(10,12,-9);Time.unscaledTime=1;Update(runtime);
  Check(root.transform.position.x==10&&Resources.Loads==loads&&copies==2,"Wind follows camera without loading or cloning during play");
  AdaptiveVfxBudget.AllowDecorative=false;Time.unscaledTime=2;Update(runtime);
  Check(visible[0].isPlaying&&visible[0].main.maxParticles==8&&visible[1].isStopped,"Slow device retains sparse flow while debris is cleared");
  MatchClock.InBackground=true;Update(runtime);Check(!root.activeSelf,"Background hides authored wind");MatchClock.InBackground=false;
  DedicatedServerNetworkRuntime.Requested=true;Update(runtime);Check(!runtime.enabled&&root.destroyed,"Server mode tears down client wind");
  Console.WriteLine($"PASS: {checks} authored wind policy, catalog, prefab configuration, budget and lifecycle checks (managed fixtures).");
 }
}
