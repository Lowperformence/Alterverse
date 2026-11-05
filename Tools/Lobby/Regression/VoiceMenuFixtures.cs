// Controlled rendering/input boundaries for the production in-match voice menu.
using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine
{
 public enum FindObjectsSortMode{None}
 public struct Rect
 {
  public float x,y,width,height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
  public float xMax=>x+width;public float yMax=>y+height;public Vector2 center=>new(x+width/2,y+height/2);
  public bool Contains(Vector2 p)=>p.x>=x&&p.x<=xMax&&p.y>=y&&p.y<=yMax;
 }
 public struct Matrix4x4{}
 public static class Screen{public static Rect safeArea=new(0,0,960,640);}
 public enum TextureFormat{RGBA32}public enum TextureWrapMode{Clamp}public enum HideFlags{HideAndDontSave}
 public enum FilterMode{Bilinear}public enum ScaleMode{StretchToFill}public enum FocusType{Passive}
 public enum EventType{Repaint,MouseDown,MouseDrag,MouseUp}
 public class Event{public static Event current=new();public EventType type;public Vector2 mousePosition;public void Use(){}}
 public static class GUI
 {
  public static bool enabled=true;public static int depth;public static Matrix4x4 matrix;public static Color color;
  public static Rect ScrollViewport,ScrollContent;
  public static Vector2 BeginScrollView(Rect r,Vector2 p,Rect c){ScrollViewport=r;ScrollContent=c;return p;}
  public static void EndScrollView(){}public static void DrawTexture(Rect r,Texture2D t,ScaleMode s,bool alpha){}
 }
 public class Texture2D:Object
 {
  public FilterMode filterMode;public TextureWrapMode wrapMode;public HideFlags hideFlags;
  public Texture2D(int w,int h,TextureFormat f,bool mip,bool linear){}
  public void SetPixels(Color[] pixels){}public void Apply(bool mip,bool release){}
 }
}
namespace Alterverse.Match
{
 public class DimensionsFront:UnityEngine.MonoBehaviour{public bool HasEnded;}
 public enum PauseReason{Settings}
 public static class MatchClock{public static bool Paused;public static void Set(PauseReason p,bool b){Paused=b;}}
 public static class MatchExit{public static System.Threading.Tasks.Task ReturnToMenuAsync()=>System.Threading.Tasks.Task.CompletedTask;}
 public static class MatchScoreboard{public static string ResolveDisplayName(Alterverse.Characters.HeroIdentity h)=>h.name;}
}
namespace Alterverse.Profile
{
 public static class FriendStickerService
 {
  public static bool IsFriendOrRequested(string s)=>false;
  public static void SendFriendRequest(string s,Alterverse.Characters.HeroId id,int n){}
 }
}
namespace Alterverse.Performance
{
 public enum MobileQuality{Battery,Balanced,Performance}
 public static class MobilePerformanceProfile{public static bool AutoModeEnabled;public static MobileQuality Current;public static void EnableAutoHardwareProfile(){AutoModeEnabled=true;}}
}
namespace Alterverse.UI
{
 public static class MainMenuController{public static bool IsVisible;}
 public class PrototypeHUD:UnityEngine.MonoBehaviour{}
 public struct CombatHudLayout{public UnityEngine.Rect Safe;public float Width,Height;public UnityEngine.Matrix4x4 Matrix;public CombatHudLayout(UnityEngine.Rect r){Safe=r;Width=r.width;Height=r.height;Matrix=new();}}
 public class LanguagePicker{public bool IsOpen;public void Close(){IsOpen=false;}public void Draw(FrontEndTheme t,UnityEngine.Rect r){}public void DrawControl(FrontEndTheme t,UnityEngine.Rect r){}}
 public class FrontEndTheme
 {
  public object Heading,Body,Small;public static UnityEngine.Color Text,Accent,Raised;
  public static readonly List<string> Labels=new();public static readonly List<(string Text,bool Enabled)> Actions=new();
  public static string Click;public static float ClickY=float.NaN;
  public static void Fill(UnityEngine.Rect r,UnityEngine.Color c){}
  public void Panel(UnityEngine.Rect r){}public void HudPanel(UnityEngine.Rect r,float a){}
  public void Label(UnityEngine.Rect r,string s,object style,bool wrap=false){Labels.Add(s);}
  public void FitLabel(UnityEngine.Rect r,string s,object style,bool muted=false){Labels.Add(s);}
  public bool Action(UnityEngine.Rect r,string s,bool primary=false,bool selected=false,bool enabled=true)
  {
   Actions.Add((s,enabled));if(!UnityEngine.GUI.enabled||!enabled||Click!=s||!float.IsNaN(ClickY)&&Math.Abs(ClickY-r.y)>.01)return false;
   Click=null;ClickY=float.NaN;return true;
  }
 }
}
