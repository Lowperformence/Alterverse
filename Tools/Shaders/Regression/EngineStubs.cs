using System;
using System.Collections.Generic;
namespace UnityEngine {
 public class Object { public static int Destroyed; public static void DestroyImmediate(Object value){Destroyed++;} }
 public enum HideFlags { HideAndDontSave }
 public class Shader:Object { public string name; public bool isSupported=true,HasError; public int Passes=1; }
 public class Material:Object { public Shader shader; public HideFlags hideFlags; public int passCount=>shader.Passes; public Material(Shader value){shader=value;} }
 public static class SystemInfo { public static Rendering.GraphicsDeviceType graphicsDeviceType=Rendering.GraphicsDeviceType.OpenGLES3; }
 public static class Resources { public static Dictionary<string,Material> Assets=new Dictionary<string,Material>(); public static T Load<T>(string path) where T:class { return Assets.TryGetValue(path,out var material)?material as T:null; } }
 public static class Debug { public static int Errors,Logs; public static void LogError(string value){Errors++;} public static void Log(string value){Logs++;} }
}
namespace UnityEngine.Rendering { public enum GraphicsDeviceType { Null,OpenGLES3 } }
namespace UnityEditor {
 [AttributeUsage(AttributeTargets.Method)] public sealed class InitializeOnLoadMethodAttribute:Attribute {}
 [AttributeUsage(AttributeTargets.Method)] public sealed class MenuItem:Attribute { public MenuItem(string name){} }
 public enum PlayModeStateChange { ExitingEditMode,EnteredPlayMode,ExitingPlayMode,EnteredEditMode }
 public static class EditorSettings { public static bool asyncShaderCompilation=true; }
 public static class EditorApplication { public static bool isPlaying=true; public static event Action<PlayModeStateChange> playModeStateChanged; public static void Emit(PlayModeStateChange state){playModeStateChanged?.Invoke(state);} }
 public static class AssetDatabase {
  public static Dictionary<string,UnityEngine.Shader> Shaders=new Dictionary<string,UnityEngine.Shader>();
  public static string[] FindAssets(string filter,string[] roots){var paths=new List<string>(Shaders.Keys);return paths.ToArray();}
  public static string GUIDToAssetPath(string guid)=>guid;
  public static T LoadAssetAtPath<T>(string path) where T:class { return Shaders.TryGetValue(path,out var shader)?shader as T:null; }
 }
 public static class ShaderUtil {
  public static int Compiled; public static string FailName,ThrowName; public static bool AllSynchronous=true;
  public static bool ShaderHasError(UnityEngine.Shader shader)=>shader.HasError;
  public static void CompilePass(UnityEngine.Material material,int pass,bool forceSync){Compiled++;AllSynchronous&=forceSync;if(material.shader.name==FailName)material.shader.HasError=true;if(material.shader.name==ThrowName)throw new Exception("compiler failed");}
 }
}
namespace UnityEditor.Build.Reporting { public class BuildReport {} }
namespace UnityEditor.Build {
 public interface IPreprocessBuildWithReport { int callbackOrder{get;} void OnPreprocessBuild(Reporting.BuildReport report); }
 public class BuildFailedException:Exception { public BuildFailedException(string message):base(message){} }
}
