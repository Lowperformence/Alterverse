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
        // Version 18 adds baked secondary finishes and the reflection-preserving glass shader.
        // Changing AuthoredHeroDetailPass alone does not refresh imported FBX data.
        public override uint GetVersion() => 18;
        private void OnPostprocessModel(GameObject root)
        {
            if(!IsHero)return;
            string hero=Path.GetFileNameWithoutExtension(assetPath);
            if(hero=="Traveler")TravelerOrbImportPass.Apply(root);
            if(hero=="Mira"||hero=="Rook"||hero=="Veyr" )AuthoredHeroDetailPass.Apply(root,hero);
            if(hero=="Timmy")
                foreach(Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    if(renderer.name=="DF_Visor")
                    {renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;}
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
            // Native foot calibration reads vertices/weights from skin bind data.
            // The importer version bump refreshes already imported hero models.
            importer.isReadable = true;
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
            if(Path.GetFileNameWithoutExtension(assetPath)=="Timmy"&&name.Contains("visor glass"))
                ConfigureTimmyVisor(material);
            if(Path.GetFileNameWithoutExtension(assetPath)=="Timmy"&&(name.Contains("tabby fur")||name.Contains("muzzle cream")||name.Contains("woven astronaut fabric")))
            {
                Texture2D albedo=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"TimmyTextures/TimmySurface_Albedo.png");
                Texture2D normal=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"TimmyTextures/TimmySurface_Normal.png");
                material.SetColor("_BaseColor",Color.white);material.SetColor("_Color",Color.white);
                if(albedo!=null)material.SetTexture("_BaseMap",albedo);
                if(normal!=null){material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",.5f);material.EnableKeyword("_NORMALMAP");}
                material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.18f);
            }
            Alterverse.Visuals.GameMaterialFinishes.Apply(material);
        }
        private static void ConfigureTimmyVisor(Material material)
        {
            Color glass=new Color(.78f,.80f,.82f,.50f); // 50% transparent.
            material.SetColor("_BaseColor",glass);
            material.SetColor("_Color",glass);
            material.SetFloat("_Metallic",0f);
            material.SetFloat("_Smoothness",.90f);
            material.SetFloat("_Surface",1f);
            material.SetFloat("_Blend",0f); // Ordinary alpha blending, one front surface.
            material.SetFloat("_BlendModePreserveSpecular",0f);
            material.SetFloat("_AlphaClip",0f);
            material.SetFloat("_AlphaToMask",0f);
            material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha",(float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite",0f);
            material.SetFloat("_Cull",(float)UnityEngine.Rendering.CullMode.Back);
            material.SetFloat("_ReceiveShadows",0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetOverrideTag("RenderType","Transparent");
            material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster",false);
            material.SetShaderPassEnabled("DepthOnly",false);
            material.SetShaderPassEnabled("DepthNormals",false);
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


