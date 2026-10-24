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
using Object = UnityEngine.Object;

namespace Alterverse.Editor
{
    // Focused import: no hero mappings, old replacement library or other effects are rebuilt.
    [InitializeOnLoad]
    public sealed class BomberNukeBuilder : IPreprocessBuildWithReport
    {
        public const string Source = "Assets/Epic Toon FX/Prefabs/Combat/Explosions/NukeConeExplosion/NukeConeExplosionFire.prefab";
        private const string Output = "Assets/Alterverse/Resources/CombatVfx/BomberNuke";
        private const string PrefabPath = Output + "/Explosion.prefab";
        private static bool building;
        public int callbackOrder => -500;

        static BomberNukeBuilder()
        {
            EditorApplication.delayCall += PrepareAutomatically;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode) PrepareAutomatically();
            };
        }

        private static void PrepareAutomatically()
        {
            if (building || EditorApplication.isPlaying || EditorApplication.isCompiling ||
                AssetDatabase.LoadAssetAtPath<GameObject>(Source) == null) return;
            try { Build(); }
            catch (Exception exception) { Debug.LogError("DIMENSIONS FRONT: bomber Nuke import failed: " + exception.Message); }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            try { Build(); }
            catch (Exception exception) { throw new BuildFailedException("Bomber Nuke Cone: " + exception.Message); }
        }

        [MenuItem("DIMENSIONS FRONT/VFX/Prepare bomber Nuke Cone only")]
        public static void Build()
        {
            if (building) return;
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (original == null) throw new FileNotFoundException("Purchased 3D NukeConeExplosionFire prefab missing: " + Source);
            var shader = Shader.Find("DimensionsFront/BomberNukeParticles");
            if (shader == null) throw new InvalidOperationException("BomberNukeParticles shader is missing.");
            string signature = BomberNukeVfx.ImportVersion + ":" + AssetDatabase.GetAssetDependencyHash(Source) + ":" +
                AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(shader));
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var previous = existing != null ? existing.GetComponent<BomberNukeVfx>() : null;
            if (previous != null && previous.sourceSignature == signature && previous.nativeRadius > 0f) return;

            building = true;
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                Directory.CreateDirectory(Output + "/Materials");
                AssetDatabase.Refresh();
                root = new GameObject("DF_Bomber_NukeConeExplosion");
                SceneManager.MoveGameObjectToScene(root, preview);
                var composition = Object.Instantiate(original, root.transform, false);
                composition.name = "NukeConeExplosionFire";
                composition.SetActive(true);
                // Keep the complete authored particle/mesh composition, never demo logic or audio.
                foreach (var transform in composition.GetComponentsInChildren<Transform>(true))
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);
                    transform.gameObject.layer = 0;
                    transform.gameObject.tag = "Untagged";
                }
                foreach (var component in composition.GetComponentsInChildren<Component>(true))
                    if (component != null && !(component is Transform) && !(component is ParticleSystem) &&
                        !(component is ParticleSystemRenderer) && !(component is MeshRenderer) && !(component is MeshFilter))
                        Object.DestroyImmediate(component);

                var systems = root.GetComponentsInChildren<ParticleSystem>(true);
                if (systems.Length == 0) throw new InvalidOperationException("Nuke Cone has no particle systems.");
                var autoPlay = new bool[systems.Length];
                for (int i = 0; i < systems.Length; i++)
                {
                    var system = systems[i];
                    autoPlay[i] = system.main.playOnAwake;
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = system.main;
                    main.loop = false;
                    main.stopAction = ParticleSystemStopAction.None;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                    main.useUnscaledTime = false;
                    // Fixed seeds make the measured native shape repeatable on every map.
                    system.useAutoRandomSeed = false;
                    system.randomSeed = (uint)(8101 + i * 97);
                    var collision = system.collision; collision.enabled = false;
                    var trigger = system.trigger; trigger.enabled = false;
                    var lights = system.lights; lights.enabled = false;
                    // Preserve authored bursts, lifetimes, speeds, curves and sub-emitters.
                }

                var materials = new Dictionary<Material, Material>();
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    var converted = renderer.sharedMaterials;
                    for (int i = 0; i < converted.Length; i++) converted[i] = ConvertMaterial(converted[i], shader, materials);
                    renderer.sharedMaterials = converted;
                    if (renderer is ParticleSystemRenderer particle && particle.trailMaterial != null)
                        particle.trailMaterial = ConvertMaterial(particle.trailMaterial, shader, materials);
                }

                float horizontalRadius = Measure(root, systems, autoPlay);
                float emissionWindow = 0f;
                foreach (var system in systems)
                    if (system.main.playOnAwake) emissionWindow = Mathf.Max(emissionWindow, LastEmissionSeconds(system));
                root.SetActive(false);
                foreach (var system in systems) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var definition = root.AddComponent<BomberNukeVfx>();
                definition.nativeRadius = horizontalRadius;
                definition.emissionStopAfter = Mathf.Max(.1f, emissionWindow + .05f);
                definition.sourceSignature = signature;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new IOException("Could not save the prepared bomber Nuke prefab.");
                AssetDatabase.SaveAssets();
                Debug.Log("DIMENSIONS FRONT VFX: NukeConeExplosionFire PREPARED; nativeRadius=" + horizontalRadius + "; " + signature);
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
                building = false;
            }
        }

        internal static Material ConvertMaterial(Material source, Shader shader, Dictionary<Material, Material> cache, string output = Output)
        {
            if (source == null) throw new InvalidOperationException("Nuke Cone contains a missing material.");
            if (cache.TryGetValue(source, out var converted)) return converted;
            string path = output + "/Materials/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) + ".mat";
            converted = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = converted == null;
            if (create) converted = new Material(shader); else converted.shader = shader;
            converted.name = (output == Output ? "DF_BomberNuke_" : "DF_SmallBullet_") + source.name;
            string texture = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null ? "_BaseMap" : "_MainTex";
            bool textured = source.HasProperty(texture) && source.GetTexture(texture) != null;
            // solid_AB is an authored colour-only material, not a missing resource.
            // White contributes no extra texture pattern and preserves its native tint.
            converted.SetTexture("_MainTex", textured ? source.GetTexture(texture) : Texture2D.whiteTexture);
            converted.SetTextureScale("_MainTex", textured ? source.GetTextureScale(texture) : Vector2.one);
            converted.SetTextureOffset("_MainTex", textured ? source.GetTextureOffset(texture) : Vector2.zero);
            converted.SetColor("_Tint", source.HasProperty("_TintColor") ? source.GetColor("_TintColor") :
                source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            converted.SetFloat("_SrcBlend", source.HasProperty("_SrcBlend") ? source.GetFloat("_SrcBlend") : (float)BlendMode.SrcAlpha);
            converted.SetFloat("_DstBlend", source.HasProperty("_DstBlend") ? source.GetFloat("_DstBlend") : (float)BlendMode.One);
            converted.renderQueue = 3000;
            if (create) AssetDatabase.CreateAsset(converted, path); else EditorUtility.SetDirty(converted);
            cache.Add(source, converted);
            return converted;
        }

        private static float FiniteTime(float value,float fallback)
        {return value>=0f&&!float.IsInfinity(value)?value:fallback;}

        // A burst-only system may have a huge duration and report IsAlive long after
        // its final visible particle. Its duration is not its final emission time.
        internal static float LastEmissionSeconds(ParticleSystem system)
        {
            var main=system.main;var emission=system.emission;
            if(!emission.enabled)return 0f;
            float duration=FiniteTime(main.duration,12f),last=0f;
            if(emission.rateOverTime.constantMax>0f||emission.rateOverDistance.constantMax>0f)last=duration;
            var bursts=new ParticleSystem.Burst[emission.burstCount];
            int count=emission.GetBursts(bursts);
            for(int i=0;i<count;i++)
            {
                var burst=bursts[i];
                float end=burst.cycleCount==0?duration:FiniteTime(burst.time,0f)+
                    Math.Max(0,burst.cycleCount-1)*FiniteTime(burst.repeatInterval,0f);
                last=Mathf.Max(last,Mathf.Min(duration,end));
            }
            float speed=main.simulationSpeed>0f&&!float.IsInfinity(main.simulationSpeed)?main.simulationSpeed:1f;
            return (FiniteTime(main.startDelay.constantMax,0f)+last)/speed;
        }

        internal static float Measure(GameObject root, ParticleSystem[] systems, bool[] autoPlay,string label="VFX")
        {
            var roots = new List<ParticleSystem>();
            float horizon=12f;
            for (int i = 0; i < systems.Length; i++)
            {
                var main=systems[i].main;
                float speed=main.simulationSpeed>0f&&!float.IsInfinity(main.simulationSpeed)?main.simulationSpeed:1f;
                horizon=Mathf.Max(horizon,LastEmissionSeconds(systems[i])+FiniteTime(main.startLifetime.constantMax,20f)/speed+1f);
                if (autoPlay[i]) systems[i].Play(false);
                bool nested = false;
                for (var parent = systems[i].transform.parent; parent != null && parent != root.transform; parent = parent.parent)
                    if (parent.GetComponent<ParticleSystem>() != null) { nested = true; break; }
                if (!nested) roots.Add(systems[i]);
            }
            float radius = 0f;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            // This is a visibility measurement, not an attempt to wait for the native
            // scheduler's IsAlive flag to finish. Bound the editor work and keep every
            // valid visible sample, including effects with very long native durations.
            int samples=(int)Math.Ceiling(Mathf.Min(horizon,30f)*120f);
            for (int frame = 0; frame < samples; frame++)
            {
                foreach (var system in roots) system.Simulate(1f / 120f, true, false, false);
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    var particles = renderer.GetComponent<ParticleSystem>();
                    if (particles != null && particles.particleCount == 0) continue;
                    var bounds = renderer.bounds;
                    // Height does not shrink a tall nuke into a miniature ground effect.
                    radius = Mathf.Max(radius, Mathf.Abs(bounds.center.x - root.transform.position.x) + bounds.extents.x,
                        Mathf.Abs(bounds.center.z - root.transform.position.z) + bounds.extents.z);
                }
            }
            if (!(radius > .01f) || float.IsInfinity(radius)) throw new InvalidOperationException(label+" has no visible measured footprint.");
            return radius;
        }
    }
}
#endif
