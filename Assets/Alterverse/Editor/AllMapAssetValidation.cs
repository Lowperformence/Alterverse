#if UNITY_EDITOR
using System;
using Alterverse.Match;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    /// <summary>Asset checks run without changing the player's map selection or active match.</summary>
    public sealed class AllMapAssetValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-80;
        public void OnPreprocessBuild(BuildReport report)=>ValidateAssets();

        [MenuItem("DIMENSIONS FRONT/Checks/All maps - shaders and assets")]
        public static void ValidateAssets()
        {
            Alterverse.Visuals.SpaceKitMapPolishRuntime.ValidateEmbeddedGeometry();
            int maps=0,shaders=0;
            foreach(ArenaVariant variant in Enum.GetValues(typeof(ArenaVariant)))
            {
                string profile=Alterverse.Visuals.ExoplanetArenaProfile.SurfaceResource(variant);
                foreach(string kind in new[]{"Ground","Stone","Macro"})
                    Require(Resources.Load<Texture2D>("PlanetSurfaces/"+profile+"/"+kind)!=null,
                        "Missing map texture: "+variant+"/"+kind+" (surface profile "+profile+")");
                maps++;
            }
            Require(Resources.Load<Texture2D>("MapPanoramas/Universe")!=null,"Missing universe panorama.");
            foreach(string guid in AssetDatabase.FindAssets("t:Shader",new[]{"Assets/Alterverse","Assets/Resources"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
                Require(shader!=null&&!ShaderUtil.ShaderHasError(shader),"Missing shader or compiler errors: "+path);
                shaders++;
            }
            foreach(string resource in new[]{"DF_RuntimeLit","DF_RuntimeUnlit","DF_PlanetSurface",
                "DF_ScenerySurface","DF_DiamondCrystal","MapPanoramas/PhotoSkybox"})
                Require(Resources.Load<Shader>(resource)!=null,"Missing map runtime shader: "+resource);
            Debug.Log($"Map assets: {maps} maps, {shaders} project shaders checked. Android GPU rendering requires a device build.");
        }

        [MenuItem("DIMENSIONS FRONT/Checks/Active map - renderers (Play Mode)")]
        public static void ValidateActiveMap()
        {
            Require(Application.isPlaying,"Start a match in Play Mode first.");
            GameObject world=GameObject.Find("DF_MatchWorld");
            Require(world!=null,"No active match world.");
            int checkedRenderers=0;
            foreach(Renderer renderer in world.GetComponentsInChildren<Renderer>(false))
            {
                if(!renderer.enabled)continue;
                foreach(Material material in renderer.sharedMaterials)
                    Require(material!=null&&material.shader!=null&&material.shader.isSupported&&
                        material.shader.name!="Hidden/InternalErrorShader"&&!ShaderUtil.ShaderHasError(material.shader),
                        "Invalid material/shader on: "+renderer.name);
                Vector3 center=renderer.bounds.center,size=renderer.bounds.size;
                Require(Finite(center)&&Finite(size),"Non-finite renderer bounds: "+renderer.name);
                MeshFilter filter=renderer.GetComponent<MeshFilter>();
                if(filter!=null)Require(filter.sharedMesh!=null&&filter.sharedMesh.vertexCount>0,
                    "Missing or empty map mesh: "+renderer.name);
                checkedRenderers++;
            }
            Require(checkedRenderers>0,"No active map renderers; wait for map initialization.");
            Debug.Log($"Active map {ArenaVariantRuntime.Current}: {checkedRenderers} renderers passed.");
        }

        private static bool Finite(Vector3 value)=>float.IsFinite(value.x)&&float.IsFinite(value.y)&&float.IsFinite(value.z);
        private static void Require(bool condition,string message)
        {if(!condition)throw new BuildFailedException(message);}
    }
}
#endif

