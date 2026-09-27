#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Keeps Mixamo motion FBXs unoptimized and turns the seven approved character templates
    /// into renderer-free runtime skeleton prefabs. The generated prefabs contain only the
    /// transform hierarchy plus an Animator; gameplay never renders the template meshes.
    /// </summary>
    public sealed class DFMixamoRunImporter : AssetPostprocessor
    {
        private const string RuntimeFolder = "Assets/Resources/AlterverseRigTemplates";
        private const string SessionKey = "Alterverse.TemplateRigs.AutoBuild.v1";

        private sealed class TemplateDefinition
        {
            public readonly string FilePrefix;
            public readonly string ResourceName;

            public TemplateDefinition(string filePrefix, string resourceName)
            {
                FilePrefix = filePrefix;
                ResourceName = resourceName;
            }
        }

        private static readonly TemplateDefinition[] Templates =
        {
            new("Pirate By P. Konstantinov@Standing 2H Magic Area Attack 02", "AV_Rig_Assassin"),
            new("Uriel A Plotexia@Standing 2H Magic Area Attack 02", "AV_Rig_Tank"),
            new("Nightshade J Friedrich@Standing 2H Magic Area Attack 02", "AV_Rig_Mage"),
            new("Pumpkinhulk L Shaw@Standing 2H Magic Area Attack 02", "AV_Rig_Brakk"),
            new("Maw J Laygo@Standing 2H Magic Area Attack 02", "AV_Rig_HotZone"),
            new("Ch35_nonPBR@Standing 2H Magic Area Attack 02", "AV_Rig_Soldier"),
            new("exo_red@Standing 2H Magic Area Attack 02", "AV_Rig_Traveler")
        };

        private bool IsMotionFbx =>
            assetPath.IndexOf("/Resources/DF_Mixamo", StringComparison.OrdinalIgnoreCase) >= 0 &&
            assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);

        private bool IsTemplateFbx =>
            assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
            TryGetTemplate(assetPath, out _);

        private void OnPreprocessModel()
        {
            if (!IsMotionFbx && !IsTemplateFbx) return;

            ModelImporter importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsMotionFbx && !IsTemplateFbx) return;

            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            bool loopingRun = assetPath.EndsWith("/DF_MixamoRun.fbx", StringComparison.OrdinalIgnoreCase);

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = loopingRun;
                clip.loopPose = loopingRun;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }

            importer.clipAnimations = clips;
        }

        [InitializeOnLoadMethod]
        private static void ScheduleAutomaticRebuild()
        {
            EditorApplication.delayCall += RebuildOncePerEditorSession;
        }

        private static void RebuildOncePerEditorSession()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            RebuildRuntimeTemplateRigs();
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string path in importedAssets)
            {
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                    !TryGetTemplate(path, out TemplateDefinition definition))
                    continue;

                string capturedPath = path;
                string capturedName = definition.ResourceName;
                EditorApplication.delayCall += () => BuildRuntimeSkeleton(capturedPath, capturedName);
            }

            foreach (string path in movedAssets)
            {
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                    !TryGetTemplate(path, out TemplateDefinition definition))
                    continue;

                string capturedPath = path;
                string capturedName = definition.ResourceName;
                EditorApplication.delayCall += () => BuildRuntimeSkeleton(capturedPath, capturedName);
            }
        }

        [MenuItem("Alterverse/Animations/Rebuild Template Rigs")]
        public static void RebuildRuntimeTemplateRigs()
        {
            EnsureRuntimeFolder();

            string[] guids = AssetDatabase.FindAssets("t:Model");
            int built = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) ||
                    !TryGetTemplate(path, out TemplateDefinition definition))
                    continue;

                if (BuildRuntimeSkeleton(path, definition.ResourceName))
                    built++;
            }

            if (built > 0)
                Debug.Log($"[Alterverse] Rebuilt {built} template movement rig(s) in {RuntimeFolder}.");
        }

        private static bool BuildRuntimeSkeleton(string fbxPath, string resourceName)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null) return false;

            EnsureRuntimeFolder();
            GameObject instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null) instance = UnityEngine.Object.Instantiate(model);

            try
            {
                instance.name = resourceName;

                if (PrefabUtility.IsPartOfPrefabInstance(instance))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        instance,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }

                // Strip everything except the actual bone hierarchy. This makes the runtime
                // object invisible and prevents template renderers/colliders/scripts from
                // affecting the hero.
                foreach (Transform node in instance.GetComponentsInChildren<Transform>(true))
                {
                    Component[] components = node.GetComponents<Component>();
                    foreach (Component component in components)
                    {
                        if (component is Transform) continue;
                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }

                Animator animator = instance.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.Normal;

                string prefabPath = $"{RuntimeFolder}/{resourceName}.prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
                return true;
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool TryGetTemplate(string path, out TemplateDefinition definition)
        {
            string fileName = Path.GetFileNameWithoutExtension(path);
            foreach (TemplateDefinition candidate in Templates)
            {
                if (!fileName.StartsWith(candidate.FilePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                definition = candidate;
                return true;
            }

            definition = null;
            return false;
        }

        private static void EnsureRuntimeFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            if (!AssetDatabase.IsValidFolder(RuntimeFolder))
                AssetDatabase.CreateFolder("Assets/Resources", "AlterverseRigTemplates");
        }
    }
}
#endif
