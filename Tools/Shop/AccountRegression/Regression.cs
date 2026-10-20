using System;using System.Collections.Generic;using System.Linq;using System.Text;using System.Text.Json;using UnityEngine;using UnityEngine.Networking;using Alterverse.Shop;using Alterverse.Profile;
static class AccountRegression
{
 sealed class Account{public int Paid;public long Revision,ProfileRevision;public AccountProfilePrefs.Entry[] Profile;public HashSet<string> Purchases=new();public HashSet<string> Tokens=new();}
 static readonly Dictionary<string,Account> accounts=new();static bool loseSpendResponse;static int checks;
 static AccountProfilePrefs.Entry Number(string key,int n)=>new(){key=key,kind=0,number=n};
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 static Account AccountFor(string id)
 {if(!accounts.TryGetValue(id,out var account))accounts[id]=account=new(){Paid=id=="a"?1200:0,Profile=new[]{new AccountProfilePrefs.Entry{key="DF_PlayerName",kind=1,text="SolarFox0000001"},Number("DF_Shards",id=="a"?100:0)}};return account;}
 static void Reply(UnityWebRequest request)
 {
  string id=request.Headers["Authorization"].Substring("Bearer ".Length);Account a=AccountFor(id);string route=new Uri(request.url).AbsolutePath;
  using var doc=JsonDocument.Parse(Encoding.UTF8.GetString(request.uploadHandler.data));var body=doc.RootElement;
  if(route=="/profile/save")
  {
   long revision=body.GetProperty("expectedProfileRevision").GetInt64();if(revision!=a.ProfileRevision){request.result=UnityWebRequest.Result.ConnectionError;request.responseCode=409;return;}
   a.Profile=JsonUtility.FromJson<AccountProfilePrefs.Entry[]>(body.GetProperty("profile").GetRawText());a.ProfileRevision++;
  }
  else if(route=="/google-play/fulfill")
  {if(a.Tokens.Add(body.GetProperty("purchaseToken").GetString())){a.Paid+=500;a.Revision++;}}
  string operation=null;
  if(route=="/wallet/spend")
  {
   operation=body.GetProperty("operationId").GetString();
   if(a.Purchases.Add(operation))
   {
    var earned=a.Profile.First(e=>e.key=="DF_Shards");int debit=Math.Min(earned.number,100);earned.number-=debit;a.Paid-=100-debit;a.Revision++;a.ProfileRevision++;
   }
   if(loseSpendResponse){loseSpendResponse=false;request.result=UnityWebRequest.Result.ConnectionError;request.responseCode=503;return;}
  }
  request.downloadHandler.text=JsonUtility.ToJson(new{fulfilled=true,wallet=new PaidShardCommerce.Snapshot{accountHash=id,balance=a.Paid,revision=a.Revision,profileRevision=a.ProfileRevision,profile=a.Profile,owned=Array.Empty<string>(),avatars=Array.Empty<int>(),billingReady=true,operationId=operation}});
 }
 static void Update(PaidShardCommerce c)=>typeof(PaidShardCommerce).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(c,null);
 static void Main()
 {
  UnityWebRequest.Reply=Reply;var commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"a","a");Scheduler.Drain();
  Check(commerce.Ready&&PlayerWallet.Balance==1300,"Cloud cache and paid wallet restore the same account's combined balance");
  AccountProfilePrefs.SetInt("DF_Shards",150);
  var first=commerce.Fulfill("dimensions_shards_500","token-one");var second=commerce.Fulfill("dimensions_shards_500","token-two");
  var flush=commerce.FlushProfileAsync();commerce.Refresh();
  Check(PaidShardCommerce.Spending&&!commerce.BeginPurchase("shield")&&!commerce.TryBeginSocialSpend(),"Verification blocks overlapping shop/social debits");
  Check(UnityWebRequest.Sent.Count(r=>r.url.EndsWith("/google-play/fulfill"))==1,"A second pending purchase waits for the first verification");
  Scheduler.Drain();
  Check(first.Result&&second.Result&&flush.Result&&UnityWebRequest.MaximumActive==1,"Verification, profile saves and refreshes never overlap");
  Check(PlayerWallet.Balance==2350&&AccountProfilePrefs.GetInt("DF_Shards")==150,"Paid grants preserve unsaved earned shards");
  var replay=commerce.Fulfill("dimensions_shards_500","token-one");Scheduler.Drain();Check(replay.Result&&PlayerWallet.Balance==2350,"Replayed Play callbacks do not credit twice");
  AccountProfilePrefs.SetInt("DF_Shards",175);var flight=commerce.FlushProfileAsync();AccountProfilePrefs.SetInt("DF_Shards",200);Scheduler.Drain();
  Check(flight.Result&&AccountProfilePrefs.GetInt("DF_Shards")==200&&AccountProfilePrefs.Dirty,"Earnings during an in-flight save survive its older response");
  commerce.FlushProfileAsync();Scheduler.Drain();
  loseSpendResponse=true;Check(commerce.BeginPurchase("shield"),"A shard purchase persists a recovery operation before sending");Scheduler.Drain();
  Check(PlayerPrefs.HasKey("DF_CommercePurchase_a")&&AccountFor("a").Purchases.Count==1,"A lost purchase response keeps the same durable operation for retry");
  PaidShardCommerce.Disconnect();commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"a","a");Scheduler.Drain();Time.unscaledTime+=6;Update(commerce);Scheduler.Drain();
  Check(!PlayerPrefs.HasKey("DF_CommercePurchase_a")&&AccountFor("a").Purchases.Count==1&&PlayerWallet.Balance==2300,"Restart recovery replays the same ID with one debit");
  var cancelled=commerce.Fulfill("dimensions_shards_500","late-token");PaidShardCommerce.Disconnect();
  commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"b","b");Scheduler.Drain();
  Check(cancelled.IsCompleted&&!cancelled.Result&&AccountProfilePrefs.Account=="b"&&PlayerWallet.Balance==0,"Old-account responses cannot populate a different account's wallet/cache");
  AccountProfilePrefs.SetInt("DF_Shards",23);AccountProfilePrefs.Save();PaidShardCommerce.Disconnect();
  commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"b","b");Scheduler.Drain();
  Check(AccountProfilePrefs.GetInt("DF_Shards")==23&&AccountProfilePrefs.Dirty,"Unsaved account cache survives reconnect at the same server revision");
  // Crash-recovery journals must remain bound to the account that created them.
  AccountProfilePrefs.SetString("DF_ProfileTransaction_v1","{\"version\":1,\"writes\":[{\"key\":\"DF_Shards\",\"number\":31,\"isText\":false}]}");AccountProfilePrefs.Save();
  PaidShardCommerce.Disconnect();commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"a","a");Scheduler.Drain();
  Check(AccountProfilePrefs.GetInt("DF_Shards")!=31,"A pending journal cannot cross accounts");
  PaidShardCommerce.Disconnect();commerce=PaidShardCommerce.Connect("https://account.invalid",()=>"b","b");Scheduler.Drain();
  Check(AccountProfilePrefs.GetInt("DF_Shards")==31&&!AccountProfilePrefs.HasKey("DF_ProfileTransaction_v1"),"Returning to the original account replays its journal once");
  ProfileTransaction.Recover();Check(AccountProfilePrefs.GetInt("DF_Shards")==31,"Journal replay is idempotent");
  Console.WriteLine("PASS: "+checks+" delayed account/wallet/purchase/save/reconnect/journal scenarios using production client classes; HTTP and disk are fixtures.");
 }
}
