using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float magnitude=>(float)Math.Sqrt(x*x+y*y);}
    public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new(1,1,1);public static Color clear=>new(0,0,0,0);}
    public struct Bounds
    {
        public Vector3 center,size;public Bounds(Vector3 c,Vector3 s){center=c;size=s;}
        public Vector3 extents=>size*.5f;public Vector3 min=>center-extents;public Vector3 max=>center+extents;
    }
    public struct BoneWeight {public int boneIndex0,boneIndex1,boneIndex2,boneIndex3;public float weight0,weight1,weight2,weight3;}
    public struct Matrix4x4
    {
        readonly Func<Vector3,Vector3> point,vector;
        public Matrix4x4(Func<Vector3,Vector3> p,Func<Vector3,Vector3> v)
        {Vector3 o=p(Vector3.zero),x=v(Vector3.right),y=v(Vector3.up),z=v(Vector3.forward);point=q=>o+x*q.x+y*q.y+z*q.z;vector=q=>x*q.x+y*q.y+z*q.z;}
        public Vector3 MultiplyPoint3x4(Vector3 v)=>point(v);public Vector3 MultiplyVector(Vector3 v)=>vector(v);
        public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new(v=>a.MultiplyPoint3x4(b.MultiplyPoint3x4(v)),v=>a.MultiplyVector(b.MultiplyVector(v)));
    }
    public class Mesh:Object
    {
        public static readonly List<Mesh> Created=new();public Mesh(){Created.Add(this);}
        public bool isReadable=true;public Bounds bounds;public int subMeshCount;public int Reads;
        Vector3[] points=Array.Empty<Vector3>();BoneWeight[] weights=Array.Empty<BoneWeight>();Matrix4x4[] binds=Array.Empty<Matrix4x4>();
        readonly Dictionary<int,int[]> faces=new();public Vector2[] uv;
        void Read(){if(!isReadable)throw new Exception("Unreadable mesh access");Reads++;}
        public Vector3[] vertices{get{Read();return points;}set{points=value;}}
        public BoneWeight[] boneWeights{get{Read();return weights;}set{weights=value;}}
        public Matrix4x4[] bindposes{get{Read();return binds;}set{binds=value;}}
        public int[] triangles{get{Read();return faces.OrderBy(x=>x.Key).SelectMany(x=>x.Value).ToArray();}set{faces.Clear();faces[0]=value;}}
        public void SetVertices(List<Vector3> data)=>points=data.ToArray();public void SetUVs(int channel,List<Vector2> data)=>uv=data.ToArray();
        public void SetTriangles(List<int> data,int sub)=>faces[sub]=data.ToArray();public int[] GetTriangles(int sub)=>faces[sub];
        public void MarkDynamic(){}public void RecalculateNormals(){}
        public void RecalculateBounds(){if(points.Length==0)return;Vector3 min=points[0],max=min;foreach(var v in points){min=Vector3.Min(min,v);max=Vector3.Max(max,v);}bounds=new Bounds((min+max)*.5f,max-min);}
    }
    public class MeshFilter:Component {public Mesh sharedMesh;}
    public class Material:Object
    {
        public static readonly List<Material> Created=new();public readonly Dictionary<string,float> Floats=new();
        public Material(){Created.Add(this);}public bool HasProperty(string property)=>true;public void SetFloat(string p,float v)=>Floats[p]=v;
    }
    public class MaterialPropertyBlock {public void SetFloat(string p,float v){}}
    public class Renderer:Component
    {
        public Material sharedMaterial=new();public Material[] sharedMaterials;
        public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;
        public Rendering.LightProbeUsage lightProbeUsage;public Rendering.ReflectionProbeUsage reflectionProbeUsage;
        public void GetPropertyBlock(MaterialPropertyBlock b){}public void SetPropertyBlock(MaterialPropertyBlock b){}
    }
    public class MeshRenderer:Renderer{}
    public class SkinnedMeshRenderer:Renderer {public Mesh sharedMesh;public Transform[] bones;public bool updateWhenOffscreen;}
    public class Texture:Object{}
    public class Texture2D:Texture {public static Texture2D whiteTexture=new();}
    public class Sprite:Object {public Texture texture;}
    public class SpriteRenderer:Renderer {public Sprite sprite;public int sortingOrder;public Color color;}
    public enum RenderTextureFormat {ARGB32}public enum RenderTextureReadWrite {sRGB}public enum FilterMode {Bilinear}public enum TextureWrapMode {Clamp}
    public class RenderTexture:Texture
    {
        public int antiAliasing;public bool useMipMap,autoGenerateMips;public FilterMode filterMode;public TextureWrapMode wrapMode;
        public bool Live;public RenderTexture(int w,int h,int d,RenderTextureFormat f,RenderTextureReadWrite r){}
        public void Create(){Live=true;}public void Release(){Live=false;}
    }
    public enum CameraClearFlags {SolidColor}public enum LightType {Point}public enum LightShadows {None}
    public class Camera:Component
    {
        public CameraClearFlags clearFlags;public Color backgroundColor;
        public bool orthographic,allowHDR,allowMSAA,allowDynamicResolution,useOcclusionCulling;
        public float orthographicSize,nearClipPlane,farClipPlane,depth;public int cullingMask;public RenderTexture targetTexture;
    }
    public class Light:Component {public LightType type;public Color color;public float intensity,range;public LightShadows shadows;public int cullingMask;}
    public struct Rect {public float width,height;public Rect(float x,float y,float w,float h){width=w;height=h;}}
    public enum ScaleMode {StretchToFill,ScaleAndCrop,ScaleToFit}
    public static class GUI {public static Color color;public static void BeginGroup(Rect r){}public static void EndGroup(){}public static void DrawTexture(Rect r,Texture t,ScaleMode m,bool blend){}}
}
namespace UnityEngine.Rendering {public enum ShadowCastingMode {Off}public enum LightProbeUsage {Off}public enum ReflectionProbeUsage {Off}}
namespace UnityEngine.Rendering.Universal {public class UniversalAdditionalCameraData:UnityEngine.Component {public bool renderPostProcessing,renderShadows;public int volumeLayerMask;}}
namespace Alterverse.Characters
{
    public enum HeroId {Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy,VoidKnight,Bolt7}
    public class HeroIdentity:UnityEngine.Component {public HeroId Hero;public void Configure(HeroId hero){Hero=hero;}}
}
namespace Alterverse.Visuals
{
    public class HeroGroundedLocomotion:UnityEngine.MonoBehaviour{}
    public static class RuntimeShaderLibrary
    {
        public static UnityEngine.Material Create(UnityEngine.Color color,bool unlit=false,float metallic=0,float smooth=.2f)=>new();
        public static UnityEngine.Material CreateGroundTelegraph(UnityEngine.Color c,float a,float b,float d)=>new();
    }
    public class StylizedHeroVisual:UnityEngine.Component {public void Build(){TestRig.Build(gameObject);}}
}
namespace Alterverse.Shop
{
    public enum UniversalHeroCosmetic {Original,Voidfall,BetaCap}
    public static class UniversalCosmeticSelection {public static UniversalHeroCosmetic EquippedFor(Alterverse.Characters.HeroId h)=>UniversalHeroCosmetic.Original;}
    public static class HeadwearSelection {public static UniversalHeroCosmetic EquippedFor(Alterverse.Characters.HeroId h)=>UniversalHeroCosmetic.Original;}
    public static class WeaponSkinInventory {public static int EquippedVariant(Alterverse.Characters.HeroId h)=>-1;public static bool Equipped(Alterverse.Characters.HeroId h)=>false;}
    public class SkinPreviewSelection:UnityEngine.Component {public UniversalHeroCosmetic UniversalCosmetic,Headwear;public bool WeaponOverdrive;public int WeaponSkinVariant;}
    public class CosmeticDefinition {public bool Headwear;}public static class CosmeticCatalog {public static CosmeticDefinition Find(UniversalHeroCosmetic c)=>null;}
    public static class PaidShardCommerce {public static event Action EntitlementsChanged;}
}
namespace Alterverse.Profile {public static class ProfileTransaction {public static event Action Committed;}}
namespace Alterverse.UI
{
    public sealed class HeroOriginBackdrop:IDisposable
    {public UnityEngine.Sprite SpriteFor(Alterverse.Characters.HeroId h)=>new(){texture=new UnityEngine.Texture()};public static UnityEngine.Color BaseColorFor(Alterverse.Characters.HeroId h)=>new();public void Dispose(){}}
}
