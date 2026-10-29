#if UNITY_EDITOR
using System;
using System.IO;
using Alterverse.Match;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorTools
{
    /// <summary>Only the selected cosmic artwork and its noise data use these import settings.</summary>
    public sealed class CosmicSkyImportSettings : AssetPostprocessor
    {
        const string MapFolder="Assets/Alterverse/Resources/MapPanoramas/";
        const string Menu="Assets/Alterverse/Resources/FrontEnd/CosmicMenu.png";
        const string Noise=MapFolder+"CosmicNoise.png";

        void OnPreprocessTexture()
        {
            bool noise=assetPath==Noise;
            bool arena=assetPath.StartsWith(MapFolder,StringComparison.Ordinal)&&
                Path.GetExtension(assetPath).Equals(".png",StringComparison.OrdinalIgnoreCase)&&
                Array.IndexOf(Enum.GetNames(typeof(ArenaVariant)),Path.GetFileNameWithoutExtension(assetPath))>=0;
            if(!noise&&!arena&&assetPath!=Menu)return;
            var texture=(TextureImporter)assetImporter;
            texture.textureType=TextureImporterType.Default;
            texture.textureShape=TextureImporterShape.Texture2D;
            texture.sRGBTexture=!noise;
            texture.alphaSource=TextureImporterAlphaSource.None;
            texture.alphaIsTransparency=false;
            texture.isReadable=false;
            texture.mipmapEnabled=true;
            texture.streamingMipmaps=false;
            texture.npotScale=TextureImporterNPOTScale.None;
            texture.wrapMode=noise?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            texture.filterMode=FilterMode.Trilinear;
            texture.anisoLevel=1;
            texture.maxTextureSize=noise?256:2048;
            texture.textureCompression=TextureImporterCompression.Compressed;
            texture.compressionQuality=70;
            texture.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name="Android",overridden=true,maxTextureSize=texture.maxTextureSize,
                format=TextureImporterFormat.ASTC_6x6,compressionQuality=70
            });
        }
    }
}
#endif
