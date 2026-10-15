using System; using System.Collections; using System.Collections.Generic; using System.Linq; using System.Reflection;
using NQ=System.Numerics.Quaternion; using NV=System.Numerics.Vector3;
namespace UnityEngine {
[AttributeUsage(AttributeTargets.Field)] public class SerializeField:Attribute {}
[AttributeUsage(AttributeTargets.Field)] public class MinAttribute:Attribute { public MinAttribute(float f){} }
[AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute:Attribute { public HeaderAttribute(string s){} }
[AttributeUsage(AttributeTargets.Field)] public class RangeAttribute:Attribute { public RangeAttribute(float a,float b){} }
[AttributeUsage(AttributeTargets.Class,AllowMultiple=true)] public class RequireComponent:Attribute {public RequireComponent(params Type[] t){} }
[AttributeUsage(AttributeTargets.Class)] public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int n){} }
[AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute:Attribute {public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){} }
public enum RuntimeInitializeLoadType {SubsystemRegistration,AfterSceneLoad,BeforeSceneLoad}
public enum FindObjectsInactive {Exclude,Include}
public enum FindObjectsSortMode {None}
public enum CameraType {Game,SceneView} public enum CameraClearFlags {SolidColor}
public static class Time {public static float time,timeScale=1,deltaTime=.1f,unscaledTime;public static int frameCount;}
public static class Debug {public static int Errors;public static void LogWarning(string s){} public static void LogError(string s){Errors++;}public static void LogException(Exception e){Errors++;}}
 public static class Mathf {
  public static int Clamp(int v,int a,int b){return Math.Max(a,Math.Min(b,v));} public static int Min(int a,int b){return Math.Min(a,b);} public static bool Approximately(float a,float b){return Math.Abs(a-b)<.00001f;} public static int Max(int a,int b){return Math.Max(a,b);} public static int RoundToInt(float a){return (int)Math.Round(a);} public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);} public static float InverseLerp(float a,float b,float x){return Clamp01((x-a)/(b-a));} public const float PI=(float)Math.PI, Deg2Rad=PI/180f;
  public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}
  public static float Clamp01(float v){return Clamp(v,0,1);} public static float Abs(float v){return Math.Abs(v);}
  public static float Sign(float v){return v>=0?1:-1;} public static float Sin(float v){return (float)Math.Sin(v);}
  public static float Cos(float v){return (float)Math.Cos(v);} public static float Pow(float a,float b){return (float)Math.Pow(a,b);}
  public static float Sqrt(float v){return (float)Math.Sqrt(v);} public static float Max(float a,float b){return Math.Max(a,b);}
  public static float Min(float a,float b){return Math.Min(a,b);} public static float Lerp(float a,float b,float t){return a+(b-a)*Clamp01(t);}
  public static float MoveTowards(float v,float target,float delta){return Abs(target-v)<=delta?target:v+Sign(target-v)*delta;}
 }
 public struct Vector3 {
  public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;}
  public static Vector3 zero=>new Vector3(); public static Vector3 one=>new Vector3(1,1,1);
  public static Vector3 up=>new Vector3(0,1,0); public static Vector3 forward=>new Vector3(0,0,1); public static Vector3 right=>new Vector3(1,0,0);
  public static Vector3 left=>new Vector3(-1,0,0);
  public float sqrMagnitude=>x*x+y*y+z*z; public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector3 normalized=>magnitude>1e-10f?this/magnitude:zero; public void Normalize(){this=normalized;}
  public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
  public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
  public static Vector3 operator -(Vector3 a){return a*-1f;} public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);}
  public static Vector3 operator *(float b,Vector3 a){return a*b;} public static Vector3 operator /(Vector3 a,float b){return a*(1f/b);}
  public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
  public static Vector3 Cross(Vector3 a,Vector3 b){return new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
  public static float Distance(Vector3 a,Vector3 b){return (a-b).magnitude;}
  public static Vector3 Lerp(Vector3 a,Vector3 b,float t){return a+(b-a)*Mathf.Clamp01(t);}
  public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n){return a-n*Dot(a,n)/n.sqrMagnitude;}
  public static Vector3 Scale(Vector3 a,Vector3 b){return new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);}
  internal NV N=>new NV(x,y,z); internal static Vector3 From(NV v){return new Vector3(v.X,v.Y,v.Z);}
 }
 public struct Quaternion {
  public float x,y,z,w; public Quaternion(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}
  public Vector3 eulerAngles=>Vector3.zero; public static Quaternion LookRotation(Vector3 f){return LookRotation(f,Vector3.up);} public static Quaternion identity=>new Quaternion(0,0,0,1); internal NQ N=>new NQ(x,y,z,w);
  internal static Quaternion From(NQ q){q=NQ.Normalize(q);return new Quaternion(q.X,q.Y,q.Z,q.W);}
  public static Quaternion operator *(Quaternion a,Quaternion b){return From(a.N*b.N);}
  public static Vector3 operator *(Quaternion a,Vector3 v){return Vector3.From(NV.Transform(v.N,NQ.Normalize(a.N)));}
  public static Quaternion Inverse(Quaternion q){return From(NQ.Inverse(q.N));}
  public static Quaternion AngleAxis(float angle,Vector3 axis){return axis.sqrMagnitude<1e-12f?identity:From(NQ.CreateFromAxisAngle(axis.normalized.N,angle*Mathf.Deg2Rad));}
  // Unity's Euler convention applies Z, then X, then Y.
  public static Quaternion Euler(float x,float y,float z){return AngleAxis(y,Vector3.up)*AngleAxis(x,Vector3.right)*AngleAxis(z,Vector3.forward);}
  public static Quaternion Slerp(Quaternion a,Quaternion b,float t){return From(NQ.Slerp(a.N,b.N,Mathf.Clamp01(t)));}
  public static float Angle(Quaternion a,Quaternion b){double dot=(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z+(double)a.w*b.w;
   double na=Math.Sqrt((double)a.x*a.x+(double)a.y*a.y+(double)a.z*a.z+(double)a.w*a.w),nb=Math.Sqrt((double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z+(double)b.w*b.w);
   return (float)(2*Math.Acos(Math.Min(1,Math.Abs(dot/(na*nb))))*180/Math.PI);}
  public static Quaternion RotateTowards(Quaternion a,Quaternion b,float delta){float angle=Angle(a,b);return Slerp(a,b,angle<1e-8f?1:Math.Min(1,delta/angle));}
  public static Quaternion FromToRotation(Vector3 from,Vector3 to){Vector3 a=from.normalized,b=to.normalized;float d=Vector3.Dot(a,b);
   if(d>.999999f)return identity;if(d<-.999999f){Vector3 axis=Vector3.Cross(a,Vector3.right);if(axis.sqrMagnitude<1e-6f)axis=Vector3.Cross(a,Vector3.up);return AngleAxis(180,axis);}
   Vector3 c=Vector3.Cross(a,b);return From(new NQ(c.x,c.y,c.z,1+d));}
  public static Quaternion LookRotation(Vector3 f,Vector3 u){f=f.normalized;Vector3 r=Vector3.Cross(u,f).normalized;u=Vector3.Cross(f,r);
   var m=new System.Numerics.Matrix4x4(r.x,r.y,r.z,0,u.x,u.y,u.z,0,f.x,f.y,f.z,0,0,0,0,1);return From(NQ.CreateFromRotationMatrix(m));}
 }

public class Object {
 public bool destroyed;
 public static void Destroy(Object o){if(o!=null){o.destroyed=true;if(o is GameObject)((GameObject)o).SetActive(false);}}
 public static T FindAnyObjectByType<T>() where T:class {return FindObjectsByType<T>().FirstOrDefault();}
 public static T[] FindObjectsByType<T>(FindObjectsInactive inactive=FindObjectsInactive.Exclude) where T:class {return GameObject.All.Where(g=>!g.destroyed&&(inactive==FindObjectsInactive.Include||g.activeInHierarchy)).SelectMany(g=>g.Components).OfType<T>().ToArray();}
 public static T[] FindObjectsByType<T>(FindObjectsInactive inactive,FindObjectsSortMode sort) where T:class=>FindObjectsByType<T>(inactive);
 public static void DontDestroyOnLoad(Object o){}
}
public class Component:Object {
 public GameObject gameObject;public Transform transform=>gameObject.transform;
 public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
 public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class=>gameObject.GetComponentsInChildren<T>(inactive);
}
public class MonoBehaviour:Component {
 public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
 public List<Coroutine> Routines=new List<Coroutine>();
 public Coroutine StartCoroutine(IEnumerator e){var c=new Coroutine(e);Routines.Add(c);e.MoveNext();return c;}
 public void StopCoroutine(Coroutine c){c.Stopped=true;}
}
public class Coroutine {public IEnumerator Enumerator; public bool Stopped;public Coroutine(IEnumerator e){Enumerator=e;}}
public class GameObject:Object {
 public static List<GameObject> All=new List<GameObject>();public List<Component> Components=new List<Component>();public string name,tag;
 public bool activeSelf=true; public bool activeInHierarchy=>!destroyed&&activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
 public Transform transform;public Scene scene=>new Scene();
 public GameObject(string s){name=s;transform=new Transform{gameObject=this};Components.Add(transform);All.Add(this);}
 public void SetActive(bool b){activeSelf=b;}
 public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);var m=typeof(T).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic);m?.Invoke(c,null);return c;}
 public T GetComponent<T>() where T:class=>Components.OfType<T>().FirstOrDefault();
 public T[] GetComponentsInChildren<T>(bool inactive=false) where T:class {var l=new List<T>();if(inactive||activeInHierarchy)l.AddRange(Components.OfType<T>());foreach(var child in transform.children)l.AddRange(child.gameObject.GetComponentsInChildren<T>(inactive));return l.ToArray();}
 public static GameObject Find(string n)=>All.FirstOrDefault(g=>g.name==n&&g.activeInHierarchy);
}
public class Scene {public GameObject[] GetRootGameObjects()=>GameObject.All.Where(g=>!g.destroyed&&g.transform.parent==null).ToArray();}
public class Transform:Component {
 public Transform parent; public List<Transform> children=new List<Transform>();public Vector3 position;public Quaternion rotation=Quaternion.identity;
 public void SetParent(Transform p,bool worldPositionStays){parent?.children.Remove(this);parent=p;p?.children.Add(this);}
 public void SetPositionAndRotation(Vector3 p,Quaternion q){position=p;rotation=q;}
}
public struct Color {public Color(float a,float b,float c,float d){}}
public class Camera:MonoBehaviour {public static Camera main;public CameraType cameraType;public Object targetTexture;public CameraClearFlags clearFlags;public Color backgroundColor;public int cullingMask=42;private void Awake(){if(main==null)main=this;}}
public class Renderer:Component {public bool enabled=true;}
public class Collider:Component {public bool enabled=true;}
public class CharacterController:Collider {}
}
namespace Unity.Netcode {public class NetworkManager {public static NetworkManager Singleton;public bool IsListening,IsServer,IsClient;}}
namespace Alterverse.Characters {
public enum HeroId {Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok}
public class HeroLocomotion:UnityEngine.MonoBehaviour {public int Resets;public void ResetMotion(){Resets++;}}
public class HeroMotor:UnityEngine.MonoBehaviour {public int Clears;public void ClearInput(){Clears++;}}
public static class PlayerHeroSelection {public static HeroId Selected;public static void Select(HeroId h){Selected=h;}}
}
namespace Alterverse.Combat {
public class Health:UnityEngine.MonoBehaviour {public bool IsDead;public event Action Died;public event Action<float,float,UnityEngine.GameObject> Damaged;public int Restores;public void Kill(){IsDead=true;Died?.Invoke();}public void RestoreFull(){Restores++;IsDead=false;}public void Hit(float damage,float absorbed){Damaged?.Invoke(damage,absorbed,null);}}
public class BasicAttack:UnityEngine.MonoBehaviour {}
public class SpawnHealingZone:UnityEngine.MonoBehaviour {public void InitializeTeam(int t){}public void Initialize(Health h){}}
public class BattleItemAirdropSchedule:UnityEngine.MonoBehaviour {public int Resets;public void ResetSchedule(){Resets++;}}
public class SpaceBomberDirector:UnityEngine.MonoBehaviour {}
public class CombatFeedback:UnityEngine.MonoBehaviour {}
}
namespace Alterverse.Audio {public enum DimensionSound {RecallStart,RecallCancel,RecallComplete,RealityShatter,Victory,Defeat,Overtime,SuddenDeath}public static class DimensionAudio {public static DimensionSound Last;public static int Count;public static void Play(DimensionSound s,float f){Last=s;Count++;}}public enum AudioCuePriority{Critical}public static class AudioCuePolicy{public static bool Allow(string s,AudioCuePriority p)=>true;}public static class AudioMixState{public static float CriticalGain=1;public static void Reset(){}}public sealed partial class ProductionMusicDirector:UnityEngine.MonoBehaviour {private int result=-1;public int Result=>result;}}
namespace Alterverse.Shop {public static class RecallCosmeticEntitlement {public static bool Owned;}public static class ArenaUnlockInventory {public static bool Owned(Alterverse.Match.ArenaVariant v)=>true;}}
namespace Alterverse.Visuals {public class RecallCosmeticVfx:UnityEngine.MonoBehaviour {public void SetRecallState(bool a,float b){}}public class DimensionsFrontVisual:UnityEngine.MonoBehaviour {}}
namespace Alterverse.Onboarding {public static class OnboardingSessionGuard {public static int Recalls;public static void Recall(object o){Recalls++;}}}
namespace Alterverse.Profile {
public enum BattleItemType {None,Medkit}
public static class AccountProfilePrefs {public static Dictionary<string,object> Data=new Dictionary<string,object>();public static int GetInt(string k,int f=0)=>Data.ContainsKey(k)?(int)Data[k]:f;public static string GetString(string k,string f="")=>Data.ContainsKey(k)?(string)Data[k]:f;public static void Save(){} }
public static class ProfileTransaction {public class Write {public string Key;public object Value;public Write(string k,int v){Key=k;Value=v;}}public static void Recover(){}public static void Commit(params Write[] w){foreach(var x in w)AccountProfilePrefs.Data[x.Key]=x.Value;}public static int Add(int v,int a)=>(int)Math.Min(int.MaxValue,Math.Max(0L,(long)v+a));}
public static class PlayerWallet {public static int EarnedBalance=>AccountProfilePrefs.GetInt("DF_Shards");}
}
namespace Alterverse.AI {public enum BotDifficulty {Easy,Normal,Hard,Expert,Nightmare} public class MobaBotBrain:UnityEngine.MonoBehaviour {public int Resumed;public void ResumeRoamAfterRespawn(){Resumed++;}}public static class BotDifficultySettings {public static void BeginMatchOverride(BotDifficulty b){}} public class FiveVsFiveTestRoster:UnityEngine.MonoBehaviour {}}
namespace Alterverse.CameraSystem {public class MobaCamera:UnityEngine.MonoBehaviour {public void SetTarget(UnityEngine.Transform t){}}}
namespace Alterverse.Match {
public enum ArenaVariant {Moon,Mars,GreenPlanet,Noxara,Kryon,Earth,Pyraxis,Neptune,Dark00,DiamondCore,GJ504b}
public static class ArenaVariantRuntime {public static ArenaVariant Selected;public static void BeginCoopMatch(ArenaVariant a){}public static void BeginMatch(){}public static void EnsureValidSelection(){}public static void Select(ArenaVariant a){Selected=a;}}
public static class WingMatchRewardBonus {public static void BeginMatch(Alterverse.Characters.HeroId h){}}
public class DimensionsFrontHUD:UnityEngine.MonoBehaviour {}
public class DimensionsFrontCombatBridge:UnityEngine.MonoBehaviour {}
public class HotZoneDirector:UnityEngine.MonoBehaviour {}
public class MatchTelemetry:UnityEngine.MonoBehaviour {}
public class MatchScoreboard:UnityEngine.MonoBehaviour {}
public class MatchOpening:UnityEngine.MonoBehaviour {}
public class DimensionPlanetDirector:UnityEngine.MonoBehaviour {}
public class PlanetBuffReceiver:UnityEngine.MonoBehaviour {}
}
namespace Alterverse.UI {
public sealed partial class MainMenuController:UnityEngine.MonoBehaviour {
private enum Page {Play,Coop}private Page page;private bool coopLaunching,launching,visible;private string launchMessage="";
public static bool IsVisible=>Instance!=null&&Instance.visible;public static MainMenuController Instance;
public static void ShowHome(){if(Instance!=null){Instance.gameObject.SetActive(true);Instance.visible=true;Instance.launching=false;}Alterverse.Match.MatchClock.Set(Alterverse.Match.PauseReason.Menu,true);}
public void Launch(bool coop){coopLaunching=coop;visible=true;launching=true;EnterMatch();}
}
public class PrototypeHUD:UnityEngine.MonoBehaviour {public void Initialize(object a,object b,object c){}}
public class InMatchSettingsMenu:UnityEngine.MonoBehaviour {}
public class TargetIndicatorHUD:UnityEngine.MonoBehaviour {}
public class HeroTeamMarkers:UnityEngine.MonoBehaviour {}
public class HotZoneHUD:UnityEngine.MonoBehaviour {}
public class HotZoneTankHUD:UnityEngine.MonoBehaviour {}
public class OvertimePresentation:UnityEngine.MonoBehaviour {}
public sealed partial class PlanetHUD:UnityEngine.MonoBehaviour {private Alterverse.Match.PlanetBuffReceiver playerBuff;private float nextFind;public Alterverse.Match.PlanetBuffReceiver Buff=>playerBuff;}
}
namespace Alterverse.Multiplayer {
public class OnlineMatchPlan:CoopMatchPlan {}
public static class CoopAuthority {public static bool Active,LocalHasServerAuthority=true,LocalIsHost=true;public static bool CanSimulateHostGameplay=true;public static CoopMatchPlan ActivePlan;public static void EndMatch(){Active=false;ActivePlan=null;}}
public static class CoopRosterBuilder {public static int LocalSlot=0;public static bool Success=true;public static int FindLocalHumanSlot(CoopMatchPlan p)=>LocalSlot;public static UnityEngine.Vector3 SafeSpawn(int t,int s)=>UnityEngine.Vector3.zero;public static bool BuildServer(CoopMatchPlan p)=>Success;public static bool Build(UnityEngine.GameObject g,CoopMatchPlan p)=>Success;}
public static class CoopNetworkBootstrap {public static int Shutdowns;public static void CancelDedicatedConnection(){}public static void ShutdownLocalNetwork(){Shutdowns++;}}
public class CoopLobbyService {public const int MinHumanPlayers=1,MaxHumanPlayers=MatchSession.TeamSize;public static CoopLobbyService Instance;public bool Busy,InLobby;public System.Threading.Tasks.Task LeaveLobbyAsync(){InLobby=false;return System.Threading.Tasks.Task.CompletedTask;}public class MemberView {public string PlayerId,DisplayName;public bool IsHost,IsLocal,Ready;public Alterverse.Characters.HeroId Hero;public Alterverse.Profile.BattleItemType BattleItem;}}
public class OnlinePartyService {public static OnlinePartyService Instance;public bool Busy,InParty;public System.Threading.Tasks.Task LeavePartyAsync(){InParty=false;return System.Threading.Tasks.Task.CompletedTask;}}
public class EdgegapOnlineFlow {public static EdgegapOnlineFlow Instance;public bool Busy;public System.Threading.Tasks.Task LeavePartyAsync()=>System.Threading.Tasks.Task.CompletedTask;}
public class CoopHeroStateSync:UnityEngine.MonoBehaviour {}
public class CoopMatchStateSync:UnityEngine.MonoBehaviour {}
public class CoopWorldEventSync:UnityEngine.MonoBehaviour {}
public class CoopWorldStateSync:UnityEngine.MonoBehaviour {}
public class CoopMatchResultSync:UnityEngine.MonoBehaviour {}
}
namespace Alterverse.Prototype {
public sealed partial class PrototypeBootstrap {
public static bool FailArena,FailPlayer;public static int ArenaBuilds;
private void BuildArena(){ArenaBuilds++;new UnityEngine.GameObject("Fixture terrain");if(FailArena)throw new InvalidOperationException("injected arena failure");}
private static UnityEngine.GameObject CreateLocalPlayer(Alterverse.Characters.HeroId hero,UnityEngine.Vector3 p,bool r,int t){var player=new UnityEngine.GameObject("AV_Player");player.AddComponent<Alterverse.Combat.Health>();if(FailPlayer)throw new InvalidOperationException("injected hero failure");return player;}
}
}
