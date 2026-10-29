using Alterverse.CameraSystem;
using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Arena-specific cosmic art and subtle effects, scoped to the gameplay camera.</summary>
    public sealed class PhotoArenaPanorama
    {
        private CosmicSkyMaterial cosmic;
        private Camera camera;
        private Skybox skybox;
        private Material previousMaterial;
        private CameraClearFlags previousFlags;
        private bool previousEnabled, addedSkybox;

        public bool Load()
        {
            if(!CosmicSkyMaterial.RenderingAllowed)return false;
            Dispose();
            cosmic=CosmicSkyMaterial.Create(CosmicSkyProfile.ForArena(ArenaVariantRuntime.Current));
            if(cosmic==null)
            {
                Dispose();
                Debug.LogWarning("DIMENSIONS FRONT: panorama missing; using procedural backdrop.");
                return false;
            }
            RefreshCamera();
            return true;
        }

        public void RefreshCamera()
        {
            if(cosmic==null)return;
            Camera current=Camera.main;
            if(current==null||current.targetTexture!=null||current.GetComponent<MobaCamera>()==null)
            {RestoreCamera();return;}
            cosmic.Update(Time.unscaledTime,current.aspect);
            if(current==camera&&skybox!=null)return;
            RestoreCamera();
            camera=current;
            previousFlags=camera.clearFlags;
            skybox=camera.GetComponent<Skybox>();
            addedSkybox=skybox==null;
            if(addedSkybox)skybox=camera.gameObject.AddComponent<Skybox>();
            previousMaterial=skybox.material;
            previousEnabled=skybox.enabled;
            skybox.material=cosmic.Material;
            skybox.enabled=true;
            camera.clearFlags=CameraClearFlags.Skybox;
        }

        public void RestoreCamera()
        {
            if(skybox!=null&&cosmic!=null&&skybox.material==cosmic.Material)
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
            cosmic?.Dispose();cosmic=null;
        }
    }
}
