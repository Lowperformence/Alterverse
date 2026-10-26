// Compile real AndroidReleaseBuild and AndroidR8Optimization with editor substitutes.
using System;
using System.Collections.Generic;
using System.IO;
using Alterverse.EditorChecks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
namespace UnityEngine {
 public class Object {}
 public static class Resources {public static Object Load(string path)=>new Object();}
 public static class JsonUtility {
  public static string ToJson(Object value)=>"{\"mAppId\":\"1029475752167\",\"mWebClientId\":\"fixture-client\"}";
  public static T FromJson<T>(string json)=>System.Text.Json.JsonSerializer.Deserialize<T>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
 }
 public static class Application {public static string dataPath=>Path.Combine(Directory.GetCurrentDirectory(),"Assets");public static string consoleLogPath=>"";}
 public static class Debug {public static void Log(object s){}public static void LogWarning(object s){}}
}
namespace UnityEngine.Rendering {public enum GraphicsDeviceType {Vulkan,OpenGLES3}}
namespace UnityEditor.Android {public interface IPostGenerateGradleAndroidProject {int callbackOrder{get;}void OnPostGenerateGradleAndroidProject(string path);}}
namespace UnityEditor.Build {
 public struct NamedBuildTarget {public static NamedBuildTarget Android;}
 public interface IPreprocessBuildWithReport {int callbackOrder{get;}void OnPreprocessBuild(BuildReport r);}
 public class BuildFailedException:Exception {public BuildFailedException(string s):base(s){}}
}
namespace UnityEditor.Build.Reporting {
 public enum BuildResult {Succeeded,Failed,Cancelled}
 public class BuildReport {public BuildSummary summary=new BuildSummary();}
 public class BuildSummary {public BuildTarget platform;public BuildOptions options;public BuildResult result;public int totalErrors;public string outputPath;public TimeSpan totalTime;}
}
namespace UnityEditor {
 public class MenuItem:Attribute {public MenuItem(string s){}}
 public enum BuildTarget {Android,StandaloneWindows64}
 [Flags] public enum BuildOptions {None=0,Development=1,AllowDebugging=2,ConnectWithProfiler=4,EnableDeepProfilingSupport=8,CleanBuildCache=16}
 [Flags] public enum AndroidArchitecture {ARM64=2}
 public enum ScriptingImplementation {IL2CPP,Mono2x}
 public struct BuildPlayerOptions {public string[] scenes;public BuildTarget target;public BuildOptions options;public string locationPathName;public string[] extraScriptingDefines;}
 public static class EditorUserBuildSettings {public static BuildTarget activeBuildTarget=BuildTarget.Android;public static bool buildAppBundle=true,development,allowDebugging,connectProfiler,buildWithDeepProfilingSupport;}
 public static class EditorApplication {public static bool isCompiling,isUpdating,isPlayingOrWillChangePlaymode;}
 public static class BuildPipeline {public static bool isBuildingPlayer;public static BuildReport BuildPlayer(BuildPlayerOptions options)=>null;}
 public static class EditorUtility {public static void RevealInFinder(string s){}public static void ClearProgressBar(){}}
 public static class SessionState {static readonly Dictionary<string,bool> Data=new();public static bool GetBool(string k,bool d)=>Data.TryGetValue(k,out bool v)?v:d;public static void SetBool(string k,bool v){Data[k]=v;}public static void EraseBool(string k){Data.Remove(k);}}
 public class EditorBuildSettingsScene {public bool enabled;public string path;}
 public static class EditorBuildSettings {public static EditorBuildSettingsScene[] scenes;}
 public static class PlayerSettings {
  public static string productName="DIMENSIONS FRONT",bundleVersion="0.5";
  public static bool openGLRequireES31AEP,openGLRequireES32,AutomaticGraphics;
  public static UnityEngine.Rendering.GraphicsDeviceType[] Graphics={UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3};
  public static string GetApplicationIdentifier(NamedBuildTarget t)=>"com.lowperformence.dimensionsfront";
  public static void SetApplicationIdentifier(NamedBuildTarget t,string s){}
  public static ScriptingImplementation GetScriptingBackend(NamedBuildTarget t)=>ScriptingImplementation.IL2CPP;
  public static void SetScriptingBackend(NamedBuildTarget t,ScriptingImplementation i){}
  public static bool GetUseDefaultGraphicsAPIs(BuildTarget t)=>AutomaticGraphics;
  public static void SetUseDefaultGraphicsAPIs(BuildTarget t,bool v){AutomaticGraphics=v;}
  public static UnityEngine.Rendering.GraphicsDeviceType[] GetGraphicsAPIs(BuildTarget t)=>Graphics;
  public static void SetGraphicsAPIs(BuildTarget t,UnityEngine.Rendering.GraphicsDeviceType[] v){Graphics=v;}
  public static class Android {public static bool minifyRelease,minifyDebug,useCustomKeystore=true;public static int targetSdkVersion=36,minSdkVersion=26,bundleVersionCode=40;public static AndroidArchitecture targetArchitectures=AndroidArchitecture.ARM64;public static string keystoreName,keyaliasName="upload";}
 }
 public class SerializedProperty {public bool boolValue {get=>AssetDatabase.CustomRules;set{AssetDatabase.CustomRules=value;}}}
 public class SerializedObject:IDisposable {public SerializedObject(object o){}public SerializedProperty FindProperty(string n)=>n=="useCustomProguardFile"?new SerializedProperty():null;public void ApplyModifiedPropertiesWithoutUndo(){AssetDatabase.Saves++;}public void Dispose(){}}
 public static class AssetDatabase {public static bool CustomRules;public static int Saves;public static object[] LoadAllAssetsAtPath(string s)=>new[]{new object()};public static void SaveAssets(){Saves++;}}
}
namespace Alterverse.EditorChecks {public static class AndroidLauncherIcons {public static int Applies;public static void Apply(){Applies++;}public static void AddValidationErrors(List<string> e){}}}
class Regression {
 static int checks;
 static void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
 static void Reject(Action action,string reason){bool failed=false;try{action();}catch(BuildFailedException){failed=true;}Check(failed,reason);}
 static void Main(string[] args){
  string temporary=Path.Combine(Path.GetTempPath(),"df-r8-check-"+Guid.NewGuid());Directory.CreateDirectory(temporary);
  try{
   string key=Path.Combine(temporary,"upload.keystore"),scene=Path.Combine(temporary,"main.unity");File.WriteAllText(key,"fixture");File.WriteAllText(scene,"fixture");
   PlayerSettings.Android.keystoreName=key;EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene{enabled=true,path=scene}};
   PlayerSettings.Android.minifyRelease=false;PlayerSettings.Android.minifyDebug=true;AssetDatabase.CustomRules=false;
   AndroidReleaseBuild.Prepare();
   Check(PlayerSettings.Android.minifyRelease&&!PlayerSettings.Android.minifyDebug&&AssetDatabase.CustomRules,"Prepare Android upload enables release R8 with custom rules only");
   int saves=AssetDatabase.Saves;AndroidR8Optimization.ApplySettings();Check(AssetDatabase.Saves==saves,"matching custom setting is not rewritten");
   var errors=new List<string>();AndroidR8Optimization.AddValidationErrors(errors);Check(errors.Count==0,"valid R8 preflight passes");
   PlayerSettings.Android.minifyRelease=false;PlayerSettings.Android.minifyDebug=true;AssetDatabase.CustomRules=false;
   AndroidR8Optimization.AddValidationErrors(errors);Check(errors.Count==3,"preflight diagnoses release/debug/custom rule flags");
   new AndroidReleaseBuild().OnPreprocessBuild(new BuildReport{summary=new BuildSummary{platform=BuildTarget.Android,options=BuildOptions.None}});
   Check(PlayerSettings.Android.minifyRelease&&!PlayerSettings.Android.minifyDebug&&AssetDatabase.CustomRules,"regular Android release Build also enables R8");
   PlayerSettings.Android.minifyRelease=false;AssetDatabase.CustomRules=false;
   new AndroidReleaseBuild().OnPreprocessBuild(new BuildReport{summary=new BuildSummary{platform=BuildTarget.Android,options=BuildOptions.Development}});
   Check(!PlayerSettings.Android.minifyRelease&&!AssetDatabase.CustomRules,"development preprocessing does not force release flags");
   new AndroidReleaseBuild().OnPreprocessBuild(new BuildReport{summary=new BuildSummary{platform=BuildTarget.StandaloneWindows64}});
   Check(!PlayerSettings.Android.minifyRelease&&!AssetDatabase.CustomRules,"non-Android builds are untouched");
   var post=new AndroidR8GradlePostprocessor();Check(post.callbackOrder>1000000,"R8 callback follows manifest compatibility processing");
   string library=Path.Combine(temporary,"unityLibrary"),launcher=Path.Combine(temporary,"launcher");Directory.CreateDirectory(library);Directory.CreateDirectory(launcher);
   string main=Path.Combine(library,"build.gradle"),app=Path.Combine(launcher,"build.gradle");
   const string lib="apply plugin: 'com.android.library'\nconsumerProguardFiles 'proguard-unity.txt', 'proguard-user.txt'\nimplementation 'example:plugin:1'\n";
   const string old="release { minifyEnabled true; proguardFiles getDefaultProguardFile('proguard-android.txt'), 'extra-rules.pro'; signingConfig signingConfigs.release }";
   File.WriteAllText(main,lib);File.WriteAllText(app,old);post.OnPostGenerateGradleAndroidProject(library);
   Check(File.ReadAllText(app)==old.Replace("proguard-android.txt","proguard-android-optimize.txt"),"only legacy default changes; signing/custom rules remain");
   Check(File.ReadAllText(main)==lib,"Unity/SDK consumer rules and dependencies stay intact");
   DateTime stamp=File.GetLastWriteTimeUtc(app);post.OnPostGenerateGradleAndroidProject(library);Check(File.GetLastWriteTimeUtc(app)==stamp,"repeated Gradle callback is idempotent");
   File.WriteAllText(app,old.Replace("'proguard-android.txt'","\"proguard-android.txt\""));post.OnPostGenerateGradleAndroidProject(library);Check(File.ReadAllText(app).Contains("\"proguard-android-optimize.txt\""),"double-quoted defaults work");
   string optionalRules=OptionalGoogleAdsCases(post,library,main,app,lib,old);
   if(args.Length==1)File.WriteAllText(args[0],optionalRules);
   File.WriteAllText(app,"release { minifyEnabled true }");Reject(()=>post.OnPostGenerateGradleAndroidProject(library),"missing optimized default cannot silently pass");
   File.Delete(app);Reject(()=>post.OnPostGenerateGradleAndroidProject(library),"missing launcher fails clearly");
   EditorUserBuildSettings.development=true;post.OnPostGenerateGradleAndroidProject(library);Check(true,"development Gradle processing is skipped");
   Console.WriteLine(checks+" real build-helper/R8 behavior checks passed with editor substitutes.");
  }finally{Directory.Delete(temporary,true);}
 }
 static string OptionalGoogleAdsCases(AndroidR8GradlePostprocessor post,string library,string main,string app,string lib,string old){
  const string standard="implementation 'com.google.android.gms:play-services-ads:25.4.0'\n";
  const string next="implementation 'com.google.android.libraries.ads.mobile.sdk:ads-mobile-sdk:1.4.0'\n";
  const string warning="-dontwarn com.google.android.libraries.ads.mobile.sdk.**";
  string rules=Path.Combine(library,"proguard-user.txt");
  string original=File.ReadAllText(AndroidR8Optimization.RulesPath);
  File.WriteAllText(app,old);File.WriteAllText(main,lib+standard);
  Reject(()=>post.OnPostGenerateGradleAndroidProject(library),"missing generated custom rules must fail before running R8");
  File.WriteAllText(rules,original);post.OnPostGenerateGradleAndroidProject(library);
  string configured=File.ReadAllText(rules);
  Check(configured.StartsWith(original,StringComparison.Ordinal)&&configured.Contains(warning),"Standard-only build retains all existing rules and adds only optional Next-Gen definitions");
  Check(!configured.Contains("-ignorewarnings")&&!configured.Contains("-dontwarn **")&&!configured.Contains("-dontwarn com.google.android.gms"),"do not suppress missing active Google Ads or unrelated dependencies");
  Check(File.ReadAllText(main)==lib+standard,"do not change or add SDK dependencies");
  DateTime stamp=File.GetLastWriteTimeUtc(rules);post.OnPostGenerateGradleAndroidProject(library);
  Check(File.ReadAllText(rules)==configured&&File.GetLastWriteTimeUtc(rules)==stamp,"repeated optional-rules pass is idempotent");
  foreach(string active in new[]{next,standard+next,""}){
   File.WriteAllText(main,lib+active);post.OnPostGenerateGradleAndroidProject(library);
   Check(File.ReadAllText(rules)==original,"remove managed ignore when Next-Gen, both SDKs or no SDK is selected");
   Check(File.ReadAllText(main)==lib+active,"SDK selection remains untouched");
   File.WriteAllText(rules,configured);
  }
  foreach(string declaration in new[]{
   "api(\"com.google.android.gms:play-services-ads:25.4.0\")\n",
   "implementation ('com.google.android.gms:play-services-ads:25.4.0')\n",
   "    implementation \"com.google.android.gms:play-services-ads:25.4.0\"\n",
   standard+"// "+next}){
   File.WriteAllText(main,lib+declaration);File.WriteAllText(rules,original);
   post.OnPostGenerateGradleAndroidProject(library);
   Check(File.ReadAllText(rules).Contains(warning),"recognize direct resolved Gradle dependency forms and ignore commented alternatives");
  }
  foreach(string inactive in new[]{"// "+standard,"testImplementation 'com.google.android.gms:play-services-ads:25.4.0'\n"}){
   File.WriteAllText(main,lib+inactive);File.WriteAllText(rules,original);
   post.OnPostGenerateGradleAndroidProject(library);
   Check(File.ReadAllText(rules)==original,"comments and test-only dependencies cannot enable missing-class suppression");
  }
  string malformed=original+"# DF_OPTIONAL_GMA_NEXTGEN_START\n# interrupted block\n";
  File.WriteAllText(main,lib+standard);File.WriteAllText(rules,malformed);
  Reject(()=>post.OnPostGenerateGradleAndroidProject(library),"malformed managed block fails clearly");
  Check(File.ReadAllText(rules)==malformed,"malformed configuration is not overwritten");
  File.WriteAllText(rules,original);EditorUserBuildSettings.development=true;
  post.OnPostGenerateGradleAndroidProject(library);
  Check(File.ReadAllText(rules)==original,"development builds are untouched");
  EditorUserBuildSettings.development=false;
  Check(File.ReadAllText(AndroidR8Optimization.RulesPath)==original,"versioned ProGuard settings never change during generated-project processing");
  return configured;
 }
}
