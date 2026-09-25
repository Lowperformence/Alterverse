#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
namespace Alterverse.EditorChecks
{
    /// <summary>Reproducible build entry point, not a claim of store approval.</summary>
    public sealed class AndroidReleaseBuild : IPreprocessBuildWithReport
    {
        private const string AppIconPath="Assets/Alterverse/Branding/DimensionsFrontAppIcon.png";
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.Android&&(report.summary.options&BuildOptions.Development)==0)
            {
                const BuildOptions debugOptions=BuildOptions.AllowDebugging|BuildOptions.ConnectWithProfiler|BuildOptions.EnableDeepProfilingSupport;
                if((report.summary.options&debugOptions)!=0)throw new BuildFailedException("Release bundle must not enable script debugging or profiler connection.");
                Validate();
            }
        }
        [MenuItem("DIMENSIONS FRONT/Release/Validate Android release settings")]
        public static void Validate()
        {
            var errors=new List<string>();
            if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)errors.Add("Switch the active Build Profile to Android first.");
            string id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if(string.IsNullOrWhiteSpace(id)||id.Contains("UnityTechnologies")||id.Contains("DefaultCompany"))errors.Add("Replace the template application identifier.");
            if(id!="com.lowperformence.dimensionsfront")errors.Add("Keep the existing Android application ID com.lowperformence.dimensionsfront stable.");
            if(PlayerSettings.productName!="DIMENSIONS FRONT")errors.Add("Product name must be DIMENSIONS FRONT.");
            if(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)!=ScriptingImplementation.IL2CPP)errors.Add("Android requires IL2CPP for this release configuration.");
            if((PlayerSettings.Android.targetArchitectures&AndroidArchitecture.ARM64)==0)errors.Add("Enable ARM64.");
            if((int)PlayerSettings.Android.targetSdkVersion<36)errors.Add("Pin target SDK 36 or newer; automatic SDK selection is not a reproducible release gate.");
            if(string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))errors.Add("Set a visible app version before uploading.");
            if(PlayerSettings.Android.bundleVersionCode<1)errors.Add("Set a positive, unused version code before uploading.");
            if((int)PlayerSettings.Android.minSdkVersion<26)errors.Add("Minimum Android SDK must remain API 26 or newer for this release profile.");

            if(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
                errors.Add("Disable automatic Android Graphics APIs so the release always keeps the OpenGL ES 3 fallback.");
            GraphicsDeviceType[] graphicsApis=PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool hasOpenGles3=false;
            if(graphicsApis!=null)foreach(GraphicsDeviceType api in graphicsApis)if(api==GraphicsDeviceType.OpenGLES3){hasOpenGles3=true;break;}
            if(!hasOpenGles3)errors.Add("Android release must include OpenGLES3 as a fallback so Vulkan support is not a Play Store device requirement.");

            // Unity 6.6 no longer exposes a standalone Require ES3.1 toggle in the Android Player UI,
            // and PlayerSettings.openGLRequireES31 can report a legacy value that cannot be cleared reliably.
            // Device compatibility is guarded by requiring an OpenGLES3 fallback and by rejecting the
            // still-exposed higher requirements below. The generated manifest guard remains authoritative.
            if(PlayerSettings.openGLRequireES31AEP)errors.Add("Require ES3.1+AEP must stay disabled; it would filter otherwise compatible Android devices.");
            if(PlayerSettings.openGLRequireES32)errors.Add("Require ES3.2 must stay disabled; it would filter otherwise compatible Android devices.");

            Texture2D[] icons=PlayerSettings.GetIcons(NamedBuildTarget.Android,IconKind.Application);
            bool hasIcon=false;if(icons!=null)foreach(Texture2D icon in icons)if(icon!=null){hasIcon=true;break;}
            if(!hasIcon)errors.Add("Android launcher icon is not assigned. Run Prepare Android upload; it assigns "+AppIconPath+".");
            if(!EditorUserBuildSettings.buildAppBundle)errors.Add("Enable Android App Bundle (.aab).");
            if(!PlayerSettings.Android.useCustomKeystore||string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName)||string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))errors.Add("Owner must configure upload signing locally. Never commit passwords or signing keys.");
            if(PlayerSettings.Android.useCustomKeystore&&!string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName))
            {
                string keystore=Path.GetFullPath(PlayerSettings.Android.keystoreName);
                string project=Path.GetFullPath(Path.Combine(Application.dataPath,".."))+Path.DirectorySeparatorChar;
                if(keystore.StartsWith(project,System.StringComparison.OrdinalIgnoreCase))errors.Add("Keep the upload keystore outside the repository.");
                if(!File.Exists(keystore))errors.Add("Configured upload keystore does not exist on this machine.");
            }
            bool scene=false;foreach(var entry in EditorBuildSettings.scenes)if(entry.enabled)
            {
                if(File.Exists(entry.path))scene=true;else errors.Add("Enabled build scene is missing: "+entry.path);
            }
            if(!scene)errors.Add("No existing enabled scene in Build Settings.");
            if(errors.Count>0)throw new BuildFailedException(string.Join("\n",errors));
            Debug.Log("DIMENSIONS FRONT: Android release gate passed, including device-compatibility graphics checks. Generated manifest guard will run during the Gradle build.");
        }
        private static void EnsureLauncherIcon()
        {
            Texture2D icon=AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
            if(icon==null)throw new BuildFailedException("Required launcher icon asset is missing: "+AppIconPath);
            Texture2D[] current=PlayerSettings.GetIcons(NamedBuildTarget.Android,IconKind.Application);
            bool assigned=false;if(current!=null)foreach(Texture2D candidate in current)if(candidate!=null){assigned=true;break;}
            if(!assigned)
            {
                int[] sizes=PlayerSettings.GetIconSizes(NamedBuildTarget.Android,IconKind.Application);
                if(sizes==null||sizes.Length==0)throw new BuildFailedException("Unity reported no Android application icon slots.");
                Texture2D[] icons=new Texture2D[sizes.Length];
                for(int i=0;i<icons.Length;i++)icons[i]=icon;
                PlayerSettings.SetIcons(NamedBuildTarget.Android,icons,IconKind.Application);
                AssetDatabase.SaveAssets();
                Debug.Log("DIMENSIONS FRONT: assigned Android launcher icon to "+icons.Length+" icon slots from "+AppIconPath);
            }
        }

        private static void ApplyDeviceCompatibilitySettings()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan,GraphicsDeviceType.OpenGLES3});
            PlayerSettings.openGLRequireES31AEP=false;
            PlayerSettings.openGLRequireES32=false;
            Debug.Log("DIMENSIONS FRONT: Android compatibility baseline applied: Vulkan + OpenGLES3 fallback, no ES3.1+AEP/ES3.2 requirement.");
        }

        [MenuItem("DIMENSIONS FRONT/Release/Prepare Android upload")]
        public static void Prepare()
        {
            EnsureLauncherIcon();
            EditorUserBuildSettings.buildAppBundle=true;
            EditorUserBuildSettings.development=false;
            EditorUserBuildSettings.allowDebugging=false;
            EditorUserBuildSettings.connectProfiler=false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport=false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            ApplyDeviceCompatibilitySettings();
            AssetDatabase.SaveAssets();
            Debug.Log("DIMENSIONS FRONT: release-safe Android build flags applied. Running upload preflight...");
            Validate();
        }

        [MenuItem("DIMENSIONS FRONT/Release/Build signed Android bundle")]
        public static void Build()
        {
            EnsureLauncherIcon();
            EditorUserBuildSettings.buildAppBundle=true;
            EditorUserBuildSettings.development=false;
            EditorUserBuildSettings.allowDebugging=false;
            EditorUserBuildSettings.connectProfiler=false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport=false;
            ApplyDeviceCompatibilitySettings();
            AssetDatabase.SaveAssets();
            Validate();
            var scenes=new List<string>();foreach(var scene in EditorBuildSettings.scenes)if(scene.enabled)scenes.Add(scene.path);
            Directory.CreateDirectory("Builds/Android");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=scenes.ToArray(),target=BuildTarget.Android,options=BuildOptions.None,
                locationPathName="Builds/Android/DIMENSIONS-FRONT-"+PlayerSettings.Android.bundleVersionCode+".aab"
            });
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("DIMENSIONS FRONT Android build failed. Inspect the Unity build log.");
            string output=Path.GetFullPath(report.summary.outputPath);
            Debug.Log("DIMENSIONS FRONT: signed Android bundle created at "+output+". Compatibility manifest snapshots are in Builds/Android.");
            EditorUtility.RevealInFinder(output);
        }

        [MenuItem("DIMENSIONS FRONT/Release/Open Android build folder")]
        public static void OpenBuildFolder()
        {
            string folder=Path.GetFullPath("Builds/Android");
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder);
            Debug.Log("DIMENSIONS FRONT: Android build folder "+folder);
        }
    }
}
#endif
