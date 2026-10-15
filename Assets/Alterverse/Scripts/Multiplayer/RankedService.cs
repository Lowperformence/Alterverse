using System;
using System.Text;
using System.Threading.Tasks;
using Alterverse.Profile;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;

namespace Alterverse.Multiplayer
{
    /// <summary>Account-authenticated solo queue. Ratings and admissions come only from the backend.</summary>
    public sealed class RankedService : MonoBehaviour
    {
        [Serializable] public sealed class Result { public string matchId; public int delta; public int winner; public bool left; }
        [Serializable] public sealed class Snapshot
        {
            public string season, rank, displayName;
            public int points, games, wins, placementsRemaining;
            public long cooldownUntil;
            public Result lastResult;
        }
        [Serializable] public sealed class Member
        {
            public string status, group_id, member_id, ticket_id, team_id, match_id, admission;
            public Assignment assignment;
        }
        [Serializable] public sealed class Assignment { public string fqdn, public_ip; public Ports ports; }
        [Serializable] public sealed class Ports { public Port gameport, game; }
        [Serializable] public sealed class Port { public int external; }
        [Serializable] sealed class Envelope<T> { public bool fulfilled; public T ranked; public string message; }
        [Serializable] sealed class Search { public string displayName; }
        [Serializable] sealed class Leaders { public Snapshot[] leaders; }
        [Serializable] sealed class Configuration { public string httpsEndpoint; }

