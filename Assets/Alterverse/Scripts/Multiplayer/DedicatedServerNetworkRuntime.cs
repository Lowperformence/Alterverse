using System;
using System.Collections;
using Alterverse.Match;
using Alterverse.Prototype;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Starts a real NGO server process with no local player. Production hosting can inject the
    /// listen port through UNITY_SERVER_PORT or -port/-serverPort. Desktop builds can opt in with
    /// -dfDedicatedServer for local testing; UNITY_SERVER builds enable this automatically.
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class DedicatedServerNetworkRuntime : MonoBehaviour
    {
        private const ushort DefaultPort=7777;
        public static DedicatedServerNetworkRuntime Instance{get;private set;}
        public static bool Requested{get;private set;}
        public static bool Listening=>NetworkManager.Singleton!=null&&NetworkManager.Singleton.IsServer&&NetworkManager.Singleton.IsListening;
        public static ushort ListenPort{get;private set;}=DefaultPort;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ensure()
        {
            Requested=DetectDedicatedServerRequest();
            if(!Requested||Instance!=null)return;
            new GameObject("DF_DedicatedServerNetworkRuntime").AddComponent<DedicatedServerNetworkRuntime>();
        }

        private void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}
            Instance=this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground=true;
            QualitySettings.vSyncCount=0;
            Application.targetFrameRate=60;
        }

        private IEnumerator Start()
        {
            if(!Requested)yield break;

            SuppressClientOnlyRuntime();
            yield return null;
            SuppressClientOnlyRuntime();

            float timeoutAt=Time.realtimeSinceStartup+8f;
            while(NetworkManager.Singleton==null&&Time.realtimeSinceStartup<timeoutAt)yield return null;

            NetworkManager manager=NetworkManager.Singleton;
            if(manager==null)
            {
                Debug.LogError("DIMENSIONS FRONT SERVER: NetworkManager bootstrap unavailable.");
                yield break;
            }
            if(manager.IsListening)
            {
                if(!manager.IsServer)Debug.LogError("DIMENSIONS FRONT SERVER: network already started as a client.");
                yield break;
            }

            UnityTransport transport=manager.GetComponent<UnityTransport>();
            if(transport==null)
            {
                Debug.LogError("DIMENSIONS FRONT SERVER: UnityTransport missing from NetworkManager.");
                yield break;
            }

            ListenPort=ResolvePort();
            transport.UseWebSockets=false;
            transport.SetConnectionData("127.0.0.1",ListenPort,"0.0.0.0");

            if(!manager.StartServer())
            {
                Debug.LogError($"DIMENSIONS FRONT SERVER: NGO failed to listen on UDP {ListenPort}.");
                yield break;
            }

            Debug.Log($"DIMENSIONS FRONT SERVER: listening on UDP {ListenPort} with no local player.");
        }

        private void Update()
        {
            if(Requested)SuppressClientOnlyRuntime();
        }

        private static void SuppressClientOnlyRuntime()
        {
            MatchClock.Set(PauseReason.Menu,false);

            // MainMenuController suppresses itself before initializing on dedicated servers.
            // No scene-wide menu lookup is needed in this per-frame path.

            // PrototypeBootstrap must remain dormant until an authoritative match plan is installed.
            // Once a plan is installed there is nothing to suppress. Avoid an
            // otherwise unused scene-wide bootstrap search on every server frame.
            if(CoopAuthority.ActivePlan==null)
            {
                PrototypeBootstrap bootstrap=FindAnyObjectByType<PrototypeBootstrap>();
                if(bootstrap!=null)bootstrap.enabled=false;
            }
        }

        public static void ReleaseMatchBootstrap()
        {
            if(!Requested||CoopAuthority.ActivePlan==null)return;
            MatchClock.Set(PauseReason.Menu,false);
            PrototypeBootstrap bootstrap=FindAnyObjectByType<PrototypeBootstrap>();
            if(bootstrap!=null)bootstrap.enabled=true;
        }

        private static ushort ResolvePort()
        {
            string environment=Environment.GetEnvironmentVariable("UNITY_SERVER_PORT");
            if(TryPort(environment,out ushort port))return port;

            string[] args=Environment.GetCommandLineArgs();
            for(int i=0;i<args.Length;i++)
            {
                string arg=args[i]??"";
                if(arg.StartsWith("-port=",StringComparison.OrdinalIgnoreCase)||arg.StartsWith("--port=",StringComparison.OrdinalIgnoreCase)||arg.StartsWith("-serverPort=",StringComparison.OrdinalIgnoreCase))
                {
                    int split=arg.IndexOf('=');
                    if(split>=0&&TryPort(arg[(split+1)..],out port))return port;
                }
                if((arg.Equals("-port",StringComparison.OrdinalIgnoreCase)||arg.Equals("--port",StringComparison.OrdinalIgnoreCase)||arg.Equals("-serverPort",StringComparison.OrdinalIgnoreCase))&&i+1<args.Length&&TryPort(args[i+1],out port))
                    return port;
            }
            return DefaultPort;
        }

        private static bool TryPort(string value,out ushort port)
        {
            return ushort.TryParse(value,out port)&&port>0;
        }
                            private static bool DetectDedicatedServerRequest()
        {
#if UNITY_EDITOR
            return false;
#elif UNITY_SERVER
    return true;
#else
    string[] args = Environment.GetCommandLineArgs();
    for (int i = 0; i < args.Length; i++)
    {
        if (string.Equals(args[i], "-dfDedicatedServer", StringComparison.OrdinalIgnoreCase))
            return true;
    }

    return false;
#endif
        }

        private void OnDestroy()
        {
            if(Instance==this)Instance=null;
        }
    }
}
