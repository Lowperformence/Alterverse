using System;
using System.Linq;
using System.Reflection;
using Alterverse.Match;
using Alterverse.Multiplayer;
using Alterverse.Performance;
using Alterverse.Visuals;
using UnityEngine;
static class Regression
{
 static int checks;
 static void Check(bool condition,string reason){if(!condition)throw new Exception(reason);checks++;}
 static void Call(object instance,string name)=>typeof(PlanetWeatherRuntime).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Invoke(instance,null);
 static void Main()
 {
  foreach(ArenaVariant arena in Enum.GetValues(typeof(ArenaVariant)))
  {
   var profile=PlanetWeatherRuntime.For(arena);
   Check(profile.Wind>=0&&profile.Wind<=1.5f&&profile.Dust>0&&profile.Dust<=13,"Restrained weather for "+arena);
   Check(profile.Mist>=0&&profile.Mist<=.04f,"Mist alpha limited for "+arena);
   for(int t=0;t<60;t++)Check(PlanetWeatherRuntime.Gust(t, (int)arena)>=.74f&&PlanetWeatherRuntime.Gust(t,(int)arena)<=1.26f,"Bounded gusts");
  }
  foreach(var arena in new[]{ArenaVariant.Moon,ArenaVariant.Dark00,ArenaVariant.DiamondCore})
  {
   var p=PlanetWeatherRuntime.For(arena);Check(p.Wind==0&&p.Mist==0,"Airless arena has no wind or fog");
  }
  Check(PlanetWeatherRuntime.For(ArenaVariant.Mars).Dust>PlanetWeatherRuntime.For(ArenaVariant.Earth).Dust,"Mars has more dust than Earth");
  Check(PlanetWeatherRuntime.For(ArenaVariant.Neptune).Tint.b>PlanetWeatherRuntime.For(ArenaVariant.Neptune).Tint.r,"Neptune mist has a cool tint");
  foreach(float scale in new[]{1f,1.25f,2f})
  {
   foreach(float x in new[]{-30f,-15f,0f,15f,30f})foreach(float z in new[]{-16f,0f,16f})
    Check(PlanetWeatherRuntime.MistAt(x*scale,z,scale)==0,"Fog cannot obscure lane center, objectives or spawns");
   Check(PlanetWeatherRuntime.MistAt(47*scale,0,scale)==1&&PlanetWeatherRuntime.MistAt(0,33,scale)==1,"Edge fog survives the court mask");
  }
  int objects=GameObject.All.Count;
  DedicatedServerNetworkRuntime.Requested=true;Call(null,"Install");
  Check(GameObject.All.Count==objects&&Resources.Loads==0,"Dedicated server creates no weather objects or materials");
  DedicatedServerNetworkRuntime.Requested=false;Application.isBatchMode=true;Call(null,"Install");
  Check(GameObject.All.Count==objects,"Batch process skips weather");Application.isBatchMode=false;
  Call(null,"Install");Call(null,"Install");
  Check(GameObject.All.Count(g=>g.name=="DF_PlanetWeatherRuntime")==1,"Installer owns only one persistent controller");
  var runtime=UnityEngine.Object.FindAnyObjectByType<PlanetWeatherRuntime>();
  ArenaVariantRuntime.Current=ArenaVariant.Neptune;var world=new GameObject("DF_MatchWorld");Call(runtime,"Update");
  var root=GameObject.Find("DF_Weather_Neptune");
  var particles=GameObject.All.Where(g=>g.transform.parent==root.transform).Select(g=>g.GetComponent<ParticleSystem>()).ToArray();
  Check(root.transform.parent==world.transform&&particles.Length==6,"Weather belongs to the match world");
  Check(particles.Sum(p=>p.main.maxParticles)<=168,"Total particle count is bounded");
  Check(particles.All(p=>p.main.simulationSpace==ParticleSystemSimulationSpace.World&&!p.main.useUnscaledTime&&!p.useAutoRandomSeed),"Weather pauses with gameplay and uses local deterministic seeds");
  Check(particles.Skip(2).All(p=>p.main.startColor.a<=.04f),"Rendered mist respects opacity cap");
  Check(Material.All.Count==2&&Material.All[1].floats["_EdgeOnly"]==1,"Mist has a separate enforced court mask");
  AdaptiveVfxBudget.AllowDecorative=false;AdaptiveVfxBudget.Intensity=.55f;Time.unscaledTime=2;Call(runtime,"Update");
  Check(particles[0].isPlaying&&particles[0].emission.rateOverTime.constant<6,"Slow device retains reduced dust");
  Check(particles.Skip(1).All(p=>p.isStopped&&p.emission.rateOverTime.constant==0),"Slow or battery mode clears wind streaks and all fog");
  AdaptiveVfxBudget.AllowDecorative=true;Time.unscaledTime=4;Call(runtime,"Update");
  Check(particles.All(p=>p.isPlaying),"Quality recovery restores optional weather");
  MatchClock.InBackground=true;Call(runtime,"Update");Check(!root.activeSelf,"Background hides weather");
  MatchClock.InBackground=false;Call(runtime,"Update");Check(root.activeSelf,"Foreground restores weather");
  int loads=Resources.Loads,created=GameObject.All.Count;Time.unscaledTime=5;Call(runtime,"Update");
  Check(Resources.Loads==loads&&GameObject.All.Count==created,"Steady weather does not load assets or allocate objects");
  Call(runtime,"OnDisable");Check(root.destroyed&&Material.All.All(m=>m.destroyed),"Disabling controller releases owned effects and materials");
  Resources.Available=false;Time.unscaledTime=6;Call(runtime,"Update");int failures=Debug.Warnings.Count;
  Time.unscaledTime=7;Call(runtime,"Update");Check(Debug.Warnings.Count==failures&&failures==1,"Shader failure is logged once per world");
  Console.WriteLine($"PASS: {checks} weather profile, readability, budget and lifecycle checks (managed fixtures, no native rendering).");
 }
}
