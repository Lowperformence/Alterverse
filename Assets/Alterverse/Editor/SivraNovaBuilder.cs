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
    // Discover actual imported blue Nova/Nova Small chains; never relabel another family.
    [InitializeOnLoad]
    public sealed class SivraNovaBuilder:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX",Output="Assets/Alterverse/Resources/CombatVfx/SivraNova";
        private const string Version="Sivra blue Nova chains 1";
        private static bool building;
        public int callbackOrder=>-480;
        private sealed class Sources
        {
            public GameObject projectile,muzzle,impact,status;
            public AudioClip castClip,impactClip,windupClip;
            public string label;
        }
        static SivraNovaBuilder()
        {
            EditorApplication.delayCall+=Auto;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Auto();};
        }
        private static void Auto()
        {
            if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder(Source))return;
            try{Build();}catch(Exception exception){Debug.LogError("DIMENSIONS FRONT VFX: Sivra Nova import: "+exception.Message);}
        }
        public void OnPreprocessBuild(BuildReport report)
        {try{Build();}catch(Exception exception){throw new BuildFailedException("Sivra Nova: "+exception.Message);}}
        [MenuItem("DIMENSIONS FRONT/VFX/Prepare Sivra blue Nova chains only")]
        public static void Build()
        {
            if(building)return;if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var all=AssetDatabase.FindAssets("t:Prefab",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(Allowed).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            var small=Resolve(all,true);var large=Resolve(all,false);
            var shader=Shader.Find("DimensionsFront/SmallBulletParticles");if(shader==null)throw new InvalidOperationException("The existing URP particle footprint shader is missing.");
            var assets=new Object[]{small.projectile,small.muzzle,small.impact,small.status,large.projectile,large.muzzle,large.impact,
                small.castClip,small.impactClip,small.windupClip,large.castClip,large.impactClip,large.windupClip,shader};
            string signature=BomberNukeBuilder.MaterialImportVersion+":"+Version+":"+string.Join(":",assets.Where(a=>a!=null).Select(a=>AssetDatabase.GetAssetPath(a)+"="+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(a))));
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");
            if(existing!=null&&existing.GetComponent<SivraNovaVfx>()?.sourceSignature==signature)return;
            building=true;var preview=EditorSceneManager.NewPreviewScene();GameObject root=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
                root=new GameObject("DF_SivraNova_Library");SceneManager.MoveGameObjectToScene(root,preview);root.SetActive(false);
                var library=root.AddComponent<SivraNovaVfx>();var cache=new Dictionary<Material,Material>();
                library.novaSmall=PrepareChain(small,"NovaSmall",true,shader,cache);
                library.nova=PrepareChain(large,"Nova",false,shader,cache);library.sourceSignature=signature;
                if(PrefabUtility.SaveAsPrefabAsset(root,Output+"/Library.prefab")==null)throw new IOException("Could not save Sivra Nova library.");
                AssetDatabase.SaveAssets();
                Debug.Log("DIMENSIONS FRONT VFX: Sivra blue Nova chains PREPARED; Nova Small: "+library.novaSmall.sources+"; Nova: "+library.nova.sources);
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
        private static string Normal(string value)=>new string((value??"").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static bool Allowed(string path)=>!string.IsNullOrEmpty(path)&&path.StartsWith(Source+"/",StringComparison.OrdinalIgnoreCase)&&
            !Normal(path).Contains("2dalternate")&&!Normal(path).Contains("demo");
        private static bool Small(string path)
        {string value=Normal(path);return value.Contains("nova")&&value.Contains("small");}
        private static bool Blue(string path)=>Normal(path).Contains("blue");
        private static bool NonBlueColour(string path)
        {
            string name=Normal(Path.GetFileNameWithoutExtension(path));
            return new[]{"white","red","fire","green","pink","purple","orange","yellow"}.Any(name.Contains);
        }
        private static int Role(string path)
        {
            string name=Normal(Path.GetFileNameWithoutExtension(path)).Replace("white","");
            if(name.Contains("muzzle")||name.Contains("flash"))return 1;
            if(name.Contains("impact")||name.Contains("hit")||name.Contains("explosion"))return 2;
            string category=Normal(Path.GetDirectoryName(path)).Replace("white","");
            if(category.Contains("muzzle"))return 1;
            if(category.Contains("impact")||category.Contains("hit")||category.Contains("explosion"))return 2;
            return 0;
        }
        private static Sources Resolve(string[] all,bool small)
        {
            string family=small?"Nova Small":"Nova";
            var discovered=all.Where(p=>Normal(p).Contains("nova")&&Small(p)==small).ToArray();
            var blue=discovered.Where(Blue).ToArray();
            string projectilePath=blue.Where(p=>Role(p)==0)
                .OrderBy(p=>Normal(p).Contains("missile")||Normal(p).Contains("projectile")?0:1)
                .ThenBy(p=>Normal(Path.GetFileNameWithoutExtension(p))==(small?"novasmallblue":"novablue")?0:1)
                .ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            var result=new Sources{label=family,projectile=projectilePath==null?null:AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath)};
            var sharedBlue=all.Where(p=>Blue(p)&&Normal(p).Contains("nova")&&!Small(p)).ToArray();
            result.muzzle=Reference(result.projectile,new[]{"muzzle","flash"})??Pick(blue,1)??Pick(sharedBlue,1);
            result.impact=Reference(result.projectile,new[]{"impact","hit","explosion"})??Pick(blue,2)??Pick(sharedBlue,2);
            // A known shared magic muzzle is a deliberate stage choice, not a substitute
            // for either explicitly requested Nova projectile family.
            if(result.muzzle==null)
                result.muzzle=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"/Prefabs/Combat/Muzzleflash/MagicMuzzle/MagicMuzzleBlue.prefab");
            result.status=small?Reference(result.projectile,new[]{"status","snare","debuff"})??result.muzzle:null;
            if(result.projectile==null||result.muzzle==null||result.impact==null)
                throw new InvalidOperationException("Blue "+family+" missing "+(result.projectile==null?"projectile ":"")+
                    (result.muzzle==null?"muzzle ":"")+(result.impact==null?"impact ":"")+". Actual "+family+" family prefabs: "+
                    (discovered.Length==0?"<none>":string.Join(", ",discovered))+". No other family or colour was substituted.");
            var clips=AssetDatabase.FindAssets("t:AudioClip",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).Where(Allowed).ToArray();
            result.castClip=Audio(result.muzzle,new[]{"cast","shoot","launch","fire","muzzle"})??Audio(result.projectile,new[]{"cast","shoot","launch","fire"})??AudioFamily(clips,small,0);
            result.impactClip=Audio(result.impact,new[]{"impact","hit","explosion","contact"})??Audio(result.projectile,new[]{"impact","hit","explosion"})??AudioFamily(clips,small,1);
            result.windupClip=Audio(result.projectile,new[]{"windup","charge","charging","prepare"},false)??AudioFamily(clips,small,2);
            if(small)
            {
                result.impactClip=result.impactClip??AudioFamily(clips,false,1);
                result.windupClip=result.windupClip??AudioFamily(clips,false,2);
            }
            // These source files were previously verified in the purchased package. This is
            // a deliberate magic/mystic-shot choice, never an invented Nova-specific clip.
            if(result.castClip==null)
            {
                string path=Source+"/Sound/"+(small?"etfx_shoot_magic.wav":"etfx_shoot_mystic.wav");
                result.castClip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if(result.castClip!=null)Debug.Log("DIMENSIONS FRONT VFX: "+family+" uses purchased "+path+" as its selected shot sound.");
            }
            if(result.impactClip==null)
            {
                string path=clips.Where(p=>
                    (Normal(p).Contains("impact")||Normal(p).Contains("explosion"))&&
                    (Normal(p).Contains("nova")||Normal(p).Contains("magic")||Normal(p).Contains("mystic")))
                    .OrderBy(p=>Normal(p).Contains("nova")?0:Normal(p).Contains(small?"magic":"mystic")?1:2)
                    .ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
                if(path!=null)
                {
                    result.impactClip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    Debug.Log("DIMENSIONS FRONT VFX: "+family+" uses purchased "+path+" as its selected impact sound.");
                }
            }
            if(result.castClip==null||result.impactClip==null)
                Debug.LogWarning("DIMENSIONS FRONT VFX: "+family+" native audio unavailable for "+(result.castClip==null?"cast ":"")+
                    (result.impactClip==null?"impact ":"")+"; no recording was fabricated or trimmed.");
            return result;
        }
        private static GameObject Pick(string[] paths,int role)
        {string path=paths.FirstOrDefault(p=>Role(p)==role);return path==null?null:AssetDatabase.LoadAssetAtPath<GameObject>(path);}
        private static GameObject Reference(GameObject source,string[] roles)
        {
            if(source==null)return null;
            foreach(var component in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(component==null)continue;var property=new SerializedObject(component).GetIterator();
                while(property.NextVisible(true))
                {
                    if(property.propertyType!=SerializedPropertyType.ObjectReference||!roles.Any(r=>Normal(property.name).Contains(r)))continue;
                    var value=property.objectReferenceValue as GameObject??(property.objectReferenceValue as Component)?.gameObject;
                    string path=value!=null?AssetDatabase.GetAssetPath(value):null;
                    if(value!=null&&Allowed(path)&&!NonBlueColour(path)&&value.GetComponentsInChildren<ParticleSystem>(true).Length>0)return value;
                }
            }
            return null;
        }
        private static AudioClip Audio(GameObject source,string[] roles,bool includeSources=true)
        {
            if(source==null)return null;
            foreach(var component in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(component==null)continue;var property=new SerializedObject(component).GetIterator();
                while(property.NextVisible(true))
                    if(property.propertyType==SerializedPropertyType.ObjectReference&&roles.Any(r=>Normal(property.name).Contains(r)))
                    {
                        var clip=property.objectReferenceValue as AudioClip;
                        if(clip!=null&&Allowed(AssetDatabase.GetAssetPath(clip)))return clip;
                    }
            }
            if(includeSources)foreach(var audio in source.GetComponentsInChildren<AudioSource>(true))
                if(audio.clip!=null&&Allowed(AssetDatabase.GetAssetPath(audio.clip)))return audio.clip;
            return null;
        }
        private static AudioClip AudioFamily(string[] paths,bool small,int phase)
        {
            string[] words=phase==0?new[]{"shoot","cast","launch","muzzle"}:phase==1?new[]{"impact","hit","explosion"}:new[]{"windup","charge","prepare"};
            string path=paths.Where(p=>Normal(p).Contains("nova")&&Small(p)==small&&words.Any(w=>Normal(p).Replace("white","").Contains(w)))
                .OrderBy(p=>Blue(p)?0:NonBlueColour(p)?2:1).ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            return path==null?null:AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        private static SivraNovaVfx.Chain PrepareChain(Sources source,string prefix,bool small,Shader shader,Dictionary<Material,Material> cache)
        {
            var result=new SivraNovaVfx.Chain();
            result.projectile=Prepare(source.projectile,prefix+"_Projectile",true,shader,cache);
            result.muzzle=Prepare(source.muzzle,prefix+"_Muzzle",false,shader,cache);
            result.impact=Prepare(source.impact,prefix+"_Impact",false,shader,cache);
            if(small)result.status=source.status==source.muzzle?result.muzzle:Prepare(source.status,prefix+"_Status",false,shader,cache);
            result.castClip=source.castClip;result.impactClip=source.impactClip;result.windupClip=source.windupClip;
            result.sources="projectile="+AssetDatabase.GetAssetPath(source.projectile)+"; muzzle="+AssetDatabase.GetAssetPath(source.muzzle)+
                "; impact="+AssetDatabase.GetAssetPath(source.impact)+"; castAudio="+ClipName(source.castClip)+
                "; impactAudio="+ClipName(source.impactClip)+"; windupAudio="+ClipName(source.windupClip);
            return result;
        }
        private static string ClipName(AudioClip clip)=>clip!=null?AssetDatabase.GetAssetPath(clip):"<not supplied by package>";
        private static SivraNovaVfx.Effect Prepare(GameObject source,string name,bool moving,Shader shader,Dictionary<Material,Material> cache)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("DF_Sivra_"+name);SceneManager.MoveGameObjectToScene(root,preview);
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
                    system.useAutoRandomSeed=false;system.randomSeed=(uint)(11309+i*53);
                    var burst=system.emission;if(moving&&burst.rateOverDistance.constantMax>0&&burst.burstCount==0)burst.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                    if(play[i])emission=Mathf.Max(emission,BomberNukeBuilder.LastEmissionSeconds(system));
                    var collision=system.collision;collision.enabled=false;var trigger=system.trigger;trigger.enabled=false;var lights=system.lights;lights.enabled=false;
                }
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    BomberNukeBuilder.ConvertRendererMaterials(renderer,shader,cache,Output,AssetDatabase.GetAssetPath(source));
                }
                float radius=BomberNukeBuilder.Measure(root,systems,play,"Sivra "+name);root.SetActive(false);
                for(int i=0;i<systems.Length;i++)
                {
                    var system=systems[i];system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=system.main;main.loop=moving&&loops[i];main.simulationSpace=moving?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
                }
                var saved=PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");
                if(saved==null)throw new IOException("Could not save "+name);
                return new SivraNovaVfx.Effect{prefab=saved,radius=radius,emissionSeconds=Mathf.Max(.05f,emission+.05f)};
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
#endif

