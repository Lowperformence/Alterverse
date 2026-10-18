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
        private const string AndroidApplicationId="com.lowperformence.dimensionsfront";
        private const string InterruptedBuildKey="DimensionsFront.AndroidBuildNeedsCleanRetry";
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            // Also repair icons for builds started from Unity's regular Build window,
            // including development builds, before the release gate checks them.
            if(report.summary.platform==BuildTarget.Android)EnsureLauncherIcon();
            if(report.summary.platform==BuildTarget.Android&&(report.summary.options&BuildOptions.Development)==0)
            {
                const BuildOptions debugOptions=BuildOptions.AllowDebugging|BuildOptions.ConnectWithProfiler|BuildOptions.EnableDeepProfilingSupport;
                if((report.summary.options&debugOptions)!=0)throw new BuildFailedException("Release bundle must not enable script debugging or profiler connection.");
                AndroidR8Optimization.ApplySettings();
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
            if(id!=AndroidApplicationId)errors.Add("Android application ID is '"+id+"'; expected '"+AndroidApplicationId+"'. Run DIMENSIONS FRONT > Release > Prepare Android upload to restore it.");
            if(PlayerSettings.productName!="DIMENSIONS FRONT")errors.Add("Product name must be DIMENSIONS FRONT.");
            if(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)!=ScriptingImplementation.IL2CPP)errors.Add("Android requires IL2CPP for this release configuration.");
            if((PlayerSettings.Android.targetArchitectures&AndroidArchitecture.ARM64)==0)errors.Add("Enable ARM64.");
            if((int)PlayerSettings.Android.targetSdkVersion<36)errors.Add("Pin target SDK 36 or newer; automatic SDK selection is not a reproducible release gate.");
            if(string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion))errors.Add("Set a visible app version before uploading.");
            if(PlayerSettings.Android.bundleVersionCode<1)errors.Add("Set a positive, unused version code before uploading.");
            if((int)PlayerSettings.Android.minSdkVersion<26)errors.Add("Minimum Android SDK must remain API 26 or newer for this release profile.");
            AndroidR8Optimization.AddValidationErrors(errors);
            ValidatePlayGamesSetup(errors);

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

#if UNITY_ANDROID
            AndroidLauncherIcons.AddValidationErrors(errors);
