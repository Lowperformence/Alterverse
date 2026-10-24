#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Alterverse.Editor
{
    // Explicit, repeatable conversion. Original licensed content is never overwritten.
    public sealed class EpicToonReplacementBuilder : IPreprocessBuildWithReport
    {
        public int callbackOrder => -500;
        private const string Source="Assets/Epic Toon FX/";
        private const string Output="Assets/Alterverse/Resources/CombatVfx/EpicToon";
        private static readonly (string key,string path,float scale)[] Sources={
            ("Slash","Combat/Sword/Slash/SwordSlashThin/SwordSlashThinWhite.prefab",.45f),
            ("HeavySlash","Combat/Sword/Slash/SwordSlashThick/SwordSlashThickWhite.prefab",.45f),
            ("Claw","Combat/Sword/Slash/SwordSlashMini/SwordSlashMiniWhite.prefab",.5f),
            ("SwordImpact","Combat/Sword/Hit/SwordHit/SwordHitBlue.prefab",.45f),
            ("Orb","Combat/Magic/Sphere/MagicSphereBlue.prefab",.5f),
            ("Missile","Combat/Missiles/Magic/MagicMissileBlue.prefab",.4f),
            ("Rune","Combat/Magic/Circle Simple/MagicCircleSimpleBlue.prefab",.5f),
            ("Shield","Combat/Magic/Shield/MagicShieldBlue.prefab",.5f),
            ("Heal","Interactive/Healing/HealOnceBurst.prefab",.5f),
            ("Portal","Interactive/Portals/VortexPortal/VortexPortalBlue.prefab",.35f),
            ("Vortex","Interactive/Portals/VortexPortal/VortexPortalBlue.prefab",.35f),
            ("Water","Environment/Water/Flowing/Soft/WaterSplashTinySoft.prefab",.55f),
            ("Smoke","Environment/Smoke/White/SmokeBurstWhiteSoft.prefab",.45f),
            ("Nova","Combat/Magic/Nova/MagicNovaBlue.prefab",.5f),
            ("Punch","Combat/Explosions (Misc)/TargetHitExplosion.prefab",.4f),
            ("Impact","Combat/Explosions (Misc)/FlashExplosion.prefab",.35f),
            ("FireImpact","Combat/Explosions (Misc)/MetalExplosion.prefab",.35f),
            ("Spark","Combat/Explosions (Misc)/SparkExplosion.prefab",.3f),
            ("Fire","Combat/Missiles/Fireball/FireballMissileFire.prefab",.4f),
            ("Muzzle","Combat/Muzzleflash/MagicMuzzle/MagicMuzzleBlue.prefab",.3f),
            ("Jet","Combat/Flamethrower/Soft/Additive/FlamethrowerAdditiveBlue.prefab",.25f)
        };
        [InitializeOnLoadMethod]
        private static void RegisterInitialBuild() { EditorApplication.delayCall+=BuildIfMissing; }
        private static void BuildIfMissing()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)
            { EditorApplication.delayCall+=BuildIfMissing;return; }
            if(EditorApplication.isPlayingOrWillChangePlaymode||
                AssetDatabase.LoadAssetAtPath<GameObject>(Source+"Prefabs/"+Sources[0].path)==null)return;
            // A previous conversion can have saved Library.asset before final validation failed.
            // Validate it before skipping setup; repair incomplete output after the next reload.
            if(AssetDatabase.LoadAssetAtPath<EpicVfxLibrary>(Output+"/Library.asset")!=null)
            {
                try { Validate();return; }
                catch(BuildFailedException) { }
            }
            try { Build(); }
            catch(Exception error) { Debug.LogError("DF: Epic Toon setup failed. Use DIMENSIONS FRONT > VFX > Build Epic Toon replacement after importing the complete package. "+error.Message); }
        }
        [MenuItem("DIMENSIONS FRONT/VFX/Build Epic Toon replacement")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
            // Validate every input before creating/replacing anything.
            foreach(var item in Sources)
                if(AssetDatabase.LoadAssetAtPath<GameObject>(Source+"Prefabs/"+item.path)==null)
                    throw new FileNotFoundException("Epic Toon FX prefab missing: "+item.path);
            Shader shader=Shader.Find("DimensionsFront/EpicParticles");
            if(shader==null)throw new InvalidOperationException("EpicParticles.shader must be imported first.");
            Directory.CreateDirectory(Output+"/Materials");Directory.CreateDirectory(Output+"/Prefabs");AssetDatabase.Refresh();
            var entries=new List<EpicVfxLibrary.Entry>();
            var materials=new Dictionary<Material,Material>();
            foreach(var item in Sources)
            {
                var original=AssetDatabase.LoadAssetAtPath<GameObject>(Source+"Prefabs/"+item.path);
                var wrapper=new GameObject("DF_Epic_"+item.key);
                try
                {
                    var go=Object.Instantiate(original,wrapper.transform,false);
                    go.name=item.key;go.transform.localPosition=Vector3.zero;
                    // Whitelist visual components only: no demo scripts, movement, audio, lights or colliders.
                    foreach(var transform in go.GetComponentsInChildren<Transform>(true))
                    {
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                        transform.gameObject.layer=0;transform.gameObject.tag="Untagged";
                    }
                    foreach(var component in go.GetComponentsInChildren<Component>(true))
                        if(component!=null&&!(component is Transform)&&!(component is ParticleSystem)&&
                           !(component is ParticleSystemRenderer)&&!(component is MeshFilter)&&!(component is MeshRenderer))
                            Object.DestroyImmediate(component);
                    float nativeSeconds=.1f;
                    var systems=go.GetComponentsInChildren<ParticleSystem>(true);
                    int perSystem=Mathf.Max(8,96/Mathf.Max(1,systems.Length));
                    foreach(var ps in systems)
                    {
                        ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main=ps.main;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;
                        main.useUnscaledTime=false;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                        main.simulationSpace=ParticleSystemSimulationSpace.Local;
                        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                        main.maxParticles=Mathf.Min(main.maxParticles,perSystem);
                        var collision=ps.collision;collision.enabled=false;
                        var trigger=ps.trigger;trigger.enabled=false;
                        var lights=ps.lights;lights.enabled=false;
                        var sub=ps.subEmitters;sub.enabled=false;
                        // Native delay/lifetime is only used to fit the visual into the already-authoritative slot duration.
                        nativeSeconds=Mathf.Max(nativeSeconds,main.startDelay.constantMax+main.startLifetime.constantMax);
                    }
                    foreach(var renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                        renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                        var converted=renderer.sharedMaterials;
                        for(int i=0;i<converted.Length;i++)
                        {
                            var source=converted[i];if(source==null)continue;
                            if(!materials.TryGetValue(source,out var result))
                            {
                                string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
                                string path=Output+"/Materials/"+guid+".mat";
                                result=AssetDatabase.LoadAssetAtPath<Material>(path);
                                bool create=result==null;if(create)result=new Material(shader);else result.shader=shader;
                                result.name="DF_Epic_"+source.name;
                                string texture=source.HasProperty("_BaseMap")&&source.GetTexture("_BaseMap")!=null?"_BaseMap":"_MainTex";
                                if(source.HasProperty(texture))
                                {
                                    result.SetTexture("_MainTex",source.GetTexture(texture));
                                    result.SetTextureScale("_MainTex",source.GetTextureScale(texture));
                                    result.SetTextureOffset("_MainTex",source.GetTextureOffset(texture));
                                }
                                result.SetColor("_Tint",Color.white);
                                float blend=source.HasProperty("_DstBlend")?source.GetFloat("_DstBlend"):(float)BlendMode.One;
                                result.SetFloat("_DstBlend",blend==(float)BlendMode.One?(float)BlendMode.One:(float)BlendMode.OneMinusSrcAlpha);
                                result.renderQueue=3000;result.enableInstancing=true;
                                if(create)AssetDatabase.CreateAsset(result,path);else EditorUtility.SetDirty(result);
                                materials.Add(source,result);
                            }
                            converted[i]=result;
                        }
                        renderer.sharedMaterials=converted;
                        if(renderer is ParticleSystemRenderer particle)
                            particle.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});
                    }
                    var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Output+"/Prefabs/"+item.key+".prefab");
                    entries.Add(new EpicVfxLibrary.Entry{key=item.key,prefab=prefab,scale=item.scale,nativeSeconds=Mathf.Clamp(nativeSeconds,.1f,2f)});
                }
                finally{Object.DestroyImmediate(wrapper);}
            }
            var beam=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Materials/ExactBeam.mat");
            bool newBeam=beam==null;if(newBeam)beam=new Material(shader);else beam.shader=shader;
            beam.name="DF_EpicExactBeam";beam.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Textures/gradient_horizontal.png"));
            beam.SetFloat("_DstBlend",(float)BlendMode.One);beam.SetColor("_Tint",Color.white);
            if(newBeam)AssetDatabase.CreateAsset(beam,Output+"/Materials/ExactBeam.mat");else EditorUtility.SetDirty(beam);
            var library=AssetDatabase.LoadAssetAtPath<EpicVfxLibrary>(Output+"/Library.asset");
            bool createLibrary=library==null;if(createLibrary)library=ScriptableObject.CreateInstance<EpicVfxLibrary>();
            library.entries=entries.ToArray();library.beamMaterial=beam;library.sourceVersion="Epic Toon FX user import; DF replacement 1";
            if(createLibrary)AssetDatabase.CreateAsset(library,Output+"/Library.asset");else EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Validate();
            Debug.Log("DF: Epic Toon replacement built. 21 effect families, visual components only. Native Play/Android validation is still required.");
        }
        [MenuItem("DIMENSIONS FRONT/VFX/Validate Epic Toon replacement")]
        public static void Validate()
        {
            var library=AssetDatabase.LoadAssetAtPath<EpicVfxLibrary>(Output+"/Library.asset");
            if(library==null||library.beamMaterial==null||library.beamMaterial.mainTexture==null)
                throw new BuildFailedException("Build the Epic Toon replacement library first.");
            foreach(var source in Sources)
            {
                var entry=library.Find(source.key);
                if(entry==null||entry.prefab==null)throw new BuildFailedException("Missing Epic VFX: "+source.key);
                foreach(var component in entry.prefab.GetComponentsInChildren<Component>(true))
                    if(component==null||!(component is Transform||component is ParticleSystem||component is Renderer||component is MeshFilter))
                        throw new BuildFailedException("Non-visual component in Epic VFX: "+source.key);
                foreach(var renderer in entry.prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var rendererMaterials=renderer.sharedMaterials;
                    if(rendererMaterials.Length==0||rendererMaterials[0]==null)
                        throw new BuildFailedException("Missing primary Epic VFX material: "+source.key+" / "+renderer.name);
                    var system=renderer is ParticleSystemRenderer?renderer.GetComponent<ParticleSystem>():null;
                    for(int i=0;i<rendererMaterials.Length;i++)
                    {
                        var material=rendererMaterials[i];
                        // Particle renderer slot 1 is the trail material. Unity preserves an empty
                        // slot even with trails disabled (e.g. both SmokeBurstWhiteSoft systems).
                        // Only this unused slot may be empty; visible particles/trails must validate.
                        if(material==null&&i==1&&system!=null&&!system.trails.enabled)continue;
                        if(material==null||material.shader==null||material.shader.name!="DimensionsFront/EpicParticles")
                            throw new BuildFailedException("Unconverted Epic VFX material: "+source.key+" / "+renderer.name+" ["+i+"]");
                    }
                }
            }
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.StandaloneLinux64&&EditorUserBuildSettings.standaloneBuildSubtarget==StandaloneBuildSubtarget.Server)return;
            Validate();
        }
    }
}
#endif
