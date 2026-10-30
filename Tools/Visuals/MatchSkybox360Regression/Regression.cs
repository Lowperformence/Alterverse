using System;
using System.Linq;
using System.Reflection;
using Alterverse.EditorTools;
using Alterverse.Visuals;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

static class Regression
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
    static (Camera,GameObject,Material) Setup(bool existing=true)
    {
        foreach(var obj in Object.All.ToArray())Object.DestroyImmediate(obj);
        Object.All.Clear();AssetDatabase.Assets.Clear();AssetDatabase.Folders.Clear();AssetDatabase.Writes.Clear();Resources.Assets.Clear();Resources.Loads=0;
        Shader.Missing=null;Shader.Cache.Clear();Material.SkyPasses=1;Mesh.Throw=false;Application.isBatchMode=false;
        Alterverse.Match.ArenaVariantRuntime.Current=Alterverse.Match.ArenaVariant.Mars;
        foreach(var name in new[]{"Sky360_StarsNebula","Planet_Azure","Planet_Copper","Planet_Ice","Planet_Violet","Planet_Moon"})
            AssetDatabase.Assets[MatchSkybox360Assets.Root+"/Textures/"+name+".png"]=new Texture2D(1024,512,TextureFormat.RGBA32,false);
        var world=new GameObject("DF_MatchWorld");var go=new GameObject("Main Camera");var camera=go.AddComponent<Camera>();go.AddComponent<Alterverse.CameraSystem.MobaCamera>();Camera.main=camera;
        camera.transform.position=new(40,12.24f,-9.18f);camera.transform.rotation=Quaternion.Euler(53,0,0);
        var old=new Material(new Shader{name="Existing background"});
        if(existing){var sky=go.AddComponent<Skybox>();sky.material=old;sky.enabled=false;}
        return(camera,world,old);
    }
    static void Dependencies()
    {
        Setup();MatchSkybox360Assets.EnsureMaterials();
        Check(Resources.Assets.Count==7,"seven material build dependencies in Resources");
        var sky=(Material)Resources.Assets[MatchSkybox360.ResourceRoot+"Sky"];
        Check(sky.shader.name=="Skybox/Panoramic"&&sky.passCount==1,"standard single-pass sky shader");
        Check((float)sky.Properties["_Mapping"]==1&&(float)sky.Properties["_ImageType"]==0,"360 latlong mapping");
        Check(ReferenceEquals(sky.Properties["_MainTex"],AssetDatabase.Assets[MatchSkybox360Assets.Root+"/Textures/Sky360_StarsNebula.png"]),"panorama is a material dependency");
        foreach(var pair in Resources.Assets.Where(x=>x.Key.Contains("Planet_")))
            Check(((Material)pair.Value).shader.name=="Universal Render Pipeline/Lit"&&((Material)pair.Value).Properties["_BaseMap"] is Texture2D,"serialized planet shader and image");
        Check(AssetDatabase.Writes.All(x=>x.StartsWith(MatchSkybox360Assets.Root+"/Generated")),"builder writes only owned materials");
        int count=AssetDatabase.Assets.Count;MatchSkybox360Assets.EnsureMaterials();Check(AssetDatabase.Assets.Count==count,"repeat import reuses assets");
        foreach(int bad in new[]{0,1,2})
        {
            Setup();if(bad==0)Shader.Missing="Skybox/Panoramic";if(bad==1)AssetDatabase.Assets.Remove(MatchSkybox360Assets.Root+"/Textures/Planet_Moon.png");if(bad==2)Material.SkyPasses=2;
            bool rejected=false;try{MatchSkybox360Assets.EnsureMaterials();}catch(UnityEditor.Build.BuildFailedException){rejected=true;}Check(rejected,"invalid build dependencies rejected");
        }
    }
    static void Cycle()
    {
        var(cam,world,old)=Setup();MatchSkybox360Assets.EnsureMaterials();var position=cam.transform.position;var rotation=cam.transform.rotation;var bg=cam.backgroundColor;
        var s=new MatchSkybox360();Check(s.Load(world.transform),"regular match loads automatically");
        Check(cam.GetComponent<Skybox>().enabled&&cam.clearFlags==CameraClearFlags.Skybox,"local skybox enabled");
        Check((float)cam.GetComponent<Skybox>().material.Properties["_Rotation"]==29.5f,"per-arena sky orientation");
        Check(cam.cullingMask==1234&&cam.farClipPlane==1000&&cam.fieldOfView==46&&cam.nearClipPlane==.3f&&!cam.orthographic&&cam.targetTexture==null,"projection, culling, clipping preserved");
        Check(cam.transform.position.Equals(position)&&cam.transform.rotation.Equals(rotation)&&cam.backgroundColor.Equals(bg),"camera pose and color preserved");
        var planets=Object.All.OfType<GameObject>().Where(o=>!o.destroyed&&o.name.StartsWith("Planet_")).ToArray();var root=GameObject.Find("DF_Skybox360");
        Check(planets.Length==5&&planets.Select(o=>o.GetComponent<MeshFilter>().sharedMesh).Distinct().Count()==1,"five 3D planets share mesh");Check(root.transform.parent==world.transform,"world owns sky geometry");
        var sphere=planets[0].GetComponent<MeshFilter>().sharedMesh;Check(sphere.Triangles.Count/3==3968,"sphere topology");
        for(int i=0;i<sphere.Triangles.Count;i+=3)
        {
            int a=sphere.Triangles[i],b=sphere.Triangles[i+1],c=sphere.Triangles[i+2];Check(a>=0&&b>=0&&c>=0&&a<sphere.Vertices.Count&&b<sphere.Vertices.Count&&c<sphere.Vertices.Count,"sphere indices");
            var va=sphere.Vertices[a];var vb=sphere.Vertices[b];var vc=sphere.Vertices[c];Check(Vector3.Dot(Vector3.Cross(vb-va,vc-va),va+vb+vc)>0,"outward sphere winding");
        }
        foreach(var planet in planets)foreach(var v in sphere.Vertices){float depth=Vector3.Dot(planet.transform.TransformPoint(v)-position,rotation*Vector3.forward);Check(depth>350&&depth<cam.farClipPlane,"planets behind arena within far clip");}
        var ring=GameObject.Find("Azure_Rings").GetComponent<MeshFilter>().sharedMesh;Check(ring.Triangles.Count/3==2560&&ring.Triangles.All(i=>i>=0&&i<ring.Vertices.Count),"ring topology");
        int before=Object.All.Count;for(int i=0;i<120;i++)s.RefreshCamera();Check(Object.All.Count==before,"no per-frame allocations");
        s.RestoreCamera();Check(cam.GetComponent<Skybox>().material==old&&!cam.GetComponent<Skybox>().enabled&&cam.clearFlags==CameraClearFlags.SolidColor&&!root.activeSelf,"disable restores background and hides planets");
        s.RefreshCamera();Check(root.activeSelf&&GameObject.Find("DF_Skybox360")==root,"re-enable reuses geometry");s.Dispose();s.Dispose();
        Check(cam.GetComponent<Skybox>().material==old&&!cam.GetComponent<Skybox>().enabled&&cam.clearFlags==CameraClearFlags.SolidColor,"dispose restores state idempotently");Check(root.destroyed&&Object.All.OfType<Mesh>().All(x=>x.destroyed),"owned geometry destroyed");Check(Resources.Assets.Values.All(x=>!x.destroyed),"shared materials preserved");
    }
    static void Failures()
    {
        var(cam,world,old)=Setup(false);MatchSkybox360Assets.EnsureMaterials();var s=new MatchSkybox360();s.Load(world.transform);s.RestoreCamera();s.RefreshCamera();s.Dispose();Check(cam.GetComponent<Skybox>()==null,"added skybox removed on dispose");
        (cam,world,old)=Setup();MatchSkybox360Assets.EnsureMaterials();s=new();s.Load(world.transform);var later=new Material(new Shader());cam.GetComponent<Skybox>().material=later;cam.clearFlags=CameraClearFlags.Depth;s.RefreshCamera();Check(!Object.All.OfType<GameObject>().Single(o=>!o.destroyed&&o.name=="DF_Skybox360").activeSelf,"later owner hides planets");s.Dispose();Check(cam.GetComponent<Skybox>().material==later&&cam.clearFlags==CameraClearFlags.Depth,"later owner not overwritten");
        (cam,world,old)=Setup();MatchSkybox360Assets.EnsureMaterials();s=new();s.Load(world.transform);var go=new GameObject("Replacement Camera");var replacement=go.AddComponent<Camera>();go.AddComponent<Alterverse.CameraSystem.MobaCamera>();Camera.main=replacement;s.RefreshCamera();Check(cam.GetComponent<Skybox>().material==old&&replacement.GetComponent<Skybox>().enabled,"camera replacement rebinds");s.Dispose();
        foreach(int failure in new[]{0,1,2})
        {
            (cam,world,old)=Setup();MatchSkybox360Assets.EnsureMaterials();if(failure==0)Resources.Assets.Remove(MatchSkybox360.ResourceRoot+"Sky");if(failure==1)((Material)Resources.Assets[MatchSkybox360.ResourceRoot+"Sky"]).shader.isSupported=false;if(failure==2)Material.SkyPasses=2;
            s=new();Check(!s.Load(world.transform),"invalid runtime material leaves fallback available");Check(cam.GetComponent<Skybox>().material==old&&cam.clearFlags==CameraClearFlags.SolidColor,"failed load preserves camera");s.Dispose();
        }
        (cam,world,old)=Setup();MatchSkybox360Assets.EnsureMaterials();Mesh.Throw=true;s=new();Check(s.Load(world.transform)&&cam.GetComponent<Skybox>().material!=old,"geometry failure retains star sky");s.Dispose();Check(Object.All.OfType<Mesh>().All(m=>m.destroyed),"failed geometry released");
        Setup();Application.isBatchMode=true;s=new();Check(!s.Load(new GameObject("Headless").transform)&&Resources.Loads==0,"headless does not load sky resources");s.Dispose();
    }
    static void Imports()
    {
        var method=typeof(MatchSkybox360TextureImporter).GetMethod("OnPreprocessTexture",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach(var path in new[]{"Assets/Alterverse/Resources/FrontEnd/CosmicMenu.png","Assets/Alterverse/Resources/MapPanoramas/Universe.png"}){var i=new TextureImporter{maxTextureSize=17};method.Invoke(new MatchSkybox360TextureImporter{assetPath=path,assetImporter=i},null);Check(i.maxTextureSize==17&&i.platform==null,"existing imports unchanged");}
        var own=new TextureImporter();method.Invoke(new MatchSkybox360TextureImporter{assetPath=MatchSkybox360Assets.Root+"/Textures/Sky360_StarsNebula.png",assetImporter=own},null);Check(own.maxTextureSize==2048&&own.platform?.format==TextureImporterFormat.ASTC_6x6&&own.platform.overridden&&own.sRGBTexture&&own.mipmapEnabled&&!own.isReadable,"Android texture profile");
    }
    static void Main()
    {
#if UNITY_SERVER
        Setup();var s=new MatchSkybox360();Check(!s.Load(new GameObject("Server").transform),"server does not activate sky");s.RefreshCamera();s.RestoreCamera();s.Dispose();Check(Resources.Loads==0,"server resource loads zero");Console.WriteLine($"Match sky server: {checks} checks passed.");
#else
        Dependencies();Cycle();Failures();Imports();Console.WriteLine($"Match sky client: {checks} checks passed (API fixtures; native Unity/Android not run).");
#endif
    }
}
