using System;
using System.Linq;
using System.Reflection;
using Alterverse.UI;
using UnityEngine;
using UnityEngine.Video;

static class Regression
{
    const string ClipPath="FrontEnd/InterstellarBackdropAnimated", PosterPath="FrontEnd/InterstellarBackdropAnimatedPoster";
    static int checks;
    static void Check(bool value, string message)
    { if(!value) throw new Exception(message); checks++; }
    static void Call(object target, string method, params object[] args)
    { target.GetType().GetMethod(method, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args); }
    static MenuBackgroundVideo Open()
    {
        var video=new GameObject().AddComponent<MenuBackgroundVideo>();
        Call(video,"OnEnable");
        return video;
    }
    static VideoPlayer Player(MenuBackgroundVideo video) => video.gameObject.Components.OfType<VideoPlayer>().Single();
    static void Reset(bool poster=true)
    {
        Resources.Assets.Clear(); Resources.Loads=Resources.Unloads=0;
        Resources.Assets[ClipPath]=new VideoClip();
        if(poster)Resources.Assets[PosterPath]=new Texture2D(1918,1080,TextureFormat.RGBA32,false);
        Graphics.Blits=0; Graphics.Throw=false; Graphics.LastSource=null; Graphics.LastTarget=null;
        RenderTexture.Allocated.Clear(); RenderTexture.CreateSucceeds=true; RenderTexture.active=null;
        QualitySettings.activeColorSpace=ColorSpace.Linear; GL.sRGBWrite=false;
        GUI.Draws.Clear(); GUI.color=new Color(.7f,.8f,.9f,.6f);
        Time.unscaledTime=0; Application.isBatchMode=false; Debug.Warnings=0;
    }
    static void CheckDraw(MenuCosmicBackdrop backdrop, Texture target)
    {
        GUI.Draws.Clear(); var tint=GUI.color; var rect=new Rect(0,0,2400,1080);
        backdrop.Draw(rect,target);
        Check(GUI.Draws.Count==1,"one draw per background frame; no moving-frame crossfade");
        var draw=GUI.Draws.Single();
        Check(draw.Texture==target&&!draw.Blend&&draw.Mode==ScaleMode.ScaleAndCrop,"stable texture/crop/opaque draw");
        Check(draw.Rect.x==0&&draw.Rect.y==0&&draw.Rect.width==2400&&draw.Rect.height==1080,"no startup translation or geometry change");
        Check(GUI.color.Equals(tint)&&draw.Tint.Equals(tint),"caller GUI tint preserved");
    }
    public static void Main()
    {
        Reset();
#if UNITY_SERVER
        var server=Open();
        Check(server.Image==null,"no server menu video image");
        Check(server.gameObject.Components.Count==1&&Resources.Loads==0,"no server decoder or resource loads");
        Check(RenderTexture.Allocated.Count==0&&Graphics.Blits==0,"no server render allocation or blit");
        Console.WriteLine($"Menu background server: {checks} checks passed.");
        return;
#else
        var previousTarget=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32);
        RenderTexture.active=previousTarget;
        var video=Open(); var player=Player(video); var image=video.Image;
        Check(image!=null&&image==player.targetTexture,"poster is visible through the video target before the first decoded frame");
        Check(image.width==1918&&image.height==1080,"retain authored video dimensions");
        Check(Graphics.LastSource==Resources.Assets[PosterPath]&&Graphics.LastTarget==image&&Graphics.Blits==1,"seed exact poster once");
        Check(Graphics.LastWrite&&RenderTexture.active==previousTarget&&!GL.sRGBWrite,"linear project write conversion and graphics state restoration");
        Check(player.SeededBeforePrepare&&player.Prepares==1,"seed target before decoder preparation");
        Check(player.waitForFirstFrame&&player.isLooping&&player.skipOnDrop&&player.audioOutputMode==VideoAudioOutputMode.None,"silent looping playback policy retained");
        Check(player.timeUpdateMode==VideoTimeUpdateMode.UnscaledGameTime&&player.sendFrameReadyEvents,"unscaled clock and first-frame callback retained");
        var backdrop=new MenuCosmicBackdrop();
        CheckDraw(backdrop,image);
        player.CompletePrepare(); player.DeliverFrame();
        Check(video.Image==image&&player.Plays==1&&!player.sendFrameReadyEvents,"decoder handover retains texture identity");
        foreach(float at in new[]{.05f,.15f,1f}) { Time.unscaledTime=at; CheckDraw(backdrop,video.Image); }
        Check(Graphics.Blits==1,"poster is not copied every frame");

