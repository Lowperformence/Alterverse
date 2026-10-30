using System;
using System.Linq;
using System.Reflection;
using Alterverse.Match;
using Alterverse.CameraSystem;
using Alterverse.UI;
using Alterverse.Visuals;
using UnityEngine;

static class Regression
{
    const string Noise="FrontEnd/CosmicNoise";
    static int checks;
    static void Check(bool value,string message)
    {if(!value)throw new Exception(message);checks++;}
    static void Call(MonoBehaviour target,string method,params object[] args)
    {
        if(method=="OnEnable")target.isActiveAndEnabled=true;
        if(method=="OnDisable"||method=="OnDestroy")target.isActiveAndEnabled=false;
        target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
    }
    static Texture2D Art(int width=1774,int height=887)=>new(width,height,TextureFormat.RGBA32,false);
    static void Reset()
    {
        Resources.Assets.Clear();Resources.Paths.Clear();Resources.UnloadedAssets.Clear();
        Resources.Loads=Resources.Unloads=Resources.AsyncLoads=0;
        Resources.Assets[CosmicSkyProfile.MenuPath]=Art();
        Resources.Assets[CosmicSkyMaterial.ShaderPath]=new Shader();
        Resources.Assets[Noise]=Art(256,256);
        Resources.Assets["MapPanoramas/Universe"]=Art();
        Resources.Assets["MapPanoramas/PhotoSkybox"]=new Shader();
        Graphics.Blits=0;Graphics.Throw=false;Graphics.LastTarget=null;Graphics.LastSource=null;
        Material.Allocated.Clear();RenderTexture.Allocated.Clear();RenderTexture.CreateSucceeds=true;
        RenderTexture.active=null;GL.sRGBWrite=false;UnityEngine.Camera.main=null;
        GUI.Draws.Clear();GUI.color=new Color(.7f,.8f,.9f,.6f);
        QualitySettings.activeColorSpace=ColorSpace.Linear;
        Time.unscaledTime=0;Application.isBatchMode=false;Debug.Warnings=0;
    }
    static MenuUniverseBackground Open()
    {var menu=new GameObject().AddComponent<MenuUniverseBackground>();Call(menu,"OnEnable");return menu;}
    static Camera Camera(bool game=true)
    {
        var go=new GameObject();var camera=go.AddComponent<UnityEngine.Camera>();
        if(game)go.AddComponent<MobaCamera>();return camera;
    }
    static void Guarded()
    {
        var menu=Open();using var still=new MenuCosmicBackdrop();
        Check(menu.Image==null,"headless has no menu image");
        Check(CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu)==null,"headless cannot allocate shared renderer");
        Check(Resources.Loads==0&&Material.Allocated.Count==0&&RenderTexture.Allocated.Count==0&&Graphics.Blits==0,
            "headless avoids resource loads, materials, render targets and blits");
        Call(menu,"OnDestroy");
    }
    public static void Main()
    {
        Reset();
#if UNITY_SERVER
        Guarded();Console.WriteLine($"Cosmic background server: {checks} checks passed.");
#else
        var importMethod=typeof(Alterverse.EditorTools.CosmicSkyImportSettings).GetMethod("OnPreprocessTexture",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(string path in new[]{"Assets/Alterverse/Resources/FrontEnd/CosmicMenu.png","Assets/Alterverse/Resources/FrontEnd/CosmicNoise.png"})
        {
            var importer=new UnityEditor.TextureImporter();
            importMethod.Invoke(new Alterverse.EditorTools.CosmicSkyImportSettings{assetPath=path,assetImporter=importer},null);
            bool data=path.EndsWith("CosmicNoise.png");
            Check(importer.platform?.name=="Android"&&importer.platform.overridden&&importer.mipmapEnabled&&!importer.isReadable&&
                importer.sRGBTexture==!data&&importer.wrapMode==(data?TextureWrapMode.Repeat:TextureWrapMode.Clamp),"menu imports retain their color space and compression");
        }
        foreach(string name in new[]{"Moon","Mars","GreenPlanet","Noxara","Kryon","Earth","Pyraxis","Neptune","Dark00","DiamondCore","GJ504b","Wasp76b","Trappist1e","Toi715b","Universe"})
        {
            var importer=new UnityEditor.TextureImporter();
            importMethod.Invoke(new Alterverse.EditorTools.CosmicSkyImportSettings{assetPath="Assets/Alterverse/Resources/MapPanoramas/"+name+".png",assetImporter=importer},null);
            Check(importer.platform==null,"menu importer must never change a map texture");
        }
        var fallback=new MenuCosmicBackdrop();var first=CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu);
        var second=CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu);
        var menuTexture=first.Texture;var noise=Resources.Assets[Noise];
        first.Dispose();first.Dispose();fallback.Dispose();fallback.Dispose();
        Check(!Resources.UnloadedAssets.Contains(menuTexture)&&!Resources.UnloadedAssets.Contains(noise),"remaining menu owner keeps shared resources alive");
        second.Dispose();
        Check(Resources.UnloadedAssets.Count(x=>x==menuTexture)==1&&Resources.UnloadedAssets.Count(x=>x==noise)==1,"last menu owner releases shared resources exactly once");
        Reset();Resources.Assets.Remove(CosmicSkyProfile.MenuPath);
        Check(CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu)==null&&Resources.Unloads==0,"missing menu art safely declines renderer");
        Reset();((Shader)Resources.Assets[CosmicSkyMaterial.ShaderPath]).isSupported=false;
        Check(CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu)==null&&Resources.Unloads==1&&Material.Allocated.Count==0,"unsupported menu shader releases image");

        Reset();var prior=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32);RenderTexture.active=prior;
        var menu=Open();var image=(RenderTexture)menu.Image;
        Check(image!=null&&image.width==1280&&image.height==640&&Graphics.Blits==1&&Graphics.LastPass==0,
            "menu first frame is seeded into one 1280x640 target with menu shader pass");
        Check(RenderTexture.active==prior&&!GL.sRGBWrite&&Graphics.LastWrite,"linear write and prior graphics state preserved");
        fallback=new MenuCosmicBackdrop();var tint=GUI.color;
        fallback.Draw(new Rect(0,0,2400,1080),menu.Image);
        var draw=GUI.Draws.Single();
        Check(draw.Texture==image&&!draw.Blend&&draw.Mode==ScaleMode.ScaleAndCrop&&draw.Tint.Equals(tint)&&GUI.color.Equals(tint),
            "one opaque crop preserves caller tint on all menu pages");
        int loads=Resources.Loads,materials=Material.Allocated.Count,targets=RenderTexture.Allocated.Count;
        for(int i=0;i<120;i++){Time.unscaledTime=i/120f;Call(menu,"Update");}
        Check(Graphics.Blits<=31&&menu.Image==image&&loads==Resources.Loads&&materials==Material.Allocated.Count&&targets==RenderTexture.Allocated.Count,
            "menu caps updates at 30fps with stable target and no per-frame resource allocation");
        Call(menu,"OnApplicationFocus",false);Call(menu,"OnApplicationPause",true);
        int before=Graphics.Blits;Time.unscaledTime=4;Call(menu,"Update");Call(menu,"OnApplicationFocus",true);
        Check(Graphics.Blits==before,"unfocused or paused menu stops render work");
        Call(menu,"OnApplicationPause",false);Check(Graphics.Blits==before+1&&menu.Image==image,"resume renders into same initialized target");
        image.Release();Check(menu.Image==null,"lost target is never exposed as ready pixels");
        Call(menu,"OnApplicationFocus",true);
        Check(menu.Image!=null&&menu.Image!=image&&image.destroyed&&Graphics.Blits==before+2,"Android context loss recreates and seeds target");
        image=(RenderTexture)menu.Image;Call(menu,"OnDisable");Call(menu,"OnDestroy");
        Check(image.destroyed&&!image.IsCreated()&&menu.Image==null,"disable and destroy release target idempotently");
        Check(!Resources.UnloadedAssets.Contains(menuTexture=Resources.Assets[CosmicSkyProfile.MenuPath] as Texture2D),"still survives animated renderer teardown");
        fallback.Dispose();Check(Resources.UnloadedAssets.Count(x=>x==menuTexture)==1,"final menu still releases texture exactly once");

        Reset();QualitySettings.activeColorSpace=ColorSpace.Gamma;GL.sRGBWrite=true;
        menu=Open();Check(menu.Image!=null&&!Graphics.LastWrite&&GL.sRGBWrite,"gamma target avoids extra write conversion");Call(menu,"OnDestroy");
        Reset();Graphics.Throw=true;prior=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32);RenderTexture.active=prior;
        menu=Open();Check(menu.Image==null&&Debug.Warnings==1&&RenderTexture.active==prior&&!GL.sRGBWrite&&
            Material.Allocated.All(x=>x.destroyed),"blit failure releases owned resources and restores graphics state");Call(menu,"OnDestroy");
        Reset();RenderTexture.CreateSucceeds=false;menu=Open();
        Check(menu.Image==null&&Graphics.Blits==0&&RenderTexture.Allocated.All(x=>x.destroyed),"target creation failure never exposes uninitialized pixels");Call(menu,"OnDestroy");

        Reset();
        var arena=new PhotoArenaPanorama();var camera=Camera();UnityEngine.Camera.main=camera;
        var skybox=camera.gameObject.AddComponent<Skybox>();var original=new Material(new Shader());
        skybox.material=original;skybox.enabled=false;camera.clearFlags=CameraClearFlags.Depth;
        Check(arena.Load()&&camera.clearFlags==CameraClearFlags.Skybox,"restored arena background loads");
        Check(Resources.Paths.SequenceEqual(new[]{"MapPanoramas/Universe","MapPanoramas/PhotoSkybox"}),"arena loads only the exact pre-change image and shader");
        Check(skybox.material.Properties[Shader.PropertyToID("_Panorama")]==Resources.Assets["MapPanoramas/Universe"],"arena binds the old shared Universe texture");
        Check(!Resources.Paths.Any(path=>path.StartsWith("FrontEnd/")),"arena never acquires the menu renderer or art");
        arena.Dispose();
        Check(skybox.material==original&&!skybox.enabled&&camera.clearFlags==CameraClearFlags.Depth,"restored arena background restores former camera state");

        Reset();Application.isBatchMode=true;Guarded();
        Console.WriteLine($"Cosmic background client: {checks} menu lifecycle and arena rollback checks passed (Unity API fixtures).");
#endif
    }
}

