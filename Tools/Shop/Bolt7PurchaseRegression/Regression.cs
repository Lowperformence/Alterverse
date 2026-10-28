using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityEngine.Networking;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Shop;
class Regression {
 static int checks,spendCalls;
 static bool loseResponse=true;
 static readonly Dictionary<string,Account> accounts=new();
 class Account{public int Earned,Paid;public long Revision=1;public bool Owned;public HashSet<string> Operations=new();}
 static void Check(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static Account AccountFor(string id){if(!accounts.TryGetValue(id,out var a))accounts[id]=a=new Account{Earned=id=="a"?700:0,Paid=id=="a"?1400:0};return a;}
 static AccountProfilePrefs.Entry Number(string key,int n)=>new(){key=key,kind=0,number=n};
 static AccountProfilePrefs.Entry[] Profile(Account a){var entries=new List<AccountProfilePrefs.Entry>{new(){key="DF_PlayerName",kind=1,text="SolarFox0000001"},Number("DF_Shards",a.Earned)};if(a.Owned)entries.Add(Number("DF_HeroOwned_Bolt7",1));return entries.ToArray();}
 static void Reply(UnityWebRequest r){
  string accountId=r.Headers["Authorization"].Replace("Bearer ","");var a=AccountFor(accountId);string operation=null;
  using var json=JsonDocument.Parse(Encoding.UTF8.GetString(r.uploadHandler.data));var data=json.RootElement;
  if(r.url.EndsWith("/profile/save")){
   foreach(var e in data.GetProperty("profile").EnumerateArray())if(e.GetProperty("key").GetString()=="DF_Shards")a.Earned=e.GetProperty("number").GetInt32();a.Revision++;
  }
  if(r.url.EndsWith("/wallet/spend")){
   spendCalls++;Check(data.GetProperty("cosmeticId").GetString()=="hero_bolt7","client submits exact catalog SKU");operation=data.GetProperty("operationId").GetString();
   if(a.Operations.Add(operation)&&!a.Owned){int contribution=Math.Min(a.Earned,1800);a.Earned-=contribution;a.Paid-=1800-contribution;a.Owned=true;a.Revision++;}
   if(loseResponse){loseResponse=false;r.responseCode=503;r.result=UnityWebRequest.Result.ConnectionError;return;}
  }
  r.downloadHandler.text=JsonUtility.ToJson(new{fulfilled=true,wallet=new PaidShardCommerce.Snapshot{accountHash=accountId,balance=a.Paid,revision=a.Revision,profileRevision=a.Revision,profile=Profile(a),owned=a.Owned?new[]{"hero_bolt7"}:Array.Empty<string>(),avatars=Array.Empty<int>(),operationId=operation,billingReady=true}});
 }
 static void Main(string[] args){
  Check(HeroUnlockCatalog.Price(HeroId.Bolt7)==1800&&HeroUnlockCatalog.Purchasable(HeroId.Bolt7)&&HeroUnlockCatalog.Released(HeroId.Bolt7),"BOLT released at 1800 shards");
  using(var json=JsonDocument.Parse(System.IO.File.ReadAllText(args[0]))){var item=json.RootElement.GetProperty("cosmetics").EnumerateArray().Single(i=>i.GetProperty("id").GetString()==HeroUnlockCatalog.PurchaseSku(HeroId.Bolt7));Check(item.GetProperty("price").GetInt32()==HeroUnlockCatalog.Price(HeroId.Bolt7),"client and server prices match");Check(item.GetProperty("writes")[0].GetProperty("key").GetString()==HeroUnlockCatalog.OwnedKey(HeroId.Bolt7),"server grant key matches client inventory");}
  Check(!HeroUnlockInventory.Owned(HeroId.Bolt7)&&!PlayerHeroSelection.Select(HeroId.Bolt7),"locked hero cannot deploy");
  AccountProfilePrefs.SetInt("DF_Shards",1799);Check(HeroUnlockInventory.Purchase(HeroId.Bolt7)==HeroUnlockResult.InsufficientShards,"1799 shards rejects purchase");Check(PlayerWallet.Balance==1799&&!HeroUnlockInventory.Owned(HeroId.Bolt7),"rejection changes no balance or ownership");
  AccountProfilePrefs.SetInt("DF_Shards",1800);Check(HeroUnlockInventory.Purchase(HeroId.Bolt7)==HeroUnlockResult.Purchased,"exact earned-shard price purchases offline");Check(PlayerWallet.Balance==0&&HeroUnlockInventory.Owned(HeroId.Bolt7),"offline atomic debit and unlock");Check(HeroUnlockInventory.Purchase(HeroId.Bolt7)==HeroUnlockResult.AlreadyOwned&&PlayerWallet.Balance==0,"already owned hero never debits twice");Check(PlayerHeroSelection.Select(HeroId.Bolt7)&&PlayerHeroSelection.Selected==HeroId.Bolt7,"purchased hero deploys");
  UnityWebRequest.Reply=Reply;var commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"a","a");Scheduler.Drain();Check(commerce.Ready&&PlayerWallet.Balance==2100&&!HeroUnlockInventory.Owned(HeroId.Bolt7),"connected account uses its own earned and paid wallet");
  Check(PlayerHeroSelection.Selected==HeroId.Veyr,"account switch drops an unowned selected premium hero");
  Check(HeroUnlockInventory.Purchase(HeroId.Bolt7)==HeroUnlockResult.Pending,"online purchase follows existing durable server path");Scheduler.Drain();Check(AccountFor("a").Owned&&PlayerPrefs.HasKey("DF_CommercePurchase_a"),"lost response leaves durable recovery id after server purchase");
  Check(AccountFor("a").Earned==0&&AccountFor("a").Paid==300,"mixed wallet spends exactly 1800");
  PaidShardCommerce.Disconnect();commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"a","a");Scheduler.Drain();Time.unscaledTime+=6;typeof(PaidShardCommerce).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(commerce,null);Scheduler.Drain();
  Check(HeroUnlockInventory.Owned(HeroId.Bolt7)&&PlayerWallet.Balance==300,"ownership and remaining shards restore after restart");Check(AccountFor("a").Operations.Count==1&&!PlayerPrefs.HasKey("DF_CommercePurchase_a"),"recovery clears the same operation without another debit");
  int spent=spendCalls;Check(HeroUnlockInventory.Purchase(HeroId.Bolt7)==HeroUnlockResult.AlreadyOwned&&spendCalls==spent,"owned UI action creates no new charge");Check(PlayerHeroSelection.Select(HeroId.Bolt7),"restored purchase can deploy");
  PaidShardCommerce.Disconnect();commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"b","b");Scheduler.Drain();Check(!HeroUnlockInventory.Owned(HeroId.Bolt7)&&PlayerWallet.Balance==0&&PlayerHeroSelection.Selected==HeroId.Veyr,"different account inherits neither purchase nor premium selection");Check(BotHeroPool.Shuffled("unowned-bot",1).Contains(HeroId.Bolt7),"bots still draw BOLT independently of player ownership");
  PaidShardCommerce.Disconnect();commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"a","a");Scheduler.Drain();Check(HeroUnlockInventory.Owned(HeroId.Bolt7)&&PlayerWallet.Balance==300,"returning account keeps permanent purchase");
  Console.WriteLine("PASS "+checks+" BOLT-7 client catalog/unlock/debit/selection/mixed-wallet/retry/restart/account-isolation checks; HTTP/storage are fixtures.");
 }
}
