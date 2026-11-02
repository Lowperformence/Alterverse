using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.UI
{
    /// <summary>One opaque, pre-rendered menu target. No video decoder or extra camera.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuUniverseBackground : MonoBehaviour
    {
        const int TargetWidth=1280;
        const float FrameInterval=1f/30f;
        CosmicSkyMaterial sky;
        RenderTexture image;
        bool ready,paused,unfocused;
        float nextFrame;
        public Texture Image=>ready&&image!=null&&image.IsCreated()?image:null;

        void OnEnable()
        {
            if(!CosmicSkyMaterial.RenderingAllowed)return;
            Release();
            sky=CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu);
            if(sky==null)return;
            RenderFrame();
        }

        void Update()
        {
            if(paused||unfocused||sky==null||Time.unscaledTime<nextFrame)return;
            if ((Application.isMobilePlatform || !Alterverse.Performance.MobilePerformanceProfile.AutoModeEnabled) &&
                Alterverse.Performance.MobilePerformanceProfile.Current == Alterverse.Performance.MobileQuality.Battery &&
                ready)
                return;
            RenderFrame();
        }

        void RenderFrame()
        {
            if(sky==null)return;
            bool lowEnd = (Application.isMobilePlatform || !Alterverse.Performance.MobilePerformanceProfile.AutoModeEnabled) &&
                          Alterverse.Performance.MobilePerformanceProfile.Current == Alterverse.Performance.MobileQuality.Battery;
            bool midMobile = Application.isMobilePlatform &&
                             Alterverse.Performance.MobilePerformanceProfile.Current == Alterverse.Performance.MobileQuality.Balanced;
            int targetW = lowEnd ? 640 : midMobile ? 960 : TargetWidth;
            if(image==null||!image.IsCreated()||image.width!=targetW)
            {
                ReleaseTarget();
                int height=Mathf.Max(1,Mathf.RoundToInt(targetW*sky.Texture.height/(float)sky.Texture.width));
                image=new RenderTexture(targetW,height,0,RenderTextureFormat.ARGB32)
                {name="DF_MenuCosmicBackground",filterMode=FilterMode.Bilinear,
                    wrapMode=TextureWrapMode.Clamp,useMipMap=false,autoGenerateMips=false};
                if(!image.Create()) {Fail("Could not create the menu background target.");return;}
            }
            RenderTexture previousTarget=RenderTexture.active;
            bool previousWrite=GL.sRGBWrite;
            try
            {
                sky.Update(Time.unscaledTime,image.width/(float)image.height);
                GL.sRGBWrite=image.sRGB;
                Graphics.Blit(sky.Texture,image,sky.Material,0);
                // The first successful render is ready before OnGUI; retain the same target afterwards.
                ready=true;
                nextFrame=Time.unscaledTime+(lowEnd?2f:midMobile?(1f/15f):FrameInterval);
            }
            catch(System.Exception error)
            {
                Fail("Menu background rendering failed: "+error.Message);
            }
            finally
            {
                RenderTexture.active=previousTarget;
                GL.sRGBWrite=previousWrite;
            }
        }

        void OnApplicationPause(bool value){paused=value;Resume();}
        void OnApplicationFocus(bool focused){unfocused=!focused;Resume();}
        void Resume()
        {
            if(!isActiveAndEnabled||paused||unfocused||sky==null)return;
            RenderFrame();
        }
        void Fail(string reason)
        {
            Release();
            Debug.LogWarning("DIMENSIONS FRONT: using the authored menu still. "+reason,this);
        }
        void ReleaseTarget()
        {
            ready=false;
            if(image==null)return;
            image.Release();Destroy(image);image=null;
        }
        void Release(){ReleaseTarget();sky?.Dispose();sky=null;}
        void OnDisable()=>Release();
        void OnDestroy()=>Release();
    }
}

