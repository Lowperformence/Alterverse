#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
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
    public sealed class UltimateLightningBuilder:IPreprocessBuildWithReport
    {
        public const string IoneSource="Assets/Epic Toon FX/Prefabs/Combat/Nova/Lightning/NovaLightningPurple.prefab";
        public const string TravelerSource="Assets/Epic Toon FX/Prefabs/Combat/Explosions/LightningExplosion/LightningExplosionYellow.prefab";
        private const string Output="Assets/Alterverse/Resources/CombatVfx/UltimateLightning";
        private const string Version="Ultimate lightning detonations 1";
        private static bool building;
        public int callbackOrder=>-470;
        static UltimateLightningBuilder()
        {
            EditorApplication.delayCall+=Auto;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Auto();};
        }
        private static void Auto()
        {
            if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder("Assets/Epic Toon FX"))return;
            try{Build();}catch(Exception exception){Debug.LogError("DIMENSIONS FRONT VFX: ultimate lightning import: "+exception.Message);}
        }
        public void OnPreprocessBuild(BuildReport report)
        {try{Build();}catch(Exception exception){throw new BuildFailedException("Ultimate lightning: "+exception.Message);}}
        [MenuItem("DIMENSIONS FRONT/VFX/Prepare Ione and Traveler lightning detonations only")]
        public static void Build()
        {
            if(building)return;if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var ione=AssetDatabase.LoadAssetAtPath<GameObject>(IoneSource);
            var traveler=AssetDatabase.LoadAssetAtPath<GameObject>(TravelerSource);
            if(ione==null||traveler==null)throw new FileNotFoundException("Selected native ultimate source missing: "+(ione==null?IoneSource+" ":"")+(traveler==null?TravelerSource:""));
            var shader=Shader.Find("DimensionsFront/BomberNukeParticles");if(shader==null)throw new InvalidOperationException("Existing URP circular-footprint particle shader is missing.");
            string signature=BomberNukeBuilder.MaterialImportVersion+":"+Version+":"+IoneSource+"="+AssetDatabase.GetAssetDependencyHash(IoneSource)+":"+
                TravelerSource+"="+AssetDatabase.GetAssetDependencyHash(TravelerSource)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(shader));
            var old=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");
            if(old!=null&&old.GetComponent<UltimateLightningVfx>()?.sourceSignature==signature)return;
            building=true;var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
                root=new GameObject("DF_UltimateLightning_Library");SceneManager.MoveGameObjectToScene(root,preview);root.SetActive(false);
                var data=root.AddComponent<UltimateLightningVfx>();var cache=new Dictionary<Material,Material>();
                data.ione=Prepare(ione,"Ione_LightningNova",IoneSource,shader,cache);
                data.traveler=Prepare(traveler,"Traveler_LightningExplosion",TravelerSource,shader,cache);
                data.sourceSignature=signature;
                if(PrefabUtility.SaveAsPrefabAsset(root,Output+"/Library.prefab")==null)throw new IOException("Could not save selected ultimate lightning resource library.");
                AssetDatabase.SaveAssets();Debug.Log("DIMENSIONS FRONT VFX: ultimate lightning PREPARED; Ione="+IoneSource+"; Traveler="+TravelerSource+"; source audio excluded.");
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
        private static UltimateLightningVfx.Entry Prepare(GameObject source,string name,string sourcePath,Shader shader,Dictionary<Material,Material> cache)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("DF_"+name);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var native=Object.Instantiate(source,root.transform,false);native.SetActive(true);
                foreach(var node in native.GetComponentsInChildren<Transform>(true))
                {GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);node.gameObject.layer=0;node.gameObject.tag="Untagged";}
                // AudioSources and demo scripts are intentionally not retained or routed.
                foreach(var component in native.GetComponentsInChildren<Component>(true))
                    if(component!=null&&!(component is Transform)&&!(component is ParticleSystem)&&!(component is ParticleSystemRenderer)&&!(component is MeshRenderer)&&!(component is MeshFilter))Object.DestroyImmediate(component);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);
                if(systems.Length==0)throw new InvalidOperationException(name+" has no native particle systems.");
                var autoPlay=new bool[systems.Length];float emission=0;
                for(int i=0;i<systems.Length;i++)
                {
                    var system=systems[i];autoPlay[i]=system.main.playOnAwake;system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main;main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.useUnscaledTime=false;
                    system.useAutoRandomSeed=false;system.randomSeed=(uint)(17309+i*61);
                    if(autoPlay[i])emission=Mathf.Max(emission,BomberNukeBuilder.LastEmissionSeconds(system));
                    var collision=system.collision;collision.enabled=false;var trigger=system.trigger;trigger.enabled=false;var lights=system.lights;lights.enabled=false;
                }
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    BomberNukeBuilder.ConvertRendererMaterials(renderer,shader,cache,Output,sourcePath);
                }
                float radius=BomberNukeBuilder.Measure(root,systems,autoPlay,name);root.SetActive(false);
                foreach(var system in systems)system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                var saved=PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");if(saved==null)throw new IOException("Could not save "+name);
                return new UltimateLightningVfx.Entry{prefab=saved,nativeRadius=radius,emissionSeconds=Mathf.Max(.1f,emission+.05f),source=sourcePath};
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
#endif

