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
    /// Phase-one private co-op lobby owner.
    /// This intentionally stops at lobby/session state. Gameplay networking is enabled in a later phase
    /// so the existing offline match cannot accidentally execute on two machines at once.
    /// </summary>
    public sealed class CoopLobbyService : MonoBehaviour, IMultiplayerProvider
    {
        public sealed class MemberView
        {
            public string PlayerId;
            public string DisplayName;
            public HeroId Hero;
            public bool Ready;
            public bool IsHost;
            public bool IsLocal;
        }

        private const string SessionType="df-private-coop-v1";
        private const string PlayerNameKey="displayName";
        private const string PlayerHeroKey="hero";
        private const string PlayerReadyKey="ready";
        private const string ArenaKey="arena";
        private const string DifficultyKey="difficulty";
        private const string PhaseKey="phase";

        public static CoopLobbyService Instance { get; private set; }

        private ISession session;
        private bool servicesInitialized,busy;
        private string status="CO-OP READY";
        private string lastError="";
        private readonly List<MemberView> memberBuffer=new(2);

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
        public int MaxPlayers=>session?.MaxPlayers??2;
        public bool RelayConnected=>InLobby&&CoopNetworkBootstrap.IsListening;

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
                if(!AuthenticationService.Instance.IsSignedIn)
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
            try
            {
                busy=true;lastError="";SetStatus("CREATING PRIVATE CO-OP LOBBY");
                if(!await EnsureServicesWhileBusyAsync())return false;
                await LeaveExistingSessionInternalAsync();

                var options=new SessionOptions
                {
                    Type=SessionType,
                    Name="DIMENSIONS FRONT CO-OP",
                    MaxPlayers=2,
                    IsPrivate=true,
                    IsLocked=false,
                    PlayerProperties=BuildLocalPlayerProperties(false),
                    SessionProperties=new Dictionary<string,SessionProperty>
                    {
                        {ArenaKey,new SessionProperty(((int)ArenaVariantRuntime.Selected).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                        {DifficultyKey,new SessionProperty(((int)BotDifficultySettings.Selected).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
                        {PhaseKey,new SessionProperty("lobby",VisibilityPropertyOptions.Member)}
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
                if(!HeroUsedByOther(candidate))
                    return await SetLocalHeroAsync(candidate);
            }
            return false;
        }

        public async Task<bool> SetLocalHeroAsync(HeroId hero)
        {
            if(session==null||busy||HeroUsedByOther(hero))return false;
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
                lastError="Both players must use different heroes.";
                SetStatus("CHOOSE A DIFFERENT HERO");
                RaiseChanged();
                return false;
            }

            try
            {
                busy=true;lastError="";
                session.CurrentPlayer.SetProperty(PlayerReadyKey,new PlayerProperty(ready?"1":"0",VisibilityPropertyOptions.Member));
                await session.SaveCurrentPlayerDataAsync();
                SetStatus(ready?"READY • WAITING FOR TEAM":"NOT READY");
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
                if(ArenaUnlockInventory.Owned(candidate))
                    return await SetHostSettingAsync(ArenaKey,((int)candidate).ToString(CultureInfo.InvariantCulture),
                        $"ARENA • {items[index].Name}");
            }
            return false;
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
                        Ready=ReadPlayer(player,PlayerReadyKey,"0")=="1",
                        IsHost=player.Id==session.Host,
                        IsLocal=player.Id==localId
                    });
                }
                memberBuffer.Sort((a,b)=>a.IsHost==b.IsHost?string.CompareOrdinal(a.PlayerId,b.PlayerId):(a.IsHost?-1:1));
                return memberBuffer;
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
                if(raw<0||raw>=ArenaShopCatalog.Items.Length)return ArenaVariantRuntime.Selected;
                return (ArenaVariant)raw;
            }
        }

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
                return members.Count>=2&&members[0].Hero==members[1].Hero;
            }
        }

        public bool CanStartLobby
        {
            get
            {
                if(session==null||!IsHost||session.PlayerCount!=2||HasHeroConflict)return false;
                IReadOnlyList<MemberView> members=Members;
                return members.Count==2&&members[0].Ready&&members[1].Ready;
            }
        }

        /// <summary>
        /// Deliberately false until the gameplay authority/network phase is implemented.
        /// Prevents accidentally launching the current local-only match on both devices.
        /// </summary>
        public bool MatchNetworkingEnabled=>false;

        private static Dictionary<string,PlayerProperty> BuildLocalPlayerProperties(bool ready)
        {
            string name=SanitizeDisplayName(PlayerProfile.PlayerName);
            return new Dictionary<string,PlayerProperty>
            {
                {PlayerNameKey,new PlayerProperty(name,VisibilityPropertyOptions.Member)},
                {PlayerHeroKey,new PlayerProperty(((int)PlayerHeroSelection.Selected).ToString(CultureInfo.InvariantCulture),VisibilityPropertyOptions.Member)},
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

        private int ReadSessionInt(string key,int fallback)
        {
            if(session!=null&&session.Properties.TryGetValue(key,out SessionProperty value)&&
               int.TryParse(value.Value,NumberStyles.Integer,CultureInfo.InvariantCulture,out int parsed))return parsed;
            return fallback;
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
                if(!AuthenticationService.Instance.IsSignedIn)
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
            value.PlayerJoined+=OnPlayerChanged;
            value.PlayerHasLeft+=OnPlayerChanged;
            value.RemovedFromSession+=OnRemovedFromSession;
            value.Deleted+=OnSessionDeleted;
            RaiseChanged();
        }

        private void DetachSession(ISession value)
        {
            if(value==null)return;
            value.Changed-=OnSessionChanged;
            value.SessionPropertiesChanged-=OnSessionChanged;
            value.PlayerPropertiesChanged-=OnSessionChanged;
            value.PlayerJoined-=OnPlayerChanged;
            value.PlayerHasLeft-=OnPlayerChanged;
            value.RemovedFromSession-=OnRemovedFromSession;
            value.Deleted-=OnSessionDeleted;
        }

        private void OnSessionChanged(){RaiseChanged();}
        private void OnPlayerChanged(string _){RaiseChanged();}

        private void OnRemovedFromSession()
        {
            DetachSession(session);
            session=null;
            SetStatus("REMOVED FROM CO-OP LOBBY");
            RaiseChanged();
        }

        private void OnSessionDeleted()
        {
            DetachSession(session);
            session=null;
            SetStatus("CO-OP LOBBY CLOSED");
            RaiseChanged();
        }

        private async Task LeaveExistingSessionInternalAsync()
        {
            ISession old=session;
            if(old==null)return;
            DetachSession(old);
            session=null;
            try
            {
                if(old.IsHost)await old.AsHost().DeleteAsync();
                else await old.LeaveAsync();
            }
            catch(Exception ex){Debug.LogWarning("DIMENSIONS FRONT: lobby leave cleanup failed: "+ex.Message);}
        }

        private async Task SafeLeaveAfterFailureAsync()
        {
            try{await LeaveExistingSessionInternalAsync();}
            catch{session=null;}
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
            try{await session.LeaveAsync();}
            catch{}
        }

        private void OnDestroy()
        {
            DetachSession(session);
            if(Instance==this)Instance=null;
        }
    }
}
