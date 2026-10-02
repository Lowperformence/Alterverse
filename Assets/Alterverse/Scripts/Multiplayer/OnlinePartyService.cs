using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Alterverse.Characters;
using Alterverse.Profile;
using Alterverse.Shop;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Online 5v5 pre-match party flow.
    /// Friends join the private party by code first. Only the party host can explicitly
    /// open the party to public player search. This deliberately stays separate from the
    /// co-op match-plan logic so online players can never accidentally launch the co-op
    /// bot roster while sharing the same UGS Session/Relay architecture.
    /// </summary>
    public sealed class OnlinePartyService : MonoBehaviour
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

        public const int MaxPartyPlayers = 5;
        private const string SessionType = "df-online-party-v1";
        private const string SessionName = "DIMENSIONS FRONT ONLINE PARTY";
        private const string PlayerNameKey = "displayName";
        private const string PlayerHeroKey = "hero";
        private const string PlayerReadyKey = "ready";
        private const string SearchKey = "playerSearch";
        private const string ModeKey = "mode";
        private const string ArenaKey = "arena";

        public static OnlinePartyService Instance { get; private set; }

        private ISession session;
        private bool busy;
        private string status = "ONLINE PARTY READY";
        private string lastError = "";
        private readonly List<MemberView> memberBuffer = new(MaxPartyPlayers);

        public event Action Changed;

        public bool InParty => session != null;
        public bool Busy => busy;
        public bool IsHost => session != null && session.IsHost;
        public string JoinCode => session?.Code ?? "";
        public int PlayerCount => session?.PlayerCount ?? 0;
        public int MaxPlayers => session?.MaxPlayers ?? MaxPartyPlayers;
        public string Status => status;
        public string LastError => lastError;
        public bool IsSearching => ReadSession(SearchKey, "0") == "1";
        public bool IsFull => PlayerCount >= MaxPartyPlayers;
        public bool LocalReady => session?.CurrentPlayer != null && ReadPlayer(session.CurrentPlayer, PlayerReadyKey, "0") == "1";
        public bool PartyReady => IsFull && !HasHeroConflict && AllPresentPlayersReady;

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
            if (busy) return false;
            if (CoopLobbyService.Instance != null && CoopLobbyService.Instance.InLobby)
            {
                lastError = "Leave the co-op lobby before creating an online party.";
                SetStatus("CO-OP LOBBY STILL ACTIVE");
                return false;
            }

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
                    MaxPlayers = MaxPartyPlayers,
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
            if (busy) return false;
            if (CoopLobbyService.Instance != null && CoopLobbyService.Instance.InLobby)
            {
                lastError = "Leave the co-op lobby before joining an online party.";
                SetStatus("CO-OP LOBBY STILL ACTIVE");
                return false;
            }

            string code = NormalizeJoinCode(rawCode);
            if (code.Length < 4)
            {
                lastError = "Enter a valid party code.";
                SetStatus("INVALID PARTY CODE");
                RaiseChanged();
                return false;
            }

            try
            {
                busy = true;
                lastError = "";
                SetStatus("JOINING FRIEND PARTY");
                if (!await EnsureServicesAsync()) return false;
                await LeaveInternalAsync();

                session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, BuildJoinOptions());
                AttachSession(session);
                SetStatus(IsSearching ? "JOINED ONLINE PARTY • PLAYER SEARCH ACTIVE" : "JOINED PRIVATE FRIEND PARTY");
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
        /// Solo quick-search entry. It joins an already public online party if one exists;
        /// otherwise it creates a private solo party and immediately opens that party to search.
        /// Friend parties should use CreatePrivatePartyAsync first, invite by code, then let the
        /// leader call BeginPlayerSearchAsync.
        /// </summary>
        public async Task<bool> QuickSearchAsync()
        {
            if (busy) return false;
            if (CoopLobbyService.Instance != null && CoopLobbyService.Instance.InLobby)
            {
                lastError = "Leave the co-op lobby before searching online.";
                SetStatus("CO-OP LOBBY STILL ACTIVE");
                return false;
            }

            try
            {
                busy = true;
                lastError = "";
                SetStatus("SEARCHING FOR ONLINE PARTY");
                if (!await EnsureServicesAsync()) return false;
                await LeaveInternalAsync();

                QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(
                    new QuerySessionsOptions { Count = 25 });

                ISessionInfo target = null;
                if (results?.Sessions != null)
                {
                    for (int i = 0; i < results.Sessions.Count; i++)
                    {
                        ISessionInfo candidate = results.Sessions[i];
                        if (candidate == null || candidate.IsLocked || candidate.AvailableSlots <= 0) continue;
                        if (candidate.MaxPlayers != MaxPartyPlayers) continue;
                        if (!string.Equals(candidate.Name, SessionName, StringComparison.Ordinal)) continue;
                        target = candidate;
                        break;
                    }
                }

                if (target != null)
                {
                    session = await MultiplayerService.Instance.JoinSessionByIdAsync(target.Id, BuildJoinOptions());
                    AttachSession(session);
                    SetStatus("PLAYER FOUND • JOINED ONLINE PARTY");
                    return true;
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

            // No public party was available. Become the searchable solo host instead.
            if (!await CreatePrivatePartyAsync()) return false;
            return await BeginPlayerSearchAsync();
        }

        public async Task<bool> BeginPlayerSearchAsync()
        {
            if (session == null || !IsHost || busy || IsFull) return false;
            try
            {
                busy = true;
                lastError = "";
                IHostSession host = session.AsHost();
                host.IsPrivate = false;
                host.SetProperty(SearchKey, new SessionProperty("1", VisibilityPropertyOptions.Public));
                await host.SavePropertiesAsync();
                SetStatus("SPIELERSUCHE AKTIV • PARTY BLEIBT ZUSAMMEN");
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
                SetStatus("PLAYER SEARCH STOPPED • PARTY PRIVATE");
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

        public async Task<bool> CycleLocalHeroAsync(int direction)
        {
            if (session == null || busy) return false;
            HeroId current = LocalHero;
            int count = Enum.GetValues(typeof(HeroId)).Length;
            for (int step = 1; step <= count; step++)
            {
                int next = Mod((int)current + Math.Sign(direction) * step, count);
                HeroId candidate = (HeroId)next;
                if (HeroUnlockInventory.Owned(candidate) && !HeroUsedByOther(candidate))
                    return await SetLocalHeroAsync(candidate);
            }
            return false;
        }

        public async Task<bool> SetLocalHeroAsync(HeroId hero)
        {
            if (session?.CurrentPlayer == null || busy || !HeroUnlockInventory.Owned(hero) || HeroUsedByOther(hero))
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
            if (session?.CurrentPlayer == null || busy) return false;
            if (ready && HeroUsedByOther(LocalHero))
            {
                lastError = "Party members on the same team need different heroes.";
                SetStatus("CHOOSE A DIFFERENT HERO");
                RaiseChanged();
                return false;
            }

            try
            {
                busy = true;
                lastError = "";
                session.CurrentPlayer.SetProperty(PlayerReadyKey,
                    new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member));
                await session.SaveCurrentPlayerDataAsync();
                SetStatus(ready ? (IsFull ? "READY • WAITING FOR TEAM" : "READY • WAITING FOR PLAYERS") : "NOT READY");
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
                        DisplayName = ReadPlayer(player, PlayerNameKey, "TRAVELER"),
                        Hero = (HeroId)ReadIntPlayer(player, PlayerHeroKey, (int)HeroId.Veyr),
                        Ready = ReadPlayer(player, PlayerReadyKey, "0") == "1",
                        IsHost = player.Id == session.Host,
                        IsLocal = player.Id == localId
                    });
                }
                memberBuffer.Sort((a, b) => a.IsHost == b.IsHost
                    ? string.CompareOrdinal(a.PlayerId, b.PlayerId)
                    : (a.IsHost ? -1 : 1));
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
                    if (!heroes.Add(members[i].Hero)) return true;
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
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
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
                { PlayerReadyKey, new PlayerProperty(ready ? "1" : "0", VisibilityPropertyOptions.Member) }
            };
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
            if (session == null || !IsHost || !IsSearching || PlayerCount < MaxPartyPlayers) return;
            try
            {
                await SetSearchVisibilityAsync(false);
                SetStatus("TEAM FULL • PLAYER SEARCH CLOSED");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("DIMENSIONS FRONT ONLINE: could not close full party search: " + ex.Message);
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

        private void OnPlayerJoined(string _)
        {
            if (IsHost && IsSearching && PlayerCount >= MaxPartyPlayers)
                _ = CloseSearchWhenFullAsync();
            RaiseChanged();
        }

        private void OnPlayerLeft(string _)
        {
            if (IsHost && !IsSearching)
                status = "PLAYER LEFT • PRESS SPIELER SUCHEN TO REFILL";
            RaiseChanged();
        }

        private void OnRemovedFromSession()
        {
            DetachSession(session);
            session = null;
            SetStatus("REMOVED FROM ONLINE PARTY");
            RaiseChanged();
        }

        private void OnSessionDeleted()
        {
            DetachSession(session);
            session = null;
            SetStatus("ONLINE PARTY CLOSED");
            RaiseChanged();
        }

        private async Task LeaveInternalAsync()
        {
            ISession old = session;
            if (old == null) return;
            DetachSession(old);
            session = null;
            try
            {
                if (old.IsHost) await old.AsHost().DeleteAsync();
                else await old.LeaveAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("DIMENSIONS FRONT ONLINE: party leave cleanup failed: " + ex.Message);
            }
        }

        private async Task SafeLeaveAfterFailureAsync()
        {
            try { await LeaveInternalAsync(); }
            catch { }
        }

        private bool HeroUsedByOther(HeroId hero)
        {
            if (session == null) return false;
            string localId = session.CurrentPlayer?.Id ?? "";
            foreach (IReadOnlyPlayer player in session.Players)
                if (player != null && player.Id != localId && ReadIntPlayer(player, PlayerHeroKey, -1) == (int)hero)
                    return true;
            return false;
        }

        private string ReadSession(string key, string fallback)
        {
            if (session != null && session.Properties.TryGetValue(key, out SessionProperty value) && !string.IsNullOrEmpty(value.Value))
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

        private static string SanitizeDisplayName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "TRAVELER" : value.Trim();
            if (name.Length > 20) name = name.Substring(0, 20);
            return name;
        }

        public static string NormalizeJoinCode(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            char[] chars = value.Trim().ToUpperInvariant().ToCharArray();
            var cleaned = new System.Text.StringBuilder(chars.Length);
            for (int i = 0; i < chars.Length; i++)
                if (char.IsLetterOrDigit(chars[i])) cleaned.Append(chars[i]);
            return cleaned.ToString();
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
