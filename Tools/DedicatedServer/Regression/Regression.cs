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
 const string Settings="Assets/GooglePlayGames/Resources/PlayGamesSettings.asset";
 const string Held="Assets/Alterverse/Editor/PlayGamesSettings.server-build.asset";
 static string Full(string path)=>Path.GetFullPath(Path.Combine(Application.dataPath,"..",path));
 static void WriteAsset(string path,string data)
 {
  Directory.CreateDirectory(Path.GetDirectoryName(Full(path)));File.WriteAllText(Full(path),data);
  File.WriteAllText(Full(path)+".meta","guid: preserve-google-play-settings");
 }
 static void Restored(string data)
 {
  Check(File.ReadAllText(Full(Settings))==data,"Restore the exact local Google Play settings, including local client IDs");
  Check(File.ReadAllText(Full(Settings)+".meta")=="guid: preserve-google-play-settings","Preserve the asset GUID");
  Check(!File.Exists(Full(Held))&&!File.Exists(Full(Held)+".meta"),"Do not leave held settings after a build");
 }
 static readonly string[] Names={"Universal Render Pipeline/Lit","Universal Render Pipeline/Unlit","DimensionsFront/HeroEnergy"};
 static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
 static void Reset()
 {
  Shader.Available.Clear();foreach(string name in Names)Shader.Available[name]=new Shader{name=name};
  AssetDatabase.Settings=new GraphicsSettings();AssetDatabase.Saves=0;builds=0;
  AssetDatabase.MoveHandler=null;
  AssetDatabase.DeleteHandler=null;
  Check(AssetDatabase.EditingDepth==0,"Every recovery releases the import pause, including failures");
  AssetDatabase.EditingStarts=AssetDatabase.EditingStops=0;
  foreach(string path in new[]{Settings,Held})
  {
   if(File.Exists(Full(path)))File.Delete(Full(path));
   if(File.Exists(Full(path)+".meta"))File.Delete(Full(path)+".meta");
  }
  string recovery=Full("Builds/ServerBuildRecovery");
  if(Directory.Exists(recovery))Directory.Delete(recovery,true);
  if(File.Exists(recovery))File.Delete(recovery);
  string editor=Full("Assets/Alterverse/Editor");
  if(Directory.Exists(editor))foreach(string path in Directory.GetFiles(editor,"PlayGamesSettings.regenerated-*"))File.Delete(path);
  Resources.Assets.Clear();HeroPresentationChecks.Checks=0;
  EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene{enabled=true,path="Assets/Game.unity"},new EditorBuildSettingsScene{enabled=false,path="Assets/Disabled.unity"}};
  BuildPipeline.Handler=options=>
  {
   builds++;
   Check(!PlayerSettings.dedicatedServerOptimizations,"Server shader stripping must be disabled while Unity builds");
   Check(Names.All(name=>AssetDatabase.Settings.Included.Contains(Shader.Find(name))),"Every runtime shader must be included before Unity builds");
   Check(options.target==BuildTarget.StandaloneLinux64&&options.subtarget==(int)StandaloneBuildSubtarget.Server,"Keep the Linux Dedicated Server target");
   Check(options.scenes.SequenceEqual(new[]{"Assets/Game.unity"}),"Build only enabled scenes");
   Check(options.options==BuildOptions.CleanBuildCache,"Rebuild player data without stale platform serialization caches");
   return new BuildReport{summary=new BuildSummary{result=BuildResult.Succeeded}};
  };
 }
 static void GooglePlaySettingsCases()
 {
  const string local="app: local-app\nwebClientId: local-client-id\n";
  foreach(int outcome in new[]{0,1,2})
  {
   Reset();WriteAsset(Settings,local);PlayerSettings.dedicatedServerOptimizations=true;
   BuildPipeline.Handler=options=>
   {
    Check(!File.Exists(Full(Settings))&&File.ReadAllText(Full(Held))==local,"Android settings are outside Resources while the Linux player builds");
    if(outcome==1)throw new Exception("native exception");
    return new BuildReport{summary=new BuildSummary{result=outcome==2?BuildResult.Failed:BuildResult.Succeeded}};
   };
   if(outcome==0)DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();
   else Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,outcome==1?"native exception":"Failed");
   Restored(local);Check(PlayerSettings.dedicatedServerOptimizations,"Restore optimization setting alongside Google Play settings");
  }
  Reset();WriteAsset(Settings,local);AssetDatabase.MoveHandler=(from,to)=>"move denied";
  Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"move denied");
  Restored(local);Check(builds==0,"Do not build when exclusion fails");
  Reset();WriteAsset(Held,local);
  DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();Restored(local);
  Reset();WriteAsset(Settings,"regenerated copy");WriteAsset(Held,local);
  File.WriteAllText(Full(Settings)+".meta","guid: regenerated-resource-copy");
  DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();Restored(local);
  Archived("regenerated copy",local);
  Check(builds==1,"Recovered collision allows the requested build to proceed");
  Check(AssetDatabase.EditingStarts==1&&AssetDatabase.EditingStops==1,"Recovery batches asset imports around both moves");
  Reset();WriteAsset(Settings,local);
  AssetDatabase.MoveHandler=(from,to)=>to==Settings?"restore denied":null;
  Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"restore denied");
  Check(File.ReadAllText(Full(Held))==local&&!File.Exists(Full(Settings)),"Keep the recoverable file if restoration fails");
  AssetDatabase.MoveHandler=null;DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();Restored(local);
  Reset();WriteAsset(Held,local);
  typeof(DimensionsFrontDedicatedServerBuilder).GetMethod("RestoreAfterInterruptedBuild",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
  EditorApplication.delayCall();Restored(local);
  RecoveryFailureCases(local);
 }
 static void Archived(string resource,string held)
 {
  string[] directories=Directory.GetDirectories(Full("Builds/ServerBuildRecovery"));
  Check(directories.Length==1,"Keep one unique archive for this collision");
  string archive=directories[0];
  Check(File.ReadAllText(Path.Combine(archive,"ResourceCopy.asset"))==resource,"Archive the exact displaced Resources configuration");
  Check(File.ReadAllText(Path.Combine(archive,"HeldOriginal.asset"))==held,"Archive the exact held pre-build configuration");
  Check(File.ReadAllText(Path.Combine(archive,"ResourceCopy.asset.meta"))=="guid: regenerated-resource-copy","Archive the displaced GUID separately");
  Check(File.ReadAllText(Path.Combine(archive,"HeldOriginal.asset.meta"))=="guid: preserve-google-play-settings","Archive the original GUID separately");
 }
 static void Collision(string local)
 {
  WriteAsset(Settings,"regenerated copy");WriteAsset(Held,local);
  File.WriteAllText(Full(Settings)+".meta","guid: regenerated-resource-copy");
 }
 static void Recover()=>typeof(DimensionsFrontDedicatedServerBuilder)
  .GetMethod("RestorePlayGamesSettings",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)
  .Invoke(null,null);
 static void RecoveryFailureCases(string local)
 {
  // A plugin creates a new Resources asset while the held original is excluded.
  foreach(int outcome in new[]{0,1,2})
  {
   Reset();WriteAsset(Settings,local);PlayerSettings.dedicatedServerOptimizations=true;
   BuildPipeline.Handler=options=>
   {
    WriteAsset(Settings,"regenerated copy");File.WriteAllText(Full(Settings)+".meta","guid: regenerated-resource-copy");
    if(outcome==1)throw new Exception("native exception after regeneration");
    return new BuildReport{summary=new BuildSummary{result=outcome==2?BuildResult.Failed:BuildResult.Succeeded}};
   };
   if(outcome==0)DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();
   else Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,outcome==1?"native exception after regeneration":"Failed");
   Restored(local);Archived("regenerated copy",local);
   Check(PlayerSettings.dedicatedServerOptimizations,"Collision recovery preserves optimization restoration on every build outcome");
   Check(AssetDatabase.EditingDepth==0,"Import pause released after build outcome");
  }
  Reset();Collision(local);
  AssetDatabase.MoveHandler=(from,to)=>
  {
   Check(AssetDatabase.EditingDepth==1,"Collision moves run while import callbacks are paused");
   return to.Contains(".regenerated-")?"quarantine denied":null;
  };
  Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"quarantine denied");
  Check(File.ReadAllText(Full(Settings))=="regenerated copy"&&File.ReadAllText(Full(Held))==local,"Failed quarantine leaves both source files untouched");
  Archived("regenerated copy",local);
  Reset();Collision(local);
  AssetDatabase.MoveHandler=(from,to)=>from==Held?"restore denied":null;
  Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"restore denied");
  Check(File.ReadAllText(Full(Settings))=="regenerated copy"&&File.ReadAllText(Full(Held))==local,"Failed original restoration rolls back the displaced Resources copy");
  Check(File.ReadAllText(Full(Settings)+".meta")=="guid: regenerated-resource-copy","Rollback preserves the displaced GUID");
  Archived("regenerated copy",local);
  Reset();Collision(local);
  AssetDatabase.MoveHandler=(from,to)=>to==Settings?"restore and rollback denied":null;
  Reject(DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer,"both copies and metadata are backed up");
  Check(File.ReadAllText(Full(Held))==local&&!File.Exists(Full(Settings)),"Failed rollback retains the held original");
  Check(Directory.GetFiles(Full("Assets/Alterverse/Editor"),"PlayGamesSettings.regenerated-*.asset").Length==1,"Failed rollback retains the quarantined copy too");
  Archived("regenerated copy",local);
  Reset();Collision(local);AssetDatabase.DeleteHandler=path=>false;
  DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();Restored(local);Archived("regenerated copy",local);
  Check(Directory.GetFiles(Full("Assets/Alterverse/Editor"),"PlayGamesSettings.regenerated-*.asset").Length==1,"A cleanup failure retains the extra Editor-only copy");
  Reset();Collision(local);
  Directory.CreateDirectory(Full("Builds"));File.WriteAllText(Full("Builds/ServerBuildRecovery"),"block backup creation");
  bool backupFailed=false;
  try{DimensionsFrontDedicatedServerBuilder.BuildLinuxDedicatedServer();}catch(IOException){backupFailed=true;}
  Check(backupFailed&&builds==0,"Cannot modify or build when backup storage is unavailable");
  Check(File.ReadAllText(Full(Settings))=="regenerated copy"&&File.ReadAllText(Full(Held))==local,"Backup failure preserves both source assets");
  Reset();Collision(local);
  typeof(DimensionsFrontDedicatedServerBuilder).GetMethod("RestoreAfterInterruptedBuild",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
  EditorApplication.delayCall();Restored(local);Archived("regenerated copy",local);
  string[] archived=Directory.GetDirectories(Full("Builds/ServerBuildRecovery"));
  Recover();
  Check(Directory.GetDirectories(Full("Builds/ServerBuildRecovery")).SequenceEqual(archived),"Repeated startup recovery is idempotent");
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
  Check(UnityEngine.Object.DestroyCalls==destroyed&&!UnityEngine.Object.RuntimeSearches.ContainsKey(typeof(Alterverse.UI.MainMenuController)),"Installed match avoids per-frame menu searches; menus suppress themselves at initialization");
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
   GooglePlaySettingsCases();RuntimeSuppressionCases();
   Console.WriteLine("PASS: "+checks+" dedicated server build checks (Unity editor substitutes; no native player build)");
  }
  finally{Directory.Delete(directory,true);}
 }
}
