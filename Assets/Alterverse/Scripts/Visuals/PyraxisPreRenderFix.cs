using System;
using Alterverse.Match;
using Alterverse.Combat;
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
        private static GameObject activeWorld;

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

            if(activeWorld!=world||groundRenderer==null)
            {
                Release();
                activeWorld=world;
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
            groundMaterial=new Material(groundRenderer.sharedMaterial){name="DF_Pyraxis_FinalCowLavaGround"};
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
                name="DF_Pyraxis_FinalCowLavaAlbedo",
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

                    // Very broad overlapping noise fields create large, rounded, irregular islands.
                    // The low frequencies are intentional: Pyraxis should read like orange/black cow spots,
                    // never like thin veins, cracks or little dots.
                    float blobA=Mathf.PerlinNoise(wx*.016f+21f,wz*.018f+77f);
                    float blobB=Mathf.PerlinNoise(wx*.022f+91f,wz*.020f+13f);
                    float blobC=Mathf.PerlinNoise(wx*.029f+143f,wz*.026f+51f);
                    float cowField=blobA*.50f+blobB*.32f+blobC*.18f;
                    float orange=Mathf.SmoothStep(.335f,.475f,cowField);

                    // Mild edge warp keeps the large bodies intact while preventing perfect round blobs.
                    float edgeWarp=Mathf.PerlinNoise(wx*.067f+44f,wz*.061f+99f);
                    orange=Mathf.Clamp01(orange+(edgeWarp-.5f)*.075f);

                    float rock=Mathf.PerlinNoise(wx*.18f+11f,wz*.18f+53f);
                    float darkValue=.80f+(rock-.5f)*.10f;
                    Color basalt=new(.026f*darkValue,.014f*darkValue,.010f*darkValue,1f);
                    Color orangeBasalt=Color.Lerp(
                        new Color(.78f,.115f,.004f,1f),
                        new Color(1f,.35f,.012f,1f),
                        Mathf.Clamp01(.28f+rock*.60f));

                    // Keep the complete base and healing-spawn footprints clean on both sides.
                    float baseProtection=Mathf.Max(
                        Protection(wx,wz,-36f,0f,8.8f,11.8f),
                        Protection(wx,wz, 36f,0f,8.8f,11.8f));
                    float spawnProtection=Mathf.Max(
                        Protection(wx,wz,-ArenaLayout.HealingX/ArenaLayout.LengthScale,0f,7.2f,9.8f),
                        Protection(wx,wz, ArenaLayout.HealingX/ArenaLayout.LengthScale,0f,7.2f,9.8f));
                    float protectedArea=Mathf.Clamp01(Mathf.Max(baseProtection,spawnProtection));
                    orange*=1f-protectedArea;

                    Color ground=Color.Lerp(basalt,orangeBasalt,orange*.98f);

                    // True molten lava is also cow-spotted. It exists only in the middle half of the map,
                    // uses even broader blobs, and never reaches either protected spawn/base footprint.
                    float moltenA=Mathf.PerlinNoise(wx*.032f+201f,wz*.036f+37f);
                    float moltenB=Mathf.PerlinNoise(wx*.049f+17f,wz*.043f+151f);
                    float moltenField=moltenA*.70f+moltenB*.30f;
                    float moltenMask=Mathf.SmoothStep(.535f,.665f,moltenField);
                    float centralBand=1f-Mathf.SmoothStep(18.0f,21.0f,Mathf.Abs(wx));
                    moltenMask*=centralBand*(1f-protectedArea);

                    float hot=Mathf.SmoothStep(.34f,.88f,moltenMask);
                    Color molten=Color.Lerp(
                        new Color(.94f,.055f,.002f,1f),
                        new Color(1f,.63f,.025f,1f),hot);
                    pixels[y*width+x]=Color.Lerp(ground,molten,Mathf.Clamp01(moltenMask*.98f));
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
            if((point-new Vector2(-ArenaLayout.CoreX,0f)).sqrMagnitude<11.8f*11.8f)return true;
            if((point-new Vector2( ArenaLayout.CoreX,0f)).sqrMagnitude<11.8f*11.8f)return true;
            if((point-new Vector2(-ArenaLayout.HealingX,0f)).sqrMagnitude<9.8f*9.8f)return true;
            if((point-new Vector2( ArenaLayout.HealingX,0f)).sqrMagnitude<9.8f*9.8f)return true;
            return false;
        }

        private static void ReleaseIfWorldChanged()
        {
            if(activeWorld==null)return;
            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null||world!=activeWorld)Release();
        }

        private static void Release()
        {
            if(groundMaterial!=null)UnityEngine.Object.Destroy(groundMaterial);
            if(groundTexture!=null)UnityEngine.Object.Destroy(groundTexture);
            groundRenderer=null;
            groundMaterial=null;
            groundTexture=null;
            activeWorld=null;
        }
    }
}

