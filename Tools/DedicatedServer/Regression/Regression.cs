using System;
using System.IO;
using System.Linq;
using Alterverse.EditorChecks;
using Alterverse.EditorTools;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

static class Regression
{
 static int checks,builds;
 static readonly string[] Names={"Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit","DimensionsFront/HeroEnergy"};
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
 static void Reset()
 {
  Shader.Available.Clear();foreach(string name in Names)Shader.Available[name]=new Shader{name=name};
  AssetDatabase.Settings=new GraphicsSettings();AssetDatabase.Saves=0;builds=0;
  Resources.Assets.Clear();HeroPresentationChecks.Checks=0;
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene{enabled=true,path="Assets/Game.unity"},new EditorBuildSettingsScene{enabled=false,path="Assets/Disabled.unity"}};
  BuildPipeline.Handler=options=>
  {
   builds++;
   Check(!PlayerSettings.dedicatedServerOptimizations,"Server shader stripping must be disabled while Unity builds");
   Check(Names.All(name=>AssetDatabase.Settings.Included.Contains(Shader.Find(name))),"Every runtime shader must be included before Unity builds");
   Check(options.target==BuildTarget.StandaloneLinux64&&options.subtarget==(int)StandaloneBuildSubtarget.Server,"Keep the Linux Dedicated Server target");
   Check(options.scenes.SequenceEqual(new[]{"Assets/Game.unity"}),"Build only enabled scenes");
   return new BuildReport{summary=new BuildSummary{result=BuildResult.Succeeded}};
  };
 }
 static void Reject(Action action,string phrase)
 {
  try{action();}catch(Exception error){Check(error.Message.Contains(phrase),"Failure must explain "+phrase);return;}
  throw new Exception("Expected failure: "+phrase);
 }
 static void RuntimeSuppressionCases()
 {
  var bootstrap=new Alterverse.Prototype.PrototypeBootstrap();
  UnityEngine.Object.RuntimeObjects[typeof(Alterverse.Prototype.PrototypeBootstrap)]=bootstrap;
  Alterverse.Multiplayer.CoopAuthority.ActivePlan=null;Alterverse.Match.MatchClock.Paused=true;
  Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Tick();
  Check(!bootstrap.enabled&&!Alterverse.Match.MatchClock.Paused,"Unstaged server still suppresses bootstrap and menu pause");
  bootstrap.enabled=true;Alterverse.Multiplayer.CoopAuthority.ActivePlan=new object();
  UnityEngine.Object.RuntimeSearches.Clear();
  for(int i=0;i<60;i++)Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Tick();
  Check(bootstrap.enabled&&!UnityEngine.Object.RuntimeSearches.ContainsKey(typeof(Alterverse.Prototype.PrototypeBootstrap)),"Installed match performs zero bootstrap scene searches across 60 frames");
  UnityEngine.Object.RuntimeObjects[typeof(Alterverse.UI.MainMenuController)]=new Alterverse.UI.MainMenuController();
  int destroyed=UnityEngine.Object.DestroyCalls;Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Tick();
  Check(UnityEngine.Object.DestroyCalls==destroyed+1,"Client menu cleanup remains active during a match");
  Alterverse.Multiplayer.CoopAuthority.ActivePlan=null;Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Tick();
  Check(!bootstrap.enabled,"Returning to unstaged state resumes bootstrap suppression immediately");
 }
 static void Main()
 {
  string directory=Path.Combine(Path.GetTempPath(),"df-server-regression-"+Guid.NewGuid());
  Application.dataPath=Path.Combine(directory,"Assets");Directory.CreateDirectory(Application.dataPath);
  try
  {
   foreach(bool previous in new[]{false,true})
   {
    Reset();PlayerSettings.dedicatedServerOptimizations=previous;
    DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();
    Check(builds==1&&PlayerSettings.dedicatedServerOptimizations==previous,"Restore the previous optimization setting after success");
    DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();
    Check(AssetDatabase.Settings.Included.Count==Names.Length&&AssetDatabase.Saves==1,"Repeated builds must not duplicate shader references");
    string docker=File.ReadAllText(Path.Combine(directory,"Builds/DedicatedServer/Linux/Dockerfile"));
    Check(docker.Contains("-nographics")&&docker.Contains("7777/udp"),"Shader preservation retains headless UDP server startup");
   }
   Reset();PlayerSettings.dedicatedServerOptimizations=true;
   BuildPipeline.Handler=options=>throw new InvalidOperationException("native build exception");
   Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"native build exception");
   Check(PlayerSettings.dedicatedServerOptimizations,"Restore optimizations when Unity throws");
   Reset();PlayerSettings.dedicatedServerOptimizations=true;
   BuildPipeline.Handler=options=>new BuildReport{summary=new BuildSummary{result=BuildResult.Failed,totalErrors=2}};
   Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"Failed (2 errors)");
   Check(PlayerSettings.dedicatedServerOptimizations,"Restore optimizations after a failed build report");
   foreach(string missing in Names)
   {
    Reset();PlayerSettings.dedicatedServerOptimizations=true;Shader.Available.Remove(missing);
    Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"Required dedicated server runtime shader is missing: "+missing);
    Check(builds==0&&PlayerSettings.dedicatedServerOptimizations,"Missing shaders stop the build and restore optimizations");
   }
   Reset();AssetDatabase.Settings=null;PlayerSettings.dedicatedServerOptimizations=true;
   Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"GraphicsSettings.asset");
   Check(builds==0&&PlayerSettings.dedicatedServerOptimizations,"Settings failure also restores optimizations");
   Reset();EditorBuildSettings.scenes=Array.Empty<EditorBuildSettingsScene>();
   Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"No enabled scenes");Check(builds==0,"Do not start a build without scenes");
   Reset();var energy=Shader.Find(Names[2]);Resources.Assets["Shader:CombatVfx/HeroEnergy"]=energy;Resources.Assets["Material:CombatVfx/HeroEnergy"]=new Material{shader=energy};
   string[] glow={"VoidArmorEnergy","VoidArmorHalo"};
   foreach(string resource in glow)
   {
    var shader=new Shader{name="DimensionsFront/"+resource};
    Shader.Available[shader.name]=shader;Resources.Assets["Shader:HeroArt/"+resource]=shader;
   }
   new AndroidRuntimeShaderPreserver().OnPreprocessBuild(new BuildReport{summary=new BuildSummary{platform=BuildTarget.Android}});
   Check(HeroPresentationChecks.Checks==1&&AssetDatabase.Settings.Included.Count==Names.Length+glow.Length,"Android preflight still preserves shaders and validates hero presentation");
   foreach(string resource in glow)
   {
    Check(AssetDatabase.Settings.Included.Contains(Shader.Find("DimensionsFront/"+resource)),"Android includes the client-only glow shader "+resource);
    Resources.Assets.Remove("Shader:HeroArt/"+resource);
    Reject(()=>new AndroidRuntimeShaderPreserver().OnPreprocessBuild(new BuildReport{summary=new BuildSummary{platform=BuildTarget.Android}}),"Void Knight armor glow shader is missing or has compiler errors: "+resource);
    Resources.Assets["Shader:HeroArt/"+resource]=Shader.Find("DimensionsFront/"+resource);
   }
   RuntimeSuppressionCases();
   Console.WriteLine("PASS: "+checks+" dedicated server build checks (Unity editor substitutes; no native player build)");
  }
  finally{Directory.Delete(directory,true);}
 }
}

