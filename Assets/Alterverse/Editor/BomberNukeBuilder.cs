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
                root.SetActive(false);
                foreach (var system in systems) system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var definition = root.AddComponent<BomberNukeVfx>();
                definition.nativeRadius = horizontalRadius;
                definition.sourceSignature = signature;
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new IOException("Could not save the prepared bomber Nuke prefab.");
                AssetDatabase.SaveAssets();
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
                building = false;
            }
        }

        private static Material ConvertMaterial(Material source, Shader shader, Dictionary<Material, Material> cache)
        {
            if (source == null) throw new InvalidOperationException("Nuke Cone contains a missing material.");
            if (cache.TryGetValue(source, out var converted)) return converted;
            string path = Output + "/Materials/" + AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)) + ".mat";
            converted = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = converted == null;
            if (create) converted = new Material(shader); else converted.shader = shader;
            converted.name = "DF_BomberNuke_" + source.name;
            string texture = source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null ? "_BaseMap" : "_MainTex";
            if (!source.HasProperty(texture) || source.GetTexture(texture) == null)
                throw new InvalidOperationException("Nuke Cone material has no effect texture: " + source.name);
            converted.SetTexture("_MainTex", source.GetTexture(texture));
            converted.SetTextureScale("_MainTex", source.GetTextureScale(texture));
            converted.SetTextureOffset("_MainTex", source.GetTextureOffset(texture));
            converted.SetColor("_Tint", source.HasProperty("_TintColor") ? source.GetColor("_TintColor") :
                source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
            converted.SetFloat("_SrcBlend", source.HasProperty("_SrcBlend") ? source.GetFloat("_SrcBlend") : (float)BlendMode.SrcAlpha);
            converted.SetFloat("_DstBlend", source.HasProperty("_DstBlend") ? source.GetFloat("_DstBlend") : (float)BlendMode.One);
            converted.renderQueue = 3000;
            if (create) AssetDatabase.CreateAsset(converted, path); else EditorUtility.SetDirty(converted);
            cache.Add(source, converted);
            return converted;
        }

        private static float Measure(GameObject root, ParticleSystem[] systems, bool[] autoPlay)
        {
            var roots = new List<ParticleSystem>();
            for (int i = 0; i < systems.Length; i++)
            {
                if (autoPlay[i]) systems[i].Play(false);
                bool nested = false;
                for (var parent = systems[i].transform.parent; parent != null && parent != root.transform; parent = parent.parent)
                    if (parent.GetComponent<ParticleSystem>() != null) { nested = true; break; }
                if (!nested) roots.Add(systems[i]);
            }
            float radius = 0f;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int frame = 0; frame < 3600; frame++)
            {
                foreach (var system in roots) system.Simulate(1f / 60f, true, false, false);
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
                bool alive = false;
                foreach (var system in systems) if (system.gameObject.activeInHierarchy && system.IsAlive(true)) { alive = true; break; }
                if (!alive)
                {
                    if (!(radius > .01f) || float.IsInfinity(radius)) throw new InvalidOperationException("Nuke Cone has no visible measured footprint.");
                    return radius;
                }
            }
            throw new InvalidOperationException("Nuke Cone did not finish within 60 seconds; check the imported prefab.");
        }
    }
}
#endif
