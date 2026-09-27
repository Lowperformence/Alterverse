#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Keep the downloaded motion skeleton intact for the runtime pose sampler.</summary>
    public sealed class DFMixamoRunImporter : AssetPostprocessor
    {
        private bool IsRun => assetPath.Contains("/Resources/DF_Mixamo") &&
            assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        private void OnPreprocessModel()
        {
            if (!IsRun) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.optimizeGameObjects = false;
            importer.importMaterials = false;
            importer.importCameras = false;
            importer.importLights = false;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsRun) return;
            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.loopTime = assetPath.EndsWith("/DF_MixamoRun.fbx", System.StringComparison.OrdinalIgnoreCase);
                clip.loopPose = clip.loopTime;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionXZ = true;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
#endif
