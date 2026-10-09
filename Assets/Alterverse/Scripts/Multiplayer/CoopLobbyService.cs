using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Alterverse.AI;
using Alterverse.Characters;
using Alterverse.Match;
using Alterverse.Profile;
using Alterverse.Shop;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Private 1-5 player co-op session owner. Sessions use Relay-backed NGO networking while
    /// preserving the existing local-vs-AI mode as an independent path. Open team slots are
    /// filled automatically by authoritative bots when the match plan is staged.
    /// </summary>
    public sealed class CoopLobbyService : MonoBehaviour, IMultiplayerProvider
    {
        public sealed class MemberView
        {
            public string PlayerId;
            public string DisplayName;
            public HeroId Hero;
            public BattleItemType BattleItem;
            public bool Ready;
            public bool IsHost;
            public bool IsLocal;
        }

        public const int MinHumanPlayers=1;
        public const int MaxHumanPlayers=5;
        private const string SessionType="df-private-coop-v2";
        private const string PlayerNameKey="displayName";
        private const string PlayerHeroKey="hero";
        private const string PlayerBattleItemKey="battleItem";
        private const string PlayerReadyKey="ready";
        private const string ArenaKey="arena";
        private const string ArenaShuffleKey="arenaShuffle";
        private const string DifficultyKey="difficulty";
        private const string PhaseKey="phase";
        private const string MatchIdKey="matchId";

        public static CoopLobbyService Instance { get; private set; }

        private ISession session;
        private bool servicesInitialized,busy;
        private string status="CO-OP READY";
        private string lastError="";
        private readonly List<MemberView> memberBuffer=new(MaxHumanPlayers);

        public event Action Changed;

        public string ProviderName=>"Unity Private Co-op";
        public bool IsAvailable=>true;
        public bool IsConnected=>servicesInitialized&&AuthenticationService.Instance.IsSignedIn;
        public bool InLobby=>session!=null;
        public bool Busy=>busy;
        public bool IsHost=>session!=null&&session.IsHost;
        public string JoinCode=>session?.Code??"";
        public string Status=>status;
        public string LastError=>lastError;
        public int PlayerCount=>session?.PlayerCount??0;
        public int MaxPlayers=>session?.MaxPlayers??MaxHumanPlayers;
        public bool RelayConnected=>InLobby&&CoopNetworkBootstrap.IsListening;
        public bool PlayerLinkReady=>InLobby&&CoopNetworkBootstrap.IsLinkReadyForPlayers(PlayerCount);
        public string NetworkLinkState=>CoopNetworkBootstrap.LinkStateForPlayers(Mathf.Max(MinHumanPlayers,PlayerCount));
        public string LobbyPhase=>ReadSession(PhaseKey,"lobby");
        public bool IsLobby=>string.Equals(LobbyPhase,"lobby",StringComparison.Ordinal);
        public bool IsStaging=>string.Equals(LobbyPhase,"staging",StringComparison.Ordinal);
        public bool IsLaunching=>string.Equals(LobbyPhase,"launching",StringComparison.Ordinal);
        public bool IsPostMatch=>string.Equals(LobbyPhase,"postmatch",StringComparison.Ordinal);
        public bool IsAborted=>string.Equals(LobbyPhase,"aborted",StringComparison.Ordinal);
        public bool IsMatchPreparing=>IsStaging||IsLaunching;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if(Instance==null)new GameObject("DF_CoopLobbyService").AddComponent<CoopLobbyService>();
        }

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            MultiplayerGate.Instance?.RegisterProvider(this);
        }

        public void Connect(Action<bool,string> completed)
        {
            _=ConnectAsync(completed);
        }

        private async Task ConnectAsync(Action<bool,string> completed)
        {
            bool ok=await EnsureServicesAsync();
            completed?.Invoke(ok,status);
        }

        public void Disconnect()
        {
            _=LeaveLobbyAsync();
        }

        public async Task<bool> EnsureServicesAsync()
        {
            if(busy)return servicesInitialized&&AuthenticationService.Instance.IsSignedIn;
            try
            {
                busy=true;lastError="";SetStatus("CONNECTING TO UNITY SERVICES");
                if(!servicesInitialized)
                {
                    await UnityServices.InitializeAsync();
                    servicesInitialized=true;
                }
                if(Alterverse.Profile.AccountServerBootstrap.Instance!=null)
                {
                    if(!await Alterverse.Profile.AccountServerBootstrap.Instance.EnsureAuthenticatedAsync())return false;
                }
                else if(!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                SetStatus("ONLINE READY");
                return true;
            }
            catch(Exception ex)
            {
                Fail("ONLINE SERVICE UNAVAILABLE",ex);
                return false;
            }
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> CreateLobbyAsync()
        {
            if(busy)return false;
            if(OnlinePartyService.Instance?.InParty==true || OnlinePartyService.Instance?.Busy==true ||
                EdgegapOnlineFlow.Instance?.Busy==true || EdgegapOnlineFlow.Instance?.IsSearching==true)
            {
                lastError="Leave the online party or finish connecting before opening a co-op lobby.";
                SetStatus("ONLINE PARTY ACTIVE");
                return false;
            }
            try
            {
                busy=true;lastError="";SetStatus("CREATING PRIVATE CO-OP LOBBY");
                if(!await EnsureServicesWhileBusyAsync())return false;
                await LeaveExistingSessionInternalAsync();

                var options=new SessionOptions
                {
                    Type=SessionType,
                    Name="DIMENSIONS FRONT CO-OP",
                    MaxPlayers=MaxHumanPlayers,
                    IsPrivate=true,
                    IsLocked=false,
                    PlayerProperties=BuildLocalPlayerProperties(false),
                    SessionProperties=new Dictionary<string,SessionProperty>
                    {
                        {ArenaKey,new SessionProperty(((int)ArenaVariantRuntime.Selected).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                        {ArenaShuffleKey,new SessionProperty(ArenaVariantRuntime.ShuffleEnabled?"1":"0",VisibilityPropertyOptions.Member)},
                        {DifficultyKey,new SessionProperty(((int)BotDifficultySettings.Selected).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                        {PhaseKey,new SessionProperty("lobby",VisibilityPropertyOptions.Member)},
                        {MatchIdKey,new SessionProperty("",VisibilityPropertyOptions.Member)}
                    }
                }.WithRelayNetwork();

                session=await MultiplayerService.Instance.CreateSessionAsync(options);
                AttachSession(session);
                SetStatus("LOBBY CREATED • SHARE THE JOIN CODE");
                return true;
            }
            catch(Exception ex)
            {
                await SafeLeaveAfterFailureAsync();
                Fail("COULD NOT CREATE LOBBY",ex);
                return false;
            }
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> JoinLobbyAsync(string rawCode)
        {
            if(busy)return false;
            if(OnlinePartyService.Instance?.InParty==true || OnlinePartyService.Instance?.Busy==true ||
                EdgegapOnlineFlow.Instance?.Busy==true || EdgegapOnlineFlow.Instance?.IsSearching==true)
            {
                lastError="Leave the online party or finish connecting before opening a co-op lobby.";
                SetStatus("ONLINE PARTY ACTIVE");
                return false;
            }
            string code=NormalizeJoinCode(rawCode);
            if(code.Length<4){lastError="Enter a valid lobby code.";SetStatus("INVALID LOBBY CODE");RaiseChanged();return false;}

            try
            {
                busy=true;lastError="";SetStatus("JOINING PRIVATE CO-OP LOBBY");
                if(!await EnsureServicesWhileBusyAsync())return false;
                await LeaveExistingSessionInternalAsync();

                var joinOptions=new JoinSessionOptions
                {
                    Type=SessionType,
                    PlayerProperties=BuildLocalPlayerProperties(false)
                };
                session=await MultiplayerService.Instance.JoinSessionByCodeAsync(code,joinOptions);
                AttachSession(session);
                if(!string.Equals(LobbyPhase,"lobby",StringComparison.Ordinal))
                {
                    await LeaveExistingSessionInternalAsync();
                    lastError="This co-op room has already started preparing a match.";
                    SetStatus("LOBBY IS ALREADY STARTING");
                    return false;
                }
                SetStatus("JOINED CO-OP LOBBY");
                return true;
            }
            catch(Exception ex)
            {
                await SafeLeaveAfterFailureAsync();
                Fail("COULD NOT JOIN LOBBY",ex);
                return false;
            }
            finally{busy=false;RaiseChanged();}
        }

        public async Task LeaveLobbyAsync()
        {
            if(busy)return;
            try
            {
                busy=true;lastError="";SetStatus("LEAVING LOBBY");
                await LeaveExistingSessionInternalAsync();
                SetStatus("CO-OP READY");
            }
            catch(Exception ex){Fail("COULD NOT LEAVE LOBBY",ex);}
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> CycleLocalHeroAsync(int direction)
        {
            if(session==null||busy)return false;
            HeroId current=LocalHero;
            int count=Enum.GetValues(typeof(HeroId)).Length;
            for(int step=1;step<=count;step++)
            {
                int next=Mod((int)current+Math.Sign(direction)*step,count);
                HeroId candidate=(HeroId)next;
                if(Alterverse.Shop.HeroUnlockInventory.Owned(candidate)&&!HeroUsedByOther(candidate))
                    return await SetLocalHeroAsync(candidate);
            }
            return false;
        }

        public async Task<bool> SetLocalHeroAsync(HeroId hero)
        {
            if(session==null||busy||!Alterverse.Shop.HeroUnlockInventory.Owned(hero)||HeroUsedByOther(hero))return false;
            try
            {
                busy=true;lastError="";
                session.CurrentPlayer.SetProperties(new Dictionary<string,PlayerProperty>
                {
                    {PlayerHeroKey,new PlayerProperty(((int)hero).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                    {PlayerReadyKey,new PlayerProperty("0",VisibilityPropertyOptions.Member)}
                });
                await session.SaveCurrentPlayerDataAsync();
                PlayerHeroSelection.Select(hero);
                SetStatus("HERO SELECTED • READY RESET");
                return true;
            }
            catch(Exception ex){Fail("COULD NOT UPDATE HERO",ex);return false;}
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> SetLocalReadyAsync(bool ready)
        {
            if(session==null||busy)return false;
            if(ready&&HeroUsedByOther(LocalHero))
            {
                lastError="Every co-op player must use a different hero.";
                SetStatus("CHOOSE A DIFFERENT HERO");
                RaiseChanged();
                return false;
            }

            try
            {
                busy=true;lastError="";
                session.CurrentPlayer.SetProperties(new Dictionary<string,PlayerProperty>
                {
                    {PlayerBattleItemKey,new PlayerProperty(((int)BattleItemInventory.Equipped).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                    {PlayerReadyKey,new PlayerProperty(ready?"1":"0",VisibilityPropertyOptions.Member)}
                });
                await session.SaveCurrentPlayerDataAsync();
                SetStatus(ready?(PlayerCount<=1?"READY • BOTS WILL FILL OPEN SLOTS":"READY • WAITING FOR TEAM"):"NOT READY");
                return true;
            }
            catch(Exception ex){Fail("COULD NOT UPDATE READY STATE",ex);return false;}
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> CycleHostArenaAsync(int direction)
        {
            if(session==null||!IsHost||busy)return false;
            ArenaShopItem[] items=ArenaShopCatalog.Items;
            if(items==null||items.Length==0)return false;
            int start=ArenaShopCatalog.IndexOf(LobbyArena);
            for(int step=1;step<=items.Length;step++)
            {
                int index=Mod(start+Math.Sign(direction)*step,items.Length);
                ArenaVariant candidate=items[index].Variant;
                if(!ArenaUnlockInventory.Owned(candidate))continue;
                try
                {
                    busy=true;lastError="";
                    IHostSession host=session.AsHost();
                    host.SetProperties(new Dictionary<string,SessionProperty>
                    {
                        {ArenaKey,new SessionProperty(((int)candidate).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                        {ArenaShuffleKey,new SessionProperty("0",VisibilityPropertyOptions.Member)}
                    });
                    await host.SavePropertiesAsync();
                    SetStatus($"ARENA • {items[index].Name}");
                    return true;
                }
                catch(Exception ex){Fail("COULD NOT UPDATE LOBBY",ex);return false;}
                finally{busy=false;RaiseChanged();}
            }
            return false;
        }

        public Task<bool> SetHostArenaShuffleAsync(bool enabled)
        {
            if(session==null||!IsHost||busy)return Task.FromResult(false);
            return SetHostSettingAsync(ArenaShuffleKey,enabled?"1":"0",
                enabled?"ARENA SHUFFLE • RANDOM OWNED MAP":"ARENA SHUFFLE • OFF");
        }

        public async Task<bool> CycleHostDifficultyAsync(int direction)
        {
            if(session==null||!IsHost||busy)return false;
            int count=Enum.GetValues(typeof(BotDifficulty)).Length;
            BotDifficulty next=(BotDifficulty)Mod((int)LobbyDifficulty+Math.Sign(direction),count);
            return await SetHostSettingAsync(DifficultyKey,((int)next).ToString(CultureInfo.InvariantCulture),
                "AI DIFFICULTY • "+BotDifficultySettings.Label(next).ToUpperInvariant());
        }

        private async Task<bool> SetHostSettingAsync(string key,string value,string okStatus)
        {
            try
            {
                busy=true;lastError="";
                IHostSession host=session.AsHost();
                host.SetProperty(key,new SessionProperty(value,VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
                SetStatus(okStatus);
                return true;
            }
            catch(Exception ex){Fail("COULD NOT UPDATE LOBBY",ex);return false;}
            finally{busy=false;RaiseChanged();}
        }

        public IReadOnlyList<MemberView> Members
        {
            get
            {
                memberBuffer.Clear();
                if(session==null)return memberBuffer;
                string localId=session.CurrentPlayer?.Id??"";
                foreach(IReadOnlyPlayer player in session.Players)
                {
                    if(player==null)continue;
                    memberBuffer.Add(new MemberView
                    {
                        PlayerId=player.Id,
                        DisplayName=ReadPlayer(player,PlayerNameKey,"TRAVELER"),
                        Hero=(HeroId)ReadIntPlayer(player,PlayerHeroKey,(int)HeroId.Veyr),
                        BattleItem=(BattleItemType)Mathf.Clamp(ReadIntPlayer(player,PlayerBattleItemKey,0),0,4),
                        Ready=ReadPlayer(player,PlayerReadyKey,"0")=="1",
                        IsHost=player.Id==session.Host,
                        IsLocal=player.Id==localId
                    });
                }
                memberBuffer.Sort((a,b)=>a.IsHost==b.IsHost?string.CompareOrdinal(a.PlayerId,b.PlayerId):(a.IsHost?-1:1));
                // Return an immutable snapshot. Lobby UI and validation may query Members again
                // while iterating; exposing the reusable buffer could mutate the caller's list.
                return memberBuffer.ToArray();
            }
        }

        public HeroId LocalHero
        {
            get
            {
                if(session?.CurrentPlayer==null)return PlayerHeroSelection.Selected;
                return (HeroId)ReadIntPlayer(session.CurrentPlayer,PlayerHeroKey,(int)PlayerHeroSelection.Selected);
            }
        }

        public bool LocalReady=>session?.CurrentPlayer!=null&&ReadPlayer(session.CurrentPlayer,PlayerReadyKey,"0")=="1";

        public ArenaVariant LobbyArena
        {
            get
            {
                int raw=ReadSessionInt(ArenaKey,(int)ArenaVariantRuntime.Selected);
                for(int i=0;i<ArenaShopCatalog.Items.Length;i++)
                    if((int)ArenaShopCatalog.Items[i].Variant==raw)return ArenaShopCatalog.Items[i].Variant;
                return ArenaVariantRuntime.Selected;
            }
        }

        public bool LobbyArenaShuffle=>ReadSession(ArenaShuffleKey,"0")=="1";

        public BotDifficulty LobbyDifficulty
        {
            get
            {
                int max=Enum.GetValues(typeof(BotDifficulty)).Length-1;
                return (BotDifficulty)Mathf.Clamp(ReadSessionInt(DifficultyKey,(int)BotDifficultySettings.Selected),0,max);
            }
        }

        public bool HasHeroConflict
        {
            get
            {
                IReadOnlyList<MemberView> members=Members;
                HashSet<HeroId> heroes=new();
                for(int i=0;i<members.Count;i++)if(!heroes.Add(members[i].Hero))return true;
                return false;
            }
        }

        public bool AllPresentPlayersReady
        {
            get
            {
                IReadOnlyList<MemberView> members=Members;
                if(members.Count<PlayerCount)return false;
                for(int i=0;i<members.Count;i++)if(!members[i].Ready)return false;
                return true;
            }
        }

        public bool CanStartLobby
        {
            get
            {
                if(session==null||!IsHost||!IsLobby||HasHeroConflict)return false;
                if(session.PlayerCount<MinHumanPlayers||session.PlayerCount>MaxHumanPlayers)return false;
                return AllPresentPlayersReady;
            }
        }

        public bool CanStageMatch=>CanStartLobby&&PlayerLinkReady;

        public bool TryBuildMatchPlan(out CoopMatchPlan plan)
        {
            plan=null;
            if(session==null||session.PlayerCount<MinHumanPlayers||session.PlayerCount>MaxHumanPlayers||HasHeroConflict)return false;
            IReadOnlyList<MemberView> members=Members;
            if(members.Count!=session.PlayerCount)return false;
            try
            {
                string matchId=ReadSession(MatchIdKey,session.Id);
                if(string.IsNullOrEmpty(matchId))matchId=session.Id;
                plan=CoopMatchPlan.Build(matchId,LobbyArena,LobbyDifficulty,members);
                return plan!=null;
            }
            catch(Exception ex)
            {
                Debug.LogError("DIMENSIONS FRONT CO-OP: could not build match plan.\n"+ex);
                return false;
            }
        }

        public async Task<bool> BeginMatchStagingAsync()
        {
            if(session==null||!IsHost||busy)return false;
            if(!CanStageMatch)
            {
                lastError=!PlayerLinkReady?"Relay link is not ready for every player.":"All present players must be connected, use different heroes, and be ready.";
                SetStatus("CO-OP MATCH NOT READY");
                RaiseChanged();
                return false;
            }
            if(!TryBuildMatchPlan(out _))
            {
                lastError="The co-op roster could not be built safely.";
                SetStatus("CO-OP ROSTER INVALID");
                RaiseChanged();
                return false;
            }

            try
            {
                busy=true;lastError="";
                IHostSession host=session.AsHost();
                host.IsLocked=true;
                ArenaVariant stagedArena=LobbyArenaShuffle
                    ?ArenaVariantRuntime.RandomOwnedArena(LobbyArena)
                    :LobbyArena;
                host.SetProperties(new Dictionary<string,SessionProperty>
                {
                    {ArenaKey,new SessionProperty(((int)stagedArena).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                    {MatchIdKey,new SessionProperty(Guid.NewGuid().ToString("N"),VisibilityPropertyOptions.Member)},
                    {PhaseKey,new SessionProperty("staging",VisibilityPropertyOptions.Member)}
                });
                await host.SavePropertiesAsync();
                SetStatus($"MATCH STAGING • {PlayerCount} PLAYERS CONNECTED");
                return true;
            }
            catch(Exception ex){Fail("COULD NOT PREPARE CO-OP MATCH",ex);return false;}
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> LaunchPreparedMatchAsync()
        {
            if(session==null||!IsHost||busy||!IsStaging)return false;
            if(!CoopPlanHandshake.PlanHandshakeReady)
            {
                lastError="Every connected player must verify the same match plan first.";
                SetStatus("WAITING FOR MATCH PLAN SYNC");
                RaiseChanged();
                return false;
            }
            if(!TryBuildMatchPlan(out CoopMatchPlan plan))
            {
                lastError="The co-op match plan is no longer valid.";
                SetStatus("CO-OP ROSTER INVALID");
                RaiseChanged();
                return false;
            }

            try
            {
                busy=true;lastError="";
                CoopAuthority.Prepare(plan);
                IHostSession host=session.AsHost();
                host.SetProperty(PhaseKey,new SessionProperty("launching",VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
                SetStatus("LAUNCHING CO-OP MATCH");
                return true;
            }
            catch(Exception ex){Fail("COULD NOT LAUNCH CO-OP MATCH",ex);return false;}
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> CancelMatchStagingAsync()
        {
            if(session==null||!IsHost||busy||!IsStaging)return false;
            try
            {
                busy=true;lastError="";
                IHostSession host=session.AsHost();
                host.IsLocked=false;
                host.SetProperty(PhaseKey,new SessionProperty("lobby",VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
                SetStatus("BACK IN CO-OP LOBBY");
                return true;
            }
            catch(Exception ex){Fail("COULD NOT RETURN TO LOBBY",ex);return false;}
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> EnterPostMatchAsync()
        {
            if(session==null||!IsHost)return false;
            try
            {
                IHostSession host=session.AsHost();
                host.IsLocked=true;
                host.SetProperty(PhaseKey,new SessionProperty("postmatch",VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
                SetStatus("MATCH COMPLETE • RETURNING TO CO-OP LOBBY");
                return true;
            }
            catch(Exception ex){Fail("COULD NOT FINALIZE CO-OP MATCH",ex);return false;}
        }

        public async Task<bool> FinishMatchAndReturnToLobbyAsync()
        {
            if(session==null)
            {
                CoopAuthority.EndMatch();
                return false;
            }
            if(busy)return false;

            try
            {
                busy=true;lastError="";
                if(session.CurrentPlayer!=null)
                {
                    session.CurrentPlayer.SetProperty(PlayerReadyKey,new PlayerProperty("0",VisibilityPropertyOptions.Member));
                    await session.SaveCurrentPlayerDataAsync();
                }

                if(IsHost)
                {
                    IHostSession host=session.AsHost();
                    host.IsLocked=false;
                    host.SetProperty(PhaseKey,new SessionProperty("lobby",VisibilityPropertyOptions.Member));
                    await host.SavePropertiesAsync();
                }

                CoopAuthority.EndMatch();
                SetStatus(IsHost?"CO-OP LOBBY READY":"WAITING IN CO-OP LOBBY");
                return true;
            }
            catch(Exception ex)
            {
                CoopAuthority.EndMatch();
                Fail("COULD NOT RESET CO-OP LOBBY",ex);
                return false;
            }
            finally{busy=false;RaiseChanged();}
        }

        public async Task<bool> ResumeRoundAsync(string matchId)
        {
            if (session == null || !IsHost || busy) return false;
            try
            {
                busy = true;
                IHostSession host = session.AsHost();
                host.IsLocked = true;
                host.SetProperties(new Dictionary<string, SessionProperty> {
                    { MatchIdKey, new SessionProperty(matchId, VisibilityPropertyOptions.Member) },
                    { PhaseKey, new SessionProperty("playing", VisibilityPropertyOptions.Member) }
                });
                await host.SavePropertiesAsync();
                SetStatus("NEXT CO-OP ROUND");
                return true;
            }
            catch (Exception ex) { Fail("COULD NOT START NEXT ROUND", ex); return false; }
            finally { busy = false; RaiseChanged(); }
        }

        private async Task AbortActiveMatchAsync(string reason)
        {
            if(session==null||!IsHost)return;
            try
            {
                IHostSession host=session.AsHost();
                host.IsLocked=false;
                host.SetProperty(PhaseKey,new SessionProperty("aborted",VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
            }
            catch(Exception ex){Debug.LogWarning("DIMENSIONS FRONT CO-OP: failed to publish aborted match state: "+ex.Message);}
            lastError=reason;
            status="CO-OP MATCH ABORTED";
            CoopAuthority.EndMatch();
            Alterverse.UI.MainMenuController.ShowCoopAfterMatch(reason);
            RaiseChanged();
        }

        public bool MatchNetworkingEnabled=>true;

        public void HandleTransportDisconnect(bool localConnectionLost)
        {
            if(session==null)return;

            if(IsHost)
            {
                if(IsStaging)
                {
                    _=CancelMatchStagingAsync();
                    status="RELAY CLIENT LOST • MATCH PREP CANCELLED";
                }
                else if((IsLaunching||CoopAuthority.Active)&&!IsPostMatch&&!IsAborted&&!NetworkRoundLoop.IsBetweenRounds)
                    _=AbortActiveMatchAsync("A co-op player lost the Relay connection.");
                RaiseChanged();
                return;
            }

            if(!localConnectionLost||(!CoopAuthority.Active&&!IsMatchPreparing))return;
            string reason="Connection to the co-op host was lost.";
            CoopAuthority.EndMatch();
            lastError=reason;
            status="CO-OP CONNECTION LOST";
            Alterverse.UI.MainMenuController.ShowCoopAfterMatch(reason);
            RaiseChanged();
            _=LeaveLobbyAsync();
        }

        private static Dictionary<string,PlayerProperty> BuildLocalPlayerProperties(bool ready)
        {
            string name=SanitizeDisplayName(PlayerProfile.PlayerName);
            return new Dictionary<string,PlayerProperty>
            {
                {PlayerNameKey,new PlayerProperty(name,VisibilityPropertyOptions.Member)},
                {PlayerHeroKey,new PlayerProperty(((int)PlayerHeroSelection.Selected).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                {PlayerBattleItemKey,new PlayerProperty(((int)BattleItemInventory.Equipped).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                {PlayerReadyKey,new PlayerProperty(ready?"1":"0",VisibilityPropertyOptions.Member)}
            };
        }

        private bool HeroUsedByOther(HeroId hero)
        {
            if(session==null)return false;
            string localId=session.CurrentPlayer?.Id??"";
            foreach(IReadOnlyPlayer p in session.Players)
                if(p!=null&&p.Id!=localId&&ReadIntPlayer(p,PlayerHeroKey,-1)==(int)hero)return true;
            return false;
        }

        private string ReadSession(string key,string fallback)
        {
            if(session!=null&&session.Properties.TryGetValue(key,out SessionProperty value)&&!string.IsNullOrEmpty(value.Value))
                return value.Value;
            return fallback;
        }

        private int ReadSessionInt(string key,int fallback)
        {
            string text=ReadSession(key,null);
            return text!=null&&int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int parsed)?parsed:fallback;
        }

        private static int ReadIntPlayer(IReadOnlyPlayer player,string key,int fallback)
        {
            string text=ReadPlayer(player,key,null);
            return text!=null&&int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int parsed)?parsed:fallback;
        }

        private static string ReadPlayer(IReadOnlyPlayer player,string key,string fallback)
        {
            if(player!=null&&player.Properties.TryGetValue(key,out PlayerProperty value)&&!string.IsNullOrEmpty(value.Value))
                return value.Value;
            return fallback;
        }

        private async Task<bool> EnsureServicesWhileBusyAsync()
        {
            try
            {
                if(!servicesInitialized)
                {
                    await UnityServices.InitializeAsync();
                    servicesInitialized=true;
                }
                if(Alterverse.Profile.AccountServerBootstrap.Instance!=null)
                {
                    if(!await Alterverse.Profile.AccountServerBootstrap.Instance.EnsureAuthenticatedAsync())return false;
                }
                else if(!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch(Exception ex){Fail("ONLINE SERVICE UNAVAILABLE",ex);return false;}
        }

        private void AttachSession(ISession value)
        {
            if(value==null)return;
            value.Changed+=OnSessionChanged;
            value.SessionPropertiesChanged+=OnSessionChanged;
            value.PlayerPropertiesChanged+=OnSessionChanged;
            value.PlayerJoined+=OnPlayerJoined;
            value.PlayerHasLeft+=OnPlayerLeft;
            value.RemovedFromSession+=OnRemovedFromSession;
            value.Deleted+=OnSessionDeleted;
            RefreshPreparedPlan();
            RaiseChanged();
        }

        private void DetachSession(ISession value)
        {
            if(value==null)return;
            value.Changed-=OnSessionChanged;
            value.SessionPropertiesChanged-=OnSessionChanged;
            value.PlayerPropertiesChanged-=OnSessionChanged;
            value.PlayerJoined-=OnPlayerJoined;
            value.PlayerHasLeft-=OnPlayerLeft;
            value.RemovedFromSession-=OnRemovedFromSession;
            value.Deleted-=OnSessionDeleted;
        }

        private void OnSessionChanged()
        {
            bool hadPreparedOrActive=CoopAuthority.PreparedPlan!=null||CoopAuthority.Active;
            bool aborted=IsAborted;
            RefreshPreparedPlan();
            if(aborted&&hadPreparedOrActive)
            {
                CoopAuthority.EndMatch();
                Alterverse.UI.MainMenuController.ShowCoopAfterMatch(
                    string.IsNullOrEmpty(lastError)?"A co-op player disconnected.":lastError);
            }
            RaiseChanged();
        }

        private void OnPlayerJoined(string _)
        {
            RefreshPreparedPlan();
            RaiseChanged();
        }

        private void OnPlayerLeft(string playerId)
        {
            int expected=CoopAuthority.ActivePlan?.HumanCount??CoopAuthority.PreparedPlan?.HumanCount??PlayerCount;
            RefreshPreparedPlan();

            if(IsHost&&IsStaging)
            {
                _=CancelMatchStagingAsync();
                status="PLAYER LEFT • MATCH PREP CANCELLED";
            }
            else if(IsHost&&!IsPostMatch&&!NetworkRoundLoop.IsBetweenRounds&&(IsLaunching||CoopAuthority.Active)&&PlayerCount<expected)
            {
                _=AbortActiveMatchAsync("A co-op player disconnected from the match.");
            }
            RaiseChanged();
        }

        private void RefreshPreparedPlan()
        {
            if(!IsMatchPreparing)
            {
                CoopAuthority.ClearPrepared();
                return;
            }
            if(TryBuildMatchPlan(out CoopMatchPlan plan))CoopAuthority.Prepare(plan);
        }

        private void OnRemovedFromSession()
        {
            bool wasMatch=CoopAuthority.Active;
            DetachSession(session);
            session=null;
            CoopAuthority.EndMatch();
            SetStatus("REMOVED FROM CO-OP LOBBY");
            if(wasMatch)Alterverse.UI.MainMenuController.ShowCoopAfterMatch("The co-op connection was lost.");
            RaiseChanged();
        }

        private void OnSessionDeleted()
        {
            bool wasMatch=CoopAuthority.Active;
            DetachSession(session);
            session=null;
            CoopAuthority.EndMatch();
            SetStatus("CO-OP LOBBY CLOSED");
            if(wasMatch)Alterverse.UI.MainMenuController.ShowCoopAfterMatch("The host closed the co-op lobby.");
            RaiseChanged();
        }

        private async Task LeaveExistingSessionInternalAsync()
        {
            ISession old=session;
            if(old==null)return;
            DetachSession(old);
            try
            {
                if(old.IsHost)await old.AsHost().DeleteAsync();
                else await old.LeaveAsync();
            }
            catch
            {
                AttachSession(old);
                throw; // Keep membership visible so the player can retry cleanup.
            }
            session=null;
            CoopAuthority.EndMatch();
            CoopNetworkBootstrap.ShutdownLocalNetwork();
        }

        private async Task SafeLeaveAfterFailureAsync()
        {
            try{await LeaveExistingSessionInternalAsync();}
            catch{}
        }

        private void Fail(string userMessage,Exception ex)
        {
            lastError=FriendlyError(ex);
            status=userMessage;
            Debug.LogError($"DIMENSIONS FRONT CO-OP: {userMessage}\n{ex}");
            RaiseChanged();
        }

        private void SetStatus(string value){status=value;RaiseChanged();}
        private void RaiseChanged()=>Changed?.Invoke();

        private static string FriendlyError(Exception ex)
        {
            if(ex is SessionException sessionException)
                return string.IsNullOrEmpty(sessionException.Message)?"Multiplayer session request failed.":sessionException.Message;
            return string.IsNullOrEmpty(ex?.Message)?"Unexpected online-service error.":ex.Message;
        }

        public static string NormalizeJoinCode(string raw)
        {
            if(string.IsNullOrWhiteSpace(raw))return "";
            char[] buffer=new char[raw.Length];int n=0;
            for(int i=0;i<raw.Length;i++)
            {
                char c=char.ToUpperInvariant(raw[i]);
                if(char.IsLetterOrDigit(c))buffer[n++]=c;
            }
            return new string(buffer,0,n);
        }

        private static string SanitizeDisplayName(string raw)
        {
            string value=string.IsNullOrWhiteSpace(raw)?"TRAVELER":raw.Trim();
            return value.Length<=PlayerProfile.MaxNameLength?value:value.Substring(0,PlayerProfile.MaxNameLength);
        }

        private static int Mod(int value,int count)
        {
            if(count<=0)return 0;
            int r=value%count;return r<0?r+count:r;
        }

        private async void OnApplicationQuit()
        {
            if(session==null)return;
            try
            {
                if(session.IsHost)await session.AsHost().DeleteAsync();
                else await session.LeaveAsync();
            }
            catch{}
        }

        private void OnDestroy()
        {
            DetachSession(session);
            if(Instance==this)Instance=null;
        }
    }
}


