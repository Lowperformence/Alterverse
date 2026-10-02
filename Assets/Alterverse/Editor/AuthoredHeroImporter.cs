#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Alterverse.EditorTools
{
    /// <summary>Reproducible FBX import; controllers and URP materials become ordinary build assets.</summary>
    public sealed class AuthoredHeroImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Alterverse/Resources/AuthoredHeroes/";
        private bool IsHero => assetPath.StartsWith(Folder, StringComparison.Ordinal) && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        public override uint GetVersion() => 7;
        private void OnPostprocessModel(GameObject root)
        {
            if(!IsHero)return;
            string hero=Path.GetFileNameWithoutExtension(assetPath);
            if(hero=="Traveler")TravelerOrbImportPass.Apply(root);
            if(hero=="Mira"||hero=="Rook"||hero=="Veyr" )AuthoredHeroDetailPass.Apply(root,hero);
        }
        private void OnPreprocessModel()
        {
            if (!IsHero) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = Path.GetFileNameWithoutExtension(assetPath) == "Mira" || Path.GetFileNameWithoutExtension(assetPath) == "Rook";
            importer.optimizeGameObjects = false; // Named muzzle and cosmetic sockets must remain accessible.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
        private void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!IsHero) return;
            string name = StateName(clip.name);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = name == "Idle" || name == "Run";
            settings.keepOriginalPositionXZ = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }
        private void OnPostprocessMaterial(Material material)
        {
            if (!IsHero) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : material.color;
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            string name = material.name.ToLowerInvariant();
            bool metal = name.Contains("metal") || name.Contains("silver") || name.Contains("enamel");
            material.SetFloat("_Metallic", metal ? .65f : 0);
            material.SetFloat("_Smoothness", name.Contains("fabric") ? .25f : .55f);
            if (name.Contains("energy"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 2f);
            }
        }
        private static string StateName(string name)
        {
            int separator = name.LastIndexOf('|');
            return separator < 0 ? name : name.Substring(separator + 1);
        }
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] from)
        {
            string[] models = imported.Where(p => p.StartsWith(Folder, StringComparison.Ordinal) && p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (models.Length == 0) return;
            EditorApplication.delayCall += () => { foreach (string model in models) BuildController(model); };
        }
        private static void BuildController(string model)
        {
            string directory = Folder + "Controllers";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Controllers");
            string path = directory + "/" + Path.GetFileNameWithoutExtension(model) + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>())
            {
                if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                string name = StateName(clip.name);
                AnimatorState state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name) ?? machine.AddState(name);
                state.motion = clip;
                state.writeDefaultValues = false;
                if (name == "Idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif


