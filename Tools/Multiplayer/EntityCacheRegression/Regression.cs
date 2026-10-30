using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using Alterverse.Combat;
using Alterverse.Multiplayer;
namespace UnityEngine
{
    public class Object
    {
        public static List<GameObject> All=new();
        public static T[] FindObjectsByType<T>()where T:class=>All.Select(g=>g.GetComponent<T>()).Where(x=>x!=null).ToArray();
    }
    public class Component:Object
    {
        public GameObject gameObject;public Transform transform=>gameObject.transform;
        public bool isActiveAndEnabled=true;
        public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();
    }
    public class MonoBehaviour:Component{}
    public class GameObject:Object
    {
        readonly Dictionary<Type,Component> components=new();
        public string name;public Transform transform=new();
        public static int HeroLookups;
        public GameObject(string name){this.name=name;All.Add(this);}
        public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;return c;}
        public T GetComponent<T>()where T:class
        {if(typeof(T)==typeof(Alterverse.Characters.HeroIdentity))HeroLookups++;return components.TryGetValue(typeof(T),out var value)?value as T:null;}
    }
    public class Transform {public Vector3 position;}
    public struct Vector3 {public float x,y,z;}
    public static class Mathf {public static int RoundToInt(float value)=>(int)Math.Round(value);}
    public static class Time {public static float unscaledTime;public static int frameCount;}
}
namespace Unity.Netcode {public class CustomMessagingManager{}}
namespace Alterverse.Characters {public class HeroIdentity:Component{}}
namespace Alterverse.Combat
{
    public class TeamMember:Component {public int Team;}
    public sealed partial class Health {private float aurenPassiveElapsed;}
}
namespace Alterverse.Multiplayer
{
    public class CoopMatchPlan{}
    public static class CoopAuthority {public static bool LocalHasServerAuthority=true;public static CoopMatchPlan ActivePlan=new();}
    public class WorldSnapshotHistory{}
    public class Presentation {public void Clear(){}}
    public sealed partial class CoopWorldEntityKey
    {
        string key="";bool configured,hasTarget;readonly Presentation presentation=new();
        public string Key=>key;public static uint IdentityVersion{get;private set;}
        public static int PresentationCaches;
        void CachePresentation(){PresentationCaches++;}
    }
}
static class Regression
{
    static int checks;
    static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
    static void Call(object value,string name)=>value.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(value,null);
    static Health Actor(string name,bool hero=false)
    {
        var go=new GameObject(name);var health=go.AddComponent<Health>();
        if(hero)go.AddComponent<Alterverse.Characters.HeroIdentity>();
        Call(health,"OnEnable");return health;
    }
    static void Main()
    {
        var actors=new List<Health>();
        for(int i=0;i<160;i++)actors.Add(Actor("world-"+i));
        for(int i=0;i<8;i++)Actor("hero-"+i,true);
        var world=new CoopWorldStateSync();world.Refresh();
        int before=GameObject.HeroLookups;
        for(int i=1;i<=12000;i++){Time.unscaledTime=i/20f;Time.frameCount=i;world.Refresh();}
        int scans=(GameObject.HeroLookups-before)/Health.Active.Count;
        Check(scans<=610,"Stable ten-minute world must not rescan all components for every snapshot frame");
        Check(CoopWorldEntityKey.PresentationCaches==0,"Dedicated server world discovery must not build renderer/collider presentation caches");
        Console.WriteLine("Stable ten-minute 20 Hz discovery calls: "+scans+" full component scans after warmup.");
        var old=actors[0];string oldKey=old.GetComponent<CoopWorldEntityKey>().Key;
        int count=Health.Active.Count;uint version=Health.RegistryVersion;
        old.isActiveAndEnabled=false;Call(old,"OnDisable");var replacement=Actor("replacement");
        Check(Health.Active.Count==count&&Health.RegistryVersion!=version,"Same-count replacement changes registry generation");
        world.Refresh();Check(!world.Contains(oldKey)&&world.Contains(replacement.GetComponent<CoopWorldEntityKey>().Key),"Discover replacement in the same frame");
        var identity=replacement.GetComponent<CoopWorldEntityKey>();string previous=identity.Key;
        identity.Configure("pool-next-occurrence");world.Refresh();
        Check(!world.Contains(previous)&&world.Contains("pool-next-occurrence"),"Pool re-key invalidates immediately without membership change");
        int reads=GameObject.HeroLookups;CoopAuthority.ActivePlan=new();world.Refresh();
        Check(GameObject.HeroLookups>reads,"New match plan invalidates discovery");
        var late=Actor("late-component");world.Refresh();var lateKey=late.GetComponent<CoopWorldEntityKey>().Key;
        late.gameObject.AddComponent<Alterverse.Characters.HeroIdentity>();Time.unscaledTime+=1.01f;world.Refresh();
        Check(!world.Contains(lateKey),"Bounded audit discovers components added to an already-enabled Health");
        version=Health.RegistryVersion;Call(replacement,"OnEnable");
        Check(Health.RegistryVersion==version,"Duplicate enable does not churn registry generation");
        CoopAuthority.LocalHasServerAuthority=false;var client=Actor("client");world.Refresh();
        Check(CoopWorldEntityKey.PresentationCaches==1,"Client discovery still caches presentation");
        reads=CoopWorldEntityKey.PresentationCaches;client.GetComponent<CoopWorldEntityKey>().Configure();
        Check(CoopWorldEntityKey.PresentationCaches==reads,"Stable configuration does not rebuild presentation");
        version=Health.RegistryVersion;
        typeof(Health).GetMethod("ResetRegistry",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        Check(Health.RegistryVersion!=version&&Health.Active.Count==0,"Registry reset invalidates a surviving discovery cache");
        world.Refresh();Check(!world.Contains("pool-next-occurrence"),"Registry reset removes stale lookup entries");
        Console.WriteLine("PASS: "+checks+" production membership, discovery, pooling, client/server and reset checks; engine fixtures.");
    }
}
