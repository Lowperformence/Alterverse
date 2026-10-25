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
    public sealed class SmallBulletBuilder:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX";
        private const string Output="Assets/Alterverse/Resources/CombatVfx/SmallBullet";
        private const string Version="Small Bullet chain 3";
        private static bool building;
        public int callbackOrder=>-490;
        static SmallBulletBuilder()
        {
            EditorApplication.delayCall+=Auto;
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode)Auto();};
        }
        private static void Auto()
        {
            if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder(Source))return;
            try{Build();}catch(Exception error){Debug.LogError("DIMENSIONS FRONT: Small Bullet chain import: "+error.Message);}
        }
        public void OnPreprocessBuild(BuildReport report)
        {try{Build();}catch(Exception error){throw new BuildFailedException("Small Bullet: "+error.Message);}}
        [MenuItem("DIMENSIONS FRONT/VFX/Prepare Small Bullet chain only")]
        public static void Build()
        {
            if(building)return;if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            var allPaths=AssetDatabase.FindAssets("t:Prefab",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p=>!Normal(p).Contains("2dalternate")&&!p.Contains("/Demo")).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            var paths=allPaths.Where(p=>Normal(p).Contains("smallbullet")||Normal(p).Contains("bulletsmall")||Normal(p).Contains("smalbullet")).ToArray();
            if(paths.Length==0)throw new FileNotFoundException("No 3D Small Bullet prefabs found in the imported Epic Toon FX package.");
            var projectile=Choose(paths,0);
            string projectilePath=projectile!=null?AssetDatabase.GetAssetPath(projectile):null;
            // Native BulletSmall shots and their shared Bullet muzzle/hit prefabs live
            // in separate package categories. Do not require every name to say Small.
            var bulletParts=allPaths.Where(p=>Normal(p).Contains("bullet")).ToArray();
            var muzzle=Referenced(projectile,new[]{"muzzle","flash"})??Choose(bulletParts,1,projectilePath);
            var impact=Referenced(projectile,new[]{"impact","hit","explosion"})??Choose(bulletParts,2,projectilePath);
            if(projectile==null||muzzle==null||impact==null)
                throw new InvalidOperationException("Missing Small Bullet parts: "+(projectile==null?"projectile ":"")+(muzzle==null?"muzzle ":"")+(impact==null?"impact ":"")+". Bullet candidates: "+string.Join(", ",bulletParts));
            var shader=Shader.Find("DimensionsFront/SmallBulletParticles");if(shader==null)throw new InvalidOperationException("Small Bullet shader missing.");
            string signature=BomberNukeBuilder.MaterialImportVersion+":"+Version+":"+string.Join(":",new[]{projectile,muzzle,impact}.Select(p=>AssetDatabase.GetAssetPath(p)+":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(p))))+
                ":"+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(shader));
            var old=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");
            if(old!=null&&old.GetComponent<SmallBulletVfx>()?.sourceSignature==signature)return;
            building=true;GameObject root=null;var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
                var cache=new Dictionary<Material,Material>();root=new GameObject("DF_SmallBullet_Library");SceneManager.MoveGameObjectToScene(root,preview);root.SetActive(false);
                var data=root.AddComponent<SmallBulletVfx>();
                data.projectile=Prepare(projectile,"Projectile",true,shader,cache,out data.projectileRadius);
                data.muzzle=Prepare(muzzle,"Muzzle",false,shader,cache,out data.muzzleRadius);
                data.impact=Prepare(impact,"Impact",false,shader,cache,out data.impactRadius);
                data.sourceSignature=signature;
                if(PrefabUtility.SaveAsPrefabAsset(root,Output+"/Library.prefab")==null)throw new IOException("Could not save Small Bullet library.");
                AssetDatabase.SaveAssets();
                Debug.Log("DIMENSIONS FRONT VFX: Small Bullet chain PREPARED; projectile="+AssetDatabase.GetAssetPath(projectile)+
                    "; muzzle="+AssetDatabase.GetAssetPath(muzzle)+"; impact="+AssetDatabase.GetAssetPath(impact));
            }
            finally{if(root!=null)Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);building=false;}
        }
        private static string Normal(string text)=>new string(text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static int Role(string path)
        {
            // "White" contains "hit"; it is a colour suffix, never an impact role.
            string name=Normal(Path.GetFileNameWithoutExtension(path)).Replace("white","");
            if(name.Contains("muzzle")||name.Contains("flash"))return 1;
            if(name.Contains("impact")||name.Contains("hit")||name.Contains("explosion"))return 2;
            string category=Normal(Path.GetDirectoryName(path)??"").Replace("white","");
            if(category.Contains("muzzle"))return 1;
            if(category.Contains("impact")||category.Contains("hit")||category.Contains("explosion"))return 2;
            return 0;
        }
        private static string Palette(string path)
        {
            if(string.IsNullOrEmpty(path))return null;
            string name=Normal(Path.GetFileNameWithoutExtension(path));
            foreach(string colour in new[]{"white","red","fire","orange","yellow","blue","green","purple"})
                if(name.Contains(colour))return colour;
            return null;
        }
        private static GameObject Choose(string[] paths,int role,string projectilePath=null)
        {
            var candidates=paths.Where(p=>Role(p)==role).ToArray();
            string palette=Palette(projectilePath);
            // Prefer the neutral version, then the warm rifle palette. Keep the family intact.
            string chosen=candidates.OrderBy(p=>palette!=null&&Palette(p)==palette?-1:Normal(p).Contains("white")?0:Normal(p).Contains("red")?1:Normal(p).Contains("fire")?2:3)
                .ThenBy(p=>Normal(p).Contains("smallbullet")||Normal(p).Contains("bulletsmall")?0:1)
                .ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            return chosen==null?null:AssetDatabase.LoadAssetAtPath<GameObject>(chosen);
        }
        private static GameObject Referenced(GameObject prefab,string[] roles)
        {
            if(prefab==null)return null;
            foreach(var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(component==null)continue;
                var iterator=new SerializedObject(component).GetIterator();
                while(iterator.NextVisible(true))
                    if(iterator.propertyType==SerializedPropertyType.ObjectReference&&roles.Any(r=>Normal(iterator.name).Contains(r)))
                    {
                        var value=iterator.objectReferenceValue as GameObject??(iterator.objectReferenceValue as Component)?.gameObject;
                        string path=value!=null?AssetDatabase.GetAssetPath(value):null;
                        if(value!=null&&!string.IsNullOrEmpty(path)&&path.StartsWith(Source+"/",StringComparison.OrdinalIgnoreCase)&&
                            !Normal(path).Contains("2dalternate")&&!Normal(path).Contains("demo")&&
                            value.GetComponentsInChildren<ParticleSystem>(true).Length>0)return value;
                    }
            }
            return null;
        }
        private static GameObject Prepare(GameObject source,string name,bool moving,Shader shader,Dictionary<Material,Material> cache,out float radius)
        {
            var preview=EditorSceneManager.NewPreviewScene();var root=new GameObject("DF_SmallBullet_"+name);SceneManager.MoveGameObjectToScene(root,preview);
            try
            {
                var native=Object.Instantiate(source,root.transform,false);native.SetActive(true);
                foreach(var node in native.GetComponentsInChildren<Transform>(true))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(node.gameObject);
                foreach(var component in native.GetComponentsInChildren<Component>(true))
                    if(component!=null&&!(component is Transform)&&!(component is ParticleSystem)&&!(component is ParticleSystemRenderer)&&!(component is MeshFilter)&&!(component is MeshRenderer))Object.DestroyImmediate(component);
                var systems=root.GetComponentsInChildren<ParticleSystem>(true);if(systems.Length==0)throw new InvalidOperationException(name+" has no particle systems.");
                var autoPlay=new bool[systems.Length];
                var nativeLoops=new bool[systems.Length];
                for(int i=0;i<systems.Length;i++)
                {
                    var ps=systems[i];autoPlay[i]=ps.main.playOnAwake;nativeLoops[i]=ps.main.loop;ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main=ps.main;main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                    main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                    ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(9203+i*31);
                    var emission=ps.emission;
                    if(moving&&emission.rateOverDistance.constantMax>0&&emission.burstCount==0)emission.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                    var collision=ps.collision;collision.enabled=false;var trigger=ps.trigger;trigger.enabled=false;var lights=ps.lights;lights.enabled=false;
                }
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    BomberNukeBuilder.ConvertRendererMaterials(renderer,shader,cache,Output,AssetDatabase.GetAssetPath(source));
                }
                radius=BomberNukeBuilder.Measure(root,systems,autoPlay);
                root.SetActive(false);
                for(int i=0;i<systems.Length;i++)
                {
                    var ps=systems[i];
                    ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    // In particular, do not turn native one-shot sub-emitters into loops.
                    var main=ps.main;main.loop=moving&&nativeLoops[i];
                    main.simulationSpace=moving?ParticleSystemSimulationSpace.World:ParticleSystemSimulationSpace.Local;
                }
                return PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+name+".prefab");
            }
            finally{Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(preview);}
        }
    }
}
#endif

