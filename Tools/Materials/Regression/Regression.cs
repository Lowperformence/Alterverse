using System;
using System.Collections.Generic;
using System.Reflection;
using Alterverse.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

// Minimal engine fixtures with real dimensional constraints: SetTexture rejects
// cubemaps/arrays/volumes for 2D slots. InjectTexture models a stale imported binding.
namespace UnityEngine.Rendering
{
    public enum TextureDimension { Tex2D, Cube, Tex3D, Tex2DArray, CubeArray }
    public enum ShaderPropertyType { Texture, Float }
    public enum ShadowCastingMode { On, Off }
    public enum GraphicsDeviceType { Null, Direct3D11 }
    public static class GraphicsSettings { public static object currentRenderPipeline = new object(); }
}
namespace UnityEngine
{
    public enum FindObjectsInactive { Exclude }
    public enum FindObjectsSortMode { None }
    public enum RuntimeInitializeLoadType { AfterSceneLoad }
    public enum TextureWrapMode { Clamp, Repeat }
    public enum FilterMode { Bilinear, Trilinear }
    public class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { } }
    public class Object
    {
        public static Renderer[] FoundRenderers=Array.Empty<Renderer>();
        public string name = "Fixture";
        public static T FindAnyObjectByType<T>() where T : class => null;
        public static T[] FindObjectsByType<T>(FindObjectsInactive inactive, FindObjectsSortMode sort) => (T[])(object)FoundRenderers;
        public static void DontDestroyOnLoad(Object target) { }
    }
    public class Component : Object { public GameObject gameObject = new GameObject(); }
    public class MonoBehaviour : Component { }
    public class Transform : Object { }
    public class GameObject : Object
    {
        public bool activeInHierarchy=true;
        public static int AddedComponents;
        public Transform transform = new Transform();
        public GameObject() { } public GameObject(string name) { this.name = name; }
        public static GameObject Find(string name) => null;
        public T AddComponent<T>() where T : new() { AddedComponents++;return new T(); }
    }
    public static class Mathf { public static int Min(int a,int b)=>Math.Min(a,b); }
    public static class SystemInfo { public static GraphicsDeviceType graphicsDeviceType=GraphicsDeviceType.Direct3D11; }
    public static class Time { public static float unscaledTime; }
    public struct Color { public float r,g,b,a; public static Color white => new Color { r=1,g=1,b=1,a=1 }; }
    public struct Vector2 { public float x,y; public Vector2(float x,float y) { this.x=x;this.y=y; } }
    public class Texture : Object { public TextureDimension dimension; public Texture(TextureDimension value) { dimension=value; } }
    public sealed class Texture2D : Texture { public Texture2D() : base(TextureDimension.Tex2D) { } }
    public sealed class RenderTexture : Texture { public RenderTexture(TextureDimension value) : base(value) { } }
    public class Shader : Object
    {
        public bool isSupported=true, HasNormal=true, HasEncoding;
        public TextureDimension NormalDimension=TextureDimension.Tex2D;
        private static readonly string[] Properties={"_BaseMap","_BumpMap","_BaseColor","_Color","_BumpScale","_Metallic","_Smoothness","_Glossiness","_EmissionColor","_NormalEncoding","_MainTex"};
        public Shader(string name) { this.name=name; HasEncoding=name=="Alterverse/RuntimeLit"; }
        public int FindPropertyIndex(string name) => name=="_BumpMap"&&!HasNormal||name=="_NormalEncoding"&&!HasEncoding||this.name=="Standard"&&(name=="_BaseMap"||name=="_BaseColor"||name=="_Smoothness") ? -1 : Array.IndexOf(Properties,name);
        public ShaderPropertyType GetPropertyType(int property) => Properties[property].EndsWith("Map")||Properties[property]=="_MainTex"?ShaderPropertyType.Texture:ShaderPropertyType.Float;
        public TextureDimension GetPropertyTextureDimension(int property) => Properties[property]=="_BumpMap"?NormalDimension:TextureDimension.Tex2D;
    }
    public class Material : Object
    {
        public Shader shader;
        private Dictionary<string,Texture> textures=new Dictionary<string,Texture>();
        private Dictionary<string,float> floats=new Dictionary<string,float>();
        private Dictionary<string,Color> colors=new Dictionary<string,Color>();
        private Dictionary<string,Vector2> scales=new Dictionary<string,Vector2>(),offsets=new Dictionary<string,Vector2>();
        private HashSet<string> keywords=new HashSet<string>();
        public Material(Shader shader) { this.shader=shader; }
        public Material(Material source) { shader=source.shader;name=source.name;textures=new Dictionary<string,Texture>(source.textures);floats=new Dictionary<string,float>(source.floats);colors=new Dictionary<string,Color>(source.colors);scales=new Dictionary<string,Vector2>(source.scales);offsets=new Dictionary<string,Vector2>(source.offsets);keywords=new HashSet<string>(source.keywords); }
        public bool HasProperty(string name) => shader.FindPropertyIndex(name)>=0;
        public Texture GetTexture(string name) => textures.TryGetValue(name,out var value)?value:null;
        public void InjectTexture(string name,Texture value) { textures[name]=value; }
        public void SetTexture(string name,Texture value)
        {
            int property=shader.FindPropertyIndex(name);
            if(value!=null&&value.dimension!=shader.GetPropertyTextureDimension(property))throw new Exception("Dimensions must match: "+name);
            textures[name]=value;
        }
        public float GetFloat(string name) => floats.TryGetValue(name,out var value)?value:0;
        public void SetFloat(string name,float value) { floats[name]=value; }
        public Color GetColor(string name) => colors.TryGetValue(name,out var value)?value:Color.white;
        public void SetColor(string name,Color value) { colors[name]=value; }
        public Vector2 GetTextureScale(string name) => scales.TryGetValue(name,out var value)?value:new Vector2(1,1);
        public void SetTextureScale(string name,Vector2 value) { scales[name]=value; }
        public Vector2 GetTextureOffset(string name) => offsets.TryGetValue(name,out var value)?value:new Vector2();
        public void SetTextureOffset(string name,Vector2 value) { offsets[name]=value; }
        public void EnableKeyword(string name) { keywords.Add(name); }
        public void DisableKeyword(string name) { keywords.Remove(name); }
        public bool IsKeywordEnabled(string name) => keywords.Contains(name);
    }
    public class Renderer : Component
    {
        Material[] materials;
        public static int ArrayReads,ListReads;
        public Material[] sharedMaterials {get {ArrayReads++;return (Material[])materials.Clone();} set {materials=value;}}
        public void GetSharedMaterials(List<Material> result) {ListReads++;result.Clear();result.AddRange(materials);}
        public void SetSharedMaterials(List<Material> result) {materials=result.ToArray();}
        public ShadowCastingMode shadowCastingMode=ShadowCastingMode.On;
    }
}
namespace Alterverse.Multiplayer {public static class DedicatedServerNetworkRuntime {public static bool Requested;}}
namespace UnityEditor
{
    public class AssetPostprocessor { public string assetPath;public object assetImporter; public virtual uint GetVersion()=>0; }
    public enum TextureImporterType { Default, NormalMap }
    public enum TextureImporterShape { Texture2D, TextureCube }
    public enum TextureImporterAlphaSource { None }
    public class TextureImporter { public TextureImporterType textureType;public TextureImporterShape textureShape;public bool sRGBTexture,mipmapEnabled,isReadable;public TextureWrapMode wrapMode;public FilterMode filterMode;public int maxTextureSize;public TextureImporterAlphaSource alphaSource; }
}
namespace Alterverse.Visuals
{
    public static class RuntimeShaderLibrary { public static Shader Lit=new Shader("Alterverse/RuntimeLit"),Unlit=new Shader("Alterverse/RuntimeUnlit"); }
    public static class SurfaceEdgeQuality { public static void Apply(Renderer renderer,Transform root) { } }
    public sealed class RuntimeMaterialPalette
    {
        public static int Repairs;
        public static RuntimeMaterialPalette For(GameObject owner)=>new RuntimeMaterialPalette();
        public void OwnRepair(Material material) { Repairs++; }
    }
}
internal static class Regression
{
    private static int checks;
    private static readonly AndroidMaterialCompatibilityGuard Guard=new AndroidMaterialCompatibilityGuard();
    private static readonly MethodInfo Repair=typeof(AndroidMaterialCompatibilityGuard).GetMethod("RepairRenderer",BindingFlags.NonPublic|BindingFlags.Instance);
    private static void Check(bool condition,string message) { checks++;if(!condition)throw new Exception(message); }
    private static Material RepairOne(Material source)
    {
        var renderer=new Renderer { sharedMaterials=new[]{source},name="Armor" };
        Repair.Invoke(Guard,new object[]{renderer});
        var result=renderer.sharedMaterials[0];int owned=RuntimeMaterialPalette.Repairs;
        Repair.Invoke(Guard,new object[]{renderer});Check(renderer.sharedMaterials[0]==result&&RuntimeMaterialPalette.Repairs==owned,"Repeated repair must reuse the material");
        return result;
    }
    private static void Main()
    {
        CheckRepairBudget();
        foreach(var dimension in new[]{TextureDimension.Cube,TextureDimension.Tex3D,TextureDimension.Tex2DArray,TextureDimension.CubeArray})
        {
            foreach(bool legacy in new[]{false,true})
            {
                var shader=legacy?new Shader("Standard"):RuntimeShaderLibrary.Lit;
                var source=new Material(shader){name="Armor"};var normal=new Texture(dimension);
                source.InjectTexture("_BumpMap",normal);source.SetFloat("_BumpScale",.7f);source.SetFloat("_Metallic",.8f);source.EnableKeyword("_NORMALMAP");
                Material result=RepairOne(source);
                Check(result!=source&&result.GetTexture("_BumpMap")==null,"Incompatible normal must be removed from a runtime copy");
                Check(source.GetTexture("_BumpMap")==normal&&source.GetFloat("_BumpScale")==.7f,"Imported source must remain intact");
                Check(result.GetFloat("_BumpScale")==0&&!result.IsKeywordEnabled("_NORMALMAP"),"Invalid normal strength/keyword must not survive");
                Check(result.GetFloat("_Metallic")==.8f,"Other material finish must survive");
            }
        }
        foreach(Texture normal in new Texture[]{new Texture2D(),new RenderTexture(TextureDimension.Tex2D),null})
        {
            var source=new Material(RuntimeShaderLibrary.Lit);source.SetTexture("_BumpMap",normal);
            Check(RepairOne(source)==source,"Valid URP normal map must remain untouched");
        }
        var valid=new Material(new Shader("Standard"));var map=new Texture2D();valid.SetTexture("_BumpMap",map);valid.SetFloat("_BumpScale",.45f);valid.SetTextureScale("_BumpMap",new Vector2(3,4));valid.SetTextureOffset("_BumpMap",new Vector2(.2f,.3f));
        var converted=RepairOne(valid);
        Check(converted.GetTexture("_BumpMap")==map&&converted.GetFloat("_BumpScale")==.45f,"Valid legacy normal must retain its texture/strength");
        Check(converted.GetTextureScale("_BumpMap").x==3&&converted.GetTextureOffset("_BumpMap").y==.3f,"Normal UV transforms must survive");
        Check(converted.GetFloat("_NormalEncoding")==1&&converted.IsKeywordEnabled("_NORMALMAP"),"Unity imported normal encoding must be used");
        var legitimate=new Material(new Shader("Custom/CubeNormal"){NormalDimension=TextureDimension.Cube});legitimate.SetTexture("_BumpMap",new Texture(TextureDimension.Cube));
        Check(RepairOne(legitimate)==legitimate,"Declared cubemap shader slots must not be changed");
        var noNormal=new Material(new Shader("Custom/NoNormal"){HasNormal=false});Check(RepairOne(noNormal)==noNormal,"Shader without a normal slot must be unchanged");
        var cubeBase=new Material(new Shader("Standard"));cubeBase.InjectTexture("_MainTex",new Texture(TextureDimension.Cube));
        Check(RepairOne(cubeBase).GetTexture("_BaseMap")==null,"Cubemap cannot be copied to a base-map 2D slot");
        foreach(bool normal in new[]{false,true})
        {
            var importer=new UnityEditor.TextureImporter{textureShape=UnityEditor.TextureImporterShape.TextureCube};
            var processor=new Alterverse.EditorTools.VoidKnightTextureImporter { assetPath="Assets/Alterverse/Resources/AuthoredHeroes/VoidKnightTextures/VoidKnightMetal_"+(normal?"Normal":"Albedo")+".png",assetImporter=importer };
            processor.GetType().GetMethod("OnPreprocessTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(processor,null);
            Check(importer.textureShape==UnityEditor.TextureImporterShape.Texture2D,"Void Knight imports must force a 2D shape");
            Check(importer.textureType==(normal?UnityEditor.TextureImporterType.NormalMap:UnityEditor.TextureImporterType.Default)&&importer.sRGBTexture!=normal,"Correct normal/color encoding must survive");
            Check(processor.GetVersion()>=2,"Old cached cube imports must be invalidated");
        }
        var other=new UnityEditor.TextureImporter{textureShape=UnityEditor.TextureImporterShape.TextureCube};
        var unrelated=new Alterverse.EditorTools.VoidKnightTextureImporter{assetPath="Assets/Other/Environment.png",assetImporter=other};
        unrelated.GetType().GetMethod("OnPreprocessTexture",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(unrelated,null);
        Check(other.textureShape==UnityEditor.TextureImporterShape.TextureCube,"Unrelated environment imports must not change");
        Console.WriteLine(checks+" material repair/budget/dimension/import checks passed using engine fixtures. Not a native Unity render test.");
    }
    private static void CheckRepairBudget()
    {
        var install=typeof(AndroidMaterialCompatibilityGuard).GetMethod("Install",BindingFlags.Static|BindingFlags.NonPublic);
        Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Requested=true;
        install.Invoke(null,null);Check(GameObject.AddedComponents==0,"No material guard on dedicated server");
        Alterverse.Multiplayer.DedicatedServerNetworkRuntime.Requested=false;
        SystemInfo.graphicsDeviceType=GraphicsDeviceType.Null;
        install.Invoke(null,null);Check(GameObject.AddedComponents==0,"No material guard without a graphics device");
        SystemInfo.graphicsDeviceType=GraphicsDeviceType.Direct3D11;
        UnityEngine.Object.FoundRenderers=new Renderer[161];
        for(int i=0;i<160;i++)UnityEngine.Object.FoundRenderers[i]=new Renderer{sharedMaterials=new[]{new Material(new Shader("Standard"))}};
        var tick=typeof(AndroidMaterialCompatibilityGuard).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        Renderer.ArrayReads=Renderer.ListReads=0;Time.unscaledTime=10;
        tick.Invoke(Guard,null);
        Check(Renderer.ListReads==48&&Renderer.ArrayReads==0,"Bound each repair frame and reuse material list");
        for(int i=0;i<3;i++)tick.Invoke(Guard,null);
        Check(Renderer.ListReads==160,"Every live renderer eventually checked; null entries skipped");
        tick.Invoke(Guard,null);Check(Renderer.ListReads==160,"No busy scanning between sweeps");
        for(int i=0;i<160;i++)Check(UnityEngine.Object.FoundRenderers[i].sharedMaterials[0].shader!=null&&
            UnityEngine.Object.FoundRenderers[i].sharedMaterials[0].shader.name!="Standard","Batched repair retains legacy shader recovery");
        Time.unscaledTime+=1.01f;int before=Renderer.ListReads;
        tick.Invoke(Guard,null);Check(Renderer.ListReads-before==48,"A later sweep retains the frame budget");
    }
}
