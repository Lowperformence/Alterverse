using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Alterverse.Profile;
using Alterverse.Shop;
using UnityEngine;
using UnityEngine.Networking;
namespace Alterverse.Characters {public enum HeroId{Veyr}public static class HeroIdentity{public const int HeroCount=11;}}
namespace Alterverse.Audio {public static class UiSoundRouter{public static void Confirm(){}}public enum DimensionSound{Purchase}public static class DimensionAudio{public static int Plays;public static void Play(DimensionSound s,float v){Plays++;}}}
namespace Alterverse.Profile {
 public static class AccountProfilePrefs {
  public class Entry{}public static bool Attached=true,Dirty;public static long Revision=7;public static string Account="owner";
  public static Entry[] Capture()=>Array.Empty<Entry>();
  public static string GetString(string k,string f)=>PlayerPrefs.GetString(Account+k,f);
  public static void SetString(string k,string v)=>PlayerPrefs.SetString(Account+k,v);public static void Save(){}
 }
 public static class PlayerProfile{public static string PlayerName="SolarFox0000001";}
 public static class PlayerWallet{public static int Balance=1000;public static int LocalSpends,LocalGrants;public static bool TrySpend(int n){LocalSpends++;return true;}public static void Grant(int n){LocalGrants++;}}
}
namespace Alterverse.Shop {
 public class PaidShardCommerce {
  public class Snapshot {public string accountHash;public AccountProfilePrefs.Entry[] profile;}
  public static PaidShardCommerce Instance=new();public static bool Spending;
  public bool Ready=true;public string AccountHash="owner";public int SessionVersion,AcceptedWallets,Flushes;
  public Task<bool> FlushProfileAsync(){Flushes++;return Task.FromResult(true);}
  public bool TryBeginSocialSpend(){if(Spending)return false;Spending=true;return true;}
  public void EndSocialSpend(int session,Snapshot value,AccountProfilePrefs.Entry[] submitted){if(session!=SessionVersion)return;if(value?.accountHash==AccountHash)AcceptedWallets++;Spending=false;}
  public void Refresh(){}
 }
}
public static class FriendGiftTests {
 static int checks;static SocialService service;static SocialService.Request last;static string lastRoute;
 static void Check(bool ok,string why){checks++;if(!ok)throw new Exception(why);}
 const string Friend="22222222222222222222";
 const string State="{\"fulfilled\":true,\"social\":{\"stickerProtocolVersion\":1,\"friends\":[{\"playerId\":\"22222222222222222222\",\"name\":\"SolarFox0000002\",\"level\":3}],\"receivedStickers\":[]}}";
 const string Delivered="{\"fulfilled\":true,\"social\":{\"stickerProtocolVersion\":1,\"wallet\":{\"accountHash\":\"owner\",\"profile\":[]},\"friends\":[{\"playerId\":\"22222222222222222222\",\"name\":\"SolarFox0000002\"}],\"receivedStickers\":[]}}";
 static void Respond(string json,long code=200){UnityWebRequest.Handler=request=>{last=JsonUtility.FromJson<SocialService.Request>(System.Text.Encoding.UTF8.GetString(request.uploadHandler.data));lastRoute=request.url;Check(request.Headers["Authorization"]=="Bearer credential","Verified account credential must be attached");request.downloadHandler.text=json;request.responseCode=code;request.result=code==200?UnityWebRequest.Result.Success:UnityWebRequest.Result.ConnectionError;return new UnityWebRequestAsyncOperation();};}
 static void Drain(){var routine=MonoBehaviour.Pending;MonoBehaviour.Pending=null;Check(routine!=null,"Expected asynchronous request");while(routine.MoveNext()){} }
 static void Prime(){Respond("{\"fulfilled\":true,\"social\":{}}");SocialService.Connect("https://account.example",()=>"credential");service=SocialService.Instance;Drain();Time.unscaledTime+=20;Respond(State);var friends=FriendStickerService.Friends;Drain();Check(FriendStickerService.Friends.Count==1,"Friends must come from server snapshot");}
 public static void Main(){
  PlayerPrefs.SetString("ownerDF_FriendStickerState_v1","{\"clanInvites\":[{\"inviteId\":\"private-owner-invite\"}],\"receivedStickers\":[{\"giftId\":\"legacy-local-only\",\"stickerId\":1}]}");
  Prime();Check(FriendStickerService.Ready,"Updated backend protocol enables gifts");
  Check(FriendStickerService.ClanInvites.Count==1,"Existing account's clan invites remain available");Check(FriendStickerService.ReceivedStickers.Count==0,"Legacy local sticker records cannot authorize a real shard claim");
  Check(FriendStickerService.SendStickerToFriend(Friend,1),"Accepted friend gift should queue");
  Respond("{\"fulfilled\":false,\"message\":\"offline\"}",503);Drain();
  string operation=last.operationId;Check(operation?.Length==32,"Gift operation must have durable ID");Check(last.expectedProfileRevision==7,"Debit must include current saved profile revision");
  Check(service.HasPendingStickerGift,"Lost reply must preserve pending operation");Check(PlayerWallet.LocalSpends==0&&PlayerWallet.LocalGrants==0,"Sending must never debit or credit local wallet");
  Check(Alterverse.Audio.DimensionAudio.Plays==0,"Failed gift delivery must not play purchase confirmation");
  Check(FriendStickerService.SendStickerToFriend(Friend,1),"Retry should queue");Respond(Delivered);Drain();Check(last.operationId==operation,"Retry must reuse same gift operation");
  Check(!service.HasPendingStickerGift,"Confirmed delivery clears pending operation");Check(PaidShardCommerce.Instance.AcceptedWallets==1,"Confirmed server wallet must be applied once");
  Check(FriendStickerService.ReceivedStickers.Count==0,"Sending must not fabricate a thank-you gift in own inbox");
  Check(Alterverse.Audio.DimensionAudio.Plays==1,"Confirmed gift keeps the existing purchase sound");
  Respond(State);Check(FriendStickerService.SendFriendRequest("SolarFox0000003"),"Named friend request should queue");Drain();Check(last.query=="SolarFox0000003"&&string.IsNullOrEmpty(last.playerId),"Friend name must be resolved by backend, never hashed locally");
  Respond(State);Check(FriendStickerService.AcceptFriendRequest(Friend),"Acceptance queues account request");Drain();Check(lastRoute.EndsWith("/social/friend/accept")&&last.playerId==Friend,"Acceptance must use server player ID");
  Respond(Delivered);Check(FriendStickerService.ConvertReceivedStickerToShards(new string('a',64)),"Claim queues server request");Drain();Check(lastRoute.EndsWith("/social/sticker/claim"),"Claims must use server endpoint");Check(PlayerWallet.LocalGrants==0,"Claim button cannot grant shards locally");
  Check(FriendStickerService.SendStickerToFriend(Friend,1),"Next gift queues");Respond(Delivered);
  var old=MonoBehaviour.Pending;MonoBehaviour.Pending=null;Check(old.MoveNext(),"First step sends request");int accepted=PaidShardCommerce.Instance.AcceptedWallets;
  SocialService.Disconnect();PaidShardCommerce.Instance.SessionVersion++;PaidShardCommerce.Instance.AccountHash="other";AccountProfilePrefs.Account="other";
  while(old.MoveNext()){}Check(PaidShardCommerce.Instance.AcceptedWallets==accepted,"Old account reply cannot apply after switching accounts");Check(service.Friends==null,"Account switch clears friend and inbox cache");
  Check(FriendStickerService.ClanInvites.Count==0,"Empty account cache cannot inherit the previous account's clan invitations");
  Check(!service.HasPendingStickerGift,"Another account cannot recover the previous account's operation");
  Check(!FriendStickerService.SendStickerToFriend(Friend,1),"Signed-out sends must fail without local mutation");Check(PlayerWallet.LocalSpends==0&&PlayerWallet.LocalGrants==0,"Failure paths never change local currency");
  string pending=PlayerPrefs.GetString("DF_CommerceStickerGift_owner");
  AccountProfilePrefs.Account="owner";PaidShardCommerce.Instance.AccountHash="owner";PaidShardCommerce.Instance.SessionVersion++;
  Respond("{\"fulfilled\":true,\"social\":{}}");SocialService.Connect("https://account.example",()=>"credential");Drain();
  Respond(Delivered);Time.unscaledTime+=20;var restored=FriendStickerService.Friends;Drain();
  Check(last.operationId==JsonUtility.FromJson<SocialService.Request>(pending).operationId,"Restart/account-return must recover the durable operation instead of creating another gift");
  Check(!service.HasPendingStickerGift&&PaidShardCommerce.Instance.AcceptedWallets==accepted+1,"Recovered server confirmation applies only to its original account");
  Console.WriteLine($"Friend/sticker production-client compile and {checks} request/retry/account-isolation checks passed.");
 }
}
