using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Client-safe Edgegap Matchmaker "Group Up" integration.
    /// The shared matchmaker Auth Token is loaded from a local Resources secret or environment
    /// variable and is intentionally never committed to source control.
    /// </summary>
    [DefaultExecutionOrder(-26000)]
    public sealed class EdgegapMatchmakerClient : MonoBehaviour
    {
        public const string DefaultApiUrl = "https://om-4tj8ca86qm.edgegap.net";
        public const string Profile = "dimensions-front-online";
        public const string TokenEnvironmentVariable = "DF_EDGEGAP_MATCHMAKER_TOKEN";
        public const string TokenResourcePath = "LocalSecrets/edgegap_matchmaker_token";

        private const int RequestTimeoutSeconds = 15;
        private const int PollIntervalMilliseconds = 3000;
        private const int MaxRequestAttempts = 3;
        private const int DedicatedConnectionTimeoutSeconds = 65;

        public static EdgegapMatchmakerClient Instance { get; private set; }

        public string ApiUrl { get; private set; } = DefaultApiUrl;
        public string GroupId { get; private set; } = "";
        public string MemberId { get; private set; } = "";
        public string TicketId { get; private set; } = "";
        public string TeamId { get; private set; } = "";
        public string MatchId { get; private set; } = "";
        public string MatchmakerStatus { get; private set; } = "IDLE";
        public string LastError { get; private set; } = "";
        public string AssignedHost { get; private set; } = "";
        public ushort AssignedPort { get; private set; }
        public bool HasAssignment => !string.IsNullOrWhiteSpace(AssignedHost) && AssignedPort > 0;
        public bool HasMembership => !string.IsNullOrWhiteSpace(GroupId) && !string.IsNullOrWhiteSpace(MemberId);
        public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveAuthToken());

        private int waitGeneration;
        private int membershipGeneration;

        public void AcceptRankedAssignment(string json)
        {
            CancelPendingAssignmentWait();
            ResetLocalState();
            ApplyMembershipResponse(JsonUtility.FromJson<MemberResponse>(json), "RANKED HOST ASSIGNED");
        }

        public event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if (Instance == null)
                new GameObject("DF_EdgegapMatchmakerClient").AddComponent<EdgegapMatchmakerClient>();
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

        public void ResetLocalState()
        {
            membershipGeneration++;
            GroupId = "";
            MemberId = "";
            TicketId = "";
            TeamId = "";
            MatchId = "";
            AssignedHost = "";
            AssignedPort = 0;
            LastError = "";
            SetStatus("IDLE");
        }

        public void CancelPendingAssignmentWait()
        {
            waitGeneration++;
            CoopNetworkBootstrap.CancelDedicatedConnection();
        }

        public async Task<bool> CreateGroupAsync(bool ready = false)
        {
            CancelPendingAssignmentWait();
            ResetLocalState();
            int generation = membershipGeneration;
            MemberResponse response = await SendMemberAsync(
                "POST",
                "/groups",
                new CreateMemberRequest { profile = Profile, is_ready = ready, attributes = new EmptyAttributes() });
            if (generation != membershipGeneration)
            {
                if (!string.IsNullOrWhiteSpace(response?.group_id))
                    await SendNoContentAsync("DELETE", "/groups/" + UnityWebRequest.EscapeURL(response.group_id), false);
                return false;
            }
            return ApplyMembershipResponse(response, "GROUP CREATED");
        }

        public async Task<bool> JoinGroupAsync(string groupId, bool ready = false)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return Fail("EDGEGAP GROUP ID IS EMPTY");

            CancelPendingAssignmentWait();
            ResetLocalState();
            GroupId = groupId.Trim();
            int generation = membershipGeneration;
            string joiningGroup = GroupId;
            MemberResponse response = await SendMemberAsync(
                "POST",
                "/groups/" + UnityWebRequest.EscapeURL(GroupId) + "/members",
                new CreateMemberRequest { profile = Profile, is_ready = ready, attributes = new EmptyAttributes() });
            if (generation != membershipGeneration)
            {
                if (!string.IsNullOrWhiteSpace(response?.member_id))
                    await SendNoContentAsync("DELETE", "/groups/" + UnityWebRequest.EscapeURL(joiningGroup) +
                        "/members/" + UnityWebRequest.EscapeURL(response.member_id), false);
                return false;
            }
            return ApplyMembershipResponse(response, "JOINED EDGEGAP GROUP");
        }

        public async Task<bool> SetReadyAsync(bool ready)
        {
            if (!HasMembership) return Fail("EDGEGAP GROUP MEMBERSHIP IS MISSING");
            int generation = membershipGeneration;
            MemberResponse response = await SendMemberAsync(
                "PATCH",
                MemberPath(),
                new ReadyRequest { is_ready = ready });
            if (generation != membershipGeneration ||
                !ApplyMembershipResponse(response, ready ? "MATCHMAKING SEARCHING" : "MATCHMAKING NOT READY"))
                return false;
            if (response.is_ready != ready)
                return Fail(ready ? "MATCHMAKER DID NOT CONFIRM PLAYER READY • TRY AGAIN"
                    : "MATCHMAKER DID NOT CONFIRM PLAYER NOT READY • TRY AGAIN");
            return true;
        }

        public async Task<bool> RefreshMembershipAsync()
        {
            if (!HasMembership) return Fail("EDGEGAP GROUP MEMBERSHIP IS MISSING");
            int generation = membershipGeneration;
            MemberResponse response = await SendMemberAsync("GET", MemberPath(), null);
            return generation == membershipGeneration && ApplyMembershipResponse(response, null);
        }

        /// <summary>
        /// Removes only this member from a Group Up group. Guests use this when leaving so a
        /// disconnected/abandoned member cannot remain ready in Edgegap as a ghost player.
        /// </summary>
        public async Task<bool> LeaveGroupAsync()
        {
            CancelPendingAssignmentWait();
            if (!HasMembership)
            {
                ResetLocalState();
                return true;
            }

            int generation = ++membershipGeneration;
            string path = MemberPath();
            bool success = await SendNoContentAsync("DELETE", path);
            if (success && generation == membershipGeneration) ResetLocalState();
            return success;
        }

        /// <summary>
        /// Cancels the whole Group Up group. This is used by the party leader before a host has
        /// been assigned, so abandoned searches do not remain queued until ticket expiration.
        /// </summary>
        public async Task<bool> CancelGroupAsync()
        {
            CancelPendingAssignmentWait();
            if (string.IsNullOrWhiteSpace(GroupId))
            {
                ResetLocalState();
                return true;
            }

            int generation = ++membershipGeneration;
            string path = "/groups/" + UnityWebRequest.EscapeURL(GroupId);
            bool success = await SendNoContentAsync("DELETE", path);
            if (success && generation == membershipGeneration) ResetLocalState();
            return success;
        }

        /// <summary>
        /// Polls the local Group Up membership every three seconds. Once HOST_ASSIGNED is returned,
        /// switches NGO/UTP to the assigned fqdn + external game port and waits until NGO confirms
        /// the direct connection (including the cold-start retry window). The generation guard
        /// prevents a cancelled/older search from connecting after a late Edgegap response.
        /// </summary>
        public async Task<bool> WaitForAssignmentAndConnectAsync(int timeoutSeconds = 180)
        {
            if (!HasMembership) return Fail("EDGEGAP GROUP MEMBERSHIP IS MISSING");
            int generation = ++waitGeneration;
            DateTime deadline = DateTime.UtcNow.AddSeconds(Mathf.Max(15, timeoutSeconds));

            while (DateTime.UtcNow < deadline)
            {
                if (generation != waitGeneration) return false;
                if (!await RefreshMembershipAsync()) return false;
                if (generation != waitGeneration) return false;

                if (HasAssignment)
                {
                    if (string.IsNullOrWhiteSpace(TicketId) || string.IsNullOrWhiteSpace(TeamId))
                        return Fail("MATCHMAKER ASSIGNMENT IS MISSING PLAYER TICKET OR TEAM");
                    if (!CoopNetworkBootstrap.ConnectToDedicatedServer(AssignedHost, AssignedPort))
                        return Fail("COULD NOT START DEDICATED SERVER CONNECTION");

                    SetStatus($"HOST ASSIGNED • CONNECTING {AssignedHost}:{AssignedPort}");
                    if (!await WaitForDedicatedConnectionAsync(generation)) return false;
                    if (generation != waitGeneration) return false;
                    LastError = "";
                    SetStatus($"DEDICATED SERVER CONNECTED • {AssignedHost}:{AssignedPort}");
                    return true;
                }

                if (string.Equals(MatchmakerStatus, "HOST_ASSIGNED", StringComparison.OrdinalIgnoreCase))
                    return Fail("MATCHMAKER ASSIGNMENT HAS NO USABLE GAME SERVER ADDRESS OR PORT");

                if (string.Equals(MatchmakerStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(MatchmakerStatus, "EXPIRED", StringComparison.OrdinalIgnoreCase))
                    return Fail("MATCHMAKING CANCELLED");

                await Task.Delay(PollIntervalMilliseconds);
            }

            if (generation != waitGeneration) return false;
            return Fail("MATCHMAKING ASSIGNMENT TIMEOUT");
        }

        private async Task<bool> WaitForDedicatedConnectionAsync(int generation)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(DedicatedConnectionTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                if (generation != waitGeneration)
                {
                    // The cancelling owner already stopped its attempt. A newer search may now
                    // own the transport, so this old waiter must not shut that connection down.
                    return false;
                }

                var manager = CoopNetworkBootstrap.Manager;
                if (manager != null && manager.IsConnectedClient && !manager.IsServer)
                    return true;

                string connectionState = CoopNetworkBootstrap.DirectServerStatus ?? "";
                if (connectionState.StartsWith("DEDICATED CONNECTION FAILED", StringComparison.Ordinal) ||
                    connectionState.StartsWith("NETWORK SHUTDOWN TIMEOUT", StringComparison.Ordinal) ||
                    connectionState.StartsWith("NETWORK MANAGER UNAVAILABLE", StringComparison.Ordinal))
                    return Fail(connectionState);

                await Task.Delay(250);
            }

            if (generation != waitGeneration) return false;
            return Fail("DEDICATED SERVER CONNECTION TIMEOUT");
        }

        private async Task<MemberResponse> SendMemberAsync(string method, string path, object body)
        {
            string token = ResolveAuthToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");
                return null;
            }

            int generation = membershipGeneration;
            string url = ApiUrl.TrimEnd('/') + path;
            string json = body == null ? null : JsonUtility.ToJson(body);

            for (int attempt = 0; attempt < MaxRequestAttempts; attempt++)
            {
                if (generation != membershipGeneration) return null;
                using (UnityWebRequest request = new UnityWebRequest(url, method))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    if (json != null)
                    {
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                        request.SetRequestHeader("Content-Type", "application/json");
                    }

                    ApplyHeaders(request, token);
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    long code = request.responseCode;
                    if (request.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
                    {
                        string text = request.downloadHandler?.text ?? "";
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            if (generation == membershipGeneration) Fail("EDGEGAP RETURNED AN EMPTY RESPONSE");
                            return null;
                        }

                        try
                        {
                            if (generation == membershipGeneration) LastError = "";
                            return JsonUtility.FromJson<MemberResponse>(text);
                        }
                        catch (Exception ex)
                        {
                            if (generation == membershipGeneration) Fail("COULD NOT PARSE EDGEGAP RESPONSE: " + ex.Message);
                            return null;
                        }
                    }

                    if (generation != membershipGeneration) return null;
                    if (!ShouldRetry(request, code) || attempt == MaxRequestAttempts - 1)
                    {
                        ReportRequestFailure(request, code);
                        return null;
                    }
                }

                await Task.Delay(500 * (1 << attempt));
            }

            return null;
        }

        private async Task<bool> SendNoContentAsync(string method, string path, bool reportErrors = true)
        {
            string token = ResolveAuthToken();
            if (string.IsNullOrWhiteSpace(token))
                return reportErrors && Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");

            int generation = membershipGeneration;
            string url = ApiUrl.TrimEnd('/') + path;
            for (int attempt = 0; attempt < MaxRequestAttempts; attempt++)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, method))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();
                    ApplyHeaders(request, token);
                    UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                    while (!operation.isDone) await Task.Yield();

                    long code = request.responseCode;
                    if ((request.result == UnityWebRequest.Result.Success && code >= 200 && code < 300) || code == 404 || code == 410)
                    {
                        if (reportErrors && generation == membershipGeneration) LastError = "";
                        return true;
                    }

                    if (!ShouldRetry(request, code) || attempt == MaxRequestAttempts - 1)
                    {
                        if (reportErrors && generation == membershipGeneration) ReportRequestFailure(request, code);
                        return false;
                    }
                }

                await Task.Delay(500 * (1 << attempt));
            }
            return false;
        }

        private void ApplyHeaders(UnityWebRequest request, string token)
        {
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Authorization", token);
            request.timeout = RequestTimeoutSeconds;
        }

        private static bool ShouldRetry(UnityWebRequest request, long code)
        {
            return code == 429 || code >= 500 || request.result == UnityWebRequest.Result.ConnectionError;
        }

        private void ReportRequestFailure(UnityWebRequest request, long code)
        {
            string serverMessage = request.downloadHandler?.text;
            Fail($"EDGEGAP REQUEST FAILED ({code})" +
                 (string.IsNullOrWhiteSpace(serverMessage) ? "" : ": " + serverMessage));
        }

        private bool ApplyMembershipResponse(MemberResponse response, string successStatus)
        {
            if (response == null) return false;

            if (!string.IsNullOrWhiteSpace(response.group_id)) GroupId = response.group_id;
            if (!string.IsNullOrWhiteSpace(response.member_id)) MemberId = response.member_id;
            if (!string.IsNullOrWhiteSpace(response.ticket_id)) TicketId = response.ticket_id;
            if (!string.IsNullOrWhiteSpace(response.team_id)) TeamId = response.team_id;
            if (!string.IsNullOrWhiteSpace(response.match_id)) MatchId = response.match_id;

            Assignment assignment = response.assignment;
            if (assignment != null)
            {
                string host = !string.IsNullOrWhiteSpace(assignment.fqdn) ? assignment.fqdn : assignment.public_ip;
                AssignmentPort mappedPort = assignment.ports?.gameport ?? assignment.ports?.game;
                int externalPort = mappedPort?.external ?? 0;
                if (!string.IsNullOrWhiteSpace(host) && externalPort > 0 && externalPort <= ushort.MaxValue)
                {
                    AssignedHost = host.Trim();
                    AssignedPort = (ushort)externalPort;
                }
            }

            if (HasAssignment)
                SetStatus($"HOST_ASSIGNED • {AssignedHost}:{AssignedPort}");
            else if (!string.IsNullOrWhiteSpace(response.status))
                SetStatus(response.status.ToUpperInvariant());
            else if (!string.IsNullOrWhiteSpace(successStatus))
                SetStatus(successStatus);
            else
                RaiseChanged();

            return true;
        }

        private string ResolveAuthToken()
        {
            string environment = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(environment)) return environment.Trim();

            TextAsset secret = Resources.Load<TextAsset>(TokenResourcePath);
            return secret == null ? "" : (secret.text ?? "").Trim();
        }

        private string MemberPath()
        {
            return "/groups/" + UnityWebRequest.EscapeURL(GroupId) +
                   "/members/" + UnityWebRequest.EscapeURL(MemberId);
        }

        private bool Fail(string message)
        {
            LastError = message ?? "EDGEGAP MATCHMAKER ERROR";
            SetStatus(LastError);
            Debug.LogWarning("DIMENSIONS FRONT ONLINE: " + LastError);
            return false;
        }

        private void SetStatus(string value)
        {
            string next = string.IsNullOrWhiteSpace(value) ? "IDLE" : value;
            if (next != MatchmakerStatus)
                Debug.Log("DIMENSIONS FRONT ONLINE: " + next);
            MatchmakerStatus = next;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            try { Changed?.Invoke(); } catch { }
        }

        [Serializable]
        private sealed class EmptyAttributes { }

        [Serializable]
        private sealed class CreateMemberRequest
        {
            public string profile;
            public bool is_ready;
            public EmptyAttributes attributes;
        }

        [Serializable]
        private sealed class ReadyRequest
        {
            public bool is_ready;
        }

        [Serializable]
        private sealed class MemberResponse
        {
            public string member_id;
            public string group_id;
            public bool is_ready;
            public string ticket_id;
            public string team_id;
            public string match_id;
            public string status;
            public Assignment assignment;
        }

        [Serializable]
        private sealed class Assignment
        {
            public string fqdn;
            public string public_ip;
            public AssignmentPorts ports;
        }

        [Serializable]
        private sealed class AssignmentPorts
        {
            public AssignmentPort gameport;
            public AssignmentPort game;
        }

        [Serializable]
        private sealed class AssignmentPort
        {
            public int external;
            public string protocol;
        }
    }
}