        public static RankedService Instance { get; private set; }
        public Snapshot Current { get; private set; }
        public Snapshot[] Leaderboard { get; private set; } = Array.Empty<Snapshot>();
        public string Status { get; private set; } = "SOLO 4v4 • 5 PLACEMENT MATCHES";
        public string Admission { get; private set; } = "";
        public bool Searching { get; private set; }
        public bool Busy { get; private set; }
        public string ResultText { get; private set; } = "";
        string endpoint, owner;
        int generation;
        int resultGeneration;
        Task<Member> queueRequest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ensure()
        {
            if (!DedicatedServerNetworkRuntime.Requested && Instance == null)
                new GameObject("DF_RankedService").AddComponent<RankedService>();
        }
        void Awake() { Instance = this; DontDestroyOnLoad(gameObject); }
        void Update()
        {
            if (owner != null && (!AuthenticationService.Instance.IsAuthorized || owner != AuthenticationService.Instance.PlayerId))
            {
                generation++; resultGeneration++;
                if (Searching) CoopNetworkBootstrap.CancelDedicatedConnection();
                Searching = false; Busy = false; Current = null; Admission = ""; owner = null; queueRequest = null;
                Leaderboard = Array.Empty<Snapshot>(); ResultText = "";
            }
        }
        async Task<bool> Authenticate()
        {
            if (AccountServerBootstrap.Instance == null || !await AccountServerBootstrap.Instance.EnsureAuthenticatedAsync())
                throw new InvalidOperationException("Sign in before Ranked.");
            var asset = Resources.Load<TextAsset>("Commerce/AccountServerConfig");
            endpoint = asset == null ? null : JsonUtility.FromJson<Configuration>(asset.text)?.httpsEndpoint;
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var url) || url.Scheme != "https")
                throw new InvalidOperationException("Ranked server is not configured.");
            owner = AuthenticationService.Instance.PlayerId;
            return true;
        }
        public async Task RefreshAsync()
        {
            if (Busy) return;
            int refresh = generation;
            Busy = true;
            try
            {
                await Authenticate();
                if (refresh != generation) return;
                Snapshot snapshot = await Request<Snapshot>("/ranked/status", "{}");
                if (refresh != generation) return;
                Leaders leaders = await Request<Leaders>("/ranked/leaderboard", "{}");
                if (refresh != generation) return;
                Current = snapshot;
                Leaderboard = leaders.leaders ?? Array.Empty<Snapshot>();
                if (!Searching) Status = "SOLO 4v4 • RANK POINTS FOR TEAM VICTORIES";
            }
            catch (Exception error) { if (refresh == generation) Status = error.Message; }
            finally { if (refresh == generation) Busy = false; }
        }
        public async Task<bool> StartSearchAsync()
        {
            if (!RankedAvailability.Enabled) { Status = RankedAvailability.LockedMessage; return false; }
            if (Busy || Searching || CoopAuthority.Active) return false;
            if (OnlinePartyService.Instance?.InParty == true || CoopLobbyService.Instance?.InLobby == true ||
                EdgegapOnlineFlow.Instance?.IsSearching == true)
            { Status = "Leave your party before Solo Ranked."; return false; }
            Busy = true; Searching = true; Admission = ""; ResultText = ""; resultGeneration++;
            int search = ++generation;
            bool queueAttempted = false;
            try
            {
                await Authenticate();
                if (search != generation) return false;
                // A guest's cached session disappears on uninstall. Verify a real
                // recovery method before earning rank on an unrecoverable account.
                var auth = AuthenticationService.Instance;
                string account = owner;
                var info = await auth.GetPlayerInfoAsync();
                if (search != generation) return false;
                if (!auth.IsAuthorized || auth.PlayerId != account || info?.Id != account)
                    throw new InvalidOperationException("Ranked account changed. Sign in again.");
                if (string.IsNullOrWhiteSpace(info.GetGooglePlayGamesId()) && string.IsNullOrWhiteSpace(info.Username))
                    throw new InvalidOperationException("Connect Google Play Games in PROFILE before Ranked. Your rank will survive reinstalling.");
                Status = "RANKED • SEARCHING FOR EIGHT PLAYERS…";
                queueAttempted = true;
                queueRequest = Request<Member>("/ranked/search", JsonUtility.ToJson(new Search { displayName = PlayerProfile.PlayerName }));
                Member member = await queueRequest;
                Busy = false;
                DateTime deadline = DateTime.UtcNow.AddMinutes(5);
                while (search == generation && DateTime.UtcNow < deadline)
                {
                    if (!string.IsNullOrEmpty(member.admission)) Admission = member.admission;
                    string assignedHost = !string.IsNullOrWhiteSpace(member.assignment?.fqdn) ? member.assignment.fqdn : member.assignment?.public_ip;
                    int assignedPort = (member.assignment?.ports?.gameport ?? member.assignment?.ports?.game)?.external ?? 0;
                    if (!string.IsNullOrWhiteSpace(assignedHost) && assignedPort > 0)
                    {
                        if (string.IsNullOrEmpty(Admission) || EdgegapMatchmakerClient.Instance == null)
                            throw new InvalidOperationException("Ranked admission missing.");
                        EdgegapMatchmakerClient.Instance.AcceptRankedAssignment(JsonUtility.ToJson(member));
                        Status = "RANKED MATCH FOUND • CONNECTING…";
                        if (assignedPort > ushort.MaxValue || !CoopNetworkBootstrap.ConnectToDedicatedServer(assignedHost.Trim(), (ushort)assignedPort))
                            throw new InvalidOperationException("Ranked connection failed.");
                        _ = MonitorConnectionAsync(search);
                        return true;
                    }
                    if (string.Equals(member.status, "CANCELLED", StringComparison.OrdinalIgnoreCase) || string.Equals(member.status, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Ranked search expired. Search again.");
                    await Task.Delay(3000);
                    if (search != generation) return false;
                    member = await Request<Member>("/ranked/poll", "{}");
                }
                if (search == generation) throw new InvalidOperationException("No full Ranked match found. Search again.");
                return false;
            }
            catch (Exception error)
            {
                if (search == generation)
                {
                    Status = error.Message;
                    if (queueAttempted && AuthenticationService.Instance.IsAuthorized && AuthenticationService.Instance.PlayerId == owner)
                        try { await Request<Member>("/ranked/cancel", "{}"); } catch { }
                    Searching = false; Admission = "";
                }
                return false;
            }
            finally { if (search == generation) Busy = false; }
        }
        public async Task CancelAsync()
        {
            int cancel = ++generation;
            string account = owner ?? AuthenticationService.Instance.PlayerId;
            Task<Member> creation = queueRequest;
            Busy = true;
            CoopNetworkBootstrap.CancelDedicatedConnection();
            try
            {
                // Wait for queue creation so cancel cannot overtake the original HTTP request.
                if (creation != null) { try { await creation; } catch { } }
                if (cancel != generation || !AuthenticationService.Instance.IsAuthorized ||
                    AuthenticationService.Instance.PlayerId != account) return;
                await Request<Member>("/ranked/cancel", "{}");
                if (cancel == generation) Status = "RANKED SEARCH CANCELLED";
            }
            catch (Exception error) { if (cancel == generation) Status = error.Message; }
            finally { if (cancel == generation) { Searching = false; Admission = ""; Busy = false; queueRequest = null; } }
        }
        public void MatchStarted() { generation++; Searching = false; Busy = false; Admission = ""; Status = "RANKED MATCH LIVE"; }
        async Task MonitorConnectionAsync(int search)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(150);
            while (search == generation && DateTime.UtcNow < deadline)
            {
                string connection = CoopNetworkBootstrap.DirectServerStatus ?? "";
                if (connection.StartsWith("DEDICATED CONNECTION FAILED", StringComparison.Ordinal)) break;
                await Task.Delay(500);
            }
            if (search != generation) return;
            await CancelAsync();
            if (generation == search + 1) Status = "Ranked start cancelled. No rank points changed. Search again.";
        }
        public async Task RefreshResultAsync(string matchId)
        {
            int result = ++resultGeneration;
            string account = AuthenticationService.Instance.PlayerId;
            bool CurrentAccount() => result == resultGeneration && AuthenticationService.Instance.IsAuthorized &&
                AuthenticationService.Instance.PlayerId == account;
            ResultText = "RANKED • VERIFYING RESULT…";
            for (int i = 0; i < 40; i++)
            {
                if (!CurrentAccount()) return;
                try
                {
                    await Authenticate();
                    if (!CurrentAccount()) return;
                    Snapshot snapshot = await Request<Snapshot>("/ranked/status", "{}");
                    if (!CurrentAccount()) return;
                    Current = snapshot;
                    if (Current.lastResult?.matchId == matchId)
                    {
                        int delta = Current.lastResult.delta;
                        ResultText = "RANKED • " + Current.rank + " • " + (delta >= 0 ? "+" : "") + delta + " RP";
                        if (Current.placementsRemaining > 0) ResultText += " • " + Current.placementsRemaining + " PLACEMENTS LEFT";
                        if (Current.lastResult.left) ResultText += " • LEAVER PENALTY";
                        return;
                    }
                }
                catch { }
                if (!CurrentAccount()) return;
                await Task.Delay(3000);
            }
            if (CurrentAccount()) ResultText = "RANKED RESULT PENDING • REFRESH IN THE RANKED MENU";
        }
        async Task<T> Request<T>(string path, string json)
        {
            if (string.IsNullOrEmpty(endpoint) || !AuthenticationService.Instance.IsAuthorized)
                throw new InvalidOperationException("Ranked sign-in required.");
            string player = AuthenticationService.Instance.PlayerId;
            using var request = new UnityWebRequest(endpoint.TrimEnd('/') + path, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 20;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + AuthenticationService.Instance.AccessToken);
            var operation = request.SendWebRequest();
            while (!operation.isDone) await Task.Yield();
            if (!AuthenticationService.Instance.IsAuthorized || AuthenticationService.Instance.PlayerId != player)
                throw new InvalidOperationException("Ranked account changed.");
            var response = JsonUtility.FromJson<Envelope<T>>(request.downloadHandler.text ?? "{}");
            if (request.result != UnityWebRequest.Result.Success || response == null || !response.fulfilled)
                throw new InvalidOperationException(response?.message ?? "Ranked server unavailable. Try again.");
            return response.ranked;
        }
        void OnDestroy() { generation++; resultGeneration++; if (Instance == this) Instance = null; }
    }
}
