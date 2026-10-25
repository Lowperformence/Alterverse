#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Alterverse.Editor
{
    // FBX settings are deterministic and applied on first import after pulling the branch.
    public sealed class Bolt7ModelImporter:AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if(!assetPath.EndsWith("/Bolt7/BOLT7_animated.fbx",StringComparison.Ordinal))return;
            var importer=(ModelImporter)assetImporter;
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
            importer.importBlendShapes=false;importer.optimizeGameObjects=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
        }
        private void OnPreprocessAnimation()
        {
            if(!assetPath.EndsWith("/Bolt7/BOLT7_animated.fbx",StringComparison.Ordinal))return;
            var importer=(ModelImporter)assetImporter;
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips)
            {
                string n=clip.name;clip.loopTime=n.Contains("Idle")||n.Contains("Walk")||n.Contains("Run")||n.Contains("Strafe")||n.Contains("Fire_Loop");
                clip.lockRootPositionXZ=true;clip.keepOriginalPositionXZ=true;
            }
            if(clips.Length>0)importer.clipAnimations=clips;
        }
        private void OnPreprocessTexture()
        {
            if(!assetPath.Contains("/Bolt7/"))return;
            var importer=(TextureImporter)assetImporter;importer.mipmapEnabled=!assetPath.Contains("/Icons/");
            importer.maxTextureSize=assetPath.Contains("/Icons/")?512:2048;
            if(assetPath.Contains("_Normal_"))importer.textureType=TextureImporterType.NormalMap;
        }
    }

    [InitializeOnLoad]
    public sealed class Bolt7Importer:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX",Output="Assets/Alterverse/Resources/Bolt7/Vfx";
        private const string Version="BOLT7 native Grenade chain / Lightning Orb / Star Fog v1";
        private static bool building;
        public int callbackOrder=>-430;
        static Bolt7Importer(){EditorApplication.delayCall+=Auto;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.ExitingEditMode)Auto();};}
        private static string Normal(string text)=>new string((text??"").ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        private static void Auto()
        {
            if(building||EditorApplication.isPlaying||EditorApplication.isCompiling||!AssetDatabase.IsValidFolder(Source))return;
            try{Build();}catch(Exception e){Debug.LogError("BOLT-7 effects: "+e.Message);}
        }
        public void OnPreprocessBuild(BuildReport report){if(AssetDatabase.IsValidFolder(Source))Build();}
        private static string Pick(string[] paths,Func<string,bool> match)
            =>paths.Where(p=>match(Normal(p))).OrderBy(p=>Normal(p).Contains("demo")?1:0)
                .ThenBy(p=>Normal(Path.GetFileNameWithoutExtension(p)).Contains("blue")?0:1)
                .ThenBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
        [MenuItem("DIMENSIONS FRONT/BOLT-7/Prepare imported effects")]
        public static void Build()
        {
            if(building)return;
            var all=AssetDatabase.FindAssets("t:Prefab",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var native=all.Where(p=>!Normal(p).Contains("demo")&&!Normal(p).Contains("2dalternate")).ToArray();
            string projectile=null,muzzle=null,explosion=null;
            // Resolve the grenade's native stage references before removing demo/controller scripts.
            foreach(var path in all.Where(p=>Normal(p).Contains("grenade")).OrderBy(p=>p,StringComparer.Ordinal))
            {
                var owner=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(owner==null)continue;
                foreach(var component in owner.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(component==null)continue;var property=new SerializedObject(component).GetIterator();
                    while(property.NextVisible(true))
                    {
                        if(property.propertyType!=SerializedPropertyType.ObjectReference)continue;
                        var go=property.objectReferenceValue as GameObject??(property.objectReferenceValue as Component)?.gameObject;
                        string target=go!=null?AssetDatabase.GetAssetPath(go):null;
                        if(string.IsNullOrEmpty(target)||!target.StartsWith(Source+"/",StringComparison.Ordinal))continue;
                        string key=Normal(property.name);
                        if(key.Contains("impact")||key.Contains("explos"))explosion??=target;
                        else if(key.Contains("muzzle"))muzzle??=target;
                        else if(key.Contains("projectile"))projectile??=target;
                    }
                }
            }
            projectile??=Pick(native,n=>n.Contains("grenade")&&(n.Contains("projectile")||n.Contains("missile")||n.Contains("trail")));
            explosion??=Pick(native,n=>n.Contains("grenade")&&(n.Contains("explos")||n.Contains("impact")));
            muzzle??=Pick(native,n=>n.Contains("grenade")&&n.Contains("muzzle"));
            var paths=new[]{muzzle,projectile,explosion,
                Pick(native,n=>n.Contains("lightning")&&n.Contains("orb")),
                Pick(native,n=>n.Contains("star")&&n.Contains("fog"))};
            if(paths[1]==null||paths[2]==null||paths[3]==null||paths[4]==null)
                throw new FileNotFoundException("Grenade projectile/impact, Lightning Orb or Star Fog not resolved. Imported grenade candidates: "+string.Join(", ",all.Where(p=>Normal(p).Contains("grenade"))));
            string signature=Version+":"+BomberNukeBuilder.MaterialImportVersion+":"+string.Join(";",paths.Select(p=>p==null?"none":p+"="+AssetDatabase.GetAssetDependencyHash(p)));
            var old=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");
            if(old!=null&&old.GetComponent<Bolt7VfxLibrary>()?.sourceSignature==signature)return;
            var shader=Shader.Find("DimensionsFront/SelectedAbilityParticles");if(shader==null)throw new InvalidOperationException("SelectedAbilityParticles shader missing.");
            building=true;GameObject library=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
                var cache=new Dictionary<Material,Material>();var prepared=new GameObject[5];
                for(int i=0;i<paths.Length;i++)if(paths[i]!=null)
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);var root=new GameObject("BOLT7_Native_"+i);
                    try
                    {
                        var instance=Object.Instantiate(source,root.transform,false);instance.SetActive(true);
                        foreach(var t in root.GetComponentsInChildren<Transform>(true)){GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);t.gameObject.layer=0;t.gameObject.tag="Untagged";}
                        foreach(var c in root.GetComponentsInChildren<Component>(true))
                            if(c!=null&&!(c is Transform)&&!(c is ParticleSystem)&&!(c is ParticleSystemRenderer)&&!(c is MeshRenderer)&&!(c is MeshFilter))Object.DestroyImmediate(c);
                        foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;
                            main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                            main.simulationSpace=ParticleSystemSimulationSpace.Local;
                            var collision=ps.collision;collision.enabled=false;var trigger=ps.trigger;trigger.enabled=false;
                            var lights=ps.lights;lights.enabled=false;
                        }
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                            BomberNukeBuilder.ConvertRendererMaterials(renderer,shader,cache,Output,paths[i]);
                        root.SetActive(false);prepared[i]=PrefabUtility.SaveAsPrefabAsset(root,Output+"/Part_"+i+".prefab");
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                library=new GameObject("BOLT7_VFX_Library");var data=library.AddComponent<Bolt7VfxLibrary>();
                data.muzzle=prepared[0];data.grenade=prepared[1];data.explosion=prepared[2];data.lightningOrb=prepared[3];data.starFog=prepared[4];data.sourceSignature=signature;
                library.SetActive(false);PrefabUtility.SaveAsPrefabAsset(library,Output+"/Library.prefab");AssetDatabase.SaveAssets();
                Debug.Log("BOLT-7: native effects prepared; original visual scale x2.5 is applied once at runtime. "+string.Join("; ",paths));
            }
            finally{if(library!=null)Object.DestroyImmediate(library);building=false;}
        }
    }
}
#endif
