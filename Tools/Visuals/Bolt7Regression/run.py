"""Compile the production BOLT-7 model/texture/pose presenters with deterministic engine fixtures.
The fixture reads real shipped resources. It does not run Unity's native renderer.
"""
import argparse,json,re,subprocess,tempfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--dotnet-root',type=Path,required=True);p.add_argument('--presentation-only',action='store_true');a=p.parse_args();sdk=a.dotnet_root.resolve();here=Path(__file__).resolve().parent;root=here.parents[2];src=root/'Assets/Alterverse/Scripts'
s=(root/'Tools/Aiming/Regression/EngineStubs.cs').read_text().split('namespace UnityEngine.InputSystem')[0]
s=s.replace('public Vector3 position,forward=', 'public Vector3 localPosition;public Quaternion localRotation=Quaternion.identity;public Vector3 position,forward=')
s=s.replace('public Vector3 InverseTransformPoint(', 'public Vector3 InverseTransformVector(Vector3 p)=>Quaternion.Inverse(rotation)*p;public Vector3 InverseTransformPoint(')
s=s.replace('public static float Atan2(', 'public static float Exp(float f)=>(float)System.Math.Exp(f);public static float Atan2(')
s=s.replace('public static Vector3 Cross(', 'public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);public static Vector3 Cross(')
s=s.replace('public static Quaternion Euler(', 'public static Quaternion Slerp(Quaternion a,Quaternion b,float t){var q=System.Numerics.Quaternion.Slerp(new(a.x,a.y,a.z,a.w),new(b.x,b.y,b.z,b.w),t);return new(){x=q.X,y=q.Y,z=q.Z,w=q.W};}public Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}public static Quaternion Euler(')
s=s.replace('public Bounds(Vector3 p){center=p;}', 'public Bounds(Vector3 p){center=p;}public Bounds(Vector3 p,Vector3 size){center=p;}')
s=s.replace('public static Color Lerp(', 'public static Color operator*(Color c,float f)=>new(c.r*f,c.g*f,c.b*f,c.a*f);public static Color Lerp(')
s=s.replace('public static T Load<T>(string s)where T:class{', 'public static T Load<T>(string s)where T:class{if(typeof(T)==typeof(TextAsset)){var file=System.IO.Path.Combine(ResourceRoot,s+".bytes");return System.IO.File.Exists(file)?new TextAsset{bytes=System.IO.File.ReadAllBytes(file)} as T:null;}')
s=s.replace('public static bool AttackAtlasAvailable=', 'public static string ResourceRoot;public static bool AttackAtlasAvailable=')
s=s.replace('public void SetColor(int p,Color c){}', 'public bool HasProperty(string s)=>true;public void SetTexture(string s,Texture2D t){}public void EnableKeyword(string k){}public void SetFloat(string s,float f){}public void SetColor(int p,Color c){}')
s=s.replace('public class Mesh:Object {', 'public struct BoneWeight{public int boneIndex0;public float weight0;}public class Mesh:Object {public Rendering.IndexFormat indexFormat;public Vector3[] vertices,normals;public Vector2[] uv;public BoneWeight[] boneWeights;public Matrix4x4[] bindposes;public int subMeshCount;public int[][] Submeshes=new int[6][];public void SetTriangles(int[] a,int i,bool b){Submeshes[i]=a;}public void RecalculateTangents(){}')
s=s.replace('bool m,bool l){width=w;height=h;}', 'bool m,bool l){width=w;height=h;}')
start=s.index('public struct Matrix4x4 {');end=s.index('\n public enum TextAnchor',start)
s=s[:start]+'''public struct Matrix4x4 {
 public System.Numerics.Matrix4x4 M;
 public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new(){M=System.Numerics.Matrix4x4.CreateScale(s.x,s.y,s.z)*System.Numerics.Matrix4x4.CreateFromQuaternion(new(q.x,q.y,q.z,q.w))*System.Numerics.Matrix4x4.CreateTranslation(p.x,p.y,p.z)};
 public Matrix4x4 inverse{get{System.Numerics.Matrix4x4.Invert(M,out var m);return new(){M=m};}}
 public static Matrix4x4 operator*(Matrix4x4 a,Matrix4x4 b)=>new(){M=b.M*a.M};
 public Vector3 MultiplyPoint3x4(Vector3 p){var v=System.Numerics.Vector3.Transform(new(p.x,p.y,p.z),M);return new(v.X,v.Y,v.Z);}
 }''' +s[end:]
