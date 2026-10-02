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
        public const string DefaultApiUrl = "https://om-71fje1pjhx.edgegap.net";
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

        public async Task<bool> CreateGroupAsync(bool ready = false)
        {
            ResetLocalState();
            MemberResponse response = await SendMemberAsync(
                "POST",
                "/groups",
                new CreateMemberRequest { profile = Profile, is_ready = ready, attributes = new EmptyAttributes() });
            return ApplyMembershipResponse(response, "GROUP CREATED");
        }

        public async Task<bool> JoinGroupAsync(string groupId, bool ready = false)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                return Fail("EDGEGAP GROUP ID IS EMPTY");

            ResetLocalState();
            GroupId = groupId.Trim();
            MemberResponse response = await SendMemberAsync(
                "POST",
                "/groups/" + UnityWebRequest.EscapeURL(GroupId) + "/members",
                new CreateMemberRequest { profile = Profile, is_ready = ready, attributes = new EmptyAttributes() });
            return ApplyMembershipResponse(response, "JOINED EDGEGAP GROUP");
        }

        public async Task<bool> SetReadyAsync(bool ready)
        {
            if (!HasMembership) return Fail("EDGEGAP GROUP MEMBERSHIP IS MISSING");

            MemberResponse response = await SendMemberAsync(
                "PATCH",
                MemberPath(),
                new ReadyRequest { is_ready = ready });
            return ApplyMembershipResponse(response, ready ? "MATCHMAKING SEARCHING" : "MATCHMAKING NOT READY");
        }

        public async Task<bool> RefreshMembershipAsync()
        {
            if (!HasMembership) return Fail("EDGEGAP GROUP MEMBERSHIP IS MISSING");
            MemberResponse response = await SendMemberAsync("GET", MemberPath(), null);
            return ApplyMembershipResponse(response, null);
        }

        /// <summary>
        /// Cancels the whole Group Up group. This is used by the party leader before a host has
        /// been assigned, so abandoned searches do not remain queued until ticket expiration.
        /// </summary>
        public async Task<bool> CancelGroupAsync()
        {
            if (string.IsNullOrWhiteSpace(GroupId))
            {
                ResetLocalState();
                return true;
            }

            string path = "/groups/" + UnityWebRequest.EscapeURL(GroupId);
            bool success = await SendNoContentAsync("DELETE", path);
            if (success) ResetLocalState();
            return success;
        }

        /// <summary>
        /// Polls the local Group Up membership every three seconds. Once HOST_ASSIGNED is returned,
        /// switches NGO/UTP to the assigned fqdn + external game port and waits until NGO confirms
        /// the direct connection (including the cold-start retry window).
        /// </summary>
        public async Task<bool> WaitForAssignmentAndConnectAsync(int timeoutSeconds = 180)
        {
            if (!HasMembership) return Fail("EDGEGAP GROUP MEMBERSHIP IS MISSING");
            DateTime deadline = DateTime.UtcNow.AddSeconds(Mathf.Max(15, timeoutSeconds));

            while (DateTime.UtcNow < deadline)
            {
                if (!await RefreshMembershipAsync()) return false;
                if (HasAssignment)
                {
                    if (!CoopNetworkBootstrap.ConnectToDedicatedServer(AssignedHost, AssignedPort))
                        return Fail("COULD NOT START DEDICATED SERVER CONNECTION");

                    SetStatus($"HOST ASSIGNED • CONNECTING {AssignedHost}:{AssignedPort}");
                    if (!await WaitForDedicatedConnectionAsync()) return false;
                    LastError = "";
                    SetStatus($"DEDICATED SERVER CONNECTED • {AssignedHost}:{AssignedPort}");
                    return true;
                }

                if (string.Equals(MatchmakerStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                    return Fail("MATCHMAKING CANCELLED");

                await Task.Delay(PollIntervalMilliseconds);
            }

            return Fail("MATCHMAKING ASSIGNMENT TIMEOUT");
        }

        private async Task<bool> WaitForDedicatedConnectionAsync()
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(DedicatedConnectionTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
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

            string url = ApiUrl.TrimEnd('/') + path;
            string json = body == null ? null : JsonUtility.ToJson(body);

            for (int attempt = 0; attempt < MaxRequestAttempts; attempt++)
            {
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
                            Fail("EDGEGAP RETURNED AN EMPTY RESPONSE");
                            return null;
                        }

                        try
                        {
                            LastError = "";
                            return JsonUtility.FromJson<MemberResponse>(text);
                        }
                        catch (Exception ex)
                        {
                            Fail("COULD NOT PARSE EDGEGAP RESPONSE: " + ex.Message);
                            return null;
                        }
                    }

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

        private async Task<bool> SendNoContentAsync(string method, string path)
        {
            string token = ResolveAuthToken();
            if (string.IsNullOrWhiteSpace(token))
                return Fail("EDGEGAP MATCHMAKER TOKEN NOT CONFIGURED");

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
                    if (request.result == UnityWebRequest.Result.Success && code >= 200 && code < 300)
                    {
                        LastError = "";
                        return true;
                    }

                    if (!ShouldRetry(request, code) || attempt == MaxRequestAttempts - 1)
                    {
                        ReportRequestFailure(request, code);
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
            if (!string.IsNullOrWhiteSpace(response.status)) MatchmakerStatus = response.status;

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
            MatchmakerStatus = string.IsNullOrWhiteSpace(value) ? "IDLE" : value;
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