        Call(video,"OnApplicationFocus",false); Call(video,"OnApplicationPause",true);
        Check(player.Pauses==2,"focus and pause stop playback");
        Call(video,"OnApplicationFocus",true);
        Check(player.Plays==1,"focus alone cannot resume a paused app");
        Call(video,"OnApplicationPause",false);
        Check(player.Plays==2&&video.Image==image&&Graphics.Blits==1,"normal resume retains image and does not reseed");
        ((RenderTexture)image).Release();
        Call(video,"OnApplicationFocus",true);
        var recovered=video.Image;
        Check(recovered!=null&&recovered!=image&&Graphics.Blits==2,"lost Android render target is recreated and reseeded");
        Check(image.destroyed&&player.Subscriptions==3,"old target disposed; no duplicate callback subscriptions");
        CheckDraw(backdrop,recovered);
        Call(video,"OnDisable");
        Check(video.Image==null&&recovered.destroyed&&!((RenderTexture)recovered).IsCreated(),"disable releases render target");
        Check(player.targetTexture==null&&player.clip==null&&!player.sendFrameReadyEvents,"disable disconnects decoder and callbacks");
        Call(video,"OnEnable");
        Check(video.Image!=null&&Graphics.Blits==3&&player.Subscriptions==3,"menu reopen seeds once without duplicate subscriptions");
        Call(video,"OnDestroy");
        Check(player.Subscriptions==0&&video.Image==null,"destroy unsubscribes all decoder callbacks");
        backdrop.Dispose();

        Reset(); QualitySettings.activeColorSpace=ColorSpace.Gamma; GL.sRGBWrite=true;
        video=Open();
        Check(video.Image!=null&&!Graphics.LastWrite&&GL.sRGBWrite,"gamma project avoids extra conversion and restores prior state");
        Call(video,"OnDestroy");

        Reset(); Graphics.Throw=true;
        previousTarget=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32); RenderTexture.active=previousTarget;
        video=Open(); player=Player(video);
        Check(video.Image==null&&Debug.Warnings==1,"failed seed is not exposed as initialized pixels");
        Check(RenderTexture.active==previousTarget&&!GL.sRGBWrite&&player.Prepares==1,"failed seed restores state and still prepares video");
        player.CompletePrepare(); player.DeliverFrame();
        Check(video.Image==player.targetTexture,"decoder can recover from poster blit failure");
        Call(video,"OnDestroy");

        Reset(false); video=Open(); player=Player(video);
        Check(video.Image==null&&Graphics.Blits==0,"missing poster does not expose uninitialized target");
        var fallback=new MenuCosmicBackdrop(); fallback.Draw(new Rect(0,0,2400,1080));
        Check(GUI.Draws.Count==1&&GUI.Draws[0].Texture is Texture2D,"missing poster retains neutral fallback");
        player.CompletePrepare(); player.DeliverFrame();
        Check(video.Image==player.targetTexture,"missing poster still allows video playback");
        fallback.Dispose(); Call(video,"OnDestroy");

        Reset(); Resources.Assets.Remove(ClipPath); video=Open();
        Check(video.Image==null&&RenderTexture.Allocated.Count==0&&Graphics.Blits==0,"missing video keeps existing still fallback without allocations");
        Call(video,"OnDestroy");

        Reset(); RenderTexture.CreateSucceeds=false; video=Open(); player=Player(video);
        Check(video.Image==null&&player.Prepares==0&&Graphics.Blits==0,"render-target creation failure never prepares or blits");
        Check(RenderTexture.Allocated.Single().destroyed&&Debug.Warnings==1,"failed allocation cleaned up with one warning");
        Call(video,"OnDestroy");

        Reset(); video=Open(); player=Player(video); image=video.Image;
        Time.unscaledTime=16; Call(video,"Update");
        Check(video.Image==null&&image.destroyed&&Debug.Warnings==1,"decoder timeout releases seeded target and returns to fallback");
        Call(video,"OnDestroy");

        Reset(); video=Open(); player=Player(video);
        Call(video,"OnApplicationPause",true); player.CompletePrepare();
        Time.unscaledTime=30; Call(video,"Update");
        Check(video.Image!=null&&player.Plays==0,"preparation while paused neither times out nor starts playback");
        Call(video,"OnApplicationPause",false);
        Check(player.Plays==1,"prepared video resumes after pause");
        player.Fail(); Check(video.Image==null&&Debug.Warnings==1,"decoder error retains still fallback path");
        Call(video,"OnDestroy");

        Reset(); Application.isBatchMode=true; video=Open();
        Check(video.Image==null&&Resources.Loads==0&&video.gameObject.Components.Count==1,"batch process has no decoder or video allocation");
        Call(video,"OnDestroy");
        Console.WriteLine($"Menu background: {checks} lifecycle/draw-command checks passed (no native GPU/decoder test).");
#endif
    }
}
