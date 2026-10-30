#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alterverse.EditorTools
{
    /// <summary>Creates only the skybox's serialized build dependencies, including standard shaders.</summary>
    public sealed class MatchSkybox360Assets : IPreprocessBuildWithReport
    {
        internal const string Root="Assets/Alterverse/Resources/MatchSkybox360";
        private static readonly string[] Names={"Azure","Copper","Ice","Violet","Moon"};
        public int callbackOrder=>-100;

        [InitializeOnLoadMethod]
        private static void QueueImport()=>EditorApplication.delayCall+=AfterImport;

        private static void AfterImport()
        {
#if !UNITY_SERVER
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)
            {EditorApplication.delayCall+=AfterImport;return;}
            try {EnsureMaterials();}
            catch(Exception error){Debug.LogWarning("DIMENSIONS FRONT: skybox dependencies will be checked again before building. "+error.Message);}
#endif
        }

        public void OnPreprocessBuild(BuildReport report)
        {
#if !UNITY_SERVER
            EnsureMaterials();
#endif
        }

        internal static void EnsureMaterials()
        {
            Shader sky=RequireShader("Skybox/Panoramic"),lit=RequireShader("Universal Render Pipeline/Lit"),unlit=RequireShader("Universal Render Pipeline/Unlit");
            var panorama=Load("Sky360_StarsNebula");
            var planets=new Texture2D[Names.Length];
            for(int i=0;i<Names.Length;i++)planets[i]=Load("Planet_"+Names[i]);
            if(!AssetDatabase.IsValidFolder(Root+"/Generated"))AssetDatabase.CreateFolder(Root,"Generated");
            Material skyMat=Get("Sky",sky);
            skyMat.SetTexture("_MainTex",panorama);skyMat.SetFloat("_Mapping",1);skyMat.SetFloat("_ImageType",0);
            skyMat.SetFloat("_Layout",0);skyMat.SetFloat("_Exposure",.8f);skyMat.SetColor("_Tint",new Color(.5f,.5f,.5f));
            if(skyMat.passCount!=1)throw new BuildFailedException("Dimensions Front skybox: the standard panorama shader must have one pass.");
            Save(skyMat);
            for(int i=0;i<Names.Length;i++)
            {
                Material material=Get("Planet_"+Names[i],lit);
                material.SetTexture("_BaseMap",planets[i]);material.SetColor("_BaseColor",Color.white);
                material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.18f);
                material.SetFloat("_ReceiveShadows",0);material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                // A subtle local emission keeps distant planet surfaces readable under each map's lighting.
                material.SetTexture("_EmissionMap",planets[i]);material.SetColor("_EmissionColor",new Color(.12f,.12f,.12f));material.EnableKeyword("_EMISSION");
                Save(material);
            }
            Material rings=Get("Rings",unlit);rings.SetColor("_BaseColor",Color.white);rings.SetFloat("_Cull",0);Save(rings);
            // Serialized materials in Resources make all required standard shaders real build dependencies.
            // No global GraphicsSettings, scenes, camera settings or unrelated assets are saved.
        }

        private static Shader RequireShader(string name)
        {
            Shader shader=Shader.Find(name);
            if(shader==null)throw new BuildFailedException("Dimensions Front skybox: required shader unavailable: "+name);
            return shader;
        }

        private static Texture2D Load(string name)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+".png");
            if(texture==null)throw new BuildFailedException("Dimensions Front skybox: missing texture "+name);
            return texture;
        }

        private static Material Get(string name,Shader shader)
        {
            string path=Root+"/Generated/"+name+".mat";
            Material material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(shader){name="DF_Skybox360_"+name};AssetDatabase.CreateAsset(material,path);}
            else if(material.shader!=shader)material.shader=shader;
            return material;
        }

        private static void Save(Material material)
        {EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);}
    }

    public sealed class MatchSkybox360TextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(MatchSkybox360Assets.Root+"/Textures/",StringComparison.Ordinal)||!assetPath.EndsWith(".png",StringComparison.OrdinalIgnoreCase))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Default;importer.textureShape=TextureImporterShape.Texture2D;
            importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.None;importer.isReadable=false;
            importer.mipmapEnabled=true;importer.wrapModeU=TextureWrapMode.Repeat;importer.wrapModeV=TextureWrapMode.Clamp;
            bool sky=assetPath.EndsWith("Sky360_StarsNebula.png",StringComparison.Ordinal);
            importer.maxTextureSize=sky?2048:1024;
            importer.textureCompression=TextureImporterCompression.Compressed;importer.compressionQuality=70;
            // Preserve small, bright star cores; planet surfaces keep the existing memory profile.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings{name="Android",overridden=true,maxTextureSize=importer.maxTextureSize,format=sky?TextureImporterFormat.ASTC_4x4:TextureImporterFormat.ASTC_6x6,compressionQuality=sky?90:70});
        }
    }
}
#endif
