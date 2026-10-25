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
    public sealed class AttackExplosionVfxBuilder:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX",Output="Assets/Alterverse/Resources/CombatVfx/AttackExplosions";
        private const string LegacyOutput="Assets/Alterverse/Resources/CombatVfx/PlanetAttackSparks";
        private const string Version="Native attack explosions 1";
        private static bool building;
        public int callbackOrder=>-420;
        static AttackExplosionVfxBuilder()
        {
            EditorApplication.delayCall+=Auto;
            EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Auto();};
        }
        private static void RemoveLegacySparks()
        {
            // Only our generated copy is obsolete. Never delete the purchased package.
            if(AssetDatabase.IsValidFolder(LegacyOutput))AssetDatabase.DeleteAsset(LegacyOutput);
        }
        private static void Auto()
        {
            if(building||EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            RemoveLegacySparks();
            if(!AssetDatabase.IsValidFolder(Source))return;
            foreach(AttackExplosionVfx.Kind kind in Enum.GetValues(typeof(AttackExplosionVfx.Kind)))
                try{BuildOne(kind);}catch(Exception e){Debug.LogError("DIMENSIONS FRONT VFX: planet "+kind+" import: "+e.Message);}
        }
        public void OnPreprocessBuild(BuildReport report)
        {try{Build();}catch(Exception e){throw new BuildFailedException("Planet attack explosions: "+e.Message);}}
        private static string Normal(string s)=>new string(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static string Family(AttackExplosionVfx.Kind kind)=>kind==AttackExplosionVfx.Kind.FluxNuke?"nukeverticalexplosion":"fireballexplosion";

        internal static int SourceRank(string path,AttackExplosionVfx.Kind kind)
        {
            if(string.IsNullOrEmpty(path))return -1;
            path=path.Replace((char)92,'/');
            string normalized=Normal(path),family=Family(kind);
            if(!path.StartsWith(Source+"/",StringComparison.OrdinalIgnoreCase)||normalized.Contains("demo")||
                path.Split('/').Any(p=>Normal(p).StartsWith("2d",StringComparison.Ordinal)))return -1;
            string name=Normal(Path.GetFileNameWithoutExtension(path));
            if(name==family+"fire")return 0;
            if(name==family)return 1;
            if(name.StartsWith(family,StringComparison.Ordinal))return 2;
            var parts=path.Split('/');
            for(int i=0;i<parts.Length-1;i++)
                if(Normal(parts[i]).StartsWith(family,StringComparison.Ordinal))return 3;
            return -1;
        }

        internal static string FindSource(AttackExplosionVfx.Kind kind)
        {
            var all=AssetDatabase.FindAssets("t:Prefab",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var ranked=all.Where(p=>SourceRank(p,kind)>=0).OrderBy(p=>SourceRank(p,kind))
                .ThenBy(p=>Normal(p).Contains("loop")?1:0).ThenBy(p=>p,StringComparer.Ordinal);
            foreach(string path in ranked)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(prefab!=null&&prefab.GetComponentsInChildren<ParticleSystem>(true).Length>0)return path;
            }
            string hint=kind==AttackExplosionVfx.Kind.FluxNuke?"nuke":"fireball";
            string candidates=string.Join(", ",all.Where(p=>Normal(p).Contains(hint)).Take(16));
            throw new FileNotFoundException("No native 3D "+Family(kind)+" particle prefab found under "+Source+
                ". Related imported prefabs: "+(candidates.Length>0?candidates:"<none>")+".");
        }

        [MenuItem("DIMENSIONS FRONT/VFX/Prepare attack explosions only")]
        public static void Build()
        {
            if(building)return;
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            RemoveLegacySparks();
            foreach(AttackExplosionVfx.Kind kind in Enum.GetValues(typeof(AttackExplosionVfx.Kind)))BuildOne(kind);
        }

        internal static void BuildOne(AttackExplosionVfx.Kind kind)
        {
            if(building)return;
            string path=FindSource(kind),output=Output+"/"+kind;
            var original=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var shader=Shader.Find("DimensionsFront/AttackExplosionParticles");
            if(original==null||shader==null)throw new InvalidOperationException("Native planet explosion source or URP particle shader missing.");
            string signature=Version+":"+BomberNukeBuilder.MaterialImportVersion+":"+path+"="+AssetDatabase.GetAssetDependencyHash(path)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(shader));
            string prefabPath=Output+"/"+kind+".prefab";
            var previous=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(previous!=null&&previous.GetComponent<AttackExplosionVfx>()?.sourceSignature==signature)return;
            building=true;var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                Directory.CreateDirectory(output+"/Materials");AssetDatabase.Refresh();
                root=new GameObject("DF_AttackExplosion_"+kind);SceneManager.MoveGameObjectToScene(root,preview);
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
                    BomberNukeBuilder.ConvertRendererMaterials(renderer,shader,cache,output,path);
                }
                var data=root.AddComponent<AttackExplosionVfx>();data.sourcePath=path;data.sourceSignature=signature;data.emissionSeconds=Mathf.Max(.1f,emission+.05f);
                root.SetActive(false);
                if(PrefabUtility.SaveAsPrefabAsset(root,prefabPath)==null)throw new IOException("Could not save prepared native "+kind+" explosion.");
                AssetDatabase.SaveAssets();Debug.Log("DIMENSIONS FRONT VFX: planet "+kind+" PREPARED; "+path+"; runtime scale="+AttackExplosionVfx.SizeMultiplier(kind)+".");
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
    }
}
#endif
