using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
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
    /// Online 4v4 pre-match flow. The Relay session is provisioned for all eight match players,
    /// but starts private so up to four players can assemble by join code first. Only the party
    /// leader can expose the session to public search. The friend-party snapshot is persisted in
    /// session data so the deterministic match plan can keep that group on the same team.
    /// </summary>
    public sealed class OnlinePartyService : MonoBehaviour
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
            public bool IsPartyMember;
        }

        public const int MaxPartyPlayers = MatchSession.TeamSize;
        public const int MaxMatchPlayers = MatchSession.MaxPlayers;

        private const string SessionType = "df-online-party-v3";
        private const string SessionName = "DIMENSIONS FRONT ONLINE 4V4";
        private const string PlayerNameKey = "displayName";
        private const string PlayerHeroKey = "hero";
        private const string PlayerBattleItemKey = "battleItem";
        private const string PlayerReadyKey = "ready";
        private const string SearchKey = "playerSearch";
        private const string MatchmakingKey = "edgegapSearch";
        private const string ModeKey = "mode";
        private const string ArenaKey = "arena";
        private const string PartyIdsKey = "partyIds";

        public static OnlinePartyService Instance { get; private set; }

        private ISession session;
        private bool servicesInitialized;
        private bool busy;
        private string status = "ONLINE PARTY READY";
        private string lastError = "";
        private readonly List<MemberView> memberBuffer = new(MaxMatchPlayers);

        public event Action Changed;

        public bool InParty => session != null;
        public bool Busy => busy;
        public bool IsHost => session != null && session.IsHost;
        public string JoinCode => session?.Code ?? "";
        public int PlayerCount => session?.PlayerCount ?? 0;
        public int MaxPlayers => session?.MaxPlayers ?? MaxMatchPlayers;
        public int PartyCapacity => MaxPartyPlayers;
        public int MatchCapacity => MaxMatchPlayers;
        public string Status => status;
        public string LastError => lastError;
        public bool MatchmakingLocked => ReadSession(MatchmakingKey, "0") == "1";
        public bool CanEditParty => InParty && !Busy && !CoopAuthority.Active && !MatchmakingLocked && !IsSearching &&
            EdgegapOnlineFlow.Instance?.Busy != true && EdgegapOnlineFlow.Instance?.IsSearching != true && !OnlinePlanHandshake.Preparing;
        public bool IsSearching => ReadSession(SearchKey, "0") == "1";
        public bool IsFull => PlayerCount >= MaxMatchPlayers;
        public bool IsPartyFull => PartyPlayerCount >= MaxPartyPlayers;
        public bool LocalReady => session?.CurrentPlayer != null && ReadPlayer(session.CurrentPlayer, PlayerReadyKey, "0") == "1";
        public bool PartyReady => PlayerCount > 0 && PlayerCount <= MaxMatchPlayers &&
            PartyPlayerCount > 0 && PartyPlayerCount <= MaxPartyPlayers && !HasHeroConflict && AllPresentPlayersReady;
        public bool CanStartMatch => IsHost && !Busy && PartyReady;

        public int PartyPlayerCount
        {
            get
            {
                int count = 0;
                IReadOnlyList<MemberView> members = Members;
                for (int i = 0; i < members.Count; i++)
                    if (members[i].IsPartyMember) count++;
                return count;
            }
        }

        public int PublicPlayerCount => Mathf.Max(0, PlayerCount - PartyPlayerCount);

        public string ReadyRosterKey
        {
            get
            {
                var key = new StringBuilder();
                foreach (MemberView member in Members)
                {
                    string id = member.PlayerId ?? "";
                    key.Append(id.Length).Append(':').Append(id).Append('|')
                        .Append((int)member.Hero).Append('|').Append((int)member.BattleItem).Append('|')
                        .Append(member.Ready ? '1' : '0').Append(';');
                }
                return key.ToString();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (Instance == null)
                new GameObject("DF_OnlinePartyService").AddComponent<OnlinePartyService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public async Task<bool> CreatePrivatePartyAsync()
        {
            if (busy || EdgegapOnlineFlow.Instance?.Busy == true || EdgegapOnlineFlow.Instance?.IsSearching == true ||
                RankedService.Instance?.Busy == true || RankedService.Instance?.Searching == true) return false;
            if (CoopLobbyService.Instance != null && (CoopLobbyService.Instance.InLobby || CoopLobbyService.Instance.Busy))
                return RejectForCoop("Leave the co-op lobby before creating an online party.");

            try
            {
                busy = true;
                lastError = "";
                SetStatus("CREATING PRIVATE ONLINE PARTY");
                if (!await EnsureServicesAsync()) return false;
                await LeaveInternalAsync();

                var options = new SessionOptions
                {
                    Type = SessionType,
                    Name = SessionName,
                    // Relay capacity must be allocated for the final 4v4 from the beginning.
                    // Privacy and our friend-party checks keep the pre-search group at four.
                    MaxPlayers = MaxMatchPlayers,
                    IsPrivate = true,
                    IsLocked = false,
                    PlayerProperties = BuildLocalPlayerProperties(false),
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { SearchKey, new SessionProperty("0", VisibilityPropertyOptions.Public) },
                        { ModeKey, new SessionProperty("online-casual", VisibilityPropertyOptions.Public) },
                        { ArenaKey, new SessionProperty(((int)ArenaVariantRuntime.Selected).ToString(CultureInfo.InvariantCulture), VisibilityPropertyOptions.Member) }
                    }
                }.WithRelayNetwork();

                session = await MultiplayerService.Instance.CreateSessionAsync(options);
                AttachSession(session);
                SetStatus("PRIVATE PARTY CREATED • SHARE THE CODE");
                return true;
            }
            catch (Exception ex)
            {
                await SafeLeaveAfterFailureAsync();
                Fail("COULD NOT CREATE ONLINE PARTY", ex);
                return false;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public async Task<bool> JoinByCodeAsync(string rawCode)
        {
            if (busy || EdgegapOnlineFlow.Instance?.Busy == true || EdgegapOnlineFlow.Instance?.IsSearching == true ||
                RankedService.Instance?.Busy == true || RankedService.Instance?.Searching == true) return false;
            if (CoopLobbyService.Instance != null && (CoopLobbyService.Instance.InLobby || CoopLobbyService.Instance.Busy))
                return RejectForCoop("Leave the co-op lobby before joining an online party.");

            string code = NormalizeJoinCode(rawCode);
            if (code.Length < 4)
            {
                lastError = "Enter a valid party code.";
                SetStatus("INVALID PARTY CODE");
                return false;
            }

            try
            {
                busy = true;
                lastError = "";
                SetStatus("JOINING FRIEND PARTY");
                if (!await EnsureServicesAsync()) return false;
                await LeaveInternalAsync();

                ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, BuildJoinOptions());
                session = joined;
                AttachSession(session);
                if (joined.MaxPlayers != MaxMatchPlayers || joined.PlayerCount > MaxMatchPlayers)
                {
                    await LeaveInternalAsync();
                    lastError = "This room uses a different game version. Create a new 4v4 party.";
                    SetStatus("INCOMPATIBLE ONLINE PARTY");
                    return false;
                }
                if (MatchmakingLocked)
                {
                    await LeaveInternalAsync();
                    lastError = "This party is already matchmaking. Ask the leader to cancel first.";
                    SetStatus("PARTY IS ALREADY SEARCHING");
                    return false;
                }
                bool searchAlreadyActive = ReadSession(joined, SearchKey, "0") == "1";

                // Before public search the private friend party is intentionally limited to one team.
                // The Relay allocation itself is larger because it must later carry both teams.
                if (!searchAlreadyActive && joined.PlayerCount > MaxPartyPlayers)
                {
                    await LeaveInternalAsync();
                    lastError = "The private friend party already has four players.";
                    SetStatus("FRIEND PARTY FULL");
                    return false;
                }

                SetStatus(searchAlreadyActive
                    ? "JOINED ONLINE MATCH LOBBY • PLAYER SEARCH ACTIVE"
                    : "JOINED PRIVATE FRIEND PARTY");
                return true;
            }
            catch (Exception ex)
            {
                await SafeLeaveAfterFailureAsync();
                Fail("COULD NOT JOIN ONLINE PARTY", ex);
                return false;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        /// <summary>
        /// Solo quick-search joins an already public online lobby. If none is available,
        /// the player creates a private lobby and immediately exposes it to public search.
        /// </summary>
        public async Task<bool> QuickSearchAsync()
        {
            if (busy || EdgegapOnlineFlow.Instance?.Busy == true || EdgegapOnlineFlow.Instance?.IsSearching == true ||
                RankedService.Instance?.Busy == true || RankedService.Instance?.Searching == true) return false;
            if (CoopLobbyService.Instance != null && (CoopLobbyService.Instance.InLobby || CoopLobbyService.Instance.Busy))
                return RejectForCoop("Leave the co-op lobby before searching online.");

            bool found = false;
            try
            {
                busy = true;
                lastError = "";
                SetStatus("SEARCHING FOR ONLINE MATCH");
                if (!await EnsureServicesAsync()) return false;
                await LeaveInternalAsync();

                QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(
                    new QuerySessionsOptions { Count = 25 });

                ISessionInfo target = FindJoinableMatch(results);
                if (target != null)
                {
                    session = await MultiplayerService.Instance.JoinSessionByIdAsync(target.Id, BuildJoinOptions());
                    AttachSession(session);
                    SetStatus("PLAYER FOUND • JOINED ONLINE MATCH LOBBY");
                    found = true;
                }
            }
            catch (Exception ex)
            {
                await SafeLeaveAfterFailureAsync();
                Fail("PLAYER SEARCH FAILED", ex);
                return false;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }

            if (found) return true;
            if (!await CreatePrivatePartyAsync()) return false;
            return await BeginPlayerSearchAsync();
        }

        public async Task<bool> BeginPlayerSearchAsync()
        {
            if (session == null || !IsHost || busy || IsFull ||
                PartyPlayerCount < 1 || PartyPlayerCount > MaxPartyPlayers) return false;

            try
            {
                busy = true;
                lastError = "";

                // Capacity is fixed by SessionOptions at creation; ISession.MaxPlayers is read-only.
                IHostSession host = session.AsHost();
                string partyIds = PlayerCount <= MaxPartyPlayers
                    ? EncodeCurrentPlayers()
                    : ReadSession(PartyIdsKey, "");

                if (string.IsNullOrEmpty(partyIds))
                    partyIds = EncodeCurrentPlayers(MaxPartyPlayers);

                host.IsPrivate = false;
                host.SetProperty(PartyIdsKey, new SessionProperty(partyIds, VisibilityPropertyOptions.Member));
                host.SetProperty(SearchKey, new SessionProperty("1", VisibilityPropertyOptions.Public));
                host.SetProperty(ArenaKey, new SessionProperty(
                    ((int)ArenaVariantRuntime.Selected).ToString(CultureInfo.InvariantCulture),
                    VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();

                SetStatus("SPIELERSUCHE AKTIV • FREUNDESPARTY BLEIBT ZUSAMMEN");
                return true;
            }
            catch (Exception ex)
            {
                Fail("COULD NOT START PLAYER SEARCH", ex);
                return false;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public async Task<bool> CancelPlayerSearchAsync()
        {
            if (session == null || !IsHost || busy) return false;
            try
            {
                busy = true;
                lastError = "";
                await SetSearchVisibilityAsync(false);
                SetStatus("PLAYER SEARCH STOPPED • LOBBY PRIVATE");
                return true;
            }
            catch (Exception ex)
            {
                Fail("COULD NOT STOP PLAYER SEARCH", ex);
                return false;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public async Task LeavePartyAsync()
        {
            if (busy) return;
            try
            {
                busy = true;
                lastError = "";
                await LeaveInternalAsync();
                SetStatus("ONLINE PARTY READY");
            }
            catch (Exception ex)
            {
                Fail("COULD NOT LEAVE ONLINE PARTY", ex);
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public async Task<bool> SetMatchmakingLockedAsync(bool locked)
        {
            if (session == null || !IsHost || busy) return false;
            try
            {
                busy = true;
                IHostSession host = session.AsHost();
                host.IsLocked = locked;
                host.SetProperty(MatchmakingKey, new SessionProperty(locked ? "1" : "0", VisibilityPropertyOptions.Member));
                await host.SavePropertiesAsync();
                return true;
            }
            catch (Exception ex) { Fail("COULD NOT UPDATE PARTY SEARCH STATE", ex); return false; }
            finally { busy = false; RaiseChanged(); }
        }

        public async Task<bool> CycleLocalHeroAsync(int direction)
        {
            if (!CanEditParty) return false;
            HeroId current = LocalHero;
            int count = Enum.GetValues(typeof(HeroId)).Length;
            for (int step = 1; step <= count; step++)
            {
                int next = Mod((int)current + Math.Sign(direction) * step, count);
                HeroId candidate = (HeroId)next;
                if (HeroUnlockInventory.Owned(candidate) && !HeroBlockedForLocal(candidate))
                    return await SetLocalHeroAsync(candidate);
            }
            return false;
        }

        public async Task<bool> SetLocalHeroAsync(HeroId hero)
        {
            if (session?.CurrentPlayer == null || !CanEditParty || !HeroUnlockInventory.Owned(hero) || HeroBlockedForLocal(hero))
                return false;

            try
            {
                busy = true;
                lastError = "";
                session.CurrentPlayer.SetProperties(new Dictionary<string, PlayerProperty>
                {
                    { PlayerHeroKey, new PlayerProperty(((int)hero).ToString(CultureInfo.InvariantCulture), VisibilityPropertyOptions.Member) },
                    { PlayerReadyKey, new PlayerProperty("0", VisibilityPropertyOptions.Member) }
                });
                await session.SaveCurrentPlayerDataAsync();
                PlayerHeroSelection.Select(hero);
                SetStatus("HERO SELECTED • READY RESET");
                return true;
            }
            catch (Exception ex)
            {
                Fail("COULD NOT UPDATE HERO", ex);
                return false;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public async Task<bool> SetLocalReadyAsync(bool ready)
        {
            if (session?.CurrentPlayer == null || !CanEditParty) return false;
            if (ready && HeroBlockedForLocal(LocalHero))
            {
                lastError = "Friend-party members on the same team need different heroes.";
                SetStatus("CHOOSE A DIFFERENT HERO");
                return false;
            }

            try
            {
                busy = true;
                lastError = "";
                session.CurrentPlayer.SetProperties(new Dictionary<string, PlayerProperty>
                {
                    { PlayerBattleItemKey, new PlayerProperty(((int)BattleItemInventory.Equipped).ToString(CultureInfo.InvariantCulture), VisibilityPropertyOptions.Member) },
                    { PlayerReadyKey, new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member) }
                });
                await session.SaveCurrentPlayerDataAsync();
                SetStatus(ready ? "READY • WAITING FOR MATCH" : "NOT READY");
                return true;
            }
            catch (Exception ex)
            {
                Fail("COULD NOT UPDATE READY STATE", ex);
                return false;
            }
            finally
            {
                busy = false;
                RaiseChanged();
            }
        }

        public IReadOnlyList<MemberView> Members
        {
            get
            {
                memberBuffer.Clear();
                if (session == null) return memberBuffer;

                string localId = session.CurrentPlayer?.Id ?? "";
                foreach (IReadOnlyPlayer player in session.Players)
                {
                    if (player == null) continue;
                    memberBuffer.Add(new MemberView
                    {
                        PlayerId = player.Id,
                        DisplayName = SafeIdentityCatalog.DisplayPlayerName(ReadPlayer(player, PlayerNameKey, "TRAVELER"),player.Id),
                        Hero = (HeroId)ReadIntPlayer(player, PlayerHeroKey, (int)HeroId.Veyr),
                        BattleItem = (BattleItemType)Mathf.Clamp(ReadIntPlayer(player, PlayerBattleItemKey, 0), 0, 4),
                        Ready = ReadPlayer(player, PlayerReadyKey, "0") == "1",
                        IsHost = player.Id == session.Host,
                        IsLocal = player.Id == localId,
                        IsPartyMember = IsPartyMemberId(player.Id)
                    });
                }

                memberBuffer.Sort(CompareMembers);
                return memberBuffer.ToArray();
            }
        }

        public HeroId LocalHero
        {
            get
            {
                if (session?.CurrentPlayer == null) return PlayerHeroSelection.Selected;
                return (HeroId)ReadIntPlayer(session.CurrentPlayer, PlayerHeroKey, (int)PlayerHeroSelection.Selected);
            }
        }

        public bool HasHeroConflict
        {
            get
            {
                IReadOnlyList<MemberView> members = Members;
                HashSet<HeroId> heroes = new();
                for (int i = 0; i < members.Count; i++)
                {
                    MemberView member = members[i];
                    if (!member.IsPartyMember) continue;
                    if (!heroes.Add(member.Hero)) return true;
                }
                return false;
            }
        }

        public bool AllPresentPlayersReady
        {
            get
            {
                IReadOnlyList<MemberView> members = Members;
                if (members.Count != PlayerCount || members.Count == 0) return false;
                for (int i = 0; i < members.Count; i++)
                    if (!members[i].Ready) return false;
                return true;
            }
        }

        private async Task<bool> EnsureServicesAsync()
        {
            try
            {
                if (!servicesInitialized)
                {
                    await UnityServices.InitializeAsync();
                    servicesInitialized = true;
                }

                if(Alterverse.Profile.AccountServerBootstrap.Instance!=null)
                {
                    if(!await Alterverse.Profile.AccountServerBootstrap.Instance.EnsureAuthenticatedAsync())return false;
                }
                else if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                return true;
            }
            catch (Exception ex)
            {
                Fail("ONLINE SERVICE UNAVAILABLE", ex);
                return false;
            }
        }

        private static JoinSessionOptions BuildJoinOptions()
        {
            return new JoinSessionOptions
            {
                Type = SessionType,
                PlayerProperties = BuildLocalPlayerProperties(false)
            };
        }

        private static Dictionary<string, PlayerProperty> BuildLocalPlayerProperties(bool ready)
        {
            return new Dictionary<string, PlayerProperty>
            {
                { PlayerNameKey, new PlayerProperty(SanitizeDisplayName(PlayerProfile.PlayerName), VisibilityPropertyOptions.Member) },
                { PlayerHeroKey, new PlayerProperty(((int)PlayerHeroSelection.Selected).ToString(CultureInfo.InvariantCulture), VisibilityPropertyOptions.Member) },
                { PlayerBattleItemKey, new PlayerProperty(((int)BattleItemInventory.Equipped).ToString(CultureInfo.InvariantCulture), VisibilityPropertyOptions.Member) },
                { PlayerReadyKey, new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member) }
            };
        }

        private static ISessionInfo FindJoinableMatch(QuerySessionsResults results)
        {
            if (results?.Sessions == null) return null;
            for (int i = 0; i < results.Sessions.Count; i++)
            {
                ISessionInfo candidate = results.Sessions[i];
                if (candidate == null || candidate.IsLocked || candidate.AvailableSlots <= 0) continue;
                if (candidate.MaxPlayers != MaxMatchPlayers) continue;
                if (!string.Equals(candidate.Name, SessionName, StringComparison.Ordinal)) continue;
                return candidate;
            }
            return null;
        }

        private async Task SetSearchVisibilityAsync(bool searchable)
        {
            if (session == null || !session.IsHost) return;
            IHostSession host = session.AsHost();
            host.IsPrivate = !searchable;
            host.SetProperty(SearchKey,
                new SessionProperty(searchable ? "1" : "0", VisibilityPropertyOptions.Public));
            await host.SavePropertiesAsync();
        }

        private async Task CloseSearchWhenFullAsync()
        {
            if (session == null || !IsHost || !IsSearching || PlayerCount < MaxMatchPlayers) return;
            try
            {
                await SetSearchVisibilityAsync(false);
                SetStatus("MATCH LOBBY FULL • PLAYER SEARCH CLOSED");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("DIMENSIONS FRONT ONLINE: could not close full match search: " + ex.Message);
            }
            RaiseChanged();
        }

        private void AttachSession(ISession value)
        {
            if (value == null) return;
            value.Changed += OnSessionChanged;
            value.SessionPropertiesChanged += OnSessionChanged;
            value.PlayerPropertiesChanged += OnSessionChanged;
            value.PlayerJoined += OnPlayerJoined;
            value.PlayerHasLeft += OnPlayerLeft;
            value.RemovedFromSession += OnRemovedFromSession;
            value.Deleted += OnSessionDeleted;
            RaiseChanged();
        }

        private void DetachSession(ISession value)
        {
            if (value == null) return;
            value.Changed -= OnSessionChanged;
            value.SessionPropertiesChanged -= OnSessionChanged;
            value.PlayerPropertiesChanged -= OnSessionChanged;
            value.PlayerJoined -= OnPlayerJoined;
            value.PlayerHasLeft -= OnPlayerLeft;
            value.RemovedFromSession -= OnRemovedFromSession;
            value.Deleted -= OnSessionDeleted;
        }

        private void OnSessionChanged()
        {
            RaiseChanged();
        }

        private void OnPlayerJoined(string playerId)
        {
            if (IsHost && IsSearching && PlayerCount >= MaxMatchPlayers)
                _ = CloseSearchWhenFullAsync();
            RaiseChanged();
        }

        private void OnPlayerLeft(string playerId)
        {
            if (IsHost)
                status = IsSearching
                    ? "PLAYER LEFT • SEARCHING FOR REPLACEMENT"
                    : "PLAYER LEFT • LOBBY PRIVATE";
            RaiseChanged();
        }

        private void OnRemovedFromSession()
        {
            DetachSession(session);
            session = null;
            SetStatus("REMOVED FROM ONLINE PARTY");
        }

        private void OnSessionDeleted()
        {
            DetachSession(session);
            session = null;
            SetStatus("ONLINE PARTY CLOSED");
        }

        private async Task LeaveInternalAsync()
        {
            ISession old = session;
            if (old == null) return;
            DetachSession(old);
            try
            {
                if (old.IsHost) await old.AsHost().DeleteAsync();
                else await old.LeaveAsync();
            }
            catch
            {
                AttachSession(old);
                throw; // Do not report a successful leave while membership remains.
            }
            session = null;
        }

        private async Task SafeLeaveAfterFailureAsync()
        {
            try { await LeaveInternalAsync(); }
            catch { }
        }

        private bool HeroBlockedForLocal(HeroId hero)
        {
            if (session?.CurrentPlayer == null || !IsPartyMemberId(session.CurrentPlayer.Id)) return false;
            string localId = session.CurrentPlayer.Id;
            foreach (IReadOnlyPlayer player in session.Players)
            {
                if (player == null || player.Id == localId || !IsPartyMemberId(player.Id)) continue;
                if (ReadIntPlayer(player, PlayerHeroKey, -1) == (int)hero) return true;
            }
            return false;
        }

        private bool IsPartyMemberId(string playerId)
        {
            if (session == null || string.IsNullOrEmpty(playerId)) return false;

            // While a private group contains no more than one team, everyone currently in it is
            // part of the friend party. On public search we switch to the persisted snapshot.
            if (!IsSearching && PlayerCount <= MaxPartyPlayers) return true;

            string encoded = ReadSession(PartyIdsKey, "");
            if (string.IsNullOrEmpty(encoded)) return false;
            string[] ids = encoded.Split('|');
            for (int i = 0; i < ids.Length; i++)
                if (string.Equals(ids[i], playerId, StringComparison.Ordinal)) return true;
            return false;
        }

        private string EncodeCurrentPlayers(int limit = MaxPartyPlayers)
        {
            if (session == null) return "";
            List<string> ids = new(Mathf.Min(limit, session.PlayerCount));
            foreach (IReadOnlyPlayer player in session.Players)
            {
                if (player == null || string.IsNullOrEmpty(player.Id)) continue;
                ids.Add(player.Id);
            }
            ids.Sort(StringComparer.Ordinal);
            if (ids.Count > limit) ids.RemoveRange(limit, ids.Count - limit);
            return string.Join("|", ids);
        }

        private static int CompareMembers(MemberView a, MemberView b)
        {
            if (a.IsHost != b.IsHost) return a.IsHost ? -1 : 1;
            if (a.IsPartyMember != b.IsPartyMember) return a.IsPartyMember ? -1 : 1;
            return string.CompareOrdinal(a.PlayerId, b.PlayerId);
        }

        private string ReadSession(string key, string fallback) => ReadSession(session, key, fallback);

        private static string ReadSession(ISession source, string key, string fallback)
        {
            if (source != null && source.Properties.TryGetValue(key, out SessionProperty value) && !string.IsNullOrEmpty(value.Value))
                return value.Value;
            return fallback;
        }

        private static int ReadIntPlayer(IReadOnlyPlayer player, string key, int fallback)
        {
            string text = ReadPlayer(player, key, null);
            return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        private static string ReadPlayer(IReadOnlyPlayer player, string key, string fallback)
        {
            if (player != null && player.Properties.TryGetValue(key, out PlayerProperty value) && !string.IsNullOrEmpty(value.Value))
                return value.Value;
            return fallback;
        }

        private static int Mod(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static string SanitizeDisplayName(string value) => SafeIdentityCatalog.DisplayPlayerName(value);

        public static string NormalizeJoinCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            char[] chars = value.Trim().ToUpperInvariant().ToCharArray();
            var cleaned = new StringBuilder(chars.Length);
            for (int i = 0; i < chars.Length; i++)
                if (char.IsLetterOrDigit(chars[i])) cleaned.Append(chars[i]);
            return cleaned.ToString();
        }

        private bool RejectForCoop(string message)
        {
            lastError = message;
            SetStatus("CO-OP LOBBY STILL ACTIVE");
            return false;
        }

        private void SetStatus(string value)
        {
            status = value ?? "";
            RaiseChanged();
        }

        private void Fail(string headline, Exception ex)
        {
            lastError = ex?.Message ?? "Unknown online error.";
            status = headline;
            Debug.LogWarning("DIMENSIONS FRONT ONLINE: " + headline + " • " + lastError);
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}


