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
 public class Texture2D:Texture{public int width,height;public static int Created,Uploads;public static Texture2D whiteTexture=new(1,1,TextureFormat.RGBA32,false);public Color32[] Pixels;public TextureWrapMode wrapMode;public FilterMode filterMode;public string name;public Texture2D(int w,int h,TextureFormat format,bool mip){EngineBoundary.MainOnly();width=w;height=h;Created++;}public void SetPixels32(Color32[] pixels){EngineBoundary.MainOnly();Uploads++;Pixels=pixels;}public void Apply(bool mip,bool discard){EngineBoundary.MainOnly();}}
 public static class Resources{
  public static string Root,MissingHero;public static int LoadCalls;
  static readonly Dictionary<string,Texture2D> cache=new();
  public static T Load<T>(string path)where T:class{
   EngineBoundary.MainOnly();LoadCalls++;string hero=path.Substring(path.LastIndexOf('/')+1);
   if(hero==MissingHero)return null;
   if(!cache.TryGetValue(path,out var texture)){
    byte[] rgb=System.IO.File.ReadAllBytes(System.IO.Path.Combine(Root,hero+".rgb"));
    texture=new Texture2D(1024,576,TextureFormat.RGBA32,false){name=hero,Pixels=new Color32[1024*576]};
    for(int i=0;i<texture.Pixels.Length;i++)texture.Pixels[i]=new(){r=rgb[i*3],g=rgb[i*3+1],b=rgb[i*3+2],a=255};
    cache[path]=texture;
   }
   return texture as T;
  }
 }
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
  public static float PerlinNoise(float x,float y)=>throw new Exception("Runtime backdrop must not generate pixels");
 }
 public static class Time{public static float unscaledTime;public static int frameCount;}
 public class Renderer{public Rendering.ShadowCastingMode shadowCastingMode;public bool receiveShadows;public Material sharedMaterial;public void GetPropertyBlock(MaterialPropertyBlock block){}public void SetPropertyBlock(MaterialPropertyBlock block){}}
 public class Material{public bool HasProperty(string name)=>true;}public class MaterialPropertyBlock{public void SetFloat(string name,float value){}}
 public static class Debug{public static void LogWarning(object value){Console.WriteLine(value);}}
}
namespace UnityEngine.Rendering{public enum ShadowCastingMode{Off}}
namespace Alterverse.Characters{/* HERO_ENUM */ public class HeroIdentity{public static readonly int HeroCount=Enum.GetValues<HeroId>().Length;public void Configure(HeroId hero){}}}
namespace Alterverse.Visuals{public class StylizedHeroVisual{public static int Builds;public void Build(){Builds++;}}}
// This suite isolates selection/import caching; the pedestal suite compiles and
// exercises the complete production grounding, platform and showcase classes.
namespace Alterverse.UI {
 internal sealed class MenuHeroGrounding {public MenuHeroGrounding(UnityEngine.GameObject actor){}}
 internal sealed class MenuHeroPedestal {}
}
namespace Alterverse.Shop{
 public enum UniversalHeroCosmetic{Original,Voidfall,BetaCap}
 public static class UniversalCosmeticSelection{public static UniversalHeroCosmetic EquippedFor(HeroId hero)=>UniversalHeroCosmetic.Original;}
 public static class HeadwearSelection{public static UniversalHeroCosmetic EquippedFor(HeroId hero)=>UniversalHeroCosmetic.Original;}
 public static class WeaponSkinInventory{public static int EquippedVariant(HeroId hero)=>-1;public static bool Equipped(HeroId hero)=>false;}
 public class SkinPreviewSelection{public UniversalHeroCosmetic UniversalCosmetic,Headwear;public bool WeaponOverdrive;public int WeaponSkinVariant;}
 public class CosmeticDefinition{public bool Headwear;}public static class CosmeticCatalog{public static CosmeticDefinition Find(UniversalHeroCosmetic cosmetic)=>null;}
}
public static class EngineBoundary{
 public static int MainThread=Thread.CurrentThread.ManagedThreadId;
 public static void MainOnly(){if(Thread.CurrentThread.ManagedThreadId!=MainThread)throw new Exception("Unity graphics object was accessed on a worker");}
}
public static class Regression{
 static int checks;static void Assert(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
 static ulong Fingerprint(UnityEngine.Texture2D texture){ulong hash=14695981039346656037;foreach(var p in texture.Pixels){hash=unchecked((hash^p.r)*1099511628211);hash=unchecked((hash^p.g)*1099511628211);hash=unchecked((hash^p.b)*1099511628211);}return hash;}
 public static void Main(string[] args){
  UnityEngine.Resources.Root=args[0];
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
  showcase.TestDispose();
  var worlds=new HeroOriginBackdrop();var fingerprints=new HashSet<ulong>();var colours=new HashSet<UnityEngine.Color>();
  foreach(HeroId hero in Enum.GetValues<HeroId>()){
   var world=worlds.SpriteFor(hero);
   Assert(world!=null,"Every hero must have finished artwork on its FIRST request, without polling or CPU preparation");
   Assert(world.name=="DF_OriginWorld_"+hero,"Every roster entry must receive its own named world");
   Assert(fingerprints.Add(Fingerprint((UnityEngine.Texture2D)world.texture)),"Every hero needs distinct imported artwork");
   Assert(colours.Add(HeroOriginBackdrop.BaseColorFor(hero)),"Every hero needs a distinct fallback colour");
   int loads=UnityEngine.Resources.LoadCalls;
   Assert(ReferenceEquals(world,worlds.SpriteFor(hero))&&loads==UnityEngine.Resources.LoadCalls,"Repeated requests must reuse the cached sprite without loading again");
  }
  var ready=worlds.SpriteFor(HeroId.Veyr);var imported=ready.texture;
  ulong original=Fingerprint((UnityEngine.Texture2D)imported);
  worlds.Dispose();worlds.Dispose();
  Assert(ready.Destroyed&&!imported.Destroyed&&worlds.SpriteFor(HeroId.Veyr)==null,"Disposal must destroy only owned sprites, never imported/shared textures");
  worlds=new HeroOriginBackdrop();var rebuilt=worlds.SpriteFor(HeroId.Veyr);
  Assert(ReferenceEquals(rebuilt.texture,imported)&&Fingerprint((UnityEngine.Texture2D)rebuilt.texture)==original,"Reopening a world must reuse the same imported image and colours");worlds.Dispose();
  var view=new MenuHeroShowcase();view.Present(HeroId.Veyr,true);var oldWorld=view.BackgroundImage;
  Assert(oldWorld!=null,"The preview must show finished art immediately on first presentation");
  UnityEngine.Resources.MissingHero="Bolt7";view.Present(HeroId.Bolt7,true);
  Assert(view.BackgroundImage==null,"A missing imported image must never leave the previous hero's world visible");
  int missingLoads=UnityEngine.Resources.LoadCalls;view.TestPoll();
  Assert(UnityEngine.Resources.LoadCalls==missingLoads,"A missing asset must not trigger work every frame");
  UnityEngine.GUI.color=new UnityEngine.Color(.2f,.5f,.8f,.7f);view.DrawPreview(new UnityEngine.Rect(0,0,200,300));
  Assert(UnityEngine.GUI.Draws.Count==1&&UnityEngine.GUI.Draws[0].colour.Equals(HeroOriginBackdrop.BaseColorFor(HeroId.Bolt7)),"Missing artwork must use the selected hero's own palette");
  UnityEngine.Resources.MissingHero=null;
  view.Present(HeroId.Veyr,true);Assert(ReferenceEquals(oldWorld,view.BackgroundImage),"Switching to a cached hero must restore its own world immediately");
  foreach(HeroId hero in new[]{HeroId.Nyra,HeroId.Elyra,HeroId.Timmy,HeroId.VoidKnight,HeroId.Nerok,HeroId.Veyr}){
   view.Present(hero,true);
   Assert(((UnityEngine.Texture2D)view.BackgroundImage).name==hero.ToString(),"Rapid switching must show the current hero's imported image on the same call");
  }
  TimeAdvance(view);
  var inherited=new UnityEngine.Color(.2f,.5f,.8f,.7f);UnityEngine.GUI.color=inherited;view.DrawPreview(new UnityEngine.Rect(0,0,200,300));
  Assert(UnityEngine.GUI.Draws[1].colour.Equals(new UnityEngine.Color(1,1,1,.7f))&&UnityEngine.GUI.color.Equals(inherited),"Other GUI controls must not recolour the background, and their tint must be restored");
  Assert(UnityEngine.Texture2D.Uploads==0,"Selecting heroes must never generate or upload pixel arrays");
  view.TestDispose();
  Console.WriteLine(checks+" preview/imported-background checks passed (controlled engine, no native frame-time claim).");
 }
 static void TimeAdvance(MenuHeroShowcase view){var image=view.BackgroundImage;UnityEngine.Time.unscaledTime+=3;view.TestPoll();Assert(ReferenceEquals(image,view.BackgroundImage),"The picture must stay the same after the former three-second preparation interval");}
}
