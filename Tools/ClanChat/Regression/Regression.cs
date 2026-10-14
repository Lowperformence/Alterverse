using System;
using System.Threading.Tasks;
using UnityEngine;
namespace Alterverse.Profile {
 public static class AccountProfilePrefs {public class Entry{}public static bool Attached=true,Dirty=false;public static long Revision;public static Entry[] Capture()=>Array.Empty<Entry>();}
 public static class PlayerExperience {public static int Level=1;}
 public static class PlayerWallet {public static int Balance=1000;}
 public class AccountServerBootstrap {public static AccountServerBootstrap Instance;public string Status;}
}
namespace Alterverse.Shop {
 public class PaidShardCommerce {
  public class Snapshot {public string accountHash;}
  public static PaidShardCommerce Instance=new();public bool Ready=true;public string AccountHash="owner";public int SessionVersion;
  public Task<bool> FlushProfileAsync()=>Task.FromResult(true);public bool TryBeginSocialSpend()=>true;
  public void EndSocialSpend(int s,Snapshot w,Alterverse.Profile.AccountProfilePrefs.Entry[] e){}public void Refresh(){}
 }
 public static class AvatarShopCatalog {public class Item {public int AvatarId;}public static Item[] Items=Array.Empty<Item>();}
 public static class AvatarInventory {public static bool IsOwned(int id)=>true;}
 public static class AvatarPresentation {public static string Name(int id)=>"Avatar";}
}
namespace Alterverse.UI {
 public class FrontEndTheme {
  public static object Positive,Accent;public GUIStyle Body=new(),Small=new(),Heading=new(),Field=new();
  public bool Action(Rect r,string l,bool primary=false,bool selected=false,bool enabled=true)=>false;
  public void Label(Rect r,string l,GUIStyle s,bool muted=false){}public void FitLabel(Rect r,string l,GUIStyle s,bool muted=false){}
  public void Panel(Rect r){}public void Center(Rect r,string l){}public void StatusBadge(Rect r,string l,object c){}public void EmptyState(Rect r,string a,string b){}
 }
 public static class AvatarBadgeRenderer {public static void Draw(Rect r,int id,Texture2D t,float time){}}
 public sealed partial class MainMenuController {
  enum Page{Profile,Leaderboard,Clans,Shop}Page page;float Left=0,Right=1100,ContentWidth=1100;FrontEndTheme theme=new();
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
  Console.WriteLine("Chat client and UI compile; founder consent and focus-cache checks passed.");
 }
}