#endif
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
        [System.Serializable] private sealed class PlayGamesSetupSnapshot
        {
            public string mAppId, mWebClientId;
        }
        private static void ValidatePlayGamesSetup(List<string> errors)
        {
            // A leaderboard ID on its own does not configure the native Play Games
            // identity. Without the Web OAuth client Unity cannot request the server
            // code used to restore the same cloud account on another device.
            UnityEngine.Object asset=Resources.Load("PlayGamesSettings");
            if(asset==null)
            {
                errors.Add("Google Play Games setup missing. Run Window > Google Play Games > Setup > Android Setup.");
                return;
            }
            try
            {
                var values=JsonUtility.FromJson<PlayGamesSetupSnapshot>(JsonUtility.ToJson(asset));
                if(values?.mAppId!="1029475752167")
                    errors.Add("Google Play Games App ID does not match the published game. Run Android Setup with the Play Console resources.");
                if(string.IsNullOrWhiteSpace(values?.mWebClientId))
                    errors.Add("Google Play Games Web Client ID is missing. Enter the Game Server OAuth web client in Android Setup before building.");
            }
            catch(System.Exception)
            {
                errors.Add("Cannot read Google Play Games settings; regenerate them through Android Setup.");
            }
            string manifest="Assets/Plugins/Android/GooglePlayGamesManifest.androidlib/AndroidManifest.xml";
            if(!File.Exists(manifest)||!File.ReadAllText(manifest).Contains("1029475752167"))
                errors.Add("Google Play Games Android manifest is not configured with the published App ID. Run Android Setup to regenerate it.");
        }
        private static void EnsureLauncherIcon()
        {
#if UNITY_ANDROID
            AndroidLauncherIcons.Apply();
#else
            throw new BuildFailedException("Switch the active Build Profile to Android before preparing launcher icons.");
#endif
        }

        private static void ApplyDeviceCompatibilitySettings()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan,GraphicsDeviceType.OpenGLES3});
            PlayerSettings.openGLRequireES31AEP=false;
            PlayerSettings.openGLRequireES32=false;
            Debug.Log("DIMENSIONS FRONT: Android compatibility baseline applied: Vulkan + OpenGLES3 fallback, no ES3.1+AEP/ES3.2 requirement.");
        }

        private static void ApplyAndroidApplicationId()
        {
            string previous=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if(previous==AndroidApplicationId)return;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,AndroidApplicationId);
            Debug.Log("DIMENSIONS FRONT: restored Android application ID from '"+previous+"' to '"+AndroidApplicationId+"'.");
        }

        [MenuItem("DIMENSIONS FRONT/Release/Prepare Android upload")]
        public static void Prepare()
        {
            ApplyAndroidApplicationId();
            EnsureLauncherIcon();
            EditorUserBuildSettings.buildAppBundle=true;
            EditorUserBuildSettings.development=false;
            EditorUserBuildSettings.allowDebugging=false;
            EditorUserBuildSettings.connectProfiler=false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport=false;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            ApplyDeviceCompatibilitySettings();
            AndroidR8Optimization.ApplySettings();
            AssetDatabase.SaveAssets();
            Debug.Log("DIMENSIONS FRONT: release-safe Android build flags applied. Running upload preflight...");
            Validate();
        }

        [MenuItem("DIMENSIONS FRONT/Release/Build signed Android bundle")]
        public static void Build()=>BuildSignedBundle(false,false);

        [MenuItem("DIMENSIONS FRONT/Release/Rebuild signed Android bundle (clean)")]
        public static void RebuildClean()=>BuildSignedBundle(true,false);

        [MenuItem("DIMENSIONS FRONT/Development/Build Google Play shard test bundle")]
        public static void BuildGooglePlayShardTest()=>BuildSignedBundle(false,true);

        private static void BuildSignedBundle(bool forceClean,bool shardTest)
        {
            if(BuildPipeline.isBuildingPlayer)
                throw new BuildFailedException("An Android build is already running. Wait for it to finish or cancel it before retrying.");
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)
                throw new BuildFailedException("Wait for Unity to finish importing/compiling and leave Play Mode before building Android.");

            ApplyAndroidApplicationId();
            EnsureLauncherIcon();
            EditorUserBuildSettings.buildAppBundle=true;
            EditorUserBuildSettings.development=false;
            EditorUserBuildSettings.allowDebugging=false;
            EditorUserBuildSettings.connectProfiler=false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport=false;
            ApplyDeviceCompatibilitySettings();
            AndroidR8Optimization.ApplySettings();
            AssetDatabase.SaveAssets();
            Validate();

            var scenes=new List<string>();
            foreach(var scene in EditorBuildSettings.scenes)if(scene.enabled)scenes.Add(scene.path);
            Directory.CreateDirectory("Builds/Android");

            bool clean=forceClean||SessionState.GetBool(InterruptedBuildKey,false);
            string prefix=shardTest?"DIMENSIONS-FRONT-IAP-TEST-":"DIMENSIONS-FRONT-";
            string outputPath="Builds/Android/"+prefix+PlayerSettings.Android.bundleVersionCode+".aab";
            var options=new BuildPlayerOptions
            {
                scenes=scenes.ToArray(),
                target=BuildTarget.Android,
                options=clean?BuildOptions.CleanBuildCache:BuildOptions.None,
                locationPathName=outputPath
            };
            if(shardTest)options.extraScriptingDefines=new[]{"DF_GOOGLE_PLAY_SHARD_TEST"};

            // Keep the retry marker on cancellation, failure or an exception. SessionState
            // survives script reloads; after restarting Unity the explicit clean menu is available.
            SessionState.SetBool(InterruptedBuildKey,true);
            Debug.Log("DIMENSIONS FRONT: starting "+(clean?"clean":"incremental")+" signed Android bundle build: "+outputPath+
                ". If progress stops, use Release/Show Unity build log to inspect Editor.log.");
            try
            {
                BuildReport report=BuildPipeline.BuildPlayer(options);
                if(report==null)
                    throw new BuildFailedException("Android build returned no report. Inspect Editor.log; the next attempt will use a clean build.");
                if(report.summary.result==BuildResult.Cancelled)
                {
                    Debug.LogWarning("DIMENSIONS FRONT: Android build cancelled. No new bundle was confirmed. The next attempt in this editor session will use a clean build.");
                    return;
                }
                if(report.summary.result!=BuildResult.Succeeded)
                    throw new BuildFailedException("DIMENSIONS FRONT Android build failed with "+report.summary.totalErrors+
                        " errors. Inspect Editor.log; the next attempt will use a clean build.");

                SessionState.EraseBool(InterruptedBuildKey);
                string output=Path.GetFullPath(report.summary.outputPath);
                if(shardTest)
                    Debug.LogWarning("DIMENSIONS FRONT: INTERNAL GOOGLE PLAY SHARD TEST bundle created at "+output+
                        ". This build bypasses the account backend for shard fulfillment and credits device-local test shards. "+
                        "Upload only to a controlled Play test track. The normal Release build does not include this bypass.");
                else
                    Debug.Log("DIMENSIONS FRONT: signed Android bundle created at "+output+". Compatibility manifest snapshots are in Builds/Android.");
                Debug.Log("DIMENSIONS FRONT: Android build completed in "+report.summary.totalTime.TotalSeconds.ToString("F1")+" seconds.");
                EditorUtility.RevealInFinder(output);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("DIMENSIONS FRONT/Release/Show Unity build log")]
        public static void ShowBuildLog()
        {
            string log=Application.consoleLogPath;
            if(string.IsNullOrWhiteSpace(log)||!File.Exists(log))
            {
                Debug.LogWarning("DIMENSIONS FRONT: Unity Editor.log is not available on this machine.");
                return;
            }
            EditorUtility.RevealInFinder(log);
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


