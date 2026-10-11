using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Networking;

namespace Alterverse.Multiplayer
{
    /// <summary>Server-only registration and retryable immutable result receipt.</summary>
    public sealed class RankedMatchReporter : MonoBehaviour
    {
        [Serializable] public sealed class Player { public string ticket, admission; public int team; }
        [Serializable] sealed class Registration { public string matchId; public Player[] players; }
        [Serializable] sealed class Receipt { public string matchId; public int winner; public string[] leavers; }
        public static RankedMatchReporter Instance { get; private set; }
        public bool ResultPending { get; private set; }
        public bool Registered { get; private set; }
        public string MatchId { get; private set; }
        Player[] players;
        readonly Dictionary<string, float> absent = new();
        readonly HashSet<string> leavers = new();
        bool finished;
        int receiptGeneration;
        Task voidRequest;
        public static bool RankedDeployment => Environment.GetEnvironmentVariable("DF_RANKED_SERVER") == "1";

        public static RankedMatchReporter Ensure()
        {
            if (Instance == null) new GameObject("DF_RankedMatchReporter").AddComponent<RankedMatchReporter>();
            return Instance;
        }
        void Awake() { Instance = this; DontDestroyOnLoad(gameObject); }
        public async Task<bool> RegisterAsync(string matchId, Player[] roster)
        {
            if (Registered) return MatchId == matchId;
            if (finished) return false;
            if (!DedicatedServerNetworkRuntime.Requested || !EdgegapInjectedMatchContext.IsEdgegapDeployment) return false;
            int registration = receiptGeneration;
            string json = JsonUtility.ToJson(new Registration { matchId = matchId, players = roster });
            if (!await Send("/ranked/server/register", json)) return false;
            // An abort may finish while this HTTP response is still in flight.
            // The backend tombstone owns that race; never reactivate local state.
            if (registration != receiptGeneration || finished) return false;
            MatchId = matchId; players = roster; Registered = true; finished = false;
            absent.Clear(); leavers.Clear();
            return true;
        }
        void Update()
        {
            if (!Registered || finished || !CoopAuthority.Active || NetworkManager.Singleton == null) return;
            foreach (Player player in players)
            {
                bool connected = false;
                foreach (ulong client in NetworkManager.Singleton.ConnectedClientsIds)
                    if (DedicatedOnlineMatchHandshake.IsAuthorizedClient(client, player.ticket)) { connected = true; break; }
                if (connected) absent.Remove(player.ticket);
                else if (!absent.ContainsKey(player.ticket)) absent[player.ticket] = Time.realtimeSinceStartup;
                else if (Time.realtimeSinceStartup - absent[player.ticket] >= 60) leavers.Add(player.ticket);
            }
        }
        public async Task SettleAsync(int winner)
        {
            if (!Registered || finished) return;
            finished = true; ResultPending = true;
            // Also account for a disconnect threshold reached on the end-of-match frame.
            foreach (var pair in absent)
                if (Time.realtimeSinceStartup - pair.Value >= 60) leavers.Add(pair.Key);
            var left = new List<string>(leavers); left.Sort(StringComparer.Ordinal);
            string json = JsonUtility.ToJson(new Receipt { matchId = MatchId, winner = winner, leavers = left.ToArray() });
            int delay = 1000;
            while (!await Send("/ranked/server/result", json))
            {
                Debug.LogWarning("DF Ranked: result pending; retrying server receipt.");
                await Task.Delay(delay); delay = Math.Min(30000, delay * 2);
            }
            ResultPending = false;
        }
        public Task VoidAsync(string matchId)
        {
            if (voidRequest != null) return voidRequest;
            receiptGeneration++;
            finished = true; Registered = false; ResultPending = true;
            voidRequest = SendVoidAsync(matchId);
            return voidRequest;
        }
        async Task SendVoidAsync(string matchId)
        {
            string json = JsonUtility.ToJson(new Registration { matchId = matchId });
            while (!await Send("/ranked/server/void", json)) await Task.Delay(3000);
            ResultPending = false;
        }
        async Task<bool> Send(string path, string json)
        {
            string endpoint = Environment.GetEnvironmentVariable("DF_RANKED_ACCOUNT_URL") ?? "";
            string key = Environment.GetEnvironmentVariable("DF_RANKED_SERVER_KEY") ?? "";
            if (!DedicatedServerNetworkRuntime.Requested || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || key.Length < 32)
                return false;
            try
            {
                using var request = new UnityWebRequest(endpoint.TrimEnd('/') + path, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 20;
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + key);
                var operation = request.SendWebRequest(); while (!operation.isDone) await Task.Yield();
                return request.result == UnityWebRequest.Result.Success && request.responseCode == 200;
            }
            catch { return false; }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
