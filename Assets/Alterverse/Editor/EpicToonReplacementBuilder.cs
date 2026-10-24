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
            ("Punch","Combat/Brawling/Toon/Directional/ToonPunchHeavy.prefab",.4f),
            ("HeavyImpact","Combat/Brawling/RoundHit/RoundHitYellow.prefab",.4f),
            ("NatureMissile","Combat/Missiles/MagicSoft/MagicMissileSoftGreen.prefab",.4f),
            ("NatureImpact","Environment/Weather/Wind & Leaves/LeafExplosion.prefab",.4f),
            ("NatureNova","Combat/Explosions/MagicNovaExplosion/MagicNovaExplosionGreen.prefab",.5f),
            ("ArcaneMissile","Combat/Missiles/Mystic/MysticMissilePurple.prefab",.4f),
            ("MagicImpact","Combat/Explosions/MysticExplosion/MysticExplosionWhite.prefab",.4f),
            ("ShadowMissile","Combat/Missiles/Shadow/ShadowMissile.prefab",.35f),
            ("FireNova","Combat/Explosions/NovaExplosion/ExplosionNovaFire.prefab",.5f),
            ("Impact","Combat/Explosions (Misc)/FlashExplosion.prefab",.35f),
            ("FireImpact","Combat/Explosions (Misc)/MetalExplosion.prefab",.35f),
            ("Spark","Combat/Explosions (Misc)/SparkExplosion.prefab",.3f),
            ("Fire","Combat/Missiles/Fireball/FireballMissileFire.prefab",.4f),
            ("Muzzle","Combat/Muzzleflash/MagicMuzzle/MagicMuzzleBlue.prefab",.3f),
            ("Jet","Combat/Flamethrower/Soft/Additive/FlamethrowerAdditiveBlue.prefab",.25f),
            ("BladeWave","Combat/Sword/Wave/SwordWaveWhite.prefab",.45f),
            ("SharpBolt","Combat/Missiles/Sharp/SharpMissileBlue.prefab",.4f),
            ("LeafDart","Environment/Weather/Wind & Leaves/LeafExplosion.prefab",.4f),
            ("SpinStrike","Combat/Sword/Spin/SwordSpinWhite.prefab",.45f),
            ("BomberNuke","Combat/Explosions/NukeConeExplosion/NukeConeExplosionFire.prefab",.5f),
            ("LightningMissile","Combat/Missiles/Lightning/LightningMissileYellow.prefab",.4f),
            ("LightningMuzzle","Combat/Muzzleflash/LightningMuzzle/LightningMuzzleYellow.prefab",.3f),
            ("LightningImpact","Combat/Explosions/LightningExplosion/LightningExplosionYellow.prefab",.4f),
            ("LightningNova","Combat/Nova/Lightning/NovaLightningPurple.prefab",.5f),
            ("HealField","Interactive/Healing/HealField.prefab",.5f),
            ("HealPod","Interactive/Healing/HealOnceLoop.prefab",.4f),
            ("HealPickup","Interactive/Healing/HealNova.prefab",.45f),
            ("TimmyJet","Combat/Flamethrower/Soft/Additive/FlamethrowerAdditiveBlue.prefab",.25f),
            ("WeatherSnow","Environment/Weather/Snow/SnowLight.prefab",1f),
            ("WeatherRain","Environment/Weather/Rain/RainLight.prefab",1f),
            ("WeatherWind","Environment/Weather/Wind & Leaves/Windlines.prefab",1f),
            ("WeatherFog","Environment/Fog/Grey/FogCalmGrey.prefab",1f),
            ("SpinPortal","Interactive/Portals/SpinPortal/SpinPortalGold.prefab",.35f),
            ("MiniHit","Combat/Sword/Hit/SwordHitMini/SwordHitMiniBlue.prefab",.2f),
            ("SoftHit","Combat/Brawling/Soft/Directional/SoftPunchLight.prefab",.2f),
            ("LeafHit","Combat/Sword/Hit/SwordHitMini/SwordHitMiniGreen.prefab",.2f),
            ("MagicHit","Combat/Sword/Hit/SwordHitMagic/SwordHitMagicBlue.prefab",.2f)
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
                try { Validate();EpicToonVisibilityReview.Schedule();return; }
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
                    var go=new GameObject(item.key);go.transform.SetParent(wrapper.transform,false);
                    // A claw strike has three parallel cuts, authored as one purchased-particle
                    // composition. Other families keep one native source prefab.
                    int copies=item.key=="Claw"?3:1;
                    for(int copy=0;copy<copies;copy++)
                    {
                        var part=Object.Instantiate(original,go.transform,false);
                        part.name=item.key+"_"+copy;
                        part.transform.localPosition=copies==1?Vector3.zero:Vector3.right*((copy-1)*.26f);
                        PreparePortalGeometry(item.key,part);
                    }
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
                    int perSystem=Mathf.Max(1,96/Mathf.Max(1,systems.Length));
                    foreach(var ps in systems)
                    {
                        ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                        var main=ps.main;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;
                        main.useUnscaledTime=false;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                        main.simulationSpace=ParticleSystemSimulationSpace.Local;
                        main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
                        main.maxParticles=Mathf.Min(main.maxParticles,perSystem);
                        PrepareManualEmission(item.key,ps);
                        var collision=ps.collision;collision.enabled=false;
                        var trigger=ps.trigger;trigger.enabled=false;
                        var lights=ps.lights;lights.enabled=false;
                        var sub=ps.subEmitters;sub.enabled=false;
                        // Native delay/lifetime is only used to fit the visual into the already-authoritative slot duration.
                        nativeSeconds=Mathf.Max(nativeSeconds,main.startDelay.constantMax+main.startLifetime.constantMax);
                    }
                    StripTypography(go);
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
                    float seconds=Mathf.Clamp(nativeSeconds,.1f,2f);
                    float bounds=MeasureRadius(wrapper,seconds);
                    var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Output+"/Prefabs/"+item.key+".prefab");
                    entries.Add(new EpicVfxLibrary.Entry{key=item.key,prefab=prefab,scale=item.scale,nativeSeconds=seconds,boundsRadius=bounds});
                }
                finally{Object.DestroyImmediate(wrapper);}
            }
            BuildAbilityLooks(entries);
            BuildBasicAudio();
            var beam=AssetDatabase.LoadAssetAtPath<Material>(Output+"/Materials/ExactBeam.mat");
            bool newBeam=beam==null;if(newBeam)beam=new Material(shader);else beam.shader=shader;
            beam.name="DF_EpicExactBeam";beam.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Source+"Textures/gradient_horizontal.png"));
            beam.SetFloat("_DstBlend",(float)BlendMode.One);beam.SetColor("_Tint",Color.white);
            if(newBeam)AssetDatabase.CreateAsset(beam,Output+"/Materials/ExactBeam.mat");else EditorUtility.SetDirty(beam);
            var library=AssetDatabase.LoadAssetAtPath<EpicVfxLibrary>(Output+"/Library.asset");
            bool createLibrary=library==null;if(createLibrary)library=ScriptableObject.CreateInstance<EpicVfxLibrary>();
            library.entries=entries.ToArray();library.beamMaterial=beam;library.sourceVersion=EpicVfxLibrary.CurrentVersion;
            if(createLibrary)AssetDatabase.CreateAsset(library,Output+"/Library.asset");else EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Validate();EpicToonVisibilityReview.Schedule();
            Debug.Log("DF: Epic Toon replacement built. "+entries.Count+" templates for 40 individual attack concepts; Nerok and Timmy retain their original visuals. Review the hero checklist in Play Mode.");
        }
        private static void BuildAbilityLooks(List<EpicVfxLibrary.Entry> entries)
        {
            var families=new Dictionary<string,EpicVfxLibrary.Entry>();
            foreach(var entry in entries)families.Add(entry.key,entry);
            foreach(var look in EpicVfxLibrary.AbilityLooks)
            {
                foreach(var variant in new[]{
                    (key:look.Key,source:look.Source,layout:look.Layout,accent:look.Accent,scale:1f),
                    (key:look.Key+"_Cast",source:look.CastSource,layout:look.Slot==3?EpicVfxLibrary.Formation.Crown:look.Layout,accent:(string)null,scale:.55f),
                    (key:look.Key+"_Hit",source:look.HitSource,layout:EpicVfxLibrary.Formation.Single,accent:(string)null,scale:.35f)})
                {
                    if(variant.source=="ExactBeam")continue;
                    var source=families[variant.source];var wrapper=new GameObject("DF_"+variant.key);
                    try
                    {
                        int count=variant.layout==EpicVfxLibrary.Formation.Single?1:
                            variant.layout==EpicVfxLibrary.Formation.Crown?4:
                            variant.layout==EpicVfxLibrary.Formation.Fan||variant.layout==EpicVfxLibrary.Formation.Orbit?3:2;
                        for(int i=0;i<count;i++)
                        {
                            var part=Object.Instantiate(source.prefab,wrapper.transform,false);
                            part.name=look.Name+"_"+i;float centered=i-(count-1)*.5f;
                            switch(variant.layout)
                            {
                                case EpicVfxLibrary.Formation.Cross:
                                    part.transform.localRotation=Quaternion.Euler(0,centered*64f,0);
                                    part.transform.localPosition=variant.source=="Portal"?Vector3.right*(centered*.8f):Vector3.up*(i*.025f);break;
                                case EpicVfxLibrary.Formation.Parallel:
                                    part.transform.localPosition=Vector3.right*(centered*.55f);break;
                                case EpicVfxLibrary.Formation.Fan:
                                    part.transform.localPosition=Vector3.right*(centered*.18f);
                                    part.transform.localRotation=Quaternion.Euler(0,centered*22f,0);break;
                                case EpicVfxLibrary.Formation.Eight:
                                    part.transform.localPosition=Vector3.forward*(centered*2f);break;
                                case EpicVfxLibrary.Formation.Crown:
                                case EpicVfxLibrary.Formation.Orbit:
                                    float angle=i*Mathf.PI*2/count;
                                    part.transform.localPosition=new Vector3(Mathf.Cos(angle)*.7f,0,Mathf.Sin(angle)*.7f);
                                    part.transform.localRotation=Quaternion.Euler(0,i*360f/count,0);break;
                            }
                        }
                        float seconds=source.nativeSeconds;
                        if(variant.accent!=null)
                        {
                            var accent=families[variant.accent];
                            var part=Object.Instantiate(accent.prefab,wrapper.transform,false);part.name=look.Name+"_accent";
                            part.transform.localScale=Vector3.one*(.48f*accent.scale/source.scale);
                            part.transform.localPosition=Vector3.up*.03f;seconds=Mathf.Max(seconds,accent.nativeSeconds);
                        }
                        // A complete signature is ONE pooled composition, including all its
                        // native layers. More cuts/runes never multiply the 96-particle budget.
                        var systems=wrapper.GetComponentsInChildren<ParticleSystem>(true);
                        int cap=Mathf.Max(1,(variant.key.EndsWith("_Hit")?18:96)/Mathf.Max(1,systems.Length));
                        foreach(var system in systems){var main=system.main;main.maxParticles=Mathf.Min(main.maxParticles,cap);}
                        float bounds=MeasureRadius(wrapper,seconds);
                        var prefab=PrefabUtility.SaveAsPrefabAsset(wrapper,Output+"/Prefabs/"+variant.key+".prefab");
                        entries.Add(new EpicVfxLibrary.Entry{key=variant.key,prefab=prefab,scale=source.scale*look.Emphasis*variant.scale,nativeSeconds=seconds,boundsRadius=bounds});
                    }
                    finally{Object.DestroyImmediate(wrapper);}
                }
            }
        }
        private static float MeasureRadius(GameObject root,float seconds)
        {
            // Sample the actual converted systems. Include renderer extents (mesh size,
            // trails, offsets and every layout copy), rather than guessing from startSize.
            float radius=.01f;var systems=root.GetComponentsInChildren<ParticleSystem>(true);
            foreach(var system in systems){system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);system.Play(false);}
            for(int sample=0;sample<24;sample++)
            {
                foreach(var system in systems)system.Simulate(seconds/24f,false,false,false);
                foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var ps=renderer.GetComponent<ParticleSystem>();
                    if(!renderer.enabled||ps!=null&&ps.particleCount==0)continue;
                    Bounds bounds=renderer.bounds;
                    radius=Mathf.Max(radius,(bounds.center-root.transform.position).magnitude+bounds.extents.magnitude);
                }
            }
            foreach(var system in systems)system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            return radius;
        }
        private static void StripTypography(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                foreach(var mat in renderer.sharedMaterials)
                {
                    Texture texture=mat!=null?mat.mainTexture:null;
                    string name=texture!=null?texture.name.ToLowerInvariant():"";
                    if(name.StartsWith("text_")||name.StartsWith("word_")||name=="pow"||name=="swoosh"||name=="crack")
                    {renderer.enabled=false;var ps=renderer.GetComponent<ParticleSystem>();if(ps!=null){var emission=ps.emission;emission.enabled=false;}break;}
                }
        }
        private static void BuildBasicAudio()
        {
            const string folder="Assets/Alterverse/Resources/Audio/EpicBasics";
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            // No sword/claw swishes exist in this package: retain the selected steel/claw
            // recordings. Only matching basic release/contact banks are replaced.
            foreach(var item in new[]{
                (hero:"Kaelon",phase:"Impact",file:"etfx_impact_metal01"),
                (hero:"Rook",phase:"Impact",file:"etfx_impact_metal03"),
                (hero:"Nyra",phase:"Cast",file:"etfx_shoot_pistol02"),
                (hero:"Elyra",phase:"Cast",file:"etfx_shoot_mystic"),
                (hero:"Mira",phase:"Cast",file:"etfx_shoot_magic"),
                (hero:"Traveler",phase:"Cast",file:"etfx_shoot_lightning")})
            {
                string path=folder+"/"+item.hero+"_"+item.phase+".wav";
                string source=Source+"Sound/"+item.file+".wav";
                if(AssetDatabase.LoadAssetAtPath<AudioClip>(source)==null)throw new FileNotFoundException(source);
                if(!File.Exists(path)&&!AssetDatabase.CopyAsset(source,path))throw new IOException("Cannot import "+path);
            }
        }
        private static void PreparePortalGeometry(string key,GameObject source)
        {
            if(key!="Portal"&&key!="Vortex"&&key!="SpinPortal")return;
            bool portal=key!="Vortex";
            // The source's 90-degree Z frame plus its discs' 90-degree X rotation
            // puts their normal on X: the gameplay preview sees only their edge.
            // Keep sparks in the portal/floor plane and render the two authored flat
            // discs as textured quads with an explicit, stable facing.
            source.transform.localRotation=portal?Quaternion.Euler(-90f,0f,0f):Quaternion.identity;
            foreach(var renderer in source.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if(renderer.renderMode!=ParticleSystemRenderMode.Mesh)continue;
                renderer.renderMode=portal?ParticleSystemRenderMode.Billboard:ParticleSystemRenderMode.HorizontalBillboard;
                renderer.alignment=portal?ParticleSystemRenderSpace.View:ParticleSystemRenderSpace.World;
                var main=renderer.GetComponent<ParticleSystem>().main;
                main.startRotation3D=false;main.startRotationX=0f;main.startRotationY=0f;
                // Authored Z spin, size, colour and alpha curves remain unchanged.
            }
        }
        private static void PrepareManualEmission(string key,ParticleSystem system)
        {
            var emission=system.emission;if(!emission.enabled)return;
            bool seed=false;
            if(emission.rateOverTime.constantMax<=0f&&emission.rateOverDistance.constantMax>0f)
            {
                // Purchased missile tails emit ONLY when their transform moves. The combat
                // pool advances local particles with a manual clock, including stationary
                // first frames and held rifts. Use bounded time emission on the cloned asset
                // and remove distance emission so movement cannot double the particle rate.
                emission.rateOverTime=Mathf.Clamp(emission.rateOverDistance.constantMax*8f,8f,48f);
                emission.rateOverDistance=0f;seed=true;
                // A trail must remain behind the moving projectile. Local space would
                // drag every emitted particle along with its head and collapse it to a ball.
                var main=system.main;main.simulationSpace=ParticleSystemSimulationSpace.World;
            }
            if((key=="Portal"||key=="Vortex"||key=="SpinPortal")&&
                emission.rateOverTime.constantMax>0f&&emission.rateOverTime.constantMax<=2f)
            {
                // The visible portal discs otherwise first appear at 0.5 seconds. Keep
                // their authored fade/rotation but start the first disc at slot birth.
                seed=true;
            }
            if(seed&&emission.burstCount==0)
                emission.SetBursts(new[]{new ParticleSystem.Burst(0f,1)});
        }
        [MenuItem("DIMENSIONS FRONT/VFX/Validate Epic Toon replacement")]
        public static void Validate()
        {
            var library=AssetDatabase.LoadAssetAtPath<EpicVfxLibrary>(Output+"/Library.asset");
            if(library==null||library.sourceVersion!=EpicVfxLibrary.CurrentVersion||library.beamMaterial==null||library.beamMaterial.mainTexture==null)
                throw new BuildFailedException("Build the Epic Toon replacement library first.");
            foreach(var source in Sources)
            {
                var entry=library.Find(source.key);
                if(entry==null||entry.prefab==null)throw new BuildFailedException("Missing Epic VFX: "+source.key);
            }
            foreach(var look in EpicVfxLibrary.AbilityLooks)
                foreach(var key in new[]{look.Source=="ExactBeam"?null:look.Key,look.Key+"_Cast",look.Key+"_Hit"})
                    if(key!=null&&(library.Find(key)==null||library.Find(key).prefab==null))
                        throw new BuildFailedException("Missing Epic ability concept: "+key);
            foreach(var entry in library.entries)
            {
                if(entry==null||entry.prefab==null)throw new BuildFailedException("Empty Epic VFX entry");
                if(float.IsNaN(entry.boundsRadius)||float.IsInfinity(entry.boundsRadius)||entry.boundsRadius<=0)
                    throw new BuildFailedException("Missing measured Epic footprint: "+entry.key);
                foreach(var component in entry.prefab.GetComponentsInChildren<Component>(true))
                    if(component==null||!(component is Transform||component is ParticleSystem||component is Renderer||component is MeshFilter))
                        throw new BuildFailedException("Non-visual component in Epic VFX: "+entry.key);
                foreach(var renderer in entry.prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var rendererMaterials=renderer.sharedMaterials;
                    if(rendererMaterials.Length==0||rendererMaterials[0]==null)
                        throw new BuildFailedException("Missing primary Epic VFX material: "+entry.key+" / "+renderer.name);
                    var system=renderer is ParticleSystemRenderer?renderer.GetComponent<ParticleSystem>():null;
                    for(int i=0;i<rendererMaterials.Length;i++)
                    {
                        var material=rendererMaterials[i];
                        // Particle renderer slot 1 is the trail material. Unity preserves an empty
                        // slot even with trails disabled (e.g. both SmokeBurstWhiteSoft systems).
                        // Only this unused slot may be empty; visible particles/trails must validate.
                        if(material==null&&i==1&&system!=null&&!system.trails.enabled)continue;
                        if(material==null||material.shader==null||material.shader.name!="DimensionsFront/EpicParticles")
                            throw new BuildFailedException("Unconverted Epic VFX material: "+entry.key+" / "+renderer.name+" ["+i+"]");
                    }
                }
            }
        }
        [MenuItem("DIMENSIONS FRONT/VFX/Test Epic Toon emission")]
        public static void TestEmission()
        {
            Validate();
            var library=AssetDatabase.LoadAssetAtPath<EpicVfxLibrary>(Output+"/Library.asset");
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            int failed=0;var report=new System.Text.StringBuilder();
            try
            {
                foreach(var entry in library.entries)
                {
                    var root=Object.Instantiate(entry.prefab);
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
                    try
                    {
                        root.SetActive(true);root.transform.localScale=Vector3.one*entry.scale;
                        var systems=root.GetComponentsInChildren<ParticleSystem>(true);int peak=0;
                        foreach(float sample in new[]{.03f,.10f,.25f,.50f})
                        {
                            int count=0;
                            foreach(var ps in systems)
                            {
                                ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                                ps.Play(false);ps.Simulate(sample,false,true,false);ps.Pause(false);
                                count+=ps.particleCount;
                            }
                            peak=Mathf.Max(peak,count);
                        }
                        bool supported=true;
                        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
                            foreach(var material in renderer.sharedMaterials)
                                if(material!=null&&(material.shader==null||!material.shader.isSupported))supported=false;
                        bool ok=peak>0&&supported;
                        if(!ok)failed++;
                        report.AppendLine((ok?"PASS ":"FAIL ")+entry.key+": peakParticles="+peak+", shaderSupported="+supported);
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
            Debug.Log("DF Epic native emission test (does not verify appearance):\n"+report);
            if(failed>0)throw new BuildFailedException("Epic native emission test failed for "+failed+" families. See Console.");
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            if(report.summary.platform==BuildTarget.StandaloneLinux64&&EditorUserBuildSettings.standaloneBuildSubtarget==StandaloneBuildSubtarget.Server)return;
            Validate();
        }
    }
}
#endif
