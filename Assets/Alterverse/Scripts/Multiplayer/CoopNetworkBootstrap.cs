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
        public static CoopNetworkBootstrap Instance { get; private set; }
        public static NetworkManager Manager => NetworkManager.Singleton;
        public static bool IsListening => NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsListening;
        public static string DirectServerHost { get; private set; } = "";
        public static ushort DirectServerPort { get; private set; }
        public static string DirectServerStatus { get; private set; } = "DIRECT SERVER IDLE";

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
        /// dedicated-server connection. This deliberately replaces the UnityTransport
        /// component so Relay protocol state cannot leak into the direct UTP connection.
        /// </summary>
        public static bool ConnectToDedicatedServer(string host,ushort port)
        {
            if(Instance==null||string.IsNullOrWhiteSpace(host)||port==0)return false;
            Instance.StartCoroutine(Instance.SwitchToDedicatedClient(host.Trim(),port));
            return true;
        }

        private IEnumerator SwitchToDedicatedClient(string host,ushort port)
        {
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null)
            {
                DirectServerStatus="NETWORK MANAGER UNAVAILABLE";
                yield break;
            }
            if(manager.IsServer&&!manager.IsHost)
            {
                DirectServerStatus="DEDICATED SERVER CANNOT START CLIENT";
                yield break;
            }

            DirectServerHost=host;
            DirectServerPort=port;
            DirectServerStatus=$"CONNECTING {host}:{port}";

            if(manager.IsListening)
            {
                manager.Shutdown();
                float timeoutAt=Time.realtimeSinceStartup+5f;
                while(manager.IsListening&&Time.realtimeSinceStartup<timeoutAt)yield return null;
                if(manager.IsListening)
                {
                    DirectServerStatus="NETWORK SHUTDOWN TIMEOUT";
                    yield break;
                }
            }

            UnityTransport previous=manager.NetworkConfig.NetworkTransport as UnityTransport;
            UnityTransport direct=manager.gameObject.AddComponent<UnityTransport>();
            direct.UseWebSockets=false;
            direct.SetConnectionData(host,port);
            manager.NetworkConfig.NetworkTransport=direct;
            if(previous!=null&&previous!=direct)Destroy(previous);
            yield return null;

            if(!manager.StartClient())
            {
                DirectServerStatus=$"FAILED TO START CLIENT {host}:{port}";
                yield break;
            }

            DirectServerStatus=$"CLIENT STARTED {host}:{port}";
            Debug.Log($"DIMENSIONS FRONT ONLINE: connecting directly to dedicated server {host}:{port}.");
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
            if(Instance==this)Instance=null;
        }
    }
}
