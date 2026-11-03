using System;
using System.Collections.Generic;
using System.Reflection;
namespace UnityEngine
{
 public class Object { public static T FindAnyObjectByType<T>() where T:class=>null; public static T[] FindObjectsByType<T>(FindObjectsSortMode s)=>Array.Empty<T>();public static void Destroy(object o){}public static void DontDestroyOnLoad(object o){} }
 public enum FindObjectsSortMode{None}
 public class DefaultExecutionOrder:Attribute{public DefaultExecutionOrder(int n){}}
 public enum RuntimeInitializeLoadType{AfterSceneLoad}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType n){}}
 public class Component:Object {public GameObject gameObject;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();}
 public class MonoBehaviour:Component{}
 public class GameObject:Object {readonly Dictionary<Type,object> components=new();public GameObject(string s=""){}public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;return c;}public T GetComponent<T>()where T:class=>components.TryGetValue(typeof(T),out var c)?(T)c:null;}
 public struct Rect {public float x,y,width,height;public Rect(float a,float b,float c,float d){x=a;y=b;width=c;height=d;}public float xMax=>x+width;public float yMax=>y+height;}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public struct Matrix4x4{public int id;}
 public class Font{}
 public static class Resources {public static T GetBuiltinResource<T>(string name)where T:new()=>new T();}
 public enum TextAnchor{MiddleLeft,MiddleCenter}
 public enum FontStyle{Normal,Bold}
 public class GUIStyle{public Font font;public int fontSize;public bool wordWrap;public TextAnchor alignment;public FontStyle fontStyle;public GUIStyle(){}public GUIStyle(GUIStyle other){font=other.font;fontSize=other.fontSize;}}
 public static class GUI {public static Matrix4x4 matrix;public static Color color;}
 public static class Screen {public static Rect safeArea=new Rect(0,0,1920,1080);}
 public static class Time {public static float unscaledTime;}
 public static class Mathf {public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int CeilToInt(float a)=>(int)Math.Ceiling(a);}
}
namespace UnityEngine.InputSystem {public class Key{public bool wasPressedThisFrame;}public class Keyboard{public static Keyboard current;public Key digit1Key=new(),digit2Key=new(),digit3Key=new();}}
namespace Alterverse.AI {public class MobaBotBrain:UnityEngine.MonoBehaviour{}}
namespace Alterverse.Combat {public class Health:UnityEngine.MonoBehaviour{public static List<Health> Active=new();}}
namespace Alterverse.Characters
{
 public enum HeroRole{Assassin,Skirmisher,Tank,Support,Bruiser}
 public class HeroIdentity:UnityEngine.MonoBehaviour {public HeroRole Role;}
 public static class LocalHeroContext {public static UnityEngine.GameObject hero;public static UnityEngine.GameObject Find()=>hero;}
}
namespace Alterverse.Multiplayer {public static class CoopAuthority {public static bool CanSimulateHostGameplay=true;}}
namespace Alterverse.Profile {public static class GameLanguage {public static string Text(string s)=>s;}}
namespace Alterverse.Match
{
 public class DimensionsFront:UnityEngine.MonoBehaviour {public bool HasEnded,IsOvertime;public float ElapsedSeconds,TimeRemaining;}
 public enum MidMatchPerkType{None,Movement,Damage,MaxHealth}
 public class PlanetBuffReceiver:UnityEngine.MonoBehaviour {public MidMatchPerkType SelectedPerk;public bool HasSelectedMidMatchPerk=>SelectedPerk!=MidMatchPerkType.None;public bool SelectMidMatchPerk(MidMatchPerkType v){SelectedPerk=v;return true;}public void ResetMidMatchPerk(){SelectedPerk=MidMatchPerkType.None;}}
}
namespace Alterverse.UI
{
 public static class MainMenuController{public static bool IsVisible;}
 public static class InMatchSettingsMenu{public static bool IsOpen;}
 public readonly struct CombatHudLayout {public readonly UnityEngine.Rect Safe;public readonly float Width,Height;public readonly UnityEngine.Matrix4x4 Matrix;public CombatHudLayout(UnityEngine.Rect r){Safe=r;float s=Math.Max(.01f,Math.Min(r.width/960f,r.height/540f));Width=r.width/s;Height=r.height/s;Matrix=new();}}
 public static class FrontEndTypography {public static UnityEngine.Font Body=new();}
 public static class DimensionUiMotif{public static void DrawCornerCuts(UnityEngine.Rect r,object p,UnityEngine.Color a,UnityEngine.Color b){}}
 internal class FrontEndTheme
 {
  public static readonly UnityEngine.Color Positive=new(0,1,1),Accent=new(1,1,0),Zero=new(.5f,0,1);public static object Pixel=new();
  public UnityEngine.GUIStyle Body=new(){fontSize=19},Small=new(){fontSize=16};
  public static List<UnityEngine.Rect> Buttons=new();public static List<(UnityEngine.Rect rect,string text,int size,bool muted)> Labels=new();public static int Click=-1;
  public void Panel(UnityEngine.Rect r){}public void HudPanel(UnityEngine.Rect r,float a){}
  public static void Fill(UnityEngine.Rect r,UnityEngine.Color c){}
  public void FitLabel(UnityEngine.Rect r,string s,UnityEngine.GUIStyle style)=>Labels.Add((r,s,style.fontSize,false));
  public void Label(UnityEngine.Rect r,string s,UnityEngine.GUIStyle style,bool muted=false)=>Labels.Add((r,s,style.fontSize,muted));
  public bool Action(UnityEngine.Rect r,string s,bool selected=false){Buttons.Add(r);return Buttons.Count-1==Click;}
 }
}
static class Regression
{
 static void Check(bool b,string msg){if(!b)throw new Exception(msg);}
 static void Draw(object x)=>x.GetType().GetMethod("OnGUI",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(x,null);
 static void Set(object x,string n,object v)=>x.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(x,v);
 static void Main()
 {
  foreach(var screen in new[]{new UnityEngine.Rect(0,0,1920,1080),new UnityEngine.Rect(100,30,2400,1080)})
  for(int choice=0;choice<3;choice++){
   UnityEngine.Screen.safeArea=screen;UnityEngine.Time.unscaledTime=3;
   var hero=new UnityEngine.GameObject();hero.AddComponent<Alterverse.Characters.HeroIdentity>().Role=Alterverse.Characters.HeroRole.Tank;
   var receiver=hero.AddComponent<Alterverse.Match.PlanetBuffReceiver>();Alterverse.Characters.LocalHeroContext.hero=hero;
   var director=new UnityEngine.GameObject().AddComponent<Alterverse.Match.MidMatchPassiveDirector>();
   Set(director,"front",new UnityEngine.GameObject().AddComponent<Alterverse.Match.DimensionsFront>());Set(director,"midMatchTriggered",true);
   Alterverse.UI.FrontEndTheme.Buttons.Clear();Alterverse.UI.FrontEndTheme.Labels.Clear();Alterverse.UI.FrontEndTheme.Click=choice;
   Draw(director);var cards=Alterverse.UI.FrontEndTheme.Buttons;var labels=Alterverse.UI.FrontEndTheme.Labels;
   Check(cards.Count==3,"three bonus buttons");
   var layout=new Alterverse.UI.CombatHudLayout(screen);
   for(int i=0;i<3;i++){var c=cards[i];Check(c.x>=0&&c.xMax<=layout.Width&&c.y>=0&&c.yMax<=layout.Height,"bonus card inside safe area");Check(c.width>=200&&c.height>=100,"large tap targets");if(i>0)Check(cards[i-1].xMax<c.x,"cards do not overlap");}
   Check(labels.FindAll(x=>x.text=="+15%"&&x.size>=28&&!x.muted).Count==3,"large high-contrast values");
   foreach(string text in new[]{"MOVEMENT SPEED","OUTGOING DAMAGE","MAX HEALTH"})Check(labels.Exists(x=>x.text==text&&x.size>=18&&!x.muted),"readable bonus label: "+text);
   Check(receiver.SelectedPerk==(Alterverse.Match.MidMatchPerkType)(choice+1),"each button selects its own bonus");
  }
  Console.WriteLine("PASS mid-match buff layout, font and three tap targets at 16:9 and wide safe areas (controlled Unity fixtures).");
 }
}
