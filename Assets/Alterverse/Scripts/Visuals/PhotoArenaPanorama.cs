using Alterverse.CameraSystem;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Per-gameplay-camera panorama; never changes global lighting or portrait cameras.</summary>
    public sealed class PhotoArenaPanorama
    {
        private Texture2D texture;
        private Material material;
        private Camera camera;
        private Skybox skybox;
        private Material previousMaterial;
        private CameraClearFlags previousFlags;
        private bool previousEnabled, addedSkybox;

        public bool Load()
        {
            texture=Resources.Load<Texture2D>("MapPanoramas/"+ArenaVariantRuntime.Current);
            Shader shader=Resources.Load<Shader>("MapPanoramas/PhotoSkybox");
            if(texture==null||shader==null||!shader.isSupported)
            {
                Dispose();
                Debug.LogWarning("DIMENSIONS FRONT: panorama missing; using procedural backdrop.");
                return false;
            }
            material=new Material(shader){name="DF_"+ArenaVariantRuntime.Current+"_PhotoPanorama"};
            material.SetTexture("_Panorama",texture);
            // Main camera looks toward +Z: align the lower-middle planet with that direction.
            material.SetFloat("_Yaw",90f);
            RefreshCamera();
            return true;
        }

        public void RefreshCamera()
        {
            if(material==null)return;
            Camera current=Camera.main;
            if(current==camera&&skybox!=null)return;
            RestoreCamera();
            if(current==null||current.targetTexture!=null||current.GetComponent<MobaCamera>()==null)return;
            camera=current;
            previousFlags=camera.clearFlags;
            skybox=camera.GetComponent<Skybox>();
            addedSkybox=skybox==null;
            if(addedSkybox)skybox=camera.gameObject.AddComponent<Skybox>();
            previousMaterial=skybox.material;
            previousEnabled=skybox.enabled;
            skybox.material=material;
            skybox.enabled=true;
            camera.clearFlags=CameraClearFlags.Skybox;
        }

        public void RestoreCamera()
        {
            if(skybox!=null&&skybox.material==material)
            {
                skybox.material=previousMaterial;
                skybox.enabled=previousEnabled;
                if(addedSkybox)Object.Destroy(skybox);
                if(camera!=null&&camera.clearFlags==CameraClearFlags.Skybox)camera.clearFlags=previousFlags;
            }
            camera=null;skybox=null;previousMaterial=null;addedSkybox=false;
        }

        public void Dispose()
        {
            RestoreCamera();
            if(material!=null)Object.Destroy(material);
            material=null;
            if(texture!=null)Resources.UnloadAsset(texture);
            texture=null;
        }
    }
}
