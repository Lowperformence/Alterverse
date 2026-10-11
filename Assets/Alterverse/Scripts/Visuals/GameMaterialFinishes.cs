using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alterverse.Visuals
{
    public enum GameSurfaceFinish { None, Metal, Stone, Concrete, Soil, Sand, Bark, Leaf, Ice, Fabric, Glass }

    /// <summary>Shared, offline-baked finishes. Reuses the existing compatibility pass;
    /// no new renderer search, material copies, geometry, lights or runtime texture baking.</summary>
    public static class GameMaterialFinishes
    {
        static readonly Dictionary<string,Texture2D> textures=new();
        static readonly Dictionary<Material,Shader> processed=new();
        static int untilPrune=256;
        static Shader glass;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            textures.Clear();processed.Clear();glass=null;untilPrune=256;
            Shader.SetGlobalFloat("_DF_FinishReady",0f);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            Texture2D metal=Data("Metal"),fabric=Data("Fabric");
            Shader.SetGlobalTexture("_DF_MetalFinish",metal);
            Shader.SetGlobalTexture("_DF_FabricFinish",fabric);
            Shader.SetGlobalFloat("_DF_FinishReady",metal!=null&&fabric!=null?1f:0f);
        }

        public static void Refresh()
        {
            textures.Clear();processed.Clear();glass=null;
            Initialize();
        }

        public static Texture2D Data(string name)=>Load(name);
        static Texture2D Load(string name)
        {
            if(!textures.TryGetValue(name,out Texture2D texture))
            {
                texture=Resources.Load<Texture2D>("MaterialFinishes/"+name);
                textures[name]=texture;
            }
            return texture;
        }

        public static GameSurfaceFinish Classify(string name,float metal,bool transparent)
        {
            string n=(name??string.Empty).ToLowerInvariant();
            if(Has(n,"energy","emission","glow","telegraph","warning","particle","hologram","lava"))
                return GameSurfaceFinish.None;
            if(transparent&&Has(n,"glass","visor","window","lens"))return GameSurfaceFinish.Glass;
            if(transparent)return GameSurfaceFinish.None;
            if(Has(n,"fur","muzzle","skin","face","hair","eye","slime"))return GameSurfaceFinish.None;
            if(Has(n,"fabric","cloth","woven","undersuit","robe","cape","jacket","scarf","leather"))return GameSurfaceFinish.Fabric;
            if(Has(n,"concrete","cement","bunker"))return GameSurfaceFinish.Concrete;
            if(Has(n,"stone","rock","basalt","golem","cliff","crater"))return GameSurfaceFinish.Stone;
            if(Has(n,"soil","dirt","earth_ground"))return GameSurfaceFinish.Soil;
            if(Has(n,"sand","regolith"))return GameSurfaceFinish.Sand;
            if(Has(n,"bark","wood","trunk"))return GameSurfaceFinish.Bark;
            if(Has(n,"leaf","leaves","foliage"))return GameSurfaceFinish.Leaf;
            if(Has(n,"ice","frost","frozen"))return GameSurfaceFinish.Ice;
            if(metal>=.35f||Has(n,"metal","steel","silver","gold","brass","titanium","blade","rifle","armor","armour","plate","enamel"))
                return GameSurfaceFinish.Metal;
            return GameSurfaceFinish.None;
        }

        static bool Has(string value,params string[] words)
        {
            foreach(string word in words)if(value.IndexOf(word,StringComparison.Ordinal)>=0)return true;
            return false;
        }

        public static void Apply(Material material,string rendererHint="")
        {
            if(material==null||material.shader==null)return;
            Shader originalShader=material.shader;
            // Specialized planet, water, energy and skin shaders keep their own contracts.
            string shaderName=originalShader.name;
            if(shaderName!="Alterverse/RuntimeLit"&&shaderName!="Universal Render Pipeline/Lit")return;
            if(processed.TryGetValue(material,out Shader previous)&&previous==originalShader)return;

            float metal=material.HasProperty("_Metallic")?material.GetFloat("_Metallic"):0f;
            bool transparent=material.renderQueue>=3000||
                (material.HasProperty("_Surface")&&material.GetFloat("_Surface")>.5f);
            GameSurfaceFinish finish=Classify(material.name+" "+rendererHint,metal,transparent);
            if(finish==GameSurfaceFinish.None&&shaderName=="Alterverse/RuntimeLit"&&!string.IsNullOrEmpty(rendererHint))
            {
                // Undo a generic factory finish after its final name identifies skin/energy.
                material.SetFloat("_FinishStrength",0f);
                material.SetFloat("_FinishRoughnessWeight",0f);
            }
            else if(finish==GameSurfaceFinish.Glass)ApplyGlass(material);
            else if(finish!=GameSurfaceFinish.None)
            {
                string name=finish.ToString();
                if(shaderName=="Alterverse/RuntimeLit")
                {
                    Texture2D data=Data(name);
                    if(data!=null)
                    {
                        material.SetTexture("_FinishData",data);
                        material.SetFloat("_FinishScale",finish==GameSurfaceFinish.Fabric?4f:2f);
                        material.SetFloat("_FinishStrength",finish==GameSurfaceFinish.Metal?.055f:finish==GameSurfaceFinish.Fabric?.06f:.12f);
                        material.SetFloat("_FinishRoughnessWeight",finish==GameSurfaceFinish.Metal?.28f:.35f);
                    }
                }
                else ApplyImportedNormal(material,name,finish);
            }
            // Factories often rename their materials after creation. Cache only the renderer pass
            // so its final name can still choose stone, fabric or exclusions instead of generic metal.
            if(string.IsNullOrEmpty(rendererHint))return;
            processed[material]=material.shader;
            if(--untilPrune==0)
            {
                untilPrune=256;
                var dead=new List<Material>();
                foreach(Material candidate in processed.Keys)if(candidate==null)dead.Add(candidate);
                foreach(Material candidate in dead)processed.Remove(candidate);
            }
        }

        static void ApplyImportedNormal(Material material,string name,GameSurfaceFinish finish)
        {
            // URP secondary normals retain the model's UVs, authored colour/normal maps,
            // per-material emission and hit-flash property blocks, including Timmy's fur.
            if(!material.HasProperty("_DetailNormalMap")||material.GetTexture("_DetailNormalMap")!=null)return;
            Texture2D normal=Load(name+"_Normal");
            if(normal==null)return;
            material.SetTexture("_DetailNormalMap",normal);
            material.SetFloat("_DetailNormalMapScale",finish==GameSurfaceFinish.Metal?.45f:.65f);
            material.SetFloat("_DetailAlbedoMapScale",0f); // Neutral detail colour: preserve every skin/team palette.
            material.SetTextureScale("_DetailAlbedoMap",new Vector2(3f,3f));
            material.EnableKeyword("_DETAIL_SCALED");
            material.DisableKeyword("_DETAIL_MULX2");
        }

        static void ApplyGlass(Material material)
        {
            if(glass==null)glass=Resources.Load<Shader>("DF_RuntimeGlass");
            if(glass==null||!glass.isSupported)return;
            Color tint=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.color;
            float smooth=material.HasProperty("_Smoothness")?material.GetFloat("_Smoothness"):.9f;
            material.shader=glass;
            material.SetColor("_BaseColor",tint); // Timmy alpha remains exactly .50.
            material.SetFloat("_Smoothness",Mathf.Clamp(smooth,.86f,.96f));
            material.SetOverrideTag("RenderType","Transparent");
            material.renderQueue=3000;
        }
    }
}
