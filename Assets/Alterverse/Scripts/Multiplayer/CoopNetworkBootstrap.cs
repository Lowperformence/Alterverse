using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Creates the idle NGO/UTP runtime required by Multiplayer Services.
    /// It does not start networking on its own, so offline matches remain untouched.
    /// </summary>
    public sealed class CoopNetworkBootstrap : MonoBehaviour
    {
        public static CoopNetworkBootstrap Instance { get; private set; }
        public static NetworkManager Manager => NetworkManager.Singleton;
        public static bool IsListening => NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsListening;

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
