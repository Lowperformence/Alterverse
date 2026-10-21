using System;using System.Collections;using System.Collections.Generic;using System.Linq;using System.Text;using System.Text.Json;using UnityEngine.Networking;
namespace UnityEngine
{
 public enum RuntimeInitializeLoadType{BeforeSceneLoad,AfterSceneLoad,SubsystemRegistration}
 public class RuntimeInitializeOnLoadMethodAttribute:Attribute{public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t){}}
 public class Object{public static void Destroy(Object o){}public static void DontDestroyOnLoad(Object o){}}
 public class GameObject:Object
 {
  public GameObject(string name=""){}public T AddComponent<T>()where T:MonoBehaviour,new(){var t=new T{gameObject=this};typeof(T).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.Invoke(t,null);return t;}
 }
 public class MonoBehaviour:Object{public GameObject gameObject;public void StartCoroutine(IEnumerator e)=>Scheduler.Start(this,e);public void StopAllCoroutines()=>Scheduler.Stop(this);}
 public static class Time{public static float unscaledTime;}
 public static class Debug{public static void LogWarning(string text){}}
 public class TextAsset{public string text;}
 public static class Resources{public static T Load<T>(string path)where T:class=>null;}
 public static class Mathf{public static int Max(int a,int b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);}
 public static class JsonUtility
 {
  sealed class JournalWriteConverter:System.Text.Json.Serialization.JsonConverter<Alterverse.Profile.ProfileTransaction.Write>
  {
   public override Alterverse.Profile.ProfileTransaction.Write Read(ref Utf8JsonReader reader,Type type,JsonSerializerOptions options)
   {using var document=JsonDocument.ParseValue(ref reader);var root=document.RootElement;string key=root.GetProperty("key").GetString();return root.TryGetProperty("isText",out var text)&&text.GetBoolean()?new(key,root.GetProperty("text").GetString()):new(key,root.GetProperty("number").GetInt32());}
   public override void Write(Utf8JsonWriter writer,Alterverse.Profile.ProfileTransaction.Write value,JsonSerializerOptions options)
   {writer.WriteStartObject();writer.WriteString("key",value.key);writer.WriteString("text",value.text);writer.WriteNumber("number",value.number);writer.WriteBoolean("isText",value.isText);writer.WriteEndObject();}
  }
  static readonly JsonSerializerOptions options=new(){IncludeFields=true,Converters={new JournalWriteConverter()}};
  public static string ToJson(object o)=>JsonSerializer.Serialize(o,o.GetType(),options);
  public static T FromJson<T>(string text)=>JsonSerializer.Deserialize<T>(text,options);
 }
 public static class PlayerPrefs
 {
  static readonly Dictionary<string,object> values=new();public static int Saves;
  public static bool HasKey(string k)=>values.ContainsKey(k);public static void DeleteKey(string k)=>values.Remove(k);
  public static string GetString(string k,string fallback="")=>values.TryGetValue(k,out var v)&&v is string s?s:fallback;
  public static int GetInt(string k,int fallback=0)=>values.TryGetValue(k,out var v)&&v is int n?n:fallback;
  public static float GetFloat(string k,float fallback=0)=>values.TryGetValue(k,out var v)&&v is float n?n:fallback;
  public static void SetString(string k,string v)=>values[k]=v;public static void SetInt(string k,int v)=>values[k]=v;public static void SetFloat(string k,float v)=>values[k]=v;public static void Save()=>Saves++;
 }
 public static class Scheduler
 {
  sealed class Job{public object Owner;public Stack<IEnumerator> Frames=new();public UnityWebRequestAsyncOperation Waiting;}
  static readonly List<Job> jobs=new();public static int Count=>jobs.Count;
  public static void Start(object owner,IEnumerator e){var job=new Job{Owner=owner};job.Frames.Push(e);jobs.Add(job);Advance(job);}
  static void Advance(Job job)
  {
   if(job.Waiting!=null&&!job.Waiting.isDone)return;job.Waiting=null;
   while(job.Frames.Count>0)
   {
    IEnumerator top=job.Frames.Peek();
    if(!top.MoveNext()){(top as IDisposable)?.Dispose();job.Frames.Pop();continue;}
    if(top.Current is IEnumerator child){job.Frames.Push(child);continue;}
    if(top.Current is UnityWebRequestAsyncOperation operation){if(operation.isDone)continue;job.Waiting=operation;}
    return;
   }
   jobs.Remove(job);
  }
  public static void Tick(){Time.unscaledTime+=.1f;UnityWebRequest.Poll();foreach(var j in jobs.ToArray())if(jobs.Contains(j))Advance(j);}
  public static void Drain(){for(int i=0;i<2000&&jobs.Count>0;i++)Tick();if(jobs.Count!=0)throw new Exception("Account coroutine stalled");}
  public static void Stop(object owner){foreach(var j in jobs.Where(j=>j.Owner==owner).ToArray()){while(j.Frames.Count>0)(j.Frames.Pop() as IDisposable)?.Dispose();jobs.Remove(j);}}
 }
}
namespace UnityEngine.Networking
{
 public class UploadHandlerRaw{public byte[] data;public UploadHandlerRaw(byte[] d){data=d;}}
 public class DownloadHandlerBuffer{public string text;}
 public class UnityWebRequestAsyncOperation{public bool isDone;internal int Frames=3;internal UnityWebRequest Request;}
 public class UnityWebRequest:IDisposable
 {
  public enum Result{Success,ConnectionError}public Result result=Result.Success;public long responseCode=200;public int timeout;
  public string url,method;public UploadHandlerRaw uploadHandler;public DownloadHandlerBuffer downloadHandler;
  public readonly Dictionary<string,string> Headers=new();static readonly List<UnityWebRequestAsyncOperation> active=new();
  public static readonly List<UnityWebRequest> Sent=new();public static int MaximumActive;public static Action<UnityWebRequest> Reply;
  public UnityWebRequest(string u,string m){url=u;method=m;}public void SetRequestHeader(string k,string v)=>Headers[k]=v;
  public UnityWebRequestAsyncOperation SendWebRequest(){Sent.Add(this);var op=new UnityWebRequestAsyncOperation{Request=this};active.Add(op);MaximumActive=Math.Max(MaximumActive,active.Count);return op;}
  public static void Poll(){foreach(var op in active.ToArray())if(--op.Frames<=0){Reply(op.Request);op.isDone=true;active.Remove(op);}}
  public void Dispose(){}
 }
}
namespace Alterverse.Profile
{
 public static class SafeIdentityCatalog{public static bool IsPlayerName(string name)=>name!=null&&name.StartsWith("SolarFox",StringComparison.Ordinal);}
 public class AccountServerBootstrap{public static AccountServerBootstrap Instance;public bool Busy;}
}
namespace Alterverse.Shop{public static class ShardPacks{public static bool IsSubscription(string id)=>id=="dimensions_shards_monthly";
        public static bool IsProduct(string id)=>Contains(id)||IsSubscription(id);
        public static bool Contains(string id)=>id=="dimensions_shards_500";}}
namespace Alterverse.Audio{public enum DimensionSound{Purchase}public static class DimensionAudio{public static void Play(DimensionSound sound,float volume){}}}
