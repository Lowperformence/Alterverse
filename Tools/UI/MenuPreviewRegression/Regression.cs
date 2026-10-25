using System;
using System.Collections.Generic;
using System.Threading;
using Alterverse.Characters;
using Alterverse.UI;

namespace UnityEngine {
 public class Object{public bool Destroyed;public static void Destroy(Object value){value.Destroyed=true;}}
 public class MonoBehaviour:Object{}
 public class GameObject:Object{public string name;public bool activeSelf=true;public int layer;public Transform transform;public GameObject(string name){this.name=name;transform=new Transform{gameObject=this};}public void SetActive(bool value){activeSelf=value;}public T AddComponent<T>()where T:new()=>new();public T[] GetComponentsInChildren<T>(bool includeInactive=false)=>Array.Empty<T>();}
 public class Transform{public GameObject gameObject;public Quaternion localRotation;public void SetParent(Transform parent,bool world){}public static bool MissingVisualRoot;public Transform Find(string name)=>MissingVisualRoot&&name=="DF_VisualRoot"?null:new();}
 public struct Quaternion{public static Quaternion Euler(float x,float y,float z)=>new();}
 public class Texture:Object{}
 public class RenderTexture:Texture{}
 public class Camera:Object{}
 public class SpriteRenderer:Object{public Sprite sprite;}
 public class Texture2D:Texture{public static int Created;public static Texture2D whiteTexture=new(1,1,TextureFormat.RGBA32,false);public Color32[] Pixels;public TextureWrapMode wrapMode;public FilterMode filterMode;public string name;public Texture2D(int w,int h,TextureFormat format,bool mip){EngineBoundary.MainOnly();Created++;}public void SetPixels32(Color32[] pixels){EngineBoundary.MainOnly();Pixels=pixels;}public void Apply(bool mip,bool discard){EngineBoundary.MainOnly();}}
 public enum TextureFormat{RGBA32}public enum TextureWrapMode{Clamp}public enum FilterMode{Bilinear}public enum SpriteMeshType{FullRect}
 public class Sprite:Object{public string name;public Texture texture;public static Sprite Create(Texture2D texture,Rect r,Vector2 pivot,float units,uint extrude,SpriteMeshType mesh){EngineBoundary.MainOnly();return new(){texture=texture};}}
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public struct Rect{public float width,height;public Rect(float x,float y,float w,float h){width=w;height=h;}}
 public enum ScaleMode{StretchToFill,ScaleAndCrop,ScaleToFit}
 public static class GUI{public static Color color=Color.white;public static List<(Texture texture,Color colour)> Draws=new();public static void BeginGroup(Rect r){Draws.Clear();}public static void EndGroup(){}public static void DrawTexture(Rect r,Texture t,ScaleMode mode,bool alpha){Draws.Add((t,color));}}
 public struct Color32{public byte r,g,b,a;public static implicit operator Color32(Color c)=>new(){r=(byte)(Mathf.Clamp01(c.r)*255),g=(byte)(Mathf.Clamp01(c.g)*255),b=(byte)(Mathf.Clamp01(c.b)*255),a=(byte)(Mathf.Clamp01(c.a)*255)};public static implicit operator Color(Color32 c)=>new(c.r/255f,c.g/255f,c.b/255f,c.a/255f);}
 public struct Color{public static Color white=>new(1,1,1);public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color operator*(Color c,float n)=>new(c.r*n,c.g*n,c.b*n,c.a*n);public static Color operator+(Color c,Color b)=>new(c.r+b.r,c.g+b.g,c.b+b.b,c.a+b.a);public static Color Lerp(Color a,Color b,float t)=>new(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t,a.a+(b.a-a.a)*t);}
 public static class Mathf{
  public const float PI=(float)Math.PI;public static int Clamp(int n,int a,int b)=>Math.Max(a,Math.Min(b,n));public static float Clamp01(float n)=>Math.Max(0,Math.Min(1,n));public static int RoundToInt(float n)=>(int)Math.Round(n);
  public static float Pow(float n,float e)=>(float)Math.Pow(n,e);public static float Abs(float n)=>Math.Abs(n);public static float Exp(float n)=>(float)Math.Exp(n);public static float Sqrt(float n)=>(float)Math.Sqrt(n);public static float Sin(float n)=>(float)Math.Sin(n);public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
  public static float PerlinNoise(float x,float y){Interlocked.Increment(ref EngineBoundary.NoiseCalls);if(EngineBoundary.BlockPixels){EngineBoundary.Started.Set();EngineBoundary.Release.Wait();}return .5f+.2f*(float)Math.Sin(x+y);}
 }
 public static class Time{public static float unscaledTime;}
 public class Renderer{public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public Material sharedMaterial;public void GetPropertyBlock(MaterialPropertyBlock block){}public void SetPropertyBlock(MaterialPropertyBlock block){}}
 public class Material{public bool HasProperty(string name)=>true;}public class MaterialPropertyBlock{public void SetFloat(string name,float value){}}
 public static class Debug{public static void LogWarning(object value){Console.WriteLine(value);}}
}
namespace UnityEngine.Rendering{public enum ShadowCastingMode{Off}}
namespace Alterverse.Characters{/* HERO_ENUM */ public class HeroIdentity{public static readonly int HeroCount=Enum.GetValues<HeroId>().Length;public void Configure(HeroId hero){}}}
namespace Alterverse.Visuals{public class StylizedHeroVisual{public static int Builds;public void Build(){Builds++;}}}
namespace Alterverse.Shop{
 public enum UniversalHeroCosmetic{Original,Voidfall,BetaCap}
 public static class UniversalCosmeticSelection{public static UniversalHeroCosmetic EquippedFor(HeroId hero)=>UniversalHeroCosmetic.Original;}
 public static class HeadwearSelection{public static UniversalHeroCosmetic EquippedFor(HeroId hero)=>UniversalHeroCosmetic.Original;}
 public static class WeaponSkinInventory{public static int EquippedVariant(HeroId hero)=>-1;public static bool Equipped(HeroId hero)=>false;}
 public class SkinPreviewSelection{public UniversalHeroCosmetic UniversalCosmetic,Headwear;public bool WeaponOverdrive;public int WeaponSkinVariant;}
 public class CosmeticDefinition{public bool Headwear;}public static class CosmeticCatalog{public static CosmeticDefinition Find(UniversalHeroCosmetic cosmetic)=>null;}
}
public static class EngineBoundary{
 public static int MainThread=Thread.CurrentThread.ManagedThreadId,NoiseCalls;
 public static volatile bool BlockPixels;public static ManualResetEventSlim Started=new(),Release=new();
 public static void MainOnly(){if(Thread.CurrentThread.ManagedThreadId!=MainThread)throw new Exception("Unity graphics object was accessed on a worker");}
}
public static class Regression{
 static int checks;static void Assert(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
 static void Wait(Func<bool> predicate){for(int i=0;i<3000;i++){if(predicate())return;Thread.Sleep(1);}throw new Exception("Preview fixture timed out");}
 static ulong Fingerprint(UnityEngine.Texture2D texture){ulong hash=14695981039346656037;foreach(var p in texture.Pixels){hash=unchecked((hash^p.r)*1099511628211);hash=unchecked((hash^p.g)*1099511628211);hash=unchecked((hash^p.b)*1099511628211);}return hash;}
 public static void Main(){
  EngineBoundary.MainThread=Thread.CurrentThread.ManagedThreadId;
  var showcase=new MenuHeroShowcase();showcase.Present(HeroId.Veyr,true);var first=showcase.TestActor;
  showcase.Present(HeroId.Kaelon,true);Assert(!first.activeSelf,"Switching must deactivate the previous actor");
  showcase.Present(HeroId.Veyr,true);Assert(ReferenceEquals(first,showcase.TestActor)&&Alterverse.Visuals.StylizedHeroVisual.Builds==2,"Returning to a cached hero must not rebuild the model");
  showcase.Present(HeroId.Nyra,true);showcase.Present(HeroId.Elyra,true);Assert(showcase.TestCacheCount==3,"The actor cache must stay bounded to three variants");
  showcase.Present(HeroId.Veyr,true);Assert(ReferenceEquals(first,showcase.TestActor),"Recent actor must survive LRU eviction");
  showcase.Present(HeroId.Veyr,true,cosmeticOverride:Alterverse.Shop.UniversalHeroCosmetic.Voidfall);Assert(!ReferenceEquals(first,showcase.TestActor),"Different cosmetics need their own correctly built preview");
  var costume=showcase.TestActor;showcase.Present(HeroId.Veyr,true,cosmeticOverride:Alterverse.Shop.UniversalHeroCosmetic.Voidfall);Assert(ReferenceEquals(costume,showcase.TestActor),"Repeated OnGUI calls must reuse the same preview");
  UnityEngine.Transform.MissingVisualRoot=true;
  showcase.Present(HeroId.Bolt7,true);
  Assert(showcase.TestActor.activeSelf&&showcase.TestNeedsFit,"A dedicated hero without a humanoid visual root must remain visible and reach camera fitting");
  var bolt=showcase.TestActor;showcase.Present(HeroId.Bolt7,true);
  Assert(ReferenceEquals(bolt,showcase.TestActor),"Repeated BOLT-7 preview calls must remain cached after a missing optional visual root");
  UnityEngine.Transform.MissingVisualRoot=false;
  Wait(()=>{showcase.TestPoll();return showcase.BackgroundImage!=null;});
  showcase.TestDispose();
  int textureCount=UnityEngine.Texture2D.Created;
  var backdrop=new HeroOriginBackdrop();EngineBoundary.BlockPixels=true;
  Assert(backdrop.SpriteFor(HeroId.Veyr)==null,"First backdrop request must return while CPU work is pending");
  Assert(EngineBoundary.Started.Wait(2000)&&UnityEngine.Texture2D.Created==textureCount,"CPU work runs off the main thread without creating Unity textures");
  EngineBoundary.BlockPixels=false;EngineBoundary.Release.Set();UnityEngine.Sprite ready=null;Wait(()=>{ready=backdrop.SpriteFor(HeroId.Veyr);return ready!=null;});
  int calls=EngineBoundary.NoiseCalls;Assert(ReferenceEquals(ready,backdrop.SpriteFor(HeroId.Veyr))&&EngineBoundary.NoiseCalls==calls,"Completed background is cached without recalculating its pixels");
  EngineBoundary.BlockPixels=true;EngineBoundary.Started.Reset();EngineBoundary.Release.Reset();backdrop.SpriteFor(HeroId.Kaelon);Assert(EngineBoundary.Started.Wait(2000),"Second backdrop worker started");
  backdrop.SpriteFor(HeroId.Nyra);EngineBoundary.BlockPixels=false;EngineBoundary.Release.Set();UnityEngine.Sprite newest=null;Wait(()=>{newest=backdrop.SpriteFor(HeroId.Nyra);return newest!=null;});
  Assert(newest.name.EndsWith("Nyra")&&UnityEngine.Texture2D.Created==textureCount+2,"A rapid switch must discard the canceled background and install only the requested hero");
  backdrop.Dispose();Assert(ready.Destroyed&&newest.Destroyed&&backdrop.SpriteFor(HeroId.Veyr)==null,"Disposal destroys cached sprites and cannot restart workers");
  var worlds=new HeroOriginBackdrop();var fingerprints=new HashSet<ulong>();var colours=new HashSet<UnityEngine.Color>();
  foreach(HeroId hero in Enum.GetValues<HeroId>()){
   UnityEngine.Sprite world=null;Wait(()=>{world=worlds.SpriteFor(hero);return world!=null;});
   Assert(world.name=="DF_OriginWorld_"+hero,"Every roster entry must receive its own named world");
   Assert(fingerprints.Add(Fingerprint((UnityEngine.Texture2D)world.texture)),"Every hero needs distinct artwork, including Timmy, Void Knight and BOLT-7");
   Assert(colours.Add(HeroOriginBackdrop.BaseColorFor(hero)),"Every hero needs a distinct immediate background colour");
  }
  ulong original=Fingerprint((UnityEngine.Texture2D)worlds.SpriteFor(HeroId.Bolt7).texture);
  worlds.Dispose();worlds=new HeroOriginBackdrop();UnityEngine.Sprite rebuilt=null;
  Wait(()=>{rebuilt=worlds.SpriteFor(HeroId.Bolt7);return rebuilt!=null;});
  Assert(Fingerprint((UnityEngine.Texture2D)rebuilt.texture)==original,"Reopening a world must reproduce the same pixels and colours");worlds.Dispose();
  var view=new MenuHeroShowcase();view.Present(HeroId.Veyr,true);
  Wait(()=>{view.TestPoll();return view.BackgroundImage!=null;});var oldWorld=view.BackgroundImage;
  EngineBoundary.BlockPixels=true;EngineBoundary.Started.Reset();EngineBoundary.Release.Reset();view.Present(HeroId.Bolt7,true);
  Assert(EngineBoundary.Started.Wait(2000)&&view.BackgroundImage==null,"A new hero must never display the previous hero's world while preparing");
  UnityEngine.GUI.color=new UnityEngine.Color(.2f,.5f,.8f,.7f);view.DrawPreview(new UnityEngine.Rect(0,0,200,300));
  Assert(UnityEngine.GUI.Draws.Count==1&&UnityEngine.GUI.Draws[0].colour.Equals(HeroOriginBackdrop.BaseColorFor(HeroId.Bolt7)),"Pending artwork must use the selected hero's own palette");
  view.Present(HeroId.Veyr,true);Assert(ReferenceEquals(oldWorld,view.BackgroundImage),"Switching to a cached hero must restore its own world immediately");
  view.Present(HeroId.Nyra,true);Assert(view.BackgroundImage==null,"Rapid switching must also clear cached worlds from other heroes");
  EngineBoundary.BlockPixels=false;EngineBoundary.Release.Set();Wait(()=>{view.TestPoll();return view.BackgroundImage!=null;});
  Assert(((UnityEngine.Texture2D)view.BackgroundImage).name=="DF_OriginWorldTexture_Nyra","Only the latest selected hero may supply completed artwork");
  var inherited=new UnityEngine.Color(.2f,.5f,.8f,.7f);UnityEngine.GUI.color=inherited;view.DrawPreview(new UnityEngine.Rect(0,0,200,300));
  Assert(UnityEngine.GUI.Draws[1].colour.Equals(new UnityEngine.Color(1,1,1,.7f))&&UnityEngine.GUI.color.Equals(inherited),"Other GUI controls must not recolour the background, and their tint must be restored");
  view.TestDispose();
  Console.WriteLine(checks+" preview cache/background scheduling checks passed (controlled engine, no native frame-time claim).");
 }
}


