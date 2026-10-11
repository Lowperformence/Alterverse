#!/usr/bin/env python3
"""Exercise Timmy and WASP release gates with isolated profile/wallet fixtures.
Not a Unity UI or live billing test. Requires Mono and Roslyn paths.
"""
import argparse, os, subprocess, tempfile
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--mono-root',type=Path,required=True)
parser.add_argument('--roslyn',type=Path,required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[3]
fixture=r'''
using System;
using System.Collections.Generic;
namespace UnityEngine {public struct Color {public Color(float r,float g,float b,float a=1){}}}
namespace Alterverse.Match {public enum ArenaVariant {Moon,Mars,GreenPlanet,Noxara,Kryon,Earth,Pyraxis,Neptune,Dark00,DiamondCore,GJ504b,Wasp76b,Trappist1e}}
namespace Alterverse.Characters {public enum HeroId {Veyr,Kaelon,Nyra,Elyra,Mira,Rook,Traveler,Nerok,Timmy}}
namespace Alterverse.Profile {
 public static class AccountProfilePrefs {
  public static Dictionary<string,int> Values=new Dictionary<string,int>();
  public static int GetInt(string key,int fallback)=>key!=null&&Values.TryGetValue(key,out int v)?v:fallback;
 }
 public static class PlayerWallet {
  public static int EarnedBalance=5000,ServerPurchases;
  public static bool CanAfford(int price)=>EarnedBalance>=price;
  public static bool BeginServerPurchase(string sku){ServerPurchases++;return true;}
 }
 public static class ProfileTransaction {
  public static int Commits,Recoveries;
  public struct Write {public string Key;public int Value;public Write(string key,int value){Key=key;Value=value;}}
  public static void Recover(){Recoveries++;}
  public static void Commit(params Write[] writes){Commits++;foreach(var w in writes){AccountProfilePrefs.Values[w.Key]=w.Value;if(w.Key=="DF_Shards")PlayerWallet.EarnedBalance=w.Value;}}
 }
}
namespace Alterverse.Shop {public class PaidShardCommerce {public static PaidShardCommerce Instance;public bool Ready=true;}}
namespace Alterverse.Audio {public enum DimensionSound{Purchase}public static class DimensionAudio {public static void Play(DimensionSound sound,float volume){}}}
class Regression {
 static int checks;
 static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
 static void Main(){
  var hero=Alterverse.Characters.HeroId.Timmy;
  Check(Alterverse.Shop.HeroUnlockCatalog.Price(hero)==1900,"Timmy price");
  Check(Alterverse.Shop.HeroUnlockCatalog.Purchasable(hero),"Timmy listed as premium");
  Check(Alterverse.Shop.HeroUnlockCatalog.PurchaseSku(hero)=="hero_timmy","Timmy SKU");
  Check(!Alterverse.Shop.HeroUnlockCatalog.Released(hero),"Timmy release held");
  foreach(bool online in new[]{false,true})foreach(int balance in new[]{0,1899,1900,5000})foreach(int owned in new[]{0,1}){
   Alterverse.Shop.PaidShardCommerce.Instance=online?new Alterverse.Shop.PaidShardCommerce():null;
   Alterverse.Profile.PlayerWallet.EarnedBalance=balance;
   Alterverse.Profile.AccountProfilePrefs.Values[Alterverse.Shop.HeroUnlockCatalog.TimmyOwnedKey]=owned;
   Check(!Alterverse.Shop.HeroUnlockInventory.Owned(hero),"old ownership cannot bypass release");
   Check(Alterverse.Shop.HeroUnlockInventory.Purchase(hero)==Alterverse.Shop.HeroUnlockResult.NotPurchasable,"purchase denied before release");
   Check(!Alterverse.Characters.PlayerHeroSelection.Select(hero),"deploy selection denied");
   Check(Alterverse.Characters.PlayerHeroSelection.Selected!=hero,"active selection excludes Timmy");
   Check(Alterverse.Profile.PlayerWallet.EarnedBalance==balance,"shards unchanged");
   Check(Alterverse.Profile.AccountProfilePrefs.Values[Alterverse.Shop.HeroUnlockCatalog.TimmyOwnedKey]==owned,"entitlement unchanged");
  }
  Check(Alterverse.Profile.ProfileTransaction.Commits==0&&Alterverse.Profile.ProfileTransaction.Recoveries==0,"locked purchase has no transaction side effects");
  Check(Alterverse.Profile.PlayerWallet.ServerPurchases==0,"locked purchase never invokes server debit");
  var wasp=Alterverse.Match.ArenaVariant.Wasp76b;
  Check(Alterverse.Shop.ArenaShopCatalog.For(wasp).Price==900,"WASP price remains 900");
  Check(Array.Exists(Alterverse.Shop.ArenaShopCatalog.PurchasableItems,item=>item.Variant==wasp),"WASP stays in shop");
  Check(!Alterverse.Shop.ArenaShopCatalog.PurchaseAvailable(wasp),"WASP purchase held");
  Check(Alterverse.Shop.ArenaUnlockInventory.UnlockRequirement(wasp)=="COMING SOON","WASP release status");
  foreach(bool online in new[]{false,true})foreach(int balance in new[]{0,899,900,5000})foreach(int owned in new[]{0,1}){
   Alterverse.Shop.PaidShardCommerce.Instance=online?new Alterverse.Shop.PaidShardCommerce():null;
   Alterverse.Profile.PlayerWallet.EarnedBalance=balance;
   Alterverse.Profile.AccountProfilePrefs.Values["DF_ArenaOwned_Wasp76b"]=owned;
   Check(Alterverse.Shop.ArenaUnlockInventory.Purchase(wasp)==Alterverse.Shop.ArenaPurchaseResult.NotPurchasable,"WASP purchase denied before release");
   Check(Alterverse.Profile.PlayerWallet.EarnedBalance==balance,"WASP hold preserves shards");
   Check(Alterverse.Shop.ArenaUnlockInventory.Owned(wasp)==(owned==1),"WASP hold preserves existing ownership");
  }
  Check(Alterverse.Profile.ProfileTransaction.Commits==0&&Alterverse.Profile.PlayerWallet.ServerPurchases==0,"WASP hold causes no debit or unlock");
  Alterverse.Shop.PaidShardCommerce.Instance=null;
  foreach(var arena in new[]{Alterverse.Match.ArenaVariant.GreenPlanet,Alterverse.Match.ArenaVariant.Noxara,Alterverse.Match.ArenaVariant.Earth,Alterverse.Match.ArenaVariant.Pyraxis,Alterverse.Match.ArenaVariant.Dark00,Alterverse.Match.ArenaVariant.DiamondCore,Alterverse.Match.ArenaVariant.Trappist1e}){
   Alterverse.Profile.PlayerWallet.EarnedBalance=900;
   Check(Alterverse.Shop.ArenaShopCatalog.PurchaseAvailable(arena),"existing arena purchases remain available");
   Check(Alterverse.Shop.ArenaUnlockInventory.Purchase(arena)==Alterverse.Shop.ArenaPurchaseResult.Purchased,"other paid arena purchases still work");
   Check(Alterverse.Profile.PlayerWallet.EarnedBalance==0&&Alterverse.Shop.ArenaUnlockInventory.Owned(arena),"other arena costs 900 and unlocks");
   Check(Alterverse.Shop.ArenaUnlockInventory.Purchase(arena)==Alterverse.Shop.ArenaPurchaseResult.AlreadyOwned,"arena duplicate never charges again");
  }
  foreach(var free in new[]{Alterverse.Characters.HeroId.Veyr,Alterverse.Characters.HeroId.Kaelon,Alterverse.Characters.HeroId.Nyra,Alterverse.Characters.HeroId.Elyra,Alterverse.Characters.HeroId.Mira,Alterverse.Characters.HeroId.Rook}){
   Check(Alterverse.Shop.HeroUnlockInventory.Owned(free),"free hero remains available");
   Check(Alterverse.Characters.PlayerHeroSelection.Select(free),"free hero deploy allowed");
  }
  Alterverse.Shop.PaidShardCommerce.Instance=null;
  foreach(var premium in new[]{Alterverse.Characters.HeroId.Traveler,Alterverse.Characters.HeroId.Nerok}){
   Alterverse.Profile.PlayerWallet.EarnedBalance=5000;
   Check(!Alterverse.Shop.HeroUnlockInventory.Owned(premium),"premium initially locked");
   int price=Alterverse.Shop.HeroUnlockCatalog.Price(premium);
   Check(Alterverse.Shop.HeroUnlockInventory.Purchase(premium)==Alterverse.Shop.HeroUnlockResult.Purchased,"existing premium still purchasable");
   Check(Alterverse.Profile.PlayerWallet.EarnedBalance==5000-price,"existing premium price unchanged");
   Check(Alterverse.Characters.PlayerHeroSelection.Select(premium),"owned premium deploy allowed");
  }
  Console.WriteLine("PASS "+checks+" production Timmy/WASP release, purchase, selection and unchanged arena assertions.");
 }
}
'''
env=dict(os.environ,MONO_PATH=str(args.mono_root/'usr/lib/mono/4.5')+':'+str(args.mono_root/'usr/lib/mono/4.5/Facades'),MONO_CFG_DIR=str(args.mono_root/'etc'),LD_LIBRARY_PATH=str(args.mono_root/'usr/lib')+':'+str(args.mono_root/'usr/lib/x86_64-linux-gnu'))
mono=args.mono_root/'usr/bin/mono-sgen'
with tempfile.TemporaryDirectory(prefix='df-timmy-release-') as folder:
 folder=Path(folder);(folder/'Fixture.cs').write_text(fixture);binary=folder/'Release.exe'
 subprocess.run([str(mono),str(args.roslyn),'-nologo','-noconfig','-langversion:9','-r:'+str(args.mono_root/'usr/lib/mono/4.5/System.dll'),'-r:'+str(args.mono_root/'usr/lib/mono/4.5/System.Core.dll'),'-out:'+str(binary),str(folder/'Fixture.cs'),str(root/'Assets/Alterverse/Scripts/Shop/HeroUnlockCatalog.cs'),str(root/'Assets/Alterverse/Scripts/Characters/PlayerHeroSelection.cs'),str(root/'Assets/Alterverse/Scripts/Shop/ArenaShopCatalog.cs')],env=env,check=True)
 subprocess.run([str(mono),str(binary)],env=env,check=True)
