
using System;using System.Collections.Generic;using System.Linq;using UnityEngine;using Alterverse.Combat;using Alterverse.Characters;using Alterverse.Match;using Alterverse.Multiplayer;using Unity.Netcode;using Unity.Collections;
namespace UnityEngine {
 public class Object {public static T FindAnyObjectByType<T>()where T:class=>null;public static T[] FindObjectsByType<T>(FindObjectsInactive f)where T:class=>GameObject.All.Select(x=>x.GetComponent<T>()).Where(x=>x!=null).ToArray();public static void Destroy(Object o){}}
 public enum FindObjectsInactive{Exclude}
 public class Component:Object {public GameObject gameObject;public Transform transform=>gameObject.transform;public T GetComponent<T>()where T:class=>gameObject.GetComponent<T>();public T GetComponentInParent<T>()where T:class=>GetComponent<T>();}
 public class MonoBehaviour:Component{}
 public class GameObject:Object {public static List<GameObject> All=new();public string name;public Transform transform=new();Dictionary<Type,object> parts=new();public GameObject(string n="unit"){name=n;All.Add(this);}public T GetComponent<T>()where T:class=>parts.TryGetValue(typeof(T),out var c)?c as T:null;public T GetComponentInParent<T>()where T:class=>GetComponent<T>();public T AddComponent<T>()where T:Component,new(){var c=new T{gameObject=this};parts[typeof(T)]=c;return c;}}
 public class Transform {public Vector3 position;}
 public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 up=>new(0,1,0);public float sqrMagnitude=>x*x+y*y+z*z;public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static float Min(float a,float b)=>Math.Min(a,b);}
 public static class Time {public static float unscaledTime;}
 public static class Debug {public static void Log(object o){}}
}
namespace Unity.Netcode {public struct FastBufferReader {object[] data;int i;public FastBufferReader(object[] d){data=d;i=0;}public bool TryBeginRead(int n)=>i<data.Length;public void ReadValueSafe<T>(out T x){x=(T)data[i++];}}}
namespace Unity.Collections {public struct FixedString128Bytes {readonly string value;public FixedString128Bytes(string text){value=text;}public override string ToString()=>value;}}
namespace Alterverse.Audio {public enum DimensionSound{Heal}public static class DimensionAudio {public static void PlayAt(DimensionSound s,Vector3 v,float f){}}}
namespace Alterverse.Characters {public enum HeroId{A,B}public class HeroIdentity:Component {public HeroId Hero;}}
namespace Alterverse.Combat {
 public partial class Health:Component {public static List<Health> Active=new();public bool IsDead,isActiveAndEnabled=true;public float Current=50;float maxHealth=100;public float Max=>maxHealth;public event Action<float,float> Changed;public void TakeDamage(float amount,GameObject source){Current-=amount;}}
 public class TeamMember:Component {public int Team;}
 public class LaneMinion:Component {public int Team;}
 public class TrenchSoldier:Component{}
 public class LaneTower:Component {public int Team;}
 public class RealityCore:Component {public int Team;}
 public class DimensionPlanet:Component{}
}
namespace Alterverse.Multiplayer {public class CoopHeroSlot:Component {public int Slot;}public static class MatchSession{public const int MaxPlayers=8;}public static class CoopAuthority {public static bool CanMutateAuthoritativeState=true,Active=true,LocalHasServerAuthority;}public static class MatchNetworkProtocol {public static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);}}
namespace Alterverse.Match {public class DimensionsFront:MonoBehaviour {public float ZeroControl,LumenControl;public event Action<int> MatchEnded;}}
namespace Alterverse.Visuals {public static class MonsterAttackVfx {public static void Resolve(GameObject g,Vector3 a,Vector3 b){}}}

namespace Alterverse.Multiplayer {
 public class CoopWorldEntityKey:Component {public string Key;}
 public class CoopMatchResultSync {public struct SlotResult {public int Kills,Deaths,Assists;public float DamageDealt,DamageTaken,Healing,FrontContribution;}public static Dictionary<int,SlotResult> Results=new();public static bool TryGetSlotResult(int slot,out SlotResult result)=>Results.TryGetValue(slot,out result);}
}
