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
    const string Noise="MapPanoramas/CosmicNoise";
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
        foreach(ArenaVariant arena in Enum.GetValues(typeof(ArenaVariant)))
            Resources.Assets[CosmicSkyProfile.ForArena(arena).ResourcePath]=Art();
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
        var arena=new PhotoArenaPanorama();
        Check(menu.Image==null&&!arena.Load(),"headless has no visual image");
        Check(CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu)==null,"headless cannot allocate shared renderer");
        Check(Resources.Loads==0&&Material.Allocated.Count==0&&RenderTexture.Allocated.Count==0&&Graphics.Blits==0,
            "headless avoids resource loads, materials, render targets and blits");
        Call(menu,"OnDestroy");arena.Dispose();
    }
    public static void Main()
    {
        Reset();
#if UNITY_SERVER
        Guarded();Console.WriteLine($"Cosmic background server: {checks} checks passed.");
#else
        var profiles=Enum.GetValues(typeof(ArenaVariant)).Cast<ArenaVariant>().Select(CosmicSkyProfile.ForArena).ToArray();
        Check(profiles.Length==14&&profiles.Select(x=>x.ResourcePath).Distinct().Count()==14,"all fourteen maps use distinct artwork");
        Check(profiles.Select(x=>x.Seed).Distinct().Count()==14,"each map has its own deterministic effect sequence");
        var importMethod=typeof(Alterverse.EditorTools.CosmicSkyImportSettings).GetMethod("OnPreprocessTexture",BindingFlags.Instance|BindingFlags.NonPublic);
        foreach(string path in profiles.Select(x=>"Assets/Alterverse/Resources/"+x.ResourcePath+".png")
            .Append("Assets/Alterverse/Resources/FrontEnd/CosmicMenu.png").Append("Assets/Alterverse/Resources/MapPanoramas/CosmicNoise.png"))
        {
            var importer=new UnityEditor.TextureImporter();
            var processor=new Alterverse.EditorTools.CosmicSkyImportSettings{assetPath=path,assetImporter=importer};
            importMethod.Invoke(processor,null);
            bool data=path.EndsWith("CosmicNoise.png");
            Check(importer.platform?.name=="Android"&&importer.platform.overridden&&importer.mipmapEnabled&&!importer.isReadable&&
                importer.platform.format==UnityEditor.TextureImporterFormat.ASTC_6x6&&importer.sRGBTexture==!data&&
                importer.wrapMode==(data?TextureWrapMode.Repeat:TextureWrapMode.Clamp)&&importer.maxTextureSize==(data?256:2048),
                "selected backgrounds receive Android compression, color-space and sampling settings");
        }
        foreach(string path in new[]{"Assets/Other/Moon.png","Assets/Alterverse/Resources/MapPanoramas/Universe.png",
            "Assets/Alterverse/Resources/MapPanoramas/Moon.jpg","Assets/Alterverse/Resources/FrontEnd/MenuCardAtlas.png"})
        {
            var importer=new UnityEditor.TextureImporter();
            importMethod.Invoke(new Alterverse.EditorTools.CosmicSkyImportSettings{assetPath=path,assetImporter=importer},null);
            Check(importer.platform==null,"unrelated artwork import settings remain untouched");
        }
        foreach(var profile in profiles)
        {
            using(var sky=CosmicSkyMaterial.Create(profile))
            {
                Check(sky!=null&&sky.Texture==Resources.Assets[profile.ResourcePath],"selected map image bound");
                Check(Resources.Paths.Count==3&&Resources.Paths.Contains(profile.ResourcePath)&&
                    Resources.Paths.Contains(Noise)&&!Resources.Paths.Contains("MapPanoramas/Universe"),"only selected map, shader and noise are loaded");
                sky.Update(float.NaN,float.PositiveInfinity);
                var values=(Vector4)sky.Material.Properties[Shader.PropertyToID("_CosmicParams")];
                Check(float.IsFinite(values.z)&&values.x==profile.Exposure,"invalid clock is sanitized and exposure is fixed");
            }
            Check(Resources.Unloads==2&&Material.Allocated.All(x=>x.destroyed),"map image and noise released with owned material");
            Reset();
        }
        // Still/animated menu and arena can briefly coexist during scene changes.
        var fallback=new MenuCosmicBackdrop();var first=CosmicSkyMaterial.Create(CosmicSkyProfile.MainMenu);
        var second=CosmicSkyMaterial.Create(profiles[0]);
        var menuTexture=first.Texture;var noise=Resources.Assets[Noise];
        first.Dispose();first.Dispose();
        Check(!Resources.UnloadedAssets.Contains(menuTexture)&&!Resources.UnloadedAssets.Contains(noise),"other owners keep shared textures alive");
        fallback.Dispose();fallback.Dispose();
        Check(Resources.UnloadedAssets.Count(x=>x==menuTexture)==1&&!Resources.UnloadedAssets.Contains(noise),"still releases only its last image lease");
        second.Dispose();Check(Resources.UnloadedAssets.Count(x=>x==noise)==1,"shared noise released only after final owner");

        Reset();Resources.Assets.Remove(profiles[0].ResourcePath);
        Check(CosmicSkyMaterial.Create(profiles[0])==null&&Resources.Unloads==0,"missing image safely declines renderer");
        Reset();((Shader)Resources.Assets[CosmicSkyMaterial.ShaderPath]).isSupported=false;
        Check(CosmicSkyMaterial.Create(profiles[0])==null&&Resources.Unloads==1&&Material.Allocated.Count==0,"unsupported shader releases image without allocation");

        int visible=0,samples=0;
        foreach(var profile in profiles)
        for(float time=0;time<600;time+=.07f)
        {
            Vector4 meteor=CosmicSkyMotion.Meteor(time,profile.MeteorInterval,profile.Seed,2.2f,profile.MeteorStrength);
            Vector4 repeat=CosmicSkyMotion.Meteor(time,profile.MeteorInterval,profile.Seed,2.2f,profile.MeteorStrength);
            if(!meteor.Equals(repeat)||!float.IsFinite(meteor.x)||!float.IsFinite(meteor.y)||meteor.z<0||meteor.z>1)
                throw new Exception("meteor sequence must be finite, deterministic and bounded");
            if(meteor.z>0){visible++;if(meteor.x<0||meteor.x>1||meteor.y<0||meteor.y>1)throw new Exception("meteor head leaves safe image area");}
            samples++;
        }
        Check(visible>0&&visible<samples*.05,"shooting stars are brief and rare over ten simulated minutes per map");

        Reset();var prior=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32);RenderTexture.active=prior;
        var menu=Open();var image=(RenderTexture)menu.Image;
        Check(image!=null&&image.width==1280&&image.height==640&&Graphics.Blits==1&&Graphics.LastPass==1,
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

        Reset();ArenaVariantRuntime.Current=ArenaVariant.Neptune;
        var arena=new PhotoArenaPanorama();var camera=Camera();UnityEngine.Camera.main=camera;
        var skybox=camera.gameObject.AddComponent<Skybox>();var original=new Material(new Shader());
        skybox.material=original;skybox.enabled=false;camera.clearFlags=CameraClearFlags.Depth;
        Check(arena.Load()&&camera.clearFlags==CameraClearFlags.Skybox&&skybox.enabled&&skybox.material!=original,
            "only gameplay camera receives selected panorama");
        var owned=skybox.material;var replacement=Camera();UnityEngine.Camera.main=replacement;arena.RefreshCamera();
        Check(skybox.material==original&&!skybox.enabled&&camera.clearFlags==CameraClearFlags.Depth,
            "camera replacement restores former camera material, enable state and clear flags");
        var added=replacement.GetComponent<Skybox>();
        Check(added!=null&&added.material==owned,"replacement gameplay camera receives current material");
        replacement.targetTexture=new RenderTexture(2,2,0,RenderTextureFormat.ARGB32);arena.RefreshCamera();
        Check(added.destroyed&&replacement.clearFlags==CameraClearFlags.SolidColor,"newly assigned render-target camera is detached immediately");
        UnityEngine.Camera.main=Camera(false);arena.RefreshCamera();
        Check(UnityEngine.Camera.main.GetComponent<Skybox>()==null,"preview cameras remain untouched");
        UnityEngine.Camera.main=camera;arena.RefreshCamera();
        var external=new Material(new Shader());skybox.material=external;camera.clearFlags=CameraClearFlags.Nothing;
        arena.Dispose();arena.Dispose();
        Check(skybox.material==external&&camera.clearFlags==CameraClearFlags.Nothing&&owned.destroyed,
            "teardown respects external camera ownership and destroys only own material");

        Reset();Application.isBatchMode=true;Guarded();
        Console.WriteLine($"Cosmic background client: {checks} lifecycle, map-selection and effect checks passed (Unity API fixtures).");
#endif
    }
}
