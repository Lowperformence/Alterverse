using System;
using System.Collections.Generic;
using UnityEditor.Build.Reporting;

namespace UnityEngine
{
 public class Object
 {
  public static readonly Dictionary<Type,Object> RuntimeObjects=new();
  public static readonly Dictionary<Type,int> RuntimeSearches=new();
  public static T FindAnyObjectByType<T>() where T:class
  {RuntimeSearches[typeof(T)]=RuntimeSearches.TryGetValue(typeof(T),out int n)?n+1:1;return RuntimeObjects.TryGetValue(typeof(T),out var x)?x as T:null;}
  public static int DestroyCalls;public static void Destroy(Object value){DestroyCalls++;}
 }
 public class Shader:Object
 {
  public string name;
  public static readonly Dictionary<string,Shader> Available=new();
  public static Shader Find(string name)=>Available.TryGetValue(name,out var shader)?shader:null;
 }
 public class Material:Object {public Shader shader;}
 public static class Application {public static string dataPath;public static bool isBatchMode=true;}
 public static class Debug {public static void Log(string text){}public static void LogWarning(string text){}}
 public static class Resources
 {
  public static readonly Dictionary<string,Object> Assets=new();
  public static T Load<T>(string path) where T:class=>Assets.TryGetValue(typeof(T).Name+":"+path,out var value)?value as T:null;
 }
}
namespace UnityEngine.Rendering
{
 public class GraphicsSettings:UnityEngine.Object {public readonly List<UnityEngine.Object> Included=new();}
}
namespace UnityEditor
{
 [AttributeUsage(AttributeTargets.Method)]public sealed class MenuItem:Attribute {public MenuItem(string path){}}
 [AttributeUsage(AttributeTargets.Method)]public sealed class InitializeOnLoadMethodAttribute:Attribute {}
 public enum BuildTarget {Android,StandaloneLinux64}
 public enum StandaloneBuildSubtarget {Player,Server}
 public enum BuildOptions {None,CleanBuildCache}
 public struct BuildPlayerOptions {public string[] scenes;public string locationPathName;public BuildTarget target;public int subtarget;public BuildOptions options;}
 public class EditorBuildSettingsScene {public bool enabled;public string path;}
 public static class EditorBuildSettings {public static EditorBuildSettingsScene[] scenes;}
 public static class EditorUserBuildSettings {public static BuildTarget activeBuildTarget;}
 public static class EditorApplication {public static Action delayCall;}
 public static class EditorUtility {public static void RevealInFinder(string path){}}
 public static class PlayerSettings {public static bool dedicatedServerOptimizations;}
 public static class BuildPipeline
 {
  public static bool isBuildingPlayer;
  public static Func<BuildPlayerOptions,BuildReport> Handler;
  public static BuildReport BuildPlayer(BuildPlayerOptions options)=>Handler(options);
 }
 public static class AssetDatabase
 {
  public static Func<string,string,string> MoveHandler;
  public static string MoveAsset(string from,string to)
  {
   string error=MoveHandler?.Invoke(from,to);
   if(!string.IsNullOrEmpty(error))return error;
   string root=System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath,".."));
   string source=System.IO.Path.Combine(root,from),destination=System.IO.Path.Combine(root,to);
   if(!System.IO.File.Exists(source))return "source is missing";
   if(System.IO.File.Exists(destination))return "destination exists";
   System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
   System.IO.File.Move(source,destination);
   if(System.IO.File.Exists(source+".meta"))System.IO.File.Move(source+".meta",destination+".meta");
   return "";
  }
  public static UnityEngine.Rendering.GraphicsSettings Settings;
  public static int Saves;
  public static T LoadAssetAtPath<T>(string path) where T:class=>Settings as T;
  public static void SaveAssets(){Saves++;}
 }
 public class SerializedObject
 {
  readonly UnityEngine.Rendering.GraphicsSettings settings;
  public SerializedObject(UnityEngine.Rendering.GraphicsSettings value){settings=value;}
  public SerializedProperty FindProperty(string name)=>new(settings.Included);
  public bool ApplyModifiedProperties()=>true;
 }
 public class SerializedProperty
 {
  readonly List<UnityEngine.Object> values;readonly int index;
  public SerializedProperty(List<UnityEngine.Object> list,int element=-1){values=list;index=element;}
  public int arraySize=>values.Count;
  public UnityEngine.Object objectReferenceValue {get=>values[index];set=>values[index]=value;}
  public SerializedProperty GetArrayElementAtIndex(int element)=>new(values,element);
  public void InsertArrayElementAtIndex(int element)=>values.Insert(element,null);
 }
 public static class ShaderUtil {public static bool ShaderHasError(UnityEngine.Shader shader)=>false;}
}
namespace UnityEditor.Build
{
 public class BuildFailedException:Exception {public BuildFailedException(string message):base(message){}}
 public interface IPreprocessBuildWithReport {int callbackOrder {get;}void OnPreprocessBuild(BuildReport report);}
}
namespace UnityEditor.Build.Reporting
{
 public enum BuildResult {Succeeded,Failed}
 public struct BuildSummary {public BuildResult result;public int totalErrors;public ulong totalSize;public TimeSpan totalTime;public UnityEditor.BuildTarget platform;}
 public class BuildReport {public BuildSummary summary;}
}
namespace Alterverse.EditorChecks
{
 public static class HeroPresentationChecks {public static int Checks;public static void CheckCombatLineTransforms(){Checks++;}}
}

namespace Alterverse.Match {public enum PauseReason{Menu}public static class MatchClock {public static bool Paused;public static void Set(PauseReason reason,bool value){Paused=value;}}}
namespace Alterverse.Prototype {public sealed class PrototypeBootstrap:UnityEngine.Object {public bool enabled=true;}}
namespace Alterverse.UI {public sealed class MainMenuController:UnityEngine.Object {public UnityEngine.Object gameObject=new();}}
namespace Alterverse.Multiplayer {public static class CoopAuthority {public static object ActivePlan;}}

