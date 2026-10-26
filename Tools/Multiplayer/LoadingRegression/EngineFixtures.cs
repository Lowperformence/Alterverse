// Test-only substitutes. No native asset build, renderer or real network is exercised.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace UnityEngine {
 public class Object {
  public static object Found; public static T FindAnyObjectByType<T>() where T:class=>Found as T;
  public static void Destroy(Object o){} public static void DontDestroyOnLoad(Object o){}
 }
 public class MonoBehaviour:Object {
  public GameObject gameObject; public IEnumerator Pending;
  public void StartCoroutine(IEnumerator e){Pending=e;e.MoveNext();}
 }
 public class GameObject:Object {
  public static readonly List<GameObject> All=new(); public string name;public bool activeInHierarchy=true;
  public GameObject(string n){name=n;All.Add(this);}public static GameObject Find(string n)=>All.Find(g=>g.name==n&&g.activeInHierarchy);
  public void SetActive(bool v){activeInHierarchy=v;}
  public T AddComponent<T>()where T:MonoBehaviour,new(){var v=new T{gameObject=this};typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(v,null);return v;}
 }
 public class Camera:Object {public int cullingMask;}
 public static class Debug {public static void LogError(object value){}}
 public enum RuntimeInitializeLoadType{SubsystemRegistration,AfterSceneLoad}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public struct Vector3{public Vector3(float x,float y,float z){}public static Vector3 one=>new();public static Vector3 operator*(Vector3 v,float n)=>v;}
 public struct Quaternion{public static Quaternion identity=>new();}
 public struct Matrix4x4{public int value;public static Matrix4x4 identity=>new();public static Matrix4x4 TRS(Vector3 a,Quaternion b,Vector3 c)=>new();}
 public struct Color{public float a;public Color(float r,float g,float b,float a){this.a=a;}public static Color white=>new(1,1,1,1);}
 public struct Rect{public float x,y,width,height;public float yMax=>y+height;public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}}
 public static class Screen{public static int width=1600,height=720;public static Rect safeArea=new(20,0,1560,720);}
 public static class Time{public static float unscaledTime,unscaledDeltaTime=.1f;public static int frameCount;}
 public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Clamp01(float a)=>Math.Clamp(a,0,1);public static float Min(float a,float b)=>Math.Min(a,b);public static float PingPong(float a,float n)=>n-Math.Abs(a%(2*n)-n);}
 public static class GUI{public static Matrix4x4 matrix;public static Color color;public static bool enabled;public static int depth;}
}
namespace Unity.Netcode{public class NetworkManager{public static NetworkManager Singleton;public bool IsServer,IsListening,IsConnectedClient;}}
namespace Alterverse.Characters{public static class PlayerHeroSelection{public static int Selected;public static void Select(int hero){Selected=hero;}}}
namespace Alterverse.Match{
 public class MatchPlayerSlot{public int Hero;}
 public class MatchSession{public bool IsFull=true;public MatchPlayerSlot[] Slots={new(){Hero=3}};}
 public enum PauseReason{Menu,Result}
 public static class MatchClock{public static bool InBackground;public static void Set(PauseReason p,bool b){}}
 public static class ArenaVariantRuntime{public static string Current="Moon",Selected="Moon";public static bool ShuffleEnabled;}
 public static class MatchExit{public static bool Leaving;public static string Error;public static Task ReturnToMenuAsync(string message=null){Leaving=true;Error=message;Alterverse.UI.OnlineMatchLoadingScreen.Cancel();return Task.CompletedTask;}}
}
namespace Alterverse.Multiplayer{
 public static class CoopMatchStateSync{public static int ArenaReadyCalls;public static void MarkLocalArenaReady(){ArenaReadyCalls++;}}
 public class OnlineMatchPlan{public Alterverse.Match.MatchSession Session=new();public int HumanCount=2;public string Arena="Moon";}
 public static class CoopAuthority{public static OnlineMatchPlan ActivePlan;public static bool Active=>ActivePlan!=null;public static bool LocalHasServerAuthority;public static void EndMatch(){ActivePlan=null;}}
 public static class CoopRosterBuilder{public static int LocalSlot;public static int FindLocalHumanSlot(OnlineMatchPlan p)=>LocalSlot;}
 public class EdgegapOnlineFlow{public static EdgegapOnlineFlow Instance=new();public bool IsSearching;public string LastError="",Status="GAME SERVER CONNECTED • WAITING FOR MATCH START";}
 public class EdgegapMatchmakerClient{public static EdgegapMatchmakerClient Instance=new();public bool HasAssignment;}
}
namespace Alterverse.UI{
 public sealed partial class MainMenuController{
  public static bool IsVisible;private FrontEndTheme theme=new();private bool coopLaunching;private int selectedHero=0;
  private string HeroName=>"ROOK";private static readonly string[] Roles={"VANGUARD"};
  public MainMenuController(bool coop=false){coopLaunching=coop;}
 }
 public class FrontEndTheme{
  public object Title,Heading,Body,Small;public static UnityEngine.Color Raised,Accent;
  public static List<string> Labels=new(),Actions=new();public static UnityEngine.Rect Cover;public static bool Click;
  public static void Fill(UnityEngine.Rect r,UnityEngine.Color c){if(r.width==UnityEngine.Screen.width)Cover=r;}
  public void Label(UnityEngine.Rect r,string s,object t,bool muted=false){Labels.Add(s);}
  public void FitLabel(UnityEngine.Rect r,string s,object t,bool muted=false){Labels.Add(s);}
  public bool Action(UnityEngine.Rect r,string s){Actions.Add(s);bool b=Click;Click=false;return b;}
 }
}
namespace Alterverse.Shop{
 public class ArenaFixture{public string Name;}
 public static class ArenaShopCatalog{public static ArenaFixture For(string arena)=>new(){Name=arena};}
}
namespace Alterverse.Prototype{
 public sealed partial class PrototypeBootstrap:UnityEngine.MonoBehaviour{
  private bool buildOnStart=true;
  private const int MenuShowcaseLayer=31;
  public UnityEngine.Camera pendingArenaCamera;public UnityEngine.GameObject pendingArenaWorld;public int revealArenaFrame;
  public static int Prepared;public int Builds,Restarts;public bool Success=true;
  public static void PrepareTransitionFrame(){Prepared++;}
  public bool BuildPrototype(){Builds++;if(!Success){RecoverFailedBuild();return false;}pendingArenaCamera=new();pendingArenaWorld=new UnityEngine.GameObject("DF_MatchWorld");revealArenaFrame=UnityEngine.Time.frameCount+1;return true;}
  public IEnumerator RestartPrototype(){Restarts++;yield return null;BuildPrototype();}
  private static void RecoverFailedBuild(){_=Alterverse.Match.MatchExit.ReturnToMenuAsync("Build failed");}
 }
}



namespace Alterverse.Combat { public static class HotZoneTank { public static bool FinishMatchPreparation()=>true; } }
namespace Alterverse.Visuals { public static class MatchInsertionTransport { public static void PrepareForReveal(UnityEngine.GameObject world){} } }

