// Engine substitutes: no native Unity/GPU performance claims.
namespace Alterverse.Match
{
    public enum ArenaVariant {Moon,Mars,GreenPlanet,Noxara,Kryon,Earth,Pyraxis,Neptune,Dark00,
        DiamondCore,GJ504b,Wasp76b,Trappist1e,Toi715b}
    public static class ArenaVariantRuntime { public static ArenaVariant Current; }
}
namespace Alterverse.CameraSystem
{public sealed class MobaCamera:UnityEngine.MonoBehaviour {}}

namespace UnityEditor
{
    public class AssetImporter { }
    public class AssetPostprocessor {public string assetPath;public AssetImporter assetImporter;}
    public enum TextureImporterType {Default}
    public enum TextureImporterShape {Texture2D}
    public enum TextureImporterAlphaSource {None}
    public enum TextureImporterNPOTScale {None}
    public enum TextureImporterCompression {Compressed}
    public enum TextureImporterFormat {ASTC_6x6}
    public class TextureImporterPlatformSettings
    {public string name;public bool overridden;public int maxTextureSize,compressionQuality;public TextureImporterFormat format;}
    public class TextureImporter:AssetImporter
    {
        public TextureImporterType textureType;public TextureImporterShape textureShape;
        public TextureImporterAlphaSource alphaSource;public TextureImporterNPOTScale npotScale;
        public TextureImporterCompression textureCompression;
        public bool sRGBTexture,alphaIsTransparency,isReadable,mipmapEnabled,streamingMipmaps;
        public UnityEngine.TextureWrapMode wrapMode;public UnityEngine.FilterMode filterMode;
        public int anisoLevel,maxTextureSize,compressionQuality;
        public TextureImporterPlatformSettings platform;
        public void SetPlatformTextureSettings(TextureImporterPlatformSettings settings)=>platform=settings;
    }
}
