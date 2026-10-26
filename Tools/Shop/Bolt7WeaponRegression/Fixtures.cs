using System;
using System.Collections.Generic;
using System.Linq;
namespace UnityEngine {
public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static bool operator==(Color a,Color b)=>a.r==b.r&&a.g==b.g&&a.b==b.b&&a.a==b.a;public static bool operator!=(Color a,Color b)=>!(a==b);public override bool Equals(object o)=>o is Color c&&this==c;public override int GetHashCode()=>HashCode.Combine(r,g,b,a);}
public static class Mathf{public static int Clamp(int n,int a,int b)=>Math.Clamp(n,a,b);}
public static class Debug{public static void Log(string s){}}
public class Transform{public string name;public Transform parent;}
public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero=>new();}
public struct Bounds{public Vector3 min,max;public Bounds(Vector3 p,Vector3 size){min=max=p;}public void Encapsulate(Vector3 p){min=new(Math.Min(min.x,p.x),Math.Min(min.y,p.y),Math.Min(min.z,p.z));max=new(Math.Max(max.x,p.x),Math.Max(max.y,p.y),Math.Max(max.z,p.z));}}
public struct BoneWeight{public int boneIndex0;public float weight0;}
public class Mesh{public Vector3[] vertices;public BoneWeight[] boneWeights;public int[][] Triangles;public int subMeshCount=>Triangles.Length;public Bounds bounds;public int[] GetTriangles(int i)=>(int[])Triangles[i].Clone();public void SetTriangles(int[] t,int i,bool calculate){Triangles[i]=t;}public Mesh Clone()=>new(){vertices=(Vector3[])vertices.Clone(),boneWeights=(BoneWeight[])boneWeights.Clone(),Triangles=Triangles.Select(t=>(int[])t.Clone()).ToArray(),bounds=bounds};}
}
namespace UnityEditor{public class MenuItem:Attribute{public MenuItem(string s){}}}
namespace Alterverse.Profile{public static class AccountProfilePrefs{public static Dictionary<string,int> Values=new();public static int GetInt(string key,int fallback)=>Values.GetValueOrDefault(key,fallback);public static bool HasKey(string key)=>Values.ContainsKey(key);}}
namespace Alterverse.Audio{public enum DimensionSound{Purchase}public static class DimensionAudio{public static void Play(DimensionSound s,float v){}}}
namespace Alterverse.Shop{
public enum AvatarPurchaseResult{Purchased,AlreadyOwned,InsufficientShards,Pending,InvalidItem}
public static class HeroUnlockCatalog{public static bool Released(Alterverse.Characters.HeroId hero)=>true;}
public static class PlayerWallet{public static int EarnedBalance,Requests;public static string LastSku;public static bool CanAfford(int price)=>EarnedBalance>=price;public static bool BeginServerPurchase(string sku){LastSku=sku;Requests++;return true;}}
public class PaidShardCommerce{public static PaidShardCommerce Instance;public bool Ready;}
public static class ProfileTransaction{public struct Write{public string Key;public int Value;public Write(string key,int value){Key=key;Value=value;}}public static int Commits;public static void Recover(){}public static void Commit(params Write[] writes){Commits++;foreach(var w in writes){Alterverse.Profile.AccountProfilePrefs.Values[w.Key]=w.Value;if(w.Key=="DF_Shards")PlayerWallet.EarnedBalance=w.Value;}}}
}
