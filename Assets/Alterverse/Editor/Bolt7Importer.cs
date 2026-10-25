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
        public override uint GetVersion()=>2;

        private void OnPreprocessModel()
        {
            if(!assetPath.EndsWith("/Bolt7/BOLT7_animated.fbx",StringComparison.Ordinal))return;
            var importer=(ModelImporter)assetImporter;
            importer.animationType=ModelImporterAnimationType.Generic;
            importer.importAnimation=true;importer.importCameras=false;importer.importLights=false;
            importer.importBlendShapes=false;importer.optimizeGameObjects=false;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            // The FBX embeds the same three PNGs shipped beside it. External material
            // import extracts those images again and can restart the refresh loop.
            // Keep materials in the FBX so import never rewrites the source textures.
            importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
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
            // A cached local preset can leave these PNGs imported as cubemaps.
            // Their colour, normal and icon materials all require 2D textures.
            importer.textureShape=TextureImporterShape.Texture2D;
        }
    }

    [InitializeOnLoad]
    public sealed class Bolt7Importer:IPreprocessBuildWithReport
    {
        private const string Source="Assets/Epic Toon FX",Output="Assets/Alterverse/Resources/Bolt7/Vfx";
        private const string Version="BOLT7 native Grenade chain / Lightning Orb / Star Fog, Cartoony Punch Heavy, Lightning Nova, original audio, complete bullet chain v4";
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
        private static string PrefabReference(GameObject source,string[] roles)
        {
            if(source==null)return null;
            foreach(var c in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(c==null)continue;var p=new SerializedObject(c).GetIterator();
                while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&roles.Any(r=>Normal(p.name).Contains(r)))
                {
                    var go=p.objectReferenceValue as GameObject??(p.objectReferenceValue as Component)?.gameObject;
                    string path=go!=null?AssetDatabase.GetAssetPath(go):null;
                    if(!string.IsNullOrEmpty(path)&&path.StartsWith(Source+"/",StringComparison.Ordinal))return path;
                }
            }
            return null;
        }
        private static AudioClip FindAudio(GameObject source,string[] roles,bool embedded)
        {
            if(source==null)return null;
            if(embedded)foreach(var voice in source.GetComponentsInChildren<AudioSource>(true))if(voice.clip!=null)return voice.clip;
            foreach(var component in source.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if(component==null)continue;var p=new SerializedObject(component).GetIterator();
                bool soundComponent=Normal(component.GetType().Name).Contains("sound")||Normal(component.GetType().Name).Contains("audio");
                while(p.NextVisible(true))if(p.propertyType==SerializedPropertyType.ObjectReference&&(roles.Any(r=>Normal(p.name).Contains(r))||embedded&&soundComponent)&&p.objectReferenceValue is AudioClip clip)
                    if(AssetDatabase.GetAssetPath(clip).StartsWith(Source+"/",StringComparison.Ordinal))return clip;
            }
            return null;
        }
        private static AudioClip FamilyAudio(string[] paths,string family,string[] roles)
        {
            string path=paths.Where(p=>Normal(Path.GetFileNameWithoutExtension(p)).Contains(family)&&roles.Any(r=>Normal(Path.GetFileNameWithoutExtension(p)).Contains(r)))
                .OrderBy(p=>p,StringComparer.Ordinal).FirstOrDefault();
            return path==null?null:AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
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
            string bullet=Pick(native,n=>n.Contains("bullet")&&n.Contains("small")&&n.Contains("fire")&&n.Contains("missile"))??Pick(native,n=>n.Contains("bullet")&&n.Contains("small")&&n.Contains("missile"));
            var bulletSource=bullet!=null?AssetDatabase.LoadAssetAtPath<GameObject>(bullet):null;
            string bulletMuzzle=PrefabReference(bulletSource,new[]{"muzzle","flash"})??Pick(native,n=>n.Contains("bullet")&&n.Contains("muzzle"));
            string bulletImpact=PrefabReference(bulletSource,new[]{"impact","explos"})??Pick(native,n=>n.Contains("bullet")&&(n.Contains("impact")||n.Contains("explos")));
            var paths=new[]{muzzle,projectile,explosion,
                Pick(native,n=>n.Contains("lightning")&&n.Contains("orb")),
                Pick(native,n=>n.Contains("star")&&n.Contains("fog")),
                Pick(native,n=>n.Contains("punch")&&n.Contains("heavy"))??Pick(native,n=>n.Contains("fight")&&n.Contains("action")),
                AssetDatabase.LoadAssetAtPath<GameObject>(UltimateLightningBuilder.IoneSource)!=null?UltimateLightningBuilder.IoneSource:Pick(native,n=>n.Contains("nova")&&n.Contains("lightning")),bulletMuzzle,bullet,bulletImpact};
            if(paths[1]==null||paths[2]==null||paths[3]==null||paths[4]==null||paths[6]==null)
                throw new FileNotFoundException("Grenade projectile/impact, Lightning Orb or Star Fog not resolved. Imported grenade candidates: "+string.Join(", ",all.Where(p=>Normal(p).Contains("grenade"))));
            var audioPaths=AssetDatabase.FindAssets("t:AudioClip",new[]{Source}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            var chain=AssetDatabase.LoadAssetAtPath<GameObject>(projectile);
            var sounds=new AudioClip[10];
            string[][] roles={new[]{"shoot","muzzle","launch","cast"},new[]{"fly","flight","projectile","travel","loop"},new[]{"impact","explos","deton","hit"},new[]{"lightning","orb","impact"},new[]{"fog","star"},new[]{"punch","fight","hit","impact"},new[]{"nova","lightning","explos","impact"},new[]{"shoot","muzzle","launch","cast"},new[]{"fly","flight","projectile","travel","loop"},new[]{"impact","explos","hit"}};
            for(int i=0;i<paths.Length;i++)if(paths[i]!=null)
            {
                sounds[i]=FindAudio(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]),roles[i],true);
                if(i<3)sounds[i]=sounds[i]??FindAudio(chain,roles[i],false)??FamilyAudio(audioPaths,"grenade",roles[i]);
                else if(i>=7)sounds[i]=sounds[i]??FindAudio(bulletSource,roles[i],false)??FamilyAudio(audioPaths,"bullet",roles[i]);
                else sounds[i]=sounds[i]??FamilyAudio(audioPaths,i==5?"punch":i==4?"fog":"lightning",roles[i]);
            }
            sounds[0]=sounds[0]??AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"/Sound/etfx_shoot_grenade.wav")??AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"/Sound/etfx_shoot_rocket.wav");
            sounds[2]=sounds[2]??AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"/Sound/etfx_explosion_grenade.wav")??AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"/Sound/etfx_explosion_1.wav");
            sounds[7]=sounds[7]??AssetDatabase.LoadAssetAtPath<AudioClip>(Source+"/Sound/etfx_shoot_single.wav");
            string signature=Version+":"+BomberNukeBuilder.MaterialImportVersion+":"+string.Join(";",paths.Select(p=>p==null?"none":p+"="+AssetDatabase.GetAssetDependencyHash(p)))+":"+string.Join(";",sounds.Select(c=>c==null?"none":AssetDatabase.GetAssetPath(c)+"="+AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(c))));
            var old=AssetDatabase.LoadAssetAtPath<GameObject>(Output+"/Library.prefab");
            if(old!=null&&old.GetComponent<Bolt7VfxLibrary>()?.sourceSignature==signature)return;
            var shader=Shader.Find("DimensionsFront/SelectedAbilityParticles");if(shader==null)throw new InvalidOperationException("SelectedAbilityParticles shader missing.");
            building=true;GameObject library=null;
            try
            {
                Directory.CreateDirectory(Output+"/Materials");AssetDatabase.Refresh();
                var cache=new Dictionary<Material,Material>();var prepared=new GameObject[10];float heavyRadius=1f,novaRadius=1f;
                for(int i=0;i<paths.Length;i++)if(paths[i]!=null)
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);var root=new GameObject("BOLT7_Native_"+i);
                    try
                    {
                        var instance=Object.Instantiate(source,root.transform,false);instance.SetActive(true);
                        foreach(var t in root.GetComponentsInChildren<Transform>(true)){GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);t.gameObject.layer=0;t.gameObject.tag="Untagged";}
                        foreach(var c in root.GetComponentsInChildren<Component>(true))
                            if(c!=null&&!(c is Transform)&&!(c is ParticleSystem)&&!(c is ParticleSystemRenderer)&&!(c is MeshRenderer)&&!(c is MeshFilter)&&!(c is AudioSource))Object.DestroyImmediate(c);
                        foreach(var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;
                            main.loop=false;main.stopAction=ParticleSystemStopAction.None;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                            var collision=ps.collision;collision.enabled=false;var trigger=ps.trigger;trigger.enabled=false;
                            var lights=ps.lights;lights.enabled=false;
                        }
                        var audio=root.GetComponentsInChildren<AudioSource>(true);
                        if(!audio.Any(a=>a.clip!=null)&&sounds[i]!=null)
                        {var voice=instance.AddComponent<AudioSource>();voice.clip=sounds[i];voice.volume=1f;voice.loop=(i==1||i==8)&&Normal(sounds[i].name).Contains("loop");audio=root.GetComponentsInChildren<AudioSource>(true);}
                        foreach(var voice in audio){voice.playOnAwake=false;voice.spatialBlend=0f;}
                        Shader stageShader=i==6?Shader.Find("DimensionsFront/BomberNukeParticles"):shader;
                        if(stageShader==null)throw new InvalidOperationException("BOLT-7 circular footprint particle shader missing.");
                        string materialsOutput=i==6?Output+"/Nova":Output;
                        Directory.CreateDirectory(materialsOutput+"/Materials");AssetDatabase.Refresh();
                        var stageCache=i==6?new Dictionary<Material,Material>():cache;
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                            BomberNukeBuilder.ConvertRendererMaterials(renderer,stageShader,stageCache,materialsOutput,paths[i]);
                        if(i==5||i==6)
                        {
                            var systems=root.GetComponentsInChildren<ParticleSystem>(true);var play=systems.Select(ps=>ps.main.playOnAwake).ToArray();
                            float measured=BomberNukeBuilder.Measure(root,systems,play,"BOLT-7 native stage "+i);if(i==5)heavyRadius=measured;else novaRadius=measured;
                        }
                        root.SetActive(false);prepared[i]=PrefabUtility.SaveAsPrefabAsset(root,Output+"/Part_"+i+".prefab");
                    }
                    finally{Object.DestroyImmediate(root);}
                }
                library=new GameObject("BOLT7_VFX_Library");var data=library.AddComponent<Bolt7VfxLibrary>();
                data.muzzle=prepared[0];data.grenade=prepared[1];data.explosion=prepared[2];data.lightningOrb=prepared[3];data.starFog=prepared[4];data.heavyPunch=prepared[5];data.heavyPunchRadius=heavyRadius;data.lightningNova=prepared[6];data.novaRadius=novaRadius;data.nativeAudioGain=1.4f;data.bulletMuzzle=prepared[7];data.bulletProjectile=prepared[8];data.bulletImpact=prepared[9];data.sourceSignature=signature;
                if(paths[5]==null)Debug.LogWarning("BOLT-7: Cartoony Punch Heavy / Fight Action source missing; keeping visible procedural ram sparks and shockwave.");
                library.SetActive(false);PrefabUtility.SaveAsPrefabAsset(library,Output+"/Library.prefab");AssetDatabase.SaveAssets();
                Debug.Log("BOLT-7 audio: "+string.Join("; ",sounds.Select((c,i)=>i+"="+(c!=null?AssetDatabase.GetAssetPath(c):"not supplied by source"))));
                Debug.Log("BOLT-7: native effects prepared; original visual scale x2.5 is applied once at runtime. "+string.Join("; ",paths));
            }
            finally{if(library!=null)Object.DestroyImmediate(library);building=false;}
        }
    }
}
#endif
