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
    public sealed class SelectedAbilityVfxBuilder:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX",Output="Assets/Alterverse/Resources/CombatVfx/SelectedAbilities",Version="Selected ability native compositions 2";
        public const string SmokeSource=Source+"/Prefabs/Environment/Smoke/White/SmokeBurstWhiteSoft.prefab";
        public const string FireSource=Source+"/Prefabs/Combat/Explosions/NovaExplosion/ExplosionNovaFire.prefab";
        private static bool building;public int callbackOrder=>-450;
        static SelectedAbilityVfxBuilder(){EditorApplication.delayCall+=Auto;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Auto();};}
        private static void Auto()
        {if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder(Source))return;try{Build();}catch(Exception e){Debug.LogError("DIMENSIONS FRONT VFX: selected ability import: "+e.Message);}}
        public void OnPreprocessBuild(BuildReport report){try{Build();}catch(Exception e){throw new BuildFailedException(e.Message);}}
        private static string Normal(string s)=>new string((s??"").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static bool Allowed(string p)=>p.StartsWith(Source+"/",StringComparison.OrdinalIgnoreCase)&&!Normal(p).Contains("2dalternate")&&!Normal(p).Contains("demo");
        private static string Pick(string[] paths,Func<string,bool> family,string preferred,string label)
        {
            var candidates=paths.Where(p=>family(Normal(p))).ToArray();
            var selected=candidates.OrderBy(p=>family(Normal(Path.GetFileNameWithoutExtension(p)))?0:1).ThenBy(p=>Normal(p).Contains(preferred)?0:1).ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            if(selected==null)throw new FileNotFoundException(label+" native prefab not found. Matching imported candidates: "+string.Join(", ",paths.Where(p=>Normal(p).Contains(label.Split(' ')[0].ToLowerInvariant()))));
            return selected;
        }
        [MenuItem("DIMENSIONS FRONT/VFX/Prepare selected Timmy Zhaev Auren Brakk effects only")]
        public static void Build()
        {
            if(building)return;if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var all=AssetDatabase.FindAssets("t:Prefab",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).Where(Allowed).ToArray();
            var paths=new[]{SmokeSource,
                Pick(all,n=>n.Contains("blood")&&(n.Contains("spiky")||n.Contains("spike"))&&n.Contains("explos"),"blood","Spiky Blood Explosion"),
                Pick(all,n=>n.Contains("lightning")&&n.Contains("strik"),"purple","Lightning Strike"),
                Pick(all,n=>n.Contains("magiccircle")&&!n.Contains("projectile")&&!n.Contains("missile"),"yellow","Magic Circle"),FireSource,
                Pick(all,n=>n.Contains("stun")&&n.Contains("star"),"circl","Stunning Circling Stars")};
            var sources=paths.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
            for(int i=0;i<sources.Length;i++)if(sources[i]==null)throw new FileNotFoundException("Selected native source missing: "+paths[i]);
            var sound=StarsSound(sources[SelectedAbilityVfx.Stars]);
            var shader=Shader.Find("DimensionsFront/SelectedAbilityParticles");if(shader==null)throw new InvalidOperationException("Selected ability URP shader missing.");
            string signature=Version+":"+string.Join(":",paths.Select(p=>p+"="+AssetDatabase.GetAssetDependencyHash(p)))+":"+
                (sound!=null?AssetDatabase.GetAssetPath(sound)+"="+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(sound)):"no-stars-audio")+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(shader));
            var old=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");if(old!=null&&old.GetComponent<SelectedAbilityVfx>()?.sourceSignature==signature)return;
            building=true;var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();root=new GameObject("DF_SelectedAbility_Library");SceneManager.MoveGameObjectToScene(root,preview);root.SetActive(false);
                var data=root.AddComponent<SelectedAbilityVfx>();var entries=new SelectedAbilityVfx.Entry[6];var cache=new Dictionary<Material,Material>();
                for(int i=0;i<6;i++)entries[i]=Prepare(sources[i],i,paths[i],shader,cache);
                data.smoke=entries[0];data.blood=entries[1];data.strike=entries[2];data.circle=entries[3];data.fire=entries[4];data.stars=entries[5];data.stars.sound=sound;data.sourceSignature=signature;
                if(PrefabUtility.SaveAsPrefabAsset(root,Output+"/Library.prefab")==null)throw new IOException("Could not save selected ability VFX library.");
                AssetDatabase.SaveAssets();Debug.Log("DIMENSIONS FRONT VFX: selected abilities PREPARED; "+string.Join("; ",paths)+"; starsSound="+(sound!=null?AssetDatabase.GetAssetPath(sound):"<missing native clip>"));
                if(sound==null)Debug.LogWarning("DIMENSIONS FRONT VFX: Stunning Circling Stars has no native referenced/stun-family sound; no unrelated clip or trimmed recording was substituted.");
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
        private static AudioClip StarsSound(GameObject source)
        {
            foreach(var c in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(c==null)continue;var p=new SerializedObject(c).GetIterator();
                while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue is AudioClip clip&&Allowed(AssetDatabase.GetAssetPath(clip)))return clip;
            }
            foreach(var audio in source.GetComponentsInChildren<AudioSource>(true))if(audio.clip!=null&&Allowed(AssetDatabase.GetAssetPath(audio.clip)))return audio.clip;
            var path=AssetDatabase.FindAssets("t:AudioClip",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).Where(Allowed)
                .Where(p=>new[]{"stun","dizzy","confus","circlingstars","twinkl","sparkle"}.Any(w=>Normal(p).Contains(w))).OrderBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            return path==null?null:AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        private static SelectedAbilityVfx.Entry Prepare(GameObject source,int kind,string path,Shader shader,Dictionary<Material,Material> cache)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("DF_Selected_"+kind);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var native=Object.Instantiate(source,root.transform,false);native.SetActive(true);
                foreach(var t in native.GetComponentsInChildren<Transform>(true)){GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);t.gameObject.layer=0;t.gameObject.tag="Untagged";}
                foreach(var c in native.GetComponentsInChildren<Component>(true))if(c!=null&&!(c is Transform)&&!(c is ParticleSystem)&&!(c is ParticleSystemRenderer)&&!(c is MeshRenderer)&&!(c is MeshFilter))Object.DestroyImmediate(c);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);if(systems.Length==0)throw new InvalidOperationException(path+" contains no particles.");
                var play=new bool[systems.Length];var loops=new bool[systems.Length];float emission=0;
                for(int i=0;i<systems.Length;i++)
                {
                    var ps=systems[i];play[i]=ps.main.playOnAwake;loops[i]=ps.main.loop;ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var m=ps.main;m.loop=false;m.stopAction=ParticleSystemStopAction.None;m.scalingMode=ParticleSystemScalingMode.Hierarchy;m.simulationSpace=ParticleSystemSimulationSpace.Local;m.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;m.useUnscaledTime=false;
                    ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(29009+i*71);
                    if(play[i])emission=Mathf.Max(emission,BomberNukeBuilder.LastEmissionSeconds(ps));
                    var collision=ps.collision;collision.enabled=false;var trigger=ps.trigger;trigger.enabled=false;var lights=ps.lights;lights.enabled=false;
                }
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=BomberNukeBuilder.ConvertMaterial(materials[i],shader,cache,Output);r.sharedMaterials=materials;
                    if(r is ParticleSystemRenderer p&&p.trailMaterial!=null)p.trailMaterial=BomberNukeBuilder.ConvertMaterial(p.trailMaterial,shader,cache,Output);
                }
                float radius=BomberNukeBuilder.Measure(root,systems,play,"Selected "+path);root.SetActive(false);
                for(int i=0;i<systems.Length;i++)
                {
                    var ps=systems[i];ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var m=ps.main;
                    m.loop=(kind==SelectedAbilityVfx.Smoke||kind==SelectedAbilityVfx.Stars||kind==SelectedAbilityVfx.Strike)&&loops[i];
                    m.simulationSpace=kind==SelectedAbilityVfx.Smoke||kind==SelectedAbilityVfx.Strike?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
                }
                var saved=PrefabUtility.SaveAsPrefabAsset(root,Output+"/Part_"+kind+".prefab");if(saved==null)throw new IOException("Could not save native part "+path);
                return new SelectedAbilityVfx.Entry{prefab=saved,nativeRadius=radius,emissionSeconds=Mathf.Max(.1f,emission+.05f),source=path};
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
#endif
