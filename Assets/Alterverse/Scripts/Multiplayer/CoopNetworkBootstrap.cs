using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Creates the idle NGO/UTP runtime required by Multiplayer Services.
    /// It does not start networking on its own, so offline matches remain untouched.
    /// Co-op can continue to use Relay, while online matches can switch to a direct
    /// dedicated-server UTP endpoint after matchmaking/allocation completes.
    /// </summary>
    public sealed class CoopNetworkBootstrap : MonoBehaviour
    {
        private const int DedicatedConnectAttempts=5;
        private const float DedicatedAttemptTimeoutSeconds=8f;
        private const float DedicatedRetryDelaySeconds=2f;

        public static CoopNetworkBootstrap Instance { get; private set; }
        public static NetworkManager Manager => NetworkManager.Singleton;
        public static bool IsListening => NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsListening;
        public static string DirectServerHost { get; private set; } = "";
        public static ushort DirectServerPort { get; private set; }
        public static string DirectServerStatus { get; private set; } = "DIRECT SERVER IDLE";

        private Coroutine dedicatedConnectRoutine;

        public static int ConnectedPeerCount
        {
            get
            {
                NetworkManager manager=NetworkManager.Singleton;
                if(manager==null||!manager.IsListening)return 0;
                if(manager.IsHost)return manager.ConnectedClientsIds?.Count??0;
                return manager.IsConnectedClient?1:0;
            }
        }

        public static bool IsLinkReadyForPlayers(int expectedPlayers)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return false;
            expectedPlayers=Mathf.Clamp(expectedPlayers,1,MatchSession.MaxPlayers);
            if(manager.IsHost)return manager.ConnectedClientsIds!=null&&manager.ConnectedClientsIds.Count>=expectedPlayers;
            return manager.IsConnectedClient;
        }

        public static string LinkStateForPlayers(int expectedPlayers)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening)return "OFFLINE";
            expectedPlayers=Mathf.Clamp(expectedPlayers,1,MatchSession.MaxPlayers);
            if(manager.IsHost)
            {
                int connected=manager.ConnectedClientsIds?.Count??0;
                if(expectedPlayers<=1&&connected>=1)return "HOST CONNECTED • BOT FILL READY";
                return connected>=expectedPlayers
                    ?$"SERVER + {Mathf.Max(0,connected-1)} PLAYER{(connected==2?"":"S")} CONNECTED"
                    :$"RELAY {connected}/{expectedPlayers} PLAYERS CONNECTED";
            }
            return manager.IsConnectedClient?"CLIENT CONNECTED":"CLIENT CONNECTING";
        }

        public static string LinkState=>LinkStateForPlayers(CoopLobbyService.MinHumanPlayers);

        public static void ShutdownLocalNetwork()
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager!=null&&manager.IsListening)manager.Shutdown();
        }

        /// <summary>
        /// Switches the local NGO runtime from any current Relay/client link to a direct
        /// dedicated-server connection. Edgegap can report HOST_ASSIGNED while the container is
        /// still finishing startup, so the direct connection is retried instead of failing once.
        /// </summary>
        public static bool ConnectToDedicatedServer(string host,ushort port)
        {
            if(Instance==null||string.IsNullOrWhiteSpace(host)||port==0)return false;
            if(Instance.dedicatedConnectRoutine!=null)Instance.StopCoroutine(Instance.dedicatedConnectRoutine);
            Instance.dedicatedConnectRoutine=Instance.StartCoroutine(Instance.SwitchToDedicatedClient(host.Trim(),port));
            return true;
        }

        private IEnumerator SwitchToDedicatedClient(string host,ushort port)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null)
            {
                DirectServerStatus="NETWORK MANAGER UNAVAILABLE";
                dedicatedConnectRoutine=null;
                yield break;
            }
            if(manager.IsServer&&!manager.IsHost)
            {
                DirectServerStatus="DEDICATED SERVER CANNOT START CLIENT";
                dedicatedConnectRoutine=null;
                yield break;
            }

            DirectServerHost=host;
            DirectServerPort=port;
            DirectServerStatus=$"PREPARING DIRECT CONNECTION {host}:{port}";

            if(manager.IsListening)
            {
                manager.Shutdown();
                float timeoutAt=Time.realtimeSinceStartup+5f;
                while(manager.IsListening&&Time.realtimeSinceStartup<timeoutAt)yield return null;
                if(manager.IsListening)
                {
                    DirectServerStatus="NETWORK SHUTDOWN TIMEOUT";
                    dedicatedConnectRoutine=null;
                    yield break;
                }
            }

            // Replace the Relay-configured transport once. The clean direct UTP transport can then
            // be restarted for each cold-start retry without carrying Relay protocol state forward.
            UnityTransport previous=manager.NetworkConfig.NetworkTransport as UnityTransport;
            UnityTransport direct=manager.gameObject.AddComponent<UnityTransport>();
            direct.UseWebSockets=false;
            direct.SetConnectionData(host,port);
            manager.NetworkConfig.NetworkTransport=direct;
            if(previous!=null&&previous!=direct)Destroy(previous);
            yield return null;

            for(int attempt=1;attempt<=DedicatedConnectAttempts;attempt++)
            {
                DirectServerStatus=$"CONNECTING {host}:{port} • TRY {attempt}/{DedicatedConnectAttempts}";

                if(!manager.StartClient())
                {
                    DirectServerStatus=$"CLIENT START FAILED • TRY {attempt}/{DedicatedConnectAttempts}";
                }
                else
                {
                    Debug.Log($"DIMENSIONS FRONT ONLINE: dedicated connection attempt {attempt}/{DedicatedConnectAttempts} to {host}:{port}.");
                    float attemptDeadline=Time.realtimeSinceStartup+DedicatedAttemptTimeoutSeconds;
                    while(Time.realtimeSinceStartup<attemptDeadline)
                    {
                        if(manager.IsConnectedClient)
                        {
                            DirectServerStatus=$"CLIENT CONNECTED {host}:{port}";
                            Debug.Log($"DIMENSIONS FRONT ONLINE: connected to dedicated server {host}:{port}.");
                            dedicatedConnectRoutine=null;
                            yield break;
                        }
                        // NGO may stop listening itself after a rejected/failed connection.
                        if(!manager.IsListening)break;
                        yield return null;
                    }
                }

                if(manager.IsListening)
                {
                    manager.Shutdown();
                    float shutdownDeadline=Time.realtimeSinceStartup+3f;
                    while(manager.IsListening&&Time.realtimeSinceStartup<shutdownDeadline)yield return null;
                }

                if(attempt<DedicatedConnectAttempts)
                {
                    DirectServerStatus=$"SERVER NOT READY • RETRY {attempt+1}/{DedicatedConnectAttempts}";
                    float retryAt=Time.realtimeSinceStartup+DedicatedRetryDelaySeconds;
                    while(Time.realtimeSinceStartup<retryAt)yield return null;
                    direct.SetConnectionData(host,port);
                }
            }

            DirectServerStatus=$"DEDICATED CONNECTION FAILED {host}:{port}";
            Debug.LogError($"DIMENSIONS FRONT ONLINE: could not connect to dedicated server {host}:{port} after {DedicatedConnectAttempts} attempts.");
            dedicatedConnectRoutine=null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if(Instance!=null)return;
            if(NetworkManager.Singleton!=null)
            {
                GameObject marker=new("DF_CoopNetworkBootstrap");
                marker.AddComponent<CoopNetworkBootstrap>();
                return;
            }

            GameObject root=new("DF_CoopNetworkManager");
            DontDestroyOnLoad(root);

            UnityTransport transport=root.AddComponent<UnityTransport>();
            NetworkManager manager=root.AddComponent<NetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport=transport;
            manager.NetworkConfig.EnableSceneManagement=false;
            manager.NetworkConfig.ConnectionApproval=false;

            root.AddComponent<CoopNetworkBootstrap>();
        }

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if(dedicatedConnectRoutine!=null)StopCoroutine(dedicatedConnectRoutine);
            dedicatedConnectRoutine=null;
            if(Instance==this)Instance=null;
        }
    }
}
