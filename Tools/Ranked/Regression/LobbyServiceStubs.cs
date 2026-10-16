// Controlled Unity Services fixtures. Production lobby and matchmaking code is compiled unmodified.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
namespace Unity.Services.Core {public static class UnityServices {public static Func<Task> Initialize;public static Task InitializeAsync()=>Initialize?.Invoke()??Task.CompletedTask;}}
namespace Unity.Services.Multiplayer {
 public enum VisibilityPropertyOptions {Public,Member}
 public class PlayerProperty {public string Value;public PlayerProperty(string v,VisibilityPropertyOptions o){Value=v;}}
 public class SessionProperty {public string Value;public SessionProperty(string v,VisibilityPropertyOptions o){Value=v;}}
 public class SessionException:Exception {public SessionException(string s):base(s){}}
 public interface IReadOnlyPlayer {string Id{get;}Dictionary<string,PlayerProperty> Properties{get;}}
 public class Player:IReadOnlyPlayer {public string Id{get;set;}public Dictionary<string,PlayerProperty> Properties{get;set;}=new();public void SetProperties(Dictionary<string,PlayerProperty> values){foreach(var p in values)Properties[p.Key]=p.Value;}public void SetProperty(string k,PlayerProperty v){Properties[k]=v;}}
 public interface ISession {
  string Id{get;}string Code{get;}string Host{get;}bool IsHost{get;}int MaxPlayers{get;}int PlayerCount{get;}
  Player CurrentPlayer{get;}List<IReadOnlyPlayer> Players{get;}Dictionary<string,SessionProperty> Properties{get;}
  event Action Changed,SessionPropertiesChanged,PlayerPropertiesChanged,RemovedFromSession,Deleted;
  event Action<string> PlayerJoined,PlayerHasLeft;
  IHostSession AsHost();Task SaveCurrentPlayerDataAsync();Task LeaveAsync();
 }
 public interface IHostSession {bool IsPrivate{get;set;}bool IsLocked{get;set;}void SetProperty(string k,SessionProperty v);void SetProperties(Dictionary<string,SessionProperty> values);Task SavePropertiesAsync();Task DeleteAsync();}
 public interface ISessionInfo {string Id{get;}string Name{get;}bool IsLocked{get;}int AvailableSlots{get;}int MaxPlayers{get;}}
 public class SessionInfo:ISessionInfo {public string Id{get;set;}public string Name{get;set;}public bool IsLocked{get;set;}public int AvailableSlots{get;set;}public int MaxPlayers{get;set;}}
 public class SessionOptions {public string Type,Name;public int MaxPlayers;public bool IsPrivate,IsLocked;public Dictionary<string,PlayerProperty> PlayerProperties;public Dictionary<string,SessionProperty> SessionProperties;public SessionOptions WithRelayNetwork()=>this;}
 public class JoinSessionOptions {public string Type;public Dictionary<string,PlayerProperty> PlayerProperties;}
 public class QuerySessionsOptions {public int Count;}
 public class QuerySessionsResults {public List<ISessionInfo> Sessions=new();}
 public class FakeSession:ISession,IHostSession {
  public string Id{get;set;}="room";public string Code{get;set;}="ABC123";public string Host{get;set;}="host";public bool IsHost=>Host==CurrentPlayer?.Id;
  public int MaxPlayers{get;set;}=8;public int PlayerCount=>Players.Count;public bool IsPrivate{get;set;}=true;public bool IsLocked{get;set;}
  public Player CurrentPlayer{get;set;}public List<IReadOnlyPlayer> Players{get;}=new();public Dictionary<string,SessionProperty> Properties{get;}=new();
  public int Saves,Deletes,Leaves;public Func<Task> Save,Leave,Delete;
  public event Action Changed,SessionPropertiesChanged,PlayerPropertiesChanged,RemovedFromSession,Deleted;public event Action<string> PlayerJoined,PlayerHasLeft;
  public IHostSession AsHost()=>this;
  public Task SaveCurrentPlayerDataAsync(){PlayerPropertiesChanged?.Invoke();return Save?.Invoke()??Task.CompletedTask;}
  public Task SavePropertiesAsync(){Saves++;SessionPropertiesChanged?.Invoke();return Save?.Invoke()??Task.CompletedTask;}
  public Task DeleteAsync(){Deletes++;return Delete?.Invoke()??Task.CompletedTask;}
  public Task LeaveAsync(){Leaves++;return Leave?.Invoke()??Task.CompletedTask;}
  public void SetProperty(string k,SessionProperty v){Properties[k]=v;}public void SetProperties(Dictionary<string,SessionProperty> values){foreach(var p in values)Properties[p.Key]=p.Value;}
  public void Add(Player p){Players.Add(p);PlayerJoined?.Invoke(p.Id);}public void Remove(string id){Players.RemoveAll(p=>p.Id==id);PlayerHasLeft?.Invoke(id);}
  public void RemoveLocal(){RemovedFromSession?.Invoke();}public void Close(){Deleted?.Invoke();}public void Notify(){Changed?.Invoke();}
 }
 public class MultiplayerService {
  public static MultiplayerService Instance=new();public SessionOptions LastOptions;public string LastCode;public int Creates,Joins;
  public Func<SessionOptions,Task<ISession>> Create;public Func<string,JoinSessionOptions,Task<ISession>> Join;public QuerySessionsResults Query=new();
  public Task<ISession> CreateSessionAsync(SessionOptions o){Creates++;LastOptions=o;return Create?.Invoke(o)??Task.FromResult<ISession>(FromOptions(o));}
  public Task<ISession> JoinSessionByCodeAsync(string code,JoinSessionOptions o){Joins++;LastCode=code;return Join?.Invoke(code,o)??Task.FromException<ISession>(new SessionException("Room not found"));}
  public Task<ISession> JoinSessionByIdAsync(string id,JoinSessionOptions o)=>JoinSessionByCodeAsync(id,o);
  public Task<QuerySessionsResults> QuerySessionsAsync(QuerySessionsOptions o)=>Task.FromResult(Query);
  static FakeSession FromOptions(SessionOptions o){var s=new FakeSession{MaxPlayers=o.MaxPlayers,IsPrivate=o.IsPrivate,IsLocked=o.IsLocked};s.CurrentPlayer=new Player{Id="host",Properties=o.PlayerProperties};s.Players.Add(s.CurrentPlayer);s.SetProperties(o.SessionProperties);return s;}
 }
}
namespace Alterverse.Shop {
 public static class HeroUnlockInventory {public static bool Owned(Alterverse.Characters.HeroId h)=>true;}
 public static class ArenaUnlockInventory {public static bool Owned(Alterverse.Match.ArenaVariant a)=>true;}
 public class ArenaShopItem {public Alterverse.Match.ArenaVariant Variant;public string Name;}
 public static class ArenaShopCatalog {public static ArenaShopItem[] Items={new(){Variant=Alterverse.Match.ArenaVariant.Moon,Name="Moon"},new(){Variant=Alterverse.Match.ArenaVariant.Earth,Name="Earth"}};public static int IndexOf(Alterverse.Match.ArenaVariant a)=>(int)a;}
}
namespace Alterverse.UI {public static class OnlinePartyOverlay {public static int Shows;public static void Show(){Shows++;}}}
namespace Alterverse.Multiplayer {public static class NetworkRoundLoop {public static bool IsBetweenRounds;}}
