using System;
using Alterverse.Match;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Final Pyraxis presentation guard that runs immediately before a camera renders.
    /// This prevents the original procedural Pyraxis albedo / legacy lava props from ever being
    /// visible for a frame while the later arena-dressing scripts are still starting up.
    /// </summary>
    internal static class PyraxisPreRenderFix
    {
        private static MeshRenderer groundRenderer;
        private static Material groundMaterial;
        private static Texture2D groundTexture;
        private static int worldInstanceId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCameraRendering;
            RenderPipelineManager.beginCameraRendering+=BeforeCameraRendering;
        }

        private static void BeforeCameraRendering(ScriptableRenderContext context,Camera camera)
        {
            if(camera==null||camera.cameraType!=CameraType.Game||camera.targetTexture!=null)return;

            if(!ArenaVariantRuntime.IsPyraxis)
            {
                ReleaseIfWorldChanged();
                return;
            }

            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null)return;

            int currentWorld=world.GetInstanceID();
            if(worldInstanceId!=currentWorld||groundRenderer==null)
            {
                Release();
                worldInstanceId=currentWorld;
                groundRenderer=FindGroundRenderer(world.transform);
                if(groundRenderer!=null)BuildGroundMaterial();
            }

            // A later runtime correction can replace the material during Update. Re-assert the final
            // Pyraxis material right before rendering so there is never a one-frame flash of the old map.
            if(groundRenderer!=null&&groundMaterial!=null&&groundRenderer.sharedMaterial!=groundMaterial)
                groundRenderer.sharedMaterial=groundMaterial;

            HideLegacyLavaAndSpawnLava(world.transform);
        }

        private static MeshRenderer FindGroundRenderer(Transform world)
        {
            MeshRenderer[] renderers=world.GetComponentsInChildren<MeshRenderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                MeshRenderer r=renderers[i];
                if(r==null)continue;
                if(r.gameObject.name=="DF_LunarRegolith"||r.gameObject.name=="DF_PyraxisRegolith")return r;
            }
            return null;
        }

        private static void BuildGroundMaterial()
        {
            if(groundRenderer==null||groundRenderer.sharedMaterial==null)return;
            groundTexture=BuildOrangeBlackTexture(1024,896);
            groundMaterial=new Material(groundRenderer.sharedMaterial){name="DF_Pyraxis_FinalOrangeBlackGround"};
            if(groundMaterial.HasProperty("_BaseMap"))groundMaterial.SetTexture("_BaseMap",groundTexture);
            if(groundMaterial.HasProperty("_MainTex"))groundMaterial.SetTexture("_MainTex",groundTexture);
            if(groundMaterial.HasProperty("_BaseColor"))groundMaterial.SetColor("_BaseColor",Color.white);
            if(groundMaterial.HasProperty("_Color"))groundMaterial.SetColor("_Color",Color.white);
            groundRenderer.sharedMaterial=groundMaterial;
        }

        private static Texture2D BuildOrangeBlackTexture(int width,int height)
        {
            Texture2D texture=new(width,height,TextureFormat.RGBA32,true,false)
            {
                name="DF_Pyraxis_FinalOrangeBlackAlbedo",
                wrapMode=TextureWrapMode.Clamp,
                filterMode=FilterMode.Trilinear,
                anisoLevel=8
            };

            Color32[] pixels=new Color32[width*height];
            for(int y=0;y<height;y++)
            {
                float wz=Mathf.Lerp(-42f,42f,y/(float)(height-1));
                for(int x=0;x<width;x++)
                {
                    float wx=Mathf.Lerp(-48f,48f,x/(float)(width-1));

                    // Much larger and more numerous orange islands than the previous pass.
                    // Very low-frequency fields create broad cow-like patches instead of little dots or veins.
                    float macro=Mathf.PerlinNoise(wx*.021f+31f,wz*.023f+73f);
                    float broadWarp=Mathf.PerlinNoise(wx*.043f+9f,wz*.039f+47f);
                    float secondary=Mathf.PerlinNoise(wx*.028f+113f,wz*.031f+19f);
                    float field=macro*.58f+broadWarp*.24f+secondary*.18f;
                    float orange=Mathf.SmoothStep(.405f,.535f,field);

                    // Break only the edges, not the bodies of the patches, so individual orange fields stay large.
                    float edge=Mathf.PerlinNoise(wx*.095f+91f,wz*.088f+17f);
                    orange=Mathf.Clamp01(orange+(edge-.5f)*.10f);

                    float rock=Mathf.PerlinNoise(wx*.19f+11f,wz*.19f+53f);
                    float darkValue=.78f+(rock-.5f)*.12f;
                    Color basalt=new(.028f*darkValue,.015f*darkValue,.011f*darkValue,1f);
                    Color orangeBasalt=Color.Lerp(
                        new Color(.74f,.105f,.004f,1f),
                        new Color(1f,.315f,.010f,1f),
                        Mathf.Clamp01(.25f+rock*.62f));

                    // The complete base / healing-spawn footprint is intentionally lava- and orange-free.
                    // Larger radii also cover the station geometry around the exact spawn plate.
                    float baseProtection=Mathf.Max(
                        Protection(wx,wz,-36f,0f,8.4f,10.7f),
                        Protection(wx,wz, 36f,0f,8.4f,10.7f));
                    float spawnProtection=Mathf.Max(
                        Protection(wx,wz,-27.5f,0f,6.2f,8.6f),
                        Protection(wx,wz, 27.5f,0f,6.2f,8.6f));
                    float protectedArea=Mathf.Clamp01(Mathf.Max(baseProtection,spawnProtection));
                    orange*=1f-protectedArea;

                    pixels[y*width+x]=Color.Lerp(basalt,orangeBasalt,orange*.98f);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true,true);
            return texture;
        }

        private static float Protection(float x,float z,float cx,float cz,float inner,float outer)
        {
            float d=Vector2.Distance(new Vector2(x,z),new Vector2(cx,cz));
            return 1f-Mathf.SmoothStep(inner,outer,d);
        }

        private static void HideLegacyLavaAndSpawnLava(Transform world)
        {
            if(world==null)return;
            Renderer[] renderers=world.GetComponentsInChildren<Renderer>(true);
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer r=renderers[i];
                if(r==null||r==groundRenderer)continue;

                string objectName=r.gameObject.name;
                Vector3 p=r.transform.position;
                bool inBaseOrSpawn=IsBaseOrSpawn(p);
                bool legacyLava=objectName=="LavaVent"||objectName=="LavaStream";
                bool namedLava=objectName.IndexOf("Lava",StringComparison.OrdinalIgnoreCase)>=0;
                bool centralPool=objectName.StartsWith("CentralLavaPool",StringComparison.Ordinal)
                              || objectName.StartsWith("DF_PyraxisCentralLava",StringComparison.Ordinal);

                // The old streams/vents are obsolete everywhere; the new pools are allowed only in the centre.
                if(legacyLava||(inBaseOrSpawn&&namedLava&&!centralPool))r.enabled=false;
            }
        }

        private static bool IsBaseOrSpawn(Vector3 p)
        {
            Vector2 point=new(p.x,p.z);
            if((point-new Vector2(-36f,0f)).sqrMagnitude<10.7f*10.7f)return true;
            if((point-new Vector2( 36f,0f)).sqrMagnitude<10.7f*10.7f)return true;
            if((point-new Vector2(-27.5f,0f)).sqrMagnitude<8.6f*8.6f)return true;
            if((point-new Vector2( 27.5f,0f)).sqrMagnitude<8.6f*8.6f)return true;
            return false;
        }

        private static void ReleaseIfWorldChanged()
        {
            if(worldInstanceId==0)return;
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null||world.GetInstanceID()!=worldInstanceId)Release();
        }

        private static void Release()
        {
            if(groundMaterial!=null)UnityEngine.Object.Destroy(groundMaterial);
            if(groundTexture!=null)UnityEngine.Object.Destroy(groundTexture);
            groundRenderer=null;
            groundMaterial=null;
            groundTexture=null;
            worldInstanceId=0;
        }
    }
}
