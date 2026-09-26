#if UNITY_EDITOR
using System;
using System.IO;
using Alterverse.Visuals;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Alterverse.EditorChecks
{
    /// <summary>Checks the real generated match, never creates a substitute test arena.</summary>
    public sealed class PlanetSurfaceValidation : IPreprocessBuildWithReport
    {
        public int callbackOrder=>-90;
        public void OnPreprocessBuild(BuildReport report)=>ValidateShader();

        [MenuItem("DIMENSIONS FRONT/Visuals/Validate active planet surface")]
        public static void ValidateActiveMap()
        {
            ValidateShader();
            if(!Application.isPlaying)throw new InvalidOperationException("Start a real match before validating its planet.");
            var ground=UnityEngine.Object.FindAnyObjectByType<ProductionLunarGround>();
            if(ground==null)throw new InvalidOperationException("No live ProductionLunarGround found.");
            if(ground.GetComponentsInChildren<Collider>(true).Length!=0)
                throw new InvalidOperationException("Presentation ground unexpectedly contains colliders.");
            int renderers=0,craterMaterials=0;
            bool macro=false,stone=false;
            foreach(var r in ground.GetComponentsInChildren<MeshRenderer>())
            {
                if(!r.enabled)continue;
                var m=r.sharedMaterial;
                if(m==null||m.shader.name!="Alterverse/PlanetSurface")continue;
                renderers++;
                if(m.GetTexture("_SandData")==null||m.GetTexture("_StoneData")==null)
                    throw new InvalidOperationException("Missing live detail data: "+r.name);
                float mode=m.GetFloat("_SurfaceMode");
                macro|=mode<.5f&&m.GetTexture("_BaseMap")==ground.SurfaceAlbedo;
                stone|=mode>1.5f;
                if(mode>.5f&&mode<1.5f)craterMaterials++;
            }
            if(!macro||!stone||craterMaterials<2)
                throw new InvalidOperationException("Ground, stone or crater material integration is missing. Wait for map initialization.");
            Debug.Log($"DIMENSIONS FRONT: active planet surface bindings passed ({renderers} renderers). Visual quality and Android frame time still require review.");
        }

        [MenuItem("DIMENSIONS FRONT/Visuals/Capture gameplay surface")]
        public static void CaptureGameplay()
        {
            ValidateActiveMap();
            var follow=UnityEngine.Object.FindAnyObjectByType<Alterverse.CameraSystem.MobaCamera>();
            var camera=follow!=null?follow.GetComponent<Camera>():null;
            if(camera==null)throw new InvalidOperationException("No actual MOBA gameplay camera found.");
            string directory="Temp/PlanetSurfaceReview";
            Directory.CreateDirectory(directory);
            string file=Path.Combine(directory,"planet-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");
            var previous=camera.targetTexture;
            var previousActive=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(Mathf.Max(1,camera.pixelWidth),Mathf.Max(1,camera.pixelHeight),24);
            Texture2D image=null;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                File.WriteAllBytes(file,image.EncodeToPNG());
                Debug.Log("Gameplay camera capture: "+Path.GetFullPath(file));
            }
            finally
            {
                camera.targetTexture=previous;RenderTexture.active=previousActive;
                RenderTexture.ReleaseTemporary(rt);
                if(image!=null)UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void ValidateShader()
        {
            var shader=Resources.Load<Shader>("DF_PlanetSurface");
            if(shader==null||ShaderUtil.ShaderHasError(shader))
                throw new BuildFailedException("Planet surface shader missing or has compiler errors.");
        }
    }
}
#endif
