using Alterverse.Combat;
using Alterverse.Visuals;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Stable identifier and interpolation target for non-hero replicated Health owners.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class CoopWorldEntityKey : MonoBehaviour
    {
        [SerializeField]private string key="";
        // Moving world snapshots arrive at 20 Hz; full discovery runs at 8 Hz.
        // Interpolate on their server sampling timeline to keep arrival jitter out of the gait.
        private readonly HeroPresentationBuffer presentation=new(.15f);
        private bool hasTarget;
        private Renderer[] renderers=System.Array.Empty<Renderer>();
        private Collider[] colliders=System.Array.Empty<Collider>();
        private bool[] rendererStates=System.Array.Empty<bool>();
        private bool[] colliderStates=System.Array.Empty<bool>();
        private bool hidden,configured;

        public string Key=>key;
        public static uint IdentityVersion { get; private set; }

        public void Configure(string explicitKey=null)
        {
            if(configured&&string.IsNullOrEmpty(explicitKey))return;
            string previousKey=key;
            if(!string.IsNullOrEmpty(explicitKey))key=explicitKey;
            if(string.IsNullOrEmpty(key))
            {
                TeamMember team=GetComponent<TeamMember>();
                int teamId=team!=null?team.Team:-1;
                Vector3 p=transform.position;
                int x=Mathf.RoundToInt(p.x*10f),z=Mathf.RoundToInt(p.z*10f);
                key=$"{gameObject.name}|T{teamId}|X{x}|Z{z}";
            }
            if(!configured||previousKey!=key) { unchecked { IdentityVersion++; } }
            presentation.Clear();hasTarget=false;
            // The server publishes state; only receiving clients hide/render replicas.
            if(!configured&&!CoopAuthority.LocalHasServerAuthority)CachePresentation();
            configured=true;
        }

        public void ApplyTarget(Vector3 position,float yaw,bool immediate,float sampledAt=float.NaN)
        {
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            bool reset=immediate||!hasTarget||(transform.position-position).sqrMagnitude>36f;
            if(float.IsNaN(sampledAt))presentation.Push(position,rotation,Time.unscaledTime,reset);
            else presentation.PushRemote(position,rotation,sampledAt,Time.unscaledTime,reset);
            hasTarget=true;
            if(reset){transform.SetPositionAndRotation(position,rotation);GetComponent<HotZoneTank>()?.ConfirmNetworkSpawn();}
        }

        public void ApplyAlivePresentation(bool alive)
        {
            if(hidden==!alive)return;
            hidden=!alive;
            if(renderers.Length==0&&colliders.Length==0)CachePresentation();
            for(int i=0;i<renderers.Length;i++)
                if(renderers[i]!=null)renderers[i].enabled=alive&&(i<rendererStates.Length?rendererStates[i]:true);
            for(int i=0;i<colliders.Length;i++)
                if(colliders[i]!=null)colliders[i].enabled=alive&&(i<colliderStates.Length?colliderStates[i]:true);
        }

        private void CachePresentation()
        {
            renderers=GetComponentsInChildren<Renderer>(true);
            rendererStates=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++)rendererStates[i]=renderers[i]!=null&&renderers[i].enabled;
            colliders=GetComponentsInChildren<Collider>(true);
            colliderStates=new bool[colliders.Length];
            for(int i=0;i<colliders.Length;i++)colliderStates[i]=colliders[i]!=null&&colliders[i].enabled;
        }

        // Run before HotZoneTank.Update samples root travel for its feet and gait.
        private void Update()
        {
            if(!hasTarget||!CoopAuthority.Active||CoopAuthority.LocalHasServerAuthority)return;
            if(presentation.Evaluate(Time.unscaledTime,out Vector3 position,out Quaternion rotation))
                transform.SetPositionAndRotation(position,rotation);
        }
    }
}