s+='''
namespace UnityEngine {
 public class Renderer:Component {public Material[] sharedMaterials;}
 public enum SkinQuality{Bone1}
 public class SkinnedMeshRenderer:Renderer{public Mesh sharedMesh;public Transform[] bones;public Transform rootBone;public SkinQuality quality;public bool updateWhenOffscreen;public Bounds localBounds;}
 public class TextAsset:Object{public byte[] bytes;}
 public static class JsonUtility{public static T FromJson<T>(string json)=>System.Text.Json.JsonSerializer.Deserialize<T>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});}
 public static class ImageConversion{public static int Decodes;public static bool LoadImage(Texture2D t,byte[] b,bool unreadable){if(b.Length<24||b[0]!=137||b[1]!=80||b[2]!=78||b[3]!=71)return false;Decodes++;t.width=(b[16]<<24)|(b[17]<<16)|(b[18]<<8)|b[19];t.height=(b[20]<<24)|(b[21]<<16)|(b[22]<<8)|b[23];return true;}}
}
namespace UnityEngine.Rendering{public enum IndexFormat{UInt16,UInt32}public enum GraphicsDeviceType{Null,Vulkan}}
namespace UnityEngine{public static class SystemInfo{public static Rendering.GraphicsDeviceType graphicsDeviceType=Rendering.GraphicsDeviceType.Vulkan;}}
namespace Alterverse.Characters{public enum HeroId{Bolt7}}
namespace Alterverse.Shop{public sealed class SkinPreviewSelection:UnityEngine.Component{}}
namespace Alterverse.Combat{public class Health:UnityEngine.MonoBehaviour{public bool IsDead;public void Die(){IsDead=true;Died?.Invoke();}public void Restore(){IsDead=false;Changed?.Invoke(280,280);}public event System.Action Died;public event System.Action<float,float> Changed;}}
namespace Alterverse.Visuals{public static class Bolt7Vfx{public static void Warm(){}}public static class RuntimeShaderLibrary{public static UnityEngine.Material Create(UnityEngine.Color c,bool u,float m,float s,UnityEngine.Color e)=>new(new UnityEngine.Shader());}}
namespace Alterverse.Visuals{public sealed class HeadwearPresentation:UnityEngine.Component{
 public UnityEngine.Transform Root;public Alterverse.Characters.HeroId Hero;public Alterverse.Shop.SkinPreviewSelection Preview;
 public static void Configure(UnityEngine.GameObject actor,UnityEngine.Transform root,Alterverse.Characters.HeroId hero,Alterverse.Shop.SkinPreviewSelection preview){
  var view=actor.GetComponent<HeadwearPresentation>()??actor.AddComponent<HeadwearPresentation>();view.Root=root;view.Hero=hero;view.Preview=preview;
 }
}}
'''
s+='''
namespace Alterverse.Multiplayer{public class CoopHeroSlot:UnityEngine.Component{public bool IsLocalHuman;}public static class CoopAuthority{public static bool Active,LocalHasServerAuthority;}}
'''
production=['Bolt7Assets.cs','Bolt7Model.cs','Bolt7Visual.cs','HeroPresentationBuffer.cs']
if a.presentation_only:
 production=['Bolt7Visual.cs','HeroPresentationBuffer.cs']
 s+='''
namespace Alterverse.Visuals {
 public static class Bolt7Assets{public static UnityEngine.Texture2D Texture(string name,bool normal=false)=>null;}
 internal class Bolt7Model{
  internal class Clip{public string name;public float length=1;}
  public UnityEngine.GameObject Root;public string LastClip;public float LastTime;
  public Clip[] Clips=new[]{new Clip{name="Idle"},new Clip{name="Walk"},new Clip{name="Death"},new Clip{name="Fire_Loop"},new Clip{name="Grenade"},new Clip{name="Heavy_Ram"},new Clip{name="Ultimate"}};
  public string[] MaterialNames=System.Array.Empty<string>();
  public static Bolt7Model Create(UnityEngine.Transform root)=>new(){Root=root.gameObject};
  public void Sample(Clip clip,float time){LastClip=clip.name;LastTime=time;}
 }
}
'''
refs=next((sdk/'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'));csc=next((sdk/'sdk').glob('*/Roslyn/bincore/csc.dll'))
with tempfile.TemporaryDirectory(prefix='bolt7-visual-') as folder:
 folder=Path(folder);(folder/'Fixture.cs').write_text(s);out=folder/'Regression.dll'
 subprocess.run([str(sdk/'dotnet'),str(csc),'-nologo','-nowarn:0649,0067','-langversion:9','-out:'+str(out),*['-r:'+str(f) for f in refs.glob('*.dll')],str(folder/'Fixture.cs'),str(here/('PresentationRegression.cs' if a.presentation_only else 'Regression.cs')),*[str(src/'Visuals'/f) for f in production]],check=True)
 out.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions':{'tfm':'net8.0','framework':{'name':'Microsoft.NETCore.App','version':'8.0.0'}}}))
 subprocess.run([str(sdk/'dotnet'),str(out),str(root/'Assets/Alterverse/Resources')],check=True)
