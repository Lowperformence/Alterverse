#if UNITY_EDITOR
using System;
using Alterverse.Match;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    public static class PlanetSceneryQualityChecks
    {
        [MenuItem("DIMENSIONS FRONT/Checks/All planet scenery finishes")]
        public static void Run()
        {
            Shader shader=Resources.Load<Shader>("DF_ScenerySurface");
            Require(shader!=null&&shader.isSupported&&!ShaderUtil.ShaderHasError(shader),"Scenery shader errors/support.");
            foreach(string finish in new[]{"Metal","Bark","Leaf","Ice"})
            {
                string path="Assets/Alterverse/Resources/SceneryFinishes/"+finish+".png";
                TextureImporter importer=AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer!=null&&!importer.sRGBTexture&&importer.mipmapEnabled&&
                    importer.wrapMode==TextureWrapMode.Repeat&&!importer.isReadable,"Invalid packed data import: "+finish);
                Require(importer.textureType==TextureImporterType.Default&&
                    !importer.alphaIsTransparency,"Packed channel conversion: "+finish);
            }
            int planets=0;
            foreach(ArenaVariant planet in Enum.GetValues(typeof(ArenaVariant)))
            {
                Require(Resources.Load<Texture2D>("PlanetSurfaces/"+planet+"/Stone")!=null,"Missing stone detail: "+planet);
                for(int layer=0;layer<4;layer++)
                {
                    Material material=RuntimeShaderLibrary.Create(new Color(.24f,.31f,.42f),false,.25f,.4f,new Color(.02f,.03f,.04f));
                    Require(material!=null,"Runtime shader unavailable.");
                    try
                    {
                        Color color=material.GetColor("_BaseColor"),emission=material.GetColor("_EmissionColor");
                        PlanetSceneryMaterials.Apply(material,planet,PlanetSceneryMaterials.LandscapeFinish(planet,layer));
                        Require(material.shader==shader&&material.GetTexture("_DetailData")!=null,"Unfinished layer: "+planet+"/"+layer);
                        Require(material.GetColor("_BaseColor")==color&&material.GetColor("_EmissionColor")==emission,
                            "Palette/emission changed: "+planet);
                        foreach(string pass in new[]{"ForwardLit","ShadowCaster","DepthOnly","DepthNormals"})
                            Require(material.FindPass(pass)>=0,"Missing pass: "+pass);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(material); }
                }
                planets++;
            }
            Debug.Log("Planet scenery: "+planets+" planets / "+planets*4+" finish layers checked; numerical data, palettes and shader passes valid. Live camera/device checks are separate.");
        }
        static void Require(bool valid,string message)
        { if(!valid)throw new InvalidOperationException(message); }
    }
}
#endif
