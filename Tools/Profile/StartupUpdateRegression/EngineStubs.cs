using System;
using System.Threading.Tasks;
namespace UnityEngine {
 public class Object{public static void Destroy(Object o){}public static void DontDestroyOnLoad(Object o){}}
 public class GameObject:Object{public GameObject(string n=""){}public T AddComponent<T>()where T:new(){var v=new T();typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.Invoke(v,null);return v;}}
 public class MonoBehaviour:Object{public GameObject gameObject=new();}
 public enum RuntimeInitializeLoadType{AfterSceneLoad}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeLoadType Type;public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){Type=t;}}
 public static class Application{public static bool isBatchMode;public static string version="1.0.0";}
 public static class Time{public static float unscaledTime,Clock,ClockStep;public static float realtimeSinceStartup{get{Clock+=ClockStep;return Clock;}}}
 public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);public static int Clamp(int n,int a,int b)=>Math.Clamp(n,a,b);}
 public struct Rect{public Rect(float x,float y,float w,float h){}}
 public delegate void AndroidJavaRunnable();
 public static class PlayFixture{public static int Installed=5,Available=10,Availability=0,InfoRequests,DialogRequests,UiRequests,WrongSignatures;public static bool Allowed=true,Successful=true,Complete=true,StartResult=true,Missing,OnUiThread;}
 public class AndroidJavaObject:IDisposable{
  readonly string kind;public AndroidJavaObject(string kind){this.kind=kind;}
  public void Dispose(){}
  public T Get<T>(string field){if(kind=="package-info"&&field=="versionCode")return (T)(object)PlayFixture.Installed;throw new Exception("Unexpected field");}
  public T Call<T>(string method,params object[] args){object value=method switch{
   "getPackageManager"=>new AndroidJavaObject("package-manager"),"getPackageName"=>"com.lowperformence.dimensionsfront","getPackageInfo"=>new AndroidJavaObject("package-info"),
   "getAppUpdateInfo"=>Info(),"isComplete"=>PlayFixture.Complete,"isSuccessful"=>PlayFixture.Successful,"getResult"=>new AndroidJavaObject("info"),
   "updateAvailability"=>PlayFixture.Availability,"availableVersionCode"=>PlayFixture.Available,"isUpdateTypeAllowed"=>PlayFixture.Allowed,
   "build"=>new AndroidJavaObject("options"),"startUpdateFlowForResult"=>Start<T>(args),_=>throw new Exception("Unexpected Java call "+method)};return (T)value;}
  static AndroidJavaObject Info(){PlayFixture.InfoRequests++;return new("task");}
  static object Start<T>(object[] args){if(typeof(T)!=typeof(bool)||!PlayFixture.OnUiThread||args.Length!=4||args[1] is not AndroidJavaObject||args[2] is not AndroidJavaObject){PlayFixture.WrongSignatures++;throw new Exception("Wrong JNI signature or thread");}PlayFixture.DialogRequests++;return PlayFixture.StartResult;}
  public void Call(string method,params object[] args){if(method!="runOnUiThread")throw new Exception("Unexpected UI operation");PlayFixture.UiRequests++;PlayFixture.OnUiThread=true;try{((AndroidJavaRunnable)args[0])();}finally{PlayFixture.OnUiThread=false;}}
 }
 public class AndroidJavaClass:IDisposable{
  readonly string name;public AndroidJavaClass(string name){if(PlayFixture.Missing&&name.Contains("AppUpdate"))throw new Exception("SDK unavailable");this.name=name;}
  public T GetStatic<T>(string field)=>(T)(object)new AndroidJavaObject("activity");
  public T CallStatic<T>(string method,params object[] args)=>(T)(object)new AndroidJavaObject(method=="create"?"manager":"builder");public void Dispose(){}
 }
}
namespace Alterverse.Multiplayer{public static class DedicatedServerNetworkRuntime{public static bool Requested;}}
namespace Alterverse.Profile{
 public class AccountServerBootstrap{public static bool CloudProfileReady=true;public static AccountServerBootstrap Instance=new();public bool Busy,GooglePlayAccountReady=true,StartupAttemptFinished=true;public string Status="Ready",GoogleStatus="Ready";public Task ConnectGoogleAsync()=>Task.CompletedTask;}
 public static class AccountProfilePrefs{public static string Account="owner";public static int GetInt(string k)=>0;}
 public static class PlayerProfile{public static float MasterVolume=1,MusicVolume=1,SfxVolume=1;}
}
namespace Alterverse.Shop{public class PaidShardCommerce{public static PaidShardCommerce Instance=new();public bool Ready=true;public int MinAndroidVersionCode;public string MinClientVersion="",Status="Ready";public static event Action EntitlementsChanged;public static void Changed()=>EntitlementsChanged?.Invoke();public void Refresh(){}}}
namespace Alterverse.UI{public static class NativeStoreReview{public static int Calls;public static void OpenStore(){Calls++;}}}
