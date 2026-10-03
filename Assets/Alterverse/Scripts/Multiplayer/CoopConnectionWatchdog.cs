using Alterverse.Match;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Watches NGO transport disconnects so a dropped phone cannot leave the rest of the
    /// co-op match in a half-connected state. Session callbacks remain the primary source;
    /// this is the immediate transport-level fallback.
    /// </summary>
    public sealed class CoopConnectionWatchdog : MonoBehaviour
    {
        public static CoopConnectionWatchdog Instance { get; private set; }

        private NetworkManager manager;
        private bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if(Instance==null)new GameObject("DF_CoopConnectionWatchdog").AddComponent<CoopConnectionWatchdog>();
        }

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            NetworkManager current=NetworkManager.Singleton;
            if(current==manager&&subscribed)return;
            Detach();
            manager=current;
            if(manager==null)return;
            manager.OnClientDisconnectCallback+=OnClientDisconnected;
            subscribed=true;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            CoopLobbyService lobby=CoopLobbyService.Instance;
            NetworkManager current=NetworkManager.Singleton;
            if(current==null)return;
            bool localLost=!current.IsServer&&clientId==current.LocalClientId;
            if (localLost && CoopAuthority.ActivePlan is OnlineMatchPlan && !MatchExit.Leaving)
            {
                _ = MatchExit.ReturnToMenuAsync();
                return;
            }
            if(lobby==null||!lobby.InLobby)return;
            if(current.IsServer)
            {
                // ServerClientId is the server itself. Remote ids represent connected players.
                if(clientId==NetworkManager.ServerClientId)return;
                lobby.HandleTransportDisconnect(false);
            }
            else if(localLost)lobby.HandleTransportDisconnect(true);
        }

        private void Detach()
        {
            if(subscribed&&manager!=null)manager.OnClientDisconnectCallback-=OnClientDisconnected;
            subscribed=false;
            manager=null;
        }

        private void OnDestroy()
        {
            Detach();
            if(Instance==this)Instance=null;
        }
    }
}
