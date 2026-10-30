using System;
using System.Linq;
using System.Reflection;
using Alterverse.EditorTools;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

static class Regression
{
    static int checks;
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
    static object Call(object value,string method)=>value.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,null);
    static (Camera,GameObject,Material) Setup(bool withSkybox=true)
    {
        foreach(var obj in Object.All.ToArray())Object.DestroyImmediate(obj);Object.All.Clear();AssetDatabase.Assets.Clear();
        Shader.Missing=null;Material.SkyPasses=1;Mesh.Throw=false;EditorApplication.isPlaying=true;EditorApplication.isCompiling=false;
        Alterverse.UI.MainMenuController.IsVisible=false;Alterverse.Multiplayer.CoopAuthority.Active=false;Unity.Netcode.NetworkManager.Singleton=null;
        foreach(string name in new[]{"Sky360_StarsNebula","Planet_Azure","Planet_Copper","Planet_Ice","Planet_Violet","Planet_Moon"})
            AssetDatabase.Assets[Skybox360PreviewSession.TextureRoot+name+".png"]=new Texture2D(1024,512,TextureFormat.RGBA32,false);
        var world=new GameObject("DF_MatchWorld");new GameObject("AV_Player");
        var go=new GameObject("Main Camera");var cam=go.AddComponent<Camera>();go.AddComponent<Alterverse.CameraSystem.MobaCamera>();Camera.main=cam;
        cam.transform.position=new(40,12.24f,-9.18f);cam.transform.rotation=Quaternion.Euler(53,0,0);
        var old=new Material(new Shader{name="Old photograph"});
        if(withSkybox){var sky=go.AddComponent<Skybox>();sky.material=old;sky.enabled=false;}
        return(cam,world,old);
    }
    static void CycleAndGeometry()
    {
        var(cam,world,old)=Setup();var pos=cam.transform.position;var rot=cam.transform.rotation;var color=cam.backgroundColor;
        Check(Skybox360PreviewSession.TryCreate(cam,world,out var s,out var error)&&error==null,"create preview");
        Check(s.IsCurrent(cam,world)&&cam.GetComponent<Skybox>().enabled&&cam.clearFlags==CameraClearFlags.Skybox,"preview owns only local skybox");
        Check(cam.cullingMask==1234&&cam.farClipPlane==1000&&cam.fieldOfView==46&&cam.nearClipPlane==.3f&&!cam.orthographic&&cam.targetTexture==null,"camera projection, target and culling are preserved");
        Check(cam.transform.position.Equals(pos)&&cam.transform.rotation.Equals(rot)&&cam.backgroundColor.Equals(color),"camera pose and background color preserved");
        var parts=Object.All.OfType<GameObject>().Where(o=>!o.destroyed&&o.name.StartsWith("Planet_")).ToArray();
        Check(parts.Length==5,"five real planet objects");
        Check(parts.Select(p=>p.GetComponent<MeshFilter>().sharedMesh).Distinct().Count()==1,"shared planet mesh");
        Check(parts.All(p=>p.transform.parent==GameObject.Find("DF_Skybox360_EditorPreview").transform),"preview hierarchy is isolated");
        var mesh=parts[0].GetComponent<MeshFilter>().sharedMesh;
        Check(mesh.Triangles.Count/3==3968,"sphere topology triangle count");
        for(int i=0;i<mesh.Triangles.Count;i+=3)
        {
            int a=mesh.Triangles[i],b=mesh.Triangles[i+1],c=mesh.Triangles[i+2];
            Check(a>=0&&b>=0&&c>=0&&a<mesh.Vertices.Count&&b<mesh.Vertices.Count&&c<mesh.Vertices.Count,"mesh indices valid");
            Vector3 va=mesh.Vertices[a],vb=mesh.Vertices[b],vc=mesh.Vertices[c];
            Check(Vector3.Dot(Vector3.Cross(vb-va,vc-va),va+vb+vc)>0,"sphere faces point outward for normal depth testing");
        }
        var forward=rot*Vector3.forward;
        foreach(var p in parts)foreach(var v in mesh.Vertices)
        {
            float depth=Vector3.Dot(p.transform.TransformPoint(v)-pos,forward);
            Check(depth>350&&depth<cam.farClipPlane,"planet geometry stays far behind gameplay and inside far clip");
        }
        var ring=GameObject.Find("Azure_Rings").GetComponent<MeshFilter>().sharedMesh;
        Check(ring.Triangles.Count/3==2560&&ring.Triangles.All(i=>i>=0&&i<ring.Vertices.Count),"ring indices valid");
        Check(Object.All.OfType<MeshRenderer>().Where(o=>!o.destroyed).All(o=>!o.receiveShadows&&o.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.Off),"planets cannot cast shadows into arena");
        s.Dispose();s.Dispose();
        Check(cam.GetComponent<Skybox>().material==old&&!cam.GetComponent<Skybox>().enabled&&cam.clearFlags==CameraClearFlags.SolidColor,"exact previous skybox state restored");
        Check(GameObject.Find("DF_Skybox360_EditorPreview")==null&&Object.All.Where(o=>(o.hideFlags&HideFlags.HideAndDontSave)==HideFlags.HideAndDontSave).All(o=>o.destroyed),"temporary objects and allocations released");
        Check(AssetDatabase.Assets.Values.All(t=>!t.destroyed),"imported textures remain shared assets");
    }
    static void OwnershipAndFailure()
    {
        var(cam,world,old)=Setup(false);
        Check(Skybox360PreviewSession.TryCreate(cam,world,out var s,out _),"camera without skybox supported");s.Dispose();
        Check(cam.GetComponent<Skybox>()==null&&cam.clearFlags==CameraClearFlags.SolidColor,"only added Skybox is removed");
        (cam,world,old)=Setup();Skybox360PreviewSession.TryCreate(cam,world,out s,out _);
        var later=new Material(new Shader());cam.GetComponent<Skybox>().material=later;cam.clearFlags=CameraClearFlags.Depth;
        Check(!s.IsCurrent(cam,world),"camera owner change detected");s.Dispose();
        Check(cam.GetComponent<Skybox>().material==later&&cam.clearFlags==CameraClearFlags.Depth,"later owner is not overwritten");
        (cam,world,old)=Setup();Skybox360PreviewSession.TryCreate(cam,world,out s,out _);cam.clearFlags=CameraClearFlags.Depth;s.Dispose();
        Check(cam.clearFlags==CameraClearFlags.Depth,"transition clear flags are preserved");
        foreach(int mode in new[]{0,1,2,3})
        {
            (cam,world,old)=Setup();
            if(mode==0)AssetDatabase.Assets.Remove(Skybox360PreviewSession.TextureRoot+"Planet_Moon.png");
            if(mode==1)Shader.Missing="Skybox/Panoramic";
            if(mode==2)Material.SkyPasses=2;
            if(mode==3)Mesh.Throw=true;
            Check(!Skybox360PreviewSession.TryCreate(cam,world,out s,out string error)&&s==null&&!string.IsNullOrEmpty(error),"setup failure declines preview");
            Check(cam.GetComponent<Skybox>().material==old&&!cam.GetComponent<Skybox>().enabled&&cam.clearFlags==CameraClearFlags.SolidColor&&GameObject.Find("DF_Skybox360_EditorPreview")==null,"failure preserves original background");
            Check(Object.All.Where(o=>(o.hideFlags&HideFlags.HideAndDontSave)==HideFlags.HideAndDontSave).All(o=>o.destroyed),"failure releases every temporary resource");
            Check(Object.All.OfType<Mesh>().All(o=>o.destroyed),"failed mesh setup cannot leak an unregistered mesh");
        }
    }
    static void WindowLifecycle()
    {
        foreach(int exit in new[]{0,1,2,3,4,5})
        {
            var(cam,world,old)=Setup();var window=new Skybox360MatchPreview();Call(window,"OnEnable");
            Check(GameObject.Find("DF_Skybox360_EditorPreview")==null,"window opens without activating preview");
            Call(window,"StartPreview");Check(GameObject.Find("DF_Skybox360_EditorPreview")!=null,"explicit button action starts preview");
            switch(exit)
            {
                case 0:Call(window,"OnDisable");break;
                case 1:EditorApplication.State(PlayModeStateChange.ExitingPlayMode);break;
                case 2:AssemblyReloadEvents.Reload();break;
                case 3:Alterverse.UI.MainMenuController.IsVisible=true;EditorApplication.Tick();break;
                case 4:Object.DestroyImmediate(world);EditorApplication.Tick();break;
                case 5:Unity.Netcode.NetworkManager.Singleton=new(){IsListening=true};EditorApplication.Tick();break;
            }
            Check(GameObject.Find("DF_Skybox360_EditorPreview")==null&&cam.GetComponent<Skybox>().material==old,"window/play/reload/menu/world/network transition cleans preview");
            Call(window,"OnDisable");
        }
        foreach(int guard in new[]{0,1,2,3,4,5,6})
        {
            var(cam,world,old)=Setup();var window=new Skybox360MatchPreview();
            switch(guard)
            {
                case 0:EditorApplication.isPlaying=false;break;
                case 1:Alterverse.Multiplayer.CoopAuthority.Active=true;break;
                case 2:cam.orthographic=true;break;
                case 3:cam.farClipPlane=100;break;
                case 4:cam.cullingMask=0;break;
                case 5:Unity.Netcode.NetworkManager.Singleton=new(){IsListening=true};break;
                case 6:Alterverse.UI.MainMenuController.IsVisible=true;break;
            }
            Call(window,"StartPreview");Check(cam.GetComponent<Skybox>().material==old&&GameObject.Find("DF_Skybox360_EditorPreview")==null,"invalid/offline guard preserves game state");
        }
    }
    static void ImportScope()
    {
        var method=typeof(Skybox360PreviewTextureImporter).GetMethod("OnPreprocessTexture",BindingFlags.NonPublic|BindingFlags.Instance);
        foreach(string path in new[]{"Assets/Alterverse/Resources/MapPanoramas/Universe.png","Assets/Alterverse/Resources/FrontEnd/CosmicMenu.png","Assets/Alterverse/Editor/Skybox360Preview/TexturesOther/test.png"})
        {
            var importer=new TextureImporter{maxTextureSize=17};method.Invoke(new Skybox360PreviewTextureImporter{assetPath=path,assetImporter=importer},null);
            Check(importer.maxTextureSize==17,"importer leaves existing game/menu assets alone");
        }
        var own=new TextureImporter();method.Invoke(new Skybox360PreviewTextureImporter{assetPath=Skybox360PreviewSession.TextureRoot+"Sky360_StarsNebula.png",assetImporter=own},null);
        Check(own.maxTextureSize==2048&&own.mipmapEnabled&&!own.isReadable&&own.sRGBTexture&&own.wrapModeV==TextureWrapMode.Clamp,"preview panorama import settings");
    }
    static void Main(){CycleAndGeometry();OwnershipAndFailure();WindowLifecycle();ImportScope();Console.WriteLine($"Skybox 360: {checks} lifecycle, rollback, geometry and isolation assertions passed (API fixtures; native Unity/Android not tested).");}
}
