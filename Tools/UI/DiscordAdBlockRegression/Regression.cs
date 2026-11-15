using System;
using System.Collections.Generic;
using Alterverse.Profile;
using UnityEngine;

namespace UnityEngine
{
 public static class PlayerPrefs
 {
  static readonly Dictionary<string,object> Data=new();
  public static int GetInt(string k,int d=0)=>Data.TryGetValue(k,out object o)&&o is int v?v:d;
  public static string GetString(string k,string d="")=>Data.TryGetValue(k,out object o)&&o is string v?v:d;
  public static void SetInt(string k,int v)=>Data[k]=v;
  public static void SetString(string k,string v)=>Data[k]=v;
  public static void DeleteKey(string k)=>Data.Remove(k);
  public static void Save(){}
  public static void Clear()=>Data.Clear();
 }
}
namespace Alterverse.Profile
{
 public static class AccountProfilePrefs
 {
  static readonly Dictionary<string,int> Cloud=new();
  public static string Account="";public static bool Attached=>Account.Length>0;
  public static int GetInt(string k,int d=0)=>Attached?(Cloud.TryGetValue(Account+k,out int v)?v:d):PlayerPrefs.GetInt(k,d);
  public static void SetInt(string k,int v){if(Attached)Cloud[Account+k]=v;else PlayerPrefs.SetInt(k,v);}
  public static void Save(){}
 }
 public static class PlayerWallet
 {
  public static int EarnedBalance=1000, Balance=1000;
  public static bool CanAfford(int v)=>Balance>=v;
  public static bool TrySpend(int v){if(!CanAfford(v))return false;Balance-=v;return true;}
 }
 public static class ProfileTransaction
 {
  public class Write {public string Key;public int Value;public Write(string k,int v){Key=k;Value=v;}}
  public static Action RecoveryAction; public static void Recover(){var action=RecoveryAction;RecoveryAction=null;action?.Invoke();}
  public static void Commit(params Write[] writes){foreach(var w in writes){if(w.Key=="DF_Shards")PlayerWallet.EarnedBalance=PlayerWallet.Balance=w.Value;else AccountProfilePrefs.SetInt(w.Key,w.Value);}}
 }
}
namespace Alterverse.Audio {public enum DimensionSound {Purchase} public static class DimensionAudio {public static void Play(DimensionSound d,float v){}}}
static class Regression
{
 static void Check(bool b,string message){if(!b)throw new Exception(message);}
 public static void Main()
 {
  PlayerPrefs.Clear();AccountProfilePrefs.Account="";PlayerWallet.EarnedBalance=PlayerWallet.Balance=1000;
  Check(!Alterverse.UI.MainMenuController.IsHomeBannerBlocked,"no purchase initially");
  Alterverse.UI.MainMenuController.BuyTest();
  Check(Alterverse.UI.MainMenuController.IsHomeBannerBlocked&&PlayerWallet.Balance==300,"offline purchase sets blocking state and spends once");
  for(int i=0;i<5;i++)Alterverse.UI.MainMenuController.SyncTest();
  Check(Alterverse.UI.MainMenuController.IsHomeBannerBlocked&&PlayerPrefs.GetInt("DF_HomeBannerBlocked_v1")==1,"offline state remains integer over repeated frames and restart");
  Alterverse.UI.MainMenuController.BuyTest();Check(PlayerWallet.Balance==300,"blocked offer cannot charge twice");
  PlayerPrefs.Clear();PlayerWallet.EarnedBalance=PlayerWallet.Balance=1000;
  ProfileTransaction.RecoveryAction=()=>{PlayerWallet.EarnedBalance=PlayerWallet.Balance=300;AccountProfilePrefs.SetInt("DF_HomeBannerBlocked_v1",1);};
  Alterverse.UI.MainMenuController.BuyTest();
  Check(PlayerWallet.Balance==300&&Alterverse.UI.MainMenuController.IsHomeBannerBlocked,"recovered purchase cannot charge twice");
  PlayerPrefs.Clear();PlayerPrefs.SetString("DF_HomeBannerBlocked_v1","1");Alterverse.UI.MainMenuController.SyncTest();
  Check(Alterverse.UI.MainMenuController.IsHomeBannerBlocked&&PlayerPrefs.GetInt("DF_HomeBannerBlocked_v1")==1,"migrate old offline string without losing purchase");
  PlayerPrefs.Clear();PlayerPrefs.SetString("DF_HomeBannerBlocked_v1","1");AccountProfilePrefs.Account="alice";Alterverse.UI.MainMenuController.SyncTest();
  Check(Alterverse.UI.MainMenuController.IsHomeBannerBlocked&&PlayerPrefs.GetString("DF_HomeBannerBlocked_v1","")=="","migrate old string to the signed-in account");
  AccountProfilePrefs.Account="bob";Check(!Alterverse.UI.MainMenuController.IsHomeBannerBlocked,"another account does not inherit migrated flag");
  AccountProfilePrefs.Account="alice";Check(Alterverse.UI.MainMenuController.IsHomeBannerBlocked,"account retains activated blocker");
  Console.WriteLine("PASS: ad blocker purchase, UI gate, repeat frames, legacy migration and account isolation.");
 }
}
