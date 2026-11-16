using System;
using System.Collections.Generic;
using Alterverse.Combat;
using Alterverse.Multiplayer;
using Alterverse.Profile;
using UnityEngine;

namespace UnityEngine
{
 public class GameObject
 {
  readonly Dictionary<Type,object> values=new();
  public void Put<T>(T v)=>values[typeof(T)]=v;
  public T GetComponent<T>() where T:class=>values.TryGetValue(typeof(T),out object v)?(T)v:null;
 }
 public struct Color {public static Color white=>new();}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);}
 public static class Time {public static float unscaledTime;}
}
namespace Alterverse.Combat
{
 public class Health {public static readonly List<Health> Active=new();public GameObject gameObject=new();public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();}
 public class TeamMember {public int Team;}
 public static class LocalHeroContext {public static GameObject Player;public static GameObject Find()=>Player;}
}
namespace Alterverse.Multiplayer
{
 public class CoopHeroSlot {public int Slot;public bool IsLocalHuman;}
 public static class CoopAuthority {public static bool Active=true,LocalHasServerAuthority;}
}
namespace Alterverse.Profile {public static class GameLanguage {public static string Text(string s)=>s;}}
namespace Alterverse.Audio {public enum DimensionSound {PlanetArrival}public static class DimensionAudio {public static void Play(DimensionSound s,float v){}}}
namespace Alterverse.UI
{
 public static class FrontEndTheme {public static Color Accent,Zero;}
 public sealed partial class CombatFeedbackHUD
 {
  public static CombatFeedbackHUD instance;
  public readonly Dictionary<Health,int> streakByHero=new();
  public int localTurretCharges,localMobileTurretCharges,networkLocalStreak;public bool localUpgradeEligible;
  public string activeBannerTitle,activeBannerSub;public Color activeBannerColor;public float activeBannerUntil;
  /*PRODUCTION_METHOD*/
 }
}
static class Regression
{
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static Health Hero(int slot,bool local)
 {
  var h=new Health();h.gameObject.Put(new CoopHeroSlot{Slot=slot,IsLocalHuman=local});h.gameObject.Put(new TeamMember{Team=0});Health.Active.Add(h);return h;
 }
 static void Main()
 {
  var hud=new Alterverse.UI.CombatFeedbackHUD();Alterverse.UI.CombatFeedbackHUD.instance=hud;var local=Hero(0,true);var remote=Hero(5,false);LocalHeroContext.Player=local.gameObject;
  Alterverse.UI.CombatFeedbackHUD.ApplyNetworkReward(5,0,0,false,3);
  Check(hud.streakByHero[remote]==3&&hud.localTurretCharges==0,"remote three-kill bounty uses host streak");
  Alterverse.UI.CombatFeedbackHUD.ApplyNetworkReward(5,0,0,false,0);
  Check(hud.streakByHero[remote]==0,"remote bounty clears when streak ends");
  Alterverse.UI.CombatFeedbackHUD.ApplyNetworkReward(0,1,0,true,4);
  Check(hud.streakByHero[local]==4&&hud.localTurretCharges==1&&hud.networkLocalStreak==4,"local turret UI still receives its own snapshot");
  CoopAuthority.LocalHasServerAuthority=true;Alterverse.UI.CombatFeedbackHUD.ApplyNetworkReward(5,0,0,false,9);
  Check(hud.streakByHero[remote]==0,"host ignores client replica handler");
  Console.WriteLine("PASS: local and remote bounty streaks follow authoritative snapshots.");
 }
}
