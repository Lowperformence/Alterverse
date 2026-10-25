#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Alterverse.Editor
{
    [InitializeOnLoad]
    public sealed class PlanetAttackSparksBuilder:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX",Output="Assets/Alterverse/Resources/CombatVfx/PlanetAttackSparks";
        private const string Version="Native planet attack Sparks 1";
        private static bool building;
        public int callbackOrder=>-420;
        static PlanetAttackSparksBuilder()
        {
            EditorApplication.delayCall+=Auto;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Auto();};
        }
        private static void Auto()
        {
            if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder(Source))return;
            try{Build();}catch(Exception e){Debug.LogError("DIMENSIONS FRONT VFX: planet attack Sparks import: "+e.Message);}
        }
        public void OnPreprocessBuild(BuildReport report)
        {try{Build();}catch(Exception e){throw new BuildFailedException("Planet attack Sparks: "+e.Message);}}
        private static string Normal(string s)=>new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        [MenuItem("DIMENSIONS FRONT/VFX/Prepare planet attack Sparks only")]
        public static void Build()
        {
            if(building)return;
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>p.StartsWith(Source+"/",StringComparison.OrdinalIgnoreCase)&&!Normal(p).Contains("2dalternate")&&!Normal(p).Contains("demo"))
                .Where(p=>Normal(Path.GetFileNameWithoutExtension(p)).Contains("sparks"))
                .OrderBy(p=>Normal(Path.GetFileNameWithoutExtension(p))=="sparks"?0:1)
                .ThenBy(p=>Normal(p).Contains("loop")?1:0).ThenBy(p=>p,StringComparer.Ordinal).ToArray();
            if(paths.Length==0)throw new FileNotFoundException("Sparks prefab not found under "+Source+". Import the purchased Epic Toon FX Sparks effect.");
            string path=paths[0];var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var shader=Shader.Find("DimensionsFront/BomberNukeParticles");
            if(original==null||shader==null)throw new InvalidOperationException("Native Sparks source or URP particle shader missing.");
            string signature=Version+":"+BomberNukeBuilder.MaterialImportVersion+":"+path+"="+AssetDatabase.GetAssetDependencyHash(path)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(shader));
            var previous=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Sparks.prefab");
            if(previous!=null&&previous.GetComponent<PlanetAttackSparksVfx>()?.sourceSignature==signature)return;
            building=true;var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
                root=new GameObject("DF_PlanetAttackSparks");SceneManager.MoveGameObjectToScene(root,preview);
                var native=Object.Instantiate(original,root.transform,false);native.SetActive(true);
                foreach(var t in native.GetComponentsInChildren<Transform>(true))
                {GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);t.gameObject.layer=0;t.gameObject.tag="Untagged";}
                foreach(var c in native.GetComponentsInChildren<Component>(true))
                    if(c!=null&&!(c is Transform)&&!(c is ParticleSystem)&&!(c is ParticleSystemRenderer)&&!(c is MeshRenderer)&&!(c is MeshFilter))Object.DestroyImmediate(c);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);
                if(systems.Length==0)throw new InvalidOperationException(path+" contains no native particles.");
                float emission=0f;
                foreach(var ps in systems)
                {
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var m=ps.main;m.loop=false;m.stopAction=ParticleSystemStopAction.None;
                    m.scalingMode=ParticleSystemScalingMode.Hierarchy;m.simulationSpace=ParticleSystemSimulationSpace.Local;
                    m.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;m.useUnscaledTime=false;
                    var collision=ps.collision;collision.enabled=false;var trigger=ps.trigger;trigger.enabled=false;var lights=ps.lights;lights.enabled=false;
                    if(m.playOnAwake)emission=Mathf.Max(emission,BomberNukeBuilder.LastEmissionSeconds(ps));
                }
                var cache=new Dictionary<Material,Material>();
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    BomberNukeBuilder.ConvertRendererMaterials(renderer,shader,cache,Output,path);
                }
                var data=root.AddComponent<PlanetAttackSparksVfx>();data.sourcePath=path;data.sourceSignature=signature;data.emissionSeconds=Mathf.Max(.1f,emission+.05f);
                root.SetActive(false);
                if(PrefabUtility.SaveAsPrefabAsset(root,Output+"/Sparks.prefab")==null)throw new IOException("Could not save prepared native Sparks.");
                AssetDatabase.SaveAssets();Debug.Log("DIMENSIONS FRONT VFX: planet attack Sparks PREPARED; "+path+"; runtime scale="+PlanetAttackSparksVfx.SizeMultiplier+".");
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
    }
}
#endif
