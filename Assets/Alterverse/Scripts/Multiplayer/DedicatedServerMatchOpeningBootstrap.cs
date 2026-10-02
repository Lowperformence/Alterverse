using Alterverse.Match;
using Unity.Netcode;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Ensures a server-only process owns the same authoritative opening countdown as a host build.
    /// Client/host builds already create MatchOpening from PrototypeBootstrap.
    /// </summary>
    public sealed class DedicatedServerMatchOpeningBootstrap : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            if(FindAnyObjectByType<DedicatedServerMatchOpeningBootstrap>()==null)
                new GameObject("DF_DedicatedServerOpeningBootstrap").AddComponent<DedicatedServerMatchOpeningBootstrap>();
        }

        private void Awake()=>DontDestroyOnLoad(gameObject);

        private void Update()
        {
            if(!CoopAuthority.Active||!CoopAuthority.LocalHasServerAuthority)return;
            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null||!manager.IsListening||!manager.IsServer||manager.IsClient)return;

            GameObject world=GameObject.Find("DF_MatchWorld");
            if(world==null||FindAnyObjectByType<MatchOpening>()!=null)return;

            GameObject openingObject=new("AV_MatchOpening_Server");
            openingObject.transform.SetParent(world.transform,false);
            openingObject.AddComponent<MatchOpening>();
        }
    }
}
