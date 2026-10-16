#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Alterverse.EditorTools
{
    // Shared small repeating maps for the five existing Void Knight materials.
    public sealed class VoidKnightTextureImporter : AssetPostprocessor
    {
        private const string Folder="Assets/Alterverse/Resources/AuthoredHeroes/VoidKnightTextures/";
        public override uint GetVersion()=>2;
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Folder,StringComparison.Ordinal))return;
            var importer=(TextureImporter)assetImporter;
            // Model surface maps must never retain an old cubemap import shape.
            importer.textureShape=TextureImporterShape.Texture2D;
            bool normal=assetPath.EndsWith("_Normal.png",StringComparison.Ordinal);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal;
            importer.mipmapEnabled=true;
            importer.wrapMode=UnityEngine.TextureWrapMode.Repeat;
            importer.filterMode=UnityEngine.FilterMode.Trilinear;
            importer.maxTextureSize=512;
            importer.alphaSource=TextureImporterAlphaSource.None;
            importer.isReadable=false;
        }
    }
}
#endif
