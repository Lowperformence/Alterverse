using System;
using System.Threading.Tasks;
using UnityEngine;
namespace Alterverse.Profile {
 public static class AccountProfilePrefs {public class Entry{}public static bool Attached=true,Dirty=false;public static long Revision;public static Entry[] Capture()=>Array.Empty<Entry>();}
 public static class PlayerExperience {public static int Level=1;}
 public static class PlayerWallet {public static int Balance=1000;}
 public static class AvatarCollection {public static System.Collections.Generic.List<int> OwnedIds()=>new(){0,1,16};}
 public class AccountServerBootstrap {public static AccountServerBootstrap Instance;public string Status;}
}
namespace Alterverse.Shop {
 public class PaidShardCommerce {
  public class Snapshot {public string accountHash;public Alterverse.Profile.AccountProfilePrefs.Entry[] profile;}
  public static PaidShardCommerce Instance=new();public bool Ready=true;public string AccountHash="owner";public int SessionVersion;
  public int Flushes;public bool SaveSucceeds=true;public TaskCompletionSource<bool> StalledSave;
  public Task<bool> FlushProfileAsync(){Flushes++;return StalledSave?.Task??Task.FromResult(SaveSucceeds);}public bool TryBeginSocialSpend()=>true;
  public void EndSocialSpend(int s,Snapshot w,Alterverse.Profile.AccountProfilePrefs.Entry[] e){}public void Refresh(){}
 }
 public static class AvatarShopCatalog {public class Item {public int AvatarId;}public static Item[] Items=Array.Empty<Item>();}
 public static class AvatarInventory {public static bool IsOwned(int id)=>true;}
 public static class AvatarPresentation {public static string Name(int id)=>"Avatar";}
}
namespace Alterverse.UI {
 public class FrontEndTheme {
   public void RankingPanel(Rect r,int rank){Panel(r);}public void RankingBadge(Rect r,int rank){}
  public static object Positive,Accent;public GUIStyle Body=new(),Small=new(),Heading=new(),Field=new();
  public static System.Collections.Generic.List<(Rect Rect,string Text,bool Enabled)> Actions=new();
  public static Func<Rect,string,bool> Click;
  public bool Action(Rect r,string l,bool primary=false,bool selected=false,bool enabled=true,bool locked=false){Actions.Add((r,l,enabled&&!locked));return enabled&&!locked&&Click?.Invoke(r,l)==true;}
  public void Label(Rect r,string l,GUIStyle s,bool muted=false){}public void FitLabel(Rect r,string l,GUIStyle s,bool muted=false){}
  public static Rect LastCheckbox;public static void Fill(Rect r,object c){}public void Panel(Rect r){}public void Frame(Rect r,bool emphasized=false){LastCheckbox=r;}public void Center(Rect r,string l){}public void StatusBadge(Rect r,string l,object c){}public void EmptyState(Rect r,string a,string b){}
 }
 public static class AvatarBadgeRenderer {public static void Draw(Rect r,int id,Texture2D t,float time){}}
 public sealed partial class MainMenuController {
  enum Page{Profile,Leaderboard,Clans,Shop}Page page;float Left=0,Right=1100,ContentWidth=1100;FrontEndTheme theme=new();
  bool skinShop,itemShop,weaponShop,mapShop,battlePassShop,heroShop,shardShop;string shopMessage;
  void SetPage(Page p){page=p;}void Heading(string a,string b){}
 }
}
public static class Fixture {
 static void Assert(bool ok,string why){if(!ok)throw new Exception(why);}
 public static void Main(){
  var service=new UnityEngine.GameObject("test").AddComponent<Alterverse.Profile.SocialService>();
  var state=new Alterverse.Profile.SocialService.Snapshot {chatEnabled=true,clanCreationCost=500,clanProtocolVersion=4,leaderRulesVersion="1"};
  typeof(Alterverse.Profile.SocialService).GetProperty("State").SetValue(service,state);
  var ep=typeof(Alterverse.Profile.SocialService).GetField("endpoint",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  var token=typeof(Alterverse.Profile.SocialService).GetField("token",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  ep.SetValue(service,"https://server.example");token.SetValue(service,(Func<string>)(()=>"token"));
  service.CreateClan(new Alterverse.Profile.SocialService.Request{accepted=false,consentVersion="1"});
  Assert(!service.Busy&&!service.HasPendingClanCreation,"Unaccepted founder terms must not create or save a request");
  var chat=new Alterverse.Profile.SocialService.Snapshot {messages=new[]{new Alterverse.Profile.SocialService.Message{text="private"}}};
  state.messages=chat.messages;
  typeof(Alterverse.Profile.SocialService).GetProperty("Chat").SetValue(service,chat);
  typeof(Alterverse.Profile.SocialService).GetMethod("OnApplicationFocus",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(service,new object[]{false});
  Assert(service.Chat==null&&service.State.messages==null,"Hidden app must clear private chat cache");
  typeof(Alterverse.Profile.SocialService).GetProperty("Chat").SetValue(service,chat);
  state.clan=new Alterverse.Profile.SocialService.Clan{clanId="private"};state.hasClan=true;
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{r.downloadHandler.text="{\"fulfilled\":true,\"social\":{\"clanProtocolVersion\":4,\"hasClan\":false,\"chatEnabled\":true}}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  var send=typeof(Alterverse.Profile.SocialService).GetMethod("Send",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
  var request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/state",new Alterverse.Profile.SocialService.Request(),0});
  while(request.MoveNext()){}
  Assert(service.State.clan==null&&service.Chat==null,"Membership revocation must remove cached chat");
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{r.responseCode=503;r.result=UnityEngine.Networking.UnityWebRequest.Result.ConnectionError;r.downloadHandler.text="{\"fulfilled\":false,\"message\":\"Clan service temporarily unavailable. Please retry.\"}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/chat/read",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(service.Chat==null&&service.ChatStatus.Contains("temporarily unavailable"),"A chat failure must explain the blocked composer instead of showing Connecting forever");
  Alterverse.Profile.AccountProfilePrefs.Dirty=true;
  Alterverse.Shop.PaidShardCommerce.Instance.SaveSucceeds=false;
  int flushes=Alterverse.Shop.PaidShardCommerce.Instance.Flushes,reads=0;
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{reads++;r.downloadHandler.text="{\"fulfilled\":true,\"social\":{\"clanId\":\"private\",\"messages\":[]}}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/chat/read",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(reads==1&&Alterverse.Shop.PaidShardCommerce.Instance.Flushes==flushes,"Reading chat must not wait for an unrelated pending profile save");
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{reads++;r.downloadHandler.text="{\"fulfilled\":true,\"social\":{\"players\":[{\"playerId\":\"saved-player\",\"level\":7,\"xp\":1800}],\"total\":1}}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/levels",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(reads==2&&Alterverse.Shop.PaidShardCommerce.Instance.Flushes==flushes+1&&service.Levels?.players?[0].level==7,"Player rankings must try saving first and still load saved server levels after a save failure");
  Assert(service.Status.Contains("synchronization pending")&&Alterverse.Profile.AccountProfilePrefs.Dirty,"Player ranking reads must retain unsaved local progression and explain the saved values");
  var stalled=new TaskCompletionSource<bool>();Alterverse.Shop.PaidShardCommerce.Instance.StalledSave=stalled;
  int beforeStall=Alterverse.Shop.PaidShardCommerce.Instance.Flushes;UnityEngine.Time.realtimeSinceStartup=0;
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/levels",new Alterverse.Profile.SocialService.Request(),0});
  Assert(request.MoveNext(),"Ranking waits briefly for a pending save");
  UnityEngine.Time.realtimeSinceStartup=6;while(request.MoveNext()){}
  Assert(Alterverse.Shop.PaidShardCommerce.Instance.Flushes==beforeStall+1&&service.Levels?.players?[0].level==7,"Stalled save must not block saved ranking data");
  Assert(service.Status.Contains("synchronization pending")&&Alterverse.Profile.AccountProfilePrefs.Dirty,"Timed out read preserves local progress");
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/levels",new Alterverse.Profile.SocialService.Request(),0});
  Assert(request.MoveNext(),"Retry waits on the same pending save");
  UnityEngine.Time.realtimeSinceStartup=12;while(request.MoveNext()){}
  Assert(Alterverse.Shop.PaidShardCommerce.Instance.Flushes==beforeStall+1,"Refresh reuses stalled save");
  stalled.SetResult(false);Alterverse.Shop.PaidShardCommerce.Instance.StalledSave=null;

  reads=1;
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{reads++;r.downloadHandler.text="{\"fulfilled\":true,\"social\":{\"clans\":[{\"clanId\":\"saved-clan\",\"totalLevel\":35}],\"total\":1}}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/clan-levels",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(reads==2&&service.ClanLevels?.clans?.Length==1&&service.ClanLevels.clans[0].totalLevel==35,"Failed account saves must still load the authoritative saved clan standings");
  Assert(service.Status.Contains("synchronization pending")&&Alterverse.Profile.AccountProfilePrefs.Dirty,"Reading saved clan standings must retain unsaved progression and explain its status");
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{reads++;r.downloadHandler.text="{\"fulfilled\":true,\"social\":{\"clanProtocolVersion\":4,\"hasClan\":true,\"clan\":{\"clanId\":\"private\"},\"chatEnabled\":true}}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/state",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(reads==3&&service.State?.clan?.clanId=="private","A pending profile save must not prevent restoring the clan membership needed by chat");
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/clan/create",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(reads==3,"Clan creation must retain its successful-save requirement before any debit");
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{reads++;r.responseCode=503;r.result=UnityEngine.Networking.UnityWebRequest.Result.ConnectionError;r.downloadHandler.text="{\"fulfilled\":false,\"message\":\"Clan service temporarily unavailable. Please retry.\"}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/clan-levels",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(service.Status.Contains("temporarily unavailable"),"A server failure must not be replaced by the saved-profile notice");
  Alterverse.Shop.PaidShardCommerce.Instance.SaveSucceeds=true;
  UnityEngine.Networking.UnityWebRequest.Handler=r=>{r.downloadHandler.text="{\"fulfilled\":true,\"social\":{\"clans\":[]}}";return new UnityEngine.Networking.UnityWebRequestAsyncOperation();};
  request=(System.Collections.IEnumerator)send.Invoke(service,new object[]{"/social/clan-levels",new Alterverse.Profile.SocialService.Request(),0});while(request.MoveNext()){}
  Assert(service.Status=="Synchronized.","Successful profile saves must clear the pending-save notice");
  Alterverse.Profile.AccountProfilePrefs.Dirty=false;Alterverse.Shop.PaidShardCommerce.Instance.SaveSucceeds=true;
  var menu=new Alterverse.UI.MainMenuController();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;var ui=menu.GetType();
  var consent=ui.GetMethod("DrawClanConsent",flags);
  Alterverse.UI.FrontEndTheme.Click=(r,l)=>r.width==900&&l=="";
  bool accepted=(bool)consent.Invoke(menu,new object[]{new Rect(0,0,900,56),false,"I accept the rules.",true});
  Assert(accepted&&Alterverse.UI.FrontEndTheme.LastCheckbox.width>=36,"Full-width consent row must toggle a clearly visible checkbox");
  accepted=(bool)consent.Invoke(menu,new object[]{new Rect(0,0,900,56),false,"I accept the rules.",false});
  Assert(!accepted,"Busy consent cannot be accepted by a stale click");
  Alterverse.UI.FrontEndTheme.Click=null;
  state.clan=new Alterverse.Profile.SocialService.Clan{clanId="private",avatar=-1,members=Array.Empty<Alterverse.Profile.SocialService.Player>()};state.me=new();state.chatRules="Chat rules";
  ui.GetField("clanDetailTab",flags).SetValue(menu,2);
  Alterverse.UI.FrontEndTheme.Actions.Clear();ui.GetMethod("DrawMyClan",flags).Invoke(menu,new object[]{service,state});
  Assert(Alterverse.UI.FrontEndTheme.Actions.Exists(a=>a.Text=="BACK TO CLAN")&&!Alterverse.UI.FrontEndTheme.Actions.Exists(a=>a.Text=="MEMBERS"),"Rules own the large content window without overlapping the clan header controls");
  ui.GetField("chatTermsAccepted",flags).SetValue(menu,true);ui.GetField("chatAgeBand",flags).SetValue(menu,"under13");
  Alterverse.UI.FrontEndTheme.Actions.Clear();ui.GetMethod("DrawChatConsent",flags).Invoke(menu,new object[]{service,state,false});
  Assert(Alterverse.UI.FrontEndTheme.Actions.Exists(a=>a.Text=="ENTER CLAN CHAT"&&!a.Enabled),"Enlarging the dialog must preserve the age gate");
  Alterverse.UI.FrontEndTheme.Actions.Clear();ui.GetMethod("DrawClanImage",flags).Invoke(menu,new object[]{service,state});
  Assert(Alterverse.UI.FrontEndTheme.Actions.Exists(a=>a.Text=="SET CLAN IMAGE")&&!Alterverse.UI.FrontEndTheme.Actions.Exists(a=>a.Text=="OPEN STORE"),"Starter and reward avatars must be offered instead of a false empty-inventory message");
  ui.GetField("skinShop",flags).SetValue(menu,true);ui.GetField("shardShop",flags).SetValue(menu,true);ui.GetMethod("OpenAvatarStore",flags).Invoke(menu,null);
  Assert(!(bool)ui.GetField("skinShop",flags).GetValue(menu)&&!(bool)ui.GetField("shardShop",flags).GetValue(menu),"Avatar browsing must not retain Wings or Shards tab state");
  Alterverse.Profile.PlayerWallet.Balance=10;Alterverse.UI.FrontEndTheme.Click=(r,l)=>true;
  var purchase=ui.GetMethod("ShardPurchaseAction",flags);
  bool buy=(bool)purchase.Invoke(menu,new object[]{new Rect(0,0,200,50),"NEED MORE SHARDS",500,true,true,false});
  Assert(!buy&&(bool)ui.GetField("shardShop",flags).GetValue(menu),"Insufficient-funds action must navigate to Shards without executing a purchase");
  ui.GetMethod("OpenAvatarStore",flags).Invoke(menu,null);
  buy=(bool)purchase.Invoke(menu,new object[]{new Rect(0,0,200,50),"COMING SOON",500,true,true,true});
  Assert(!buy&&!(bool)ui.GetField("shardShop",flags).GetValue(menu),"Unreleased content must remain locked");
  buy=(bool)purchase.Invoke(menu,new object[]{new Rect(0,0,200,50),"EQUIP",0,true,true,false});
  Assert(buy&&!(bool)ui.GetField("shardShop",flags).GetValue(menu),"Owned equipment must never redirect for insufficient funds");
  CheckRankingRefresh(service);
  Console.WriteLine("Chat UI, ownership, navigation, failure states and ranking refresh checks passed.");
 }
 static void CheckRankingRefresh(Alterverse.Profile.SocialService service){
  var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
  var menu=new Alterverse.UI.MainMenuController();var ui=menu.GetType();
  var draw=ui.GetMethod("DrawRankingTabs",flags);
  var busy=service.GetType().GetProperty("Busy");
  Alterverse.UI.FrontEndTheme.Click=null;
  ui.GetField("rankingTab",flags).SetValue(menu,1);
  busy.SetValue(service,true);draw.Invoke(menu,null);
  Assert((int)ui.GetField("requestedRankingTab",flags).GetValue(menu)==-1,"Busy service must defer the ranking request rather than lose it");
  busy.SetValue(service,false);draw.Invoke(menu,null);
  Assert(service.Busy&&(int)ui.GetField("requestedRankingTab",flags).GetValue(menu)==1,"Deferred player ranking must load once the service is idle");
  busy.SetValue(service,false);draw.Invoke(menu,null);
  Assert(!service.Busy,"IMGUI redraws must not resend the same ranking request");
  Alterverse.Profile.AccountProfilePrefs.Revision++;draw.Invoke(menu,null);
  Assert(service.Busy,"A newly saved profile must refresh level standings");
  busy.SetValue(service,false);
  Alterverse.Shop.PaidShardCommerce.Instance.AccountHash="other-player";draw.Invoke(menu,null);
  Assert(service.Busy,"A changed account must not keep another account's ranking request state");
  busy.SetValue(service,false);
  ui.GetField("rankingTab",flags).SetValue(menu,2);draw.Invoke(menu,null);
  Assert(service.Busy&&(int)ui.GetField("requestedRankingTab",flags).GetValue(menu)==2,"Clan rankings must load independently from player levels");
  busy.SetValue(service,false);UnityEngine.Time.unscaledTime+=31f;draw.Invoke(menu,null);
  Assert(service.Busy,"Visible rankings must periodically refresh without a manual tap");
 }
}



