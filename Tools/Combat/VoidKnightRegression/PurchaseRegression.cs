using System;
using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Shop;
class PurchaseRegression
{
 static int passed;
 static void Check(bool value,string name){if(!value)throw new Exception("FAIL: "+name);passed++;Console.WriteLine("PASS: "+name);}
 static void Reset(int earned,int paid=0,bool server=false)
 {
  AccountProfilePrefs.Default=0;AccountProfilePrefs.Values.Clear();
  ProfileTransaction.Commits=ProfileTransaction.Recoveries=0;
  PlayerWallet.EarnedBalance=earned;PlayerWallet.PaidBalance=paid;PlayerWallet.ServerRequests=0;PlayerWallet.LastSku=null;PlayerWallet.ServerAccepted=true;
  PaidShardCommerce.Instance.Ready=server;
 }
 static void Main()
 {
  const HeroId hero=HeroId.VoidKnight;
  Check(HeroUnlockCatalog.Released(hero),"production Void Knight release enabled");
  Reset(1599);
  Check(HeroUnlockInventory.Purchase(hero)==HeroUnlockResult.InsufficientShards,"1599 shards cannot buy Knight");
  Check(ProfileTransaction.Commits==0&&PlayerWallet.EarnedBalance==1599&&!HeroUnlockInventory.Owned(hero),"failed purchase preserves wallet and ownership");
  Reset(2000);
  Check(!HeroUnlockInventory.Owned(hero)&&!PlayerHeroSelection.Select(hero),"released but unowned Knight cannot deploy");
  Check(HeroUnlockInventory.Purchase(hero)==HeroUnlockResult.Purchased,"earned shard purchase succeeds");
  Check(PlayerWallet.EarnedBalance==400&&ProfileTransaction.Commits==1,"local transaction debits exactly 1600 shards");
  Check(AccountProfilePrefs.GetInt("DF_HeroOwned_VoidKnight",0)==1&&HeroUnlockInventory.Owned(hero),"purchase persists Knight ownership");
  Check(HeroUnlockInventory.Purchase(hero)==HeroUnlockResult.AlreadyOwned&&PlayerWallet.EarnedBalance==400&&ProfileTransaction.Commits==1,"duplicate purchase cannot debit twice");
  Check(PlayerHeroSelection.Select(hero),"owned released Knight can deploy");
  Reset(1600);
  Check(HeroUnlockInventory.Purchase(hero)==HeroUnlockResult.Purchased&&PlayerWallet.EarnedBalance==0,"exact price buys Knight");
  Reset(100,1500,true);
  Check(HeroUnlockInventory.Purchase(hero)==HeroUnlockResult.Pending&&PlayerWallet.LastSku=="hero_void_knight"&&PlayerWallet.ServerRequests==1,"paid shard purchase uses correct server SKU");
  Check(ProfileTransaction.Commits==0&&PlayerWallet.EarnedBalance==100&&!HeroUnlockInventory.Owned(hero),"pending server purchase grants nothing locally");
  PlayerWallet.ServerAccepted=false;
  Check(HeroUnlockInventory.Purchase(hero)==HeroUnlockResult.NotPurchasable&&ProfileTransaction.Commits==0,"rejected server request does not debit");
  Reset(100,1500);
  Check(HeroUnlockInventory.Purchase(hero)==HeroUnlockResult.NotPurchasable&&PlayerWallet.EarnedBalance==100,"offline fallback cannot spend paid shards");
  Console.WriteLine("VOID KNIGHT RELEASE: "+passed+" purchase checks passed. Production release catalog.");
 }
}

