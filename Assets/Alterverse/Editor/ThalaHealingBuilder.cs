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
    public sealed class ThalaHealingBuilder:IPreprocessBuildWithReport
    {
        public const string TargetSource="Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealOnceLoop.prefab";
        public const string AreaSource="Assets/Epic Toon FX/Prefabs/Interactive/Healing/HealField.prefab";
        private const string Output="Assets/Alterverse/Resources/CombatVfx/ThalaHealing";
        private const string Version="Thala marked healing field and rising crosses 2";
        private static bool building;
        public int callbackOrder=>-460;
        static ThalaHealingBuilder()
        {
            EditorApplication.delayCall+=Auto;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Auto();};
        }
        private static void Auto()
        {
            if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder("Assets/Epic Toon FX"))return;
            try{Build();}catch(Exception exception){Debug.LogError("DIMENSIONS FRONT VFX: Thala healing import: "+exception.Message);}
        }
        public void OnPreprocessBuild(BuildReport report)
        {try{Build();}catch(Exception exception){throw new BuildFailedException("Thala healing: "+exception.Message);}}
        [MenuItem("DIMENSIONS FRONT/VFX/Prepare Thala native healing only")]
        public static void Build()
        {
            if(building)return;if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var target=AssetDatabase.LoadAssetAtPath<GameObject>(TargetSource);var area=AssetDatabase.LoadAssetAtPath<GameObject>(AreaSource);
            if(target==null||area==null)throw new FileNotFoundException("Selected native healing source missing: "+(target==null?TargetSource+" ":"")+(area==null?AreaSource:""));
            var shader=Shader.Find("DimensionsFront/BomberNukeParticles");if(shader==null)throw new InvalidOperationException("Existing URP circular-footprint particle shader is missing.");
            string signature=BomberNukeBuilder.MaterialImportVersion+":"+Version+":"+TargetSource+"="+AssetDatabase.GetAssetDependencyHash(TargetSource)+":"+
                AreaSource+"="+AssetDatabase.GetAssetDependencyHash(AreaSource)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(shader));
            var previous=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");
            if(previous!=null&&previous.GetComponent<ThalaHealingVfx>()?.sourceSignature==signature)return;
            building=true;var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
                root=new GameObject("DF_ThalaHealing_Library");SceneManager.MoveGameObjectToScene(root,preview);root.SetActive(false);
                var data=root.AddComponent<ThalaHealingVfx>();var cache=new Dictionary<Material,Material>();
                data.target=Prepare(target,"TargetHeal",TargetSource,shader,cache);
                data.area=Prepare(area,"WildsurgeHealField",AreaSource,shader,cache);data.sourceSignature=signature;
                if(PrefabUtility.SaveAsPrefabAsset(root,Output+"/Library.prefab")==null)throw new IOException("Could not save Thala healing resource library.");
                AssetDatabase.SaveAssets();Debug.Log("DIMENSIONS FRONT VFX: Thala healing PREPARED; target="+TargetSource+"; Wildsurge="+AreaSource+"; source audio excluded.");
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
        private static ThalaHealingVfx.Entry Prepare(GameObject source,string name,string sourcePath,Shader shader,Dictionary<Material,Material> cache)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("DF_Thala_"+name);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var native=Object.Instantiate(source,root.transform,false);native.SetActive(true);
                foreach(var node in native.GetComponentsInChildren<Transform>(true))
                {GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);node.gameObject.layer=0;node.gameObject.tag="Untagged";}
                foreach(var component in native.GetComponentsInChildren<Component>(true))
                    if(component!=null&&!(component is Transform)&&!(component is ParticleSystem)&&!(component is ParticleSystemRenderer)&&!(component is MeshRenderer)&&!(component is MeshFilter))Object.DestroyImmediate(component);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);if(systems.Length==0)throw new InvalidOperationException(name+" has no native particle systems.");
                var autoPlay=new bool[systems.Length];float emission=0;
                for(int i=0;i<systems.Length;i++)
                {
                    var system=systems[i];autoPlay[i]=system.main.playOnAwake;system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main;
                    // Looping showcase fields become a short heal pulse. Keep emitted
                    // particles and sub-emitter tails instead of showing a fictitious heal-over-time zone.
                    if(main.loop&&(system.emission.rateOverTime.constantMax>0||system.emission.rateOverDistance.constantMax>0))
                        main.duration=Mathf.Min(main.duration,.65f*Mathf.Max(.01f,main.simulationSpeed));
                    main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.useUnscaledTime=false;
                    system.useAutoRandomSeed=false;system.randomSeed=(uint)(20309+i*67);
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
                return new ThalaHealingVfx.Entry{prefab=saved,nativeRadius=radius,emissionSeconds=Mathf.Max(.1f,emission+.05f),source=sourcePath};
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
#endif

