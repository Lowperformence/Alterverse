#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace Alterverse.EditorChecks
{
    /// <summary>Reproducible build entry point, not a claim of store approval.</summary>
    public sealed class AndroidReleaseBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder=>0;
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.Android&&(report.summary.options&BuildOptions.Development)==0)Validate();
        }
        [MenuItem("DIMENSIONS FRONT/Release/Validate Android release settings")]
        public static void Validate()
        {
            var errors=new List<string>();
            string id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if(string.IsNullOrWhiteSpace(id)||id.Contains("UnityTechnologies")||id.Contains("DefaultCompany"))errors.Add("Replace the template application identifier.");
            if(PlayerSettings.productName!="DIMENSIONS FRONT")errors.Add("Product name must be DIMENSIONS FRONT.");
            if(PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)!=ScriptingImplementation.IL2CPP)errors.Add("Android requires IL2CPP for this release configuration.");
            if((PlayerSettings.Android.targetArchitectures&AndroidArchitecture.ARM64)==0)errors.Add("Enable ARM64.");
            if((int)PlayerSettings.Android.targetSdkVersion<36)errors.Add("Pin target SDK 36 or newer; automatic SDK selection is not a reproducible release gate.");
            if(PlayerSettings.Android.bundleVersionCode<1)errors.Add("Set a positive, unused version code before uploading.");
            if(!EditorUserBuildSettings.buildAppBundle)errors.Add("Enable Android App Bundle (.aab).");
            if(!PlayerSettings.Android.useCustomKeystore||string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName)||string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))errors.Add("Owner must configure upload signing locally. Never commit passwords or signing keys.");
            bool scene=false;foreach(var entry in EditorBuildSettings.scenes)if(entry.enabled&&File.Exists(entry.path)){scene=true;break;}
            if(!scene)errors.Add("No existing enabled scene in Build Settings.");
            if(errors.Count>0)throw new BuildFailedException(string.Join("\n",errors));
            Debug.Log("DIMENSIONS FRONT: static Android settings gate passed. Device, manifest, asset and Play Console checks still required.");
        }
        [MenuItem("DIMENSIONS FRONT/Release/Build signed Android bundle")]
        public static void Build()
        {
            EditorUserBuildSettings.buildAppBundle=true;
            Validate();
            var scenes=new List<string>();foreach(var scene in EditorBuildSettings.scenes)if(scene.enabled)scenes.Add(scene.path);
            Directory.CreateDirectory("Builds/Android");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes=scenes.ToArray(),target=BuildTarget.Android,options=BuildOptions.None,
                locationPathName="Builds/Android/DIMENSIONS-FRONT-"+PlayerSettings.Android.bundleVersionCode+".aab"
            });
            if(report.summary.result!=BuildResult.Succeeded)throw new BuildFailedException("DIMENSIONS FRONT Android build failed. Inspect the Unity build log.");
        }
    }
}
#endif
