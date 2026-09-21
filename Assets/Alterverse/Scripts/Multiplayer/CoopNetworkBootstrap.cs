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
