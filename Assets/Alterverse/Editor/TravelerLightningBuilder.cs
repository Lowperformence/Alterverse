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
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Alterverse.Editor
{
    [InitializeOnLoad]
    public sealed class TravelerLightningBuilder:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX",Output="Assets/Alterverse/Resources/CombatVfx/TravelerLightning",Version="Traveler lightning shots with Chrono arrival Nova 2";
        public const string ProjectileSource=Source+"/Prefabs/Combat/Missiles/Lightning/LightningMissileYellow.prefab";
        public const string MuzzleSource=Source+"/Prefabs/Combat/Muzzleflash/LightningMuzzle/LightningMuzzleYellow.prefab";
        public const string ImpactSource=Source+"/Prefabs/Combat/Explosions/LightningExplosion/LightningExplosionYellow.prefab";
        public const string CastSound=Source+"/Sound/etfx_shoot_lightning.wav";
        public const string NovaSource=Source+"/Prefabs/Combat/Magic/Nova/MagicNovaYellow.prefab";
        private static bool building;public int callbackOrder=>-440;
        static TravelerLightningBuilder(){EditorApplication.delayCall+=Auto;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Auto();};}
        private static void Auto(){if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder(Source))return;try{Build();}catch(Exception e){Debug.LogError("DIMENSIONS FRONT VFX: Traveler lightning import: "+e.Message);}}
        public void OnPreprocessBuild(BuildReport report){try{Build();}catch(Exception e){throw new BuildFailedException(e.Message);}}
        private static string Normal(string s)=>new string((s??"").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static bool Allowed(string p)=>!string.IsNullOrEmpty(p)&&p.StartsWith(Source+"/",StringComparison.OrdinalIgnoreCase)&&!Normal(p).Contains("2dalternate")&&!Normal(p).Contains("demo");
        private static GameObject Reference(GameObject source,string[] roles)
        {
            foreach(var c in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(c==null)continue;var p=new SerializedObject(c).GetIterator();
                while(p.NextVisible(true))
                {
                    if(p.propertyType!=SerializedPropertyType.ObjectReference||!roles.Any(r=>Normal(p.name).Contains(r)))continue;
                    var value=p.objectReferenceValue as GameObject??(p.objectReferenceValue as Component)?.gameObject;
                    string path=value!=null?AssetDatabase.GetAssetPath(value):null;string n=Normal(Path.GetFileNameWithoutExtension(path??""));
                    if(value!=null&&Allowed(path)&&(n.Contains("lightning")||n.Contains("electric"))&&!new[]{"purple","blue","green","red","pink","orange"}.Any(n.Contains)&&value.GetComponentsInChildren<ParticleSystem>(true).Length>0)return value;
                }
            }
            return null;
        }
        private static AudioClip Audio(GameObject source,string[] roles,bool includeSources=true)
        {
            foreach(var c in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(c==null)continue;var p=new SerializedObject(c).GetIterator();
                while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&roles.Any(r=>Normal(p.name).Contains(r))&&p.objectReferenceValue is AudioClip clip&&Allowed(AssetDatabase.GetAssetPath(clip)))return clip;
            }
            if(includeSources)foreach(var a in source.GetComponentsInChildren<AudioSource>(true))if(a.clip!=null&&Allowed(AssetDatabase.GetAssetPath(a.clip)))return a.clip;
            return null;
        }
        private static AudioClip Family(string[] paths,string[] roles,bool permitGeneric=false)
        {
            var path=paths.Where(p=>roles.Any(r=>Normal(Path.GetFileNameWithoutExtension(p)).Contains(r)))
                .Where(p=>permitGeneric||new[]{"lightning","electric","thunder"}.Any(w=>Normal(p).Contains(w)))
                .Where(p=>!new[]{"nuke","blood","fire","sword","cartoon"}.Any(w=>Normal(Path.GetFileNameWithoutExtension(p)).Contains(w)))
                .OrderBy(p=>new[]{"lightning","electric","thunder"}.Any(w=>Normal(p).Contains(w))?0:Normal(p).Contains("magic")?1:2)
                .ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            return path==null?null:AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        private static GameObject SnareNova()
        {
            var exact=AssetDatabase.LoadAssetAtPath<GameObject>(NovaSource);if(exact!=null)return exact;
            var all=AssetDatabase.FindAssets("t:Prefab",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).Where(Allowed).ToArray();
            var path=all.Where(p=>Normal(Path.GetFileNameWithoutExtension(p)).Contains("nova"))
                .Where(p=>!new[]{"missile","projectile","small","lightning","fire","blood"}.Any(w=>Normal(p).Contains(w)))
                .OrderBy(p=>Normal(p).Contains("yellow")?0:Normal(p).Contains("white")?1:2)
                .ThenBy(p=>Normal(p).Contains("magicnova")?0:1).ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            if(path==null)throw new FileNotFoundException("Chrono Snare requires a native full Nova detonation, not a Nova projectile/Small/Lightning variant. Preferred "+NovaSource+". Imported Nova candidates: "+string.Join(", ",all.Where(p=>Normal(p).Contains("nova"))));
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        [MenuItem("DIMENSIONS FRONT/VFX/Prepare Traveler lightning shot chains only")]
        public static void Build()
        {
            if(building)return;if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var projectile=AssetDatabase.LoadAssetAtPath<GameObject>(ProjectileSource);
            if(projectile==null)throw new FileNotFoundException("Selected native lightning projectile missing: "+ProjectileSource);
            var muzzle=Reference(projectile,new[]{"muzzle","flash"})??AssetDatabase.LoadAssetAtPath<GameObject>(MuzzleSource);
            var impact=Reference(projectile,new[]{"impact","hit","explosion"})??AssetDatabase.LoadAssetAtPath<GameObject>(ImpactSource);
            if(muzzle==null||impact==null)throw new FileNotFoundException("Complete lightning chain missing "+(muzzle==null?MuzzleSource+" ":"")+(impact==null?ImpactSource:""));
            var nova=SnareNova();
            var clips=AssetDatabase.FindAssets("t:AudioClip",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).Where(Allowed).ToArray();
            var cast=Audio(muzzle,new[]{"cast","shoot","launch","muzzle"})??Audio(projectile,new[]{"cast","shoot","launch"})??AssetDatabase.LoadAssetAtPath<AudioClip>(CastSound)??Family(clips,new[]{"shoot","cast","launch"});
            var hit=Audio(impact,new[]{"impact","hit","explosion","contact"})??Audio(projectile,new[]{"impact","hit","explosion"})??Family(clips,new[]{"impact","hit","explosion"});
            if(hit==null)hit=Family(clips,new[]{"impact","hit","explosion"},true);
            var windup=Audio(projectile,new[]{"windup","charge","prepare"},false)??Family(clips,new[]{"windup","charge","prepare"});
            if(cast==null||hit==null)throw new FileNotFoundException("Traveler requires complete package shot/impact audio. Missing "+(cast==null?"cast ":"")+(hit==null?"impact ":"")+". Actual imported clips: "+string.Join(", ",clips));
            var shader=Shader.Find("DimensionsFront/SmallBulletParticles");if(shader==null)throw new InvalidOperationException("Existing URP projectile footprint shader is missing.");
            var assets=new Object[]{projectile,muzzle,impact,nova,cast,hit,windup,shader};
            string signature=Version+":"+string.Join(":",assets.Where(a=>a!=null).Select(a=>AssetDatabase.GetAssetPath(a)+"="+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(a))));
            var old=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");if(old!=null&&old.GetComponent<TravelerLightningVfx>()?.sourceSignature==signature)return;
            building=true;var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();root=new GameObject("DF_TravelerLightning_Library");SceneManager.MoveGameObjectToScene(root,preview);root.SetActive(false);
                var data=root.AddComponent<TravelerLightningVfx>();var cache=new Dictionary<Material,Material>();
                data.lightning=new TravelerLightningVfx.Chain{muzzle=Prepare(muzzle,"Muzzle",false,shader,cache),projectile=Prepare(projectile,"Projectile",true,shader,cache),impact=Prepare(impact,"Impact",false,shader,cache),snareNova=Prepare(nova,"ChronoNova",false,shader,cache),castClip=cast,impactClip=hit,windupClip=windup};
                data.lightning.sources="muzzle="+AssetDatabase.GetAssetPath(muzzle)+"; projectile="+ProjectileSource+"; impact="+AssetDatabase.GetAssetPath(impact)+"; chronoNova="+AssetDatabase.GetAssetPath(nova)+"; castAudio="+AssetDatabase.GetAssetPath(cast)+"; impactAudio="+AssetDatabase.GetAssetPath(hit);
                data.sourceSignature=signature;if(PrefabUtility.SaveAsPrefabAsset(root,Output+"/Library.prefab")==null)throw new IOException("Could not save Traveler native lightning library.");
                AssetDatabase.SaveAssets();Debug.Log("DIMENSIONS FRONT VFX: Traveler complete lightning chain PREPARED; "+data.lightning.sources);
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
        private static TravelerLightningVfx.Effect Prepare(GameObject source,string name,bool moving,Shader shader,Dictionary<Material,Material> cache)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("DF_Traveler_"+name);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var native=Object.Instantiate(source,root.transform,false);native.SetActive(true);
                foreach(var node in native.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
                foreach(var component in native.GetComponentsInChildren<Component>(true))
                    if(component!=null&&!(component is Transform)&&!(component is ParticleSystem)&&!(component is ParticleSystemRenderer)&&!(component is MeshRenderer)&&!(component is MeshFilter))Object.DestroyImmediate(component);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);if(systems.Length==0)throw new InvalidOperationException(name+" has no native particle systems.");
                var play=new bool[systems.Length];var loops=new bool[systems.Length];float emission=0;
                for(int i=0;i<systems.Length;i++)
                {
                    var system=systems[i];play[i]=system.main.playOnAwake;loops[i]=system.main.loop;system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main;main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;main.useUnscaledTime=false;
                    system.useAutoRandomSeed=false;system.randomSeed=(uint)(31019+i*53);
                    var burst=system.emission;if(moving&&burst.rateOverDistance.constantMax>0&&burst.burstCount==0)burst.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                    if(play[i])emission=Mathf.Max(emission,BomberNukeBuilder.LastEmissionSeconds(system));
                    var collision=system.collision;collision.enabled=false;var trigger=system.trigger;trigger.enabled=false;var lights=system.lights;lights.enabled=false;
                }
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++)materials[i]=BomberNukeBuilder.ConvertMaterial(materials[i],shader,cache,Output);
                    renderer.sharedMaterials=materials;
                    if(renderer is ParticleSystemRenderer ps&&ps.trailMaterial!=null)ps.trailMaterial=BomberNukeBuilder.ConvertMaterial(ps.trailMaterial,shader,cache,Output);
                }
                float radius=BomberNukeBuilder.Measure(root,systems,play,"Traveler "+name);root.SetActive(false);
                for(int i=0;i<systems.Length;i++)
                {
                    var system=systems[i];system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main;main.loop=moving&&loops[i];main.simulationSpace=moving?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
                }
                var saved=PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");
                if(saved==null)throw new IOException("Could not save "+name);
                return new TravelerLightningVfx.Effect{prefab=saved,radius=radius,emissionSeconds=Mathf.Max(.05f,emission+.05f)};
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
#endif
