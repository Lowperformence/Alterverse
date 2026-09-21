using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Stable identifier and interpolation target for non-hero replicated Health owners.</summary>
    public sealed class CoopWorldEntityKey : MonoBehaviour
    {
        [SerializeField]private string key="";
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private bool hasTarget;
        private Renderer[] renderers=System.Array.Empty<Renderer>();
        private Collider[] colliders=System.Array.Empty<Collider>();
        private bool[] rendererStates=System.Array.Empty<bool>();
        private bool[] colliderStates=System.Array.Empty<bool>();
        private bool hidden,configured;

        public string Key=>key;

        public void Configure(string explicitKey=null)
        {
            if(configured&&string.IsNullOrEmpty(explicitKey))return;
            if(!string.IsNullOrEmpty(explicitKey))key=explicitKey;
            if(string.IsNullOrEmpty(key))
            {
                TeamMember team=GetComponent<TeamMember>();
                int teamId=team!=null?team.Team:-1;
                Vector3 p=transform.position;
                int x=Mathf.RoundToInt(p.x*10f),z=Mathf.RoundToInt(p.z*10f);
                key=$"{gameObject.name}|T{teamId}|X{x}|Z{z}";
            }
            targetPosition=transform.position;
            targetRotation=transform.rotation;
            CachePresentation();
            configured=true;
        }

        public void ApplyTarget(Vector3 position,float yaw,bool immediate)
        {
            targetPosition=position;
            targetRotation=Quaternion.Euler(0,yaw,0);
            hasTarget=true;
            if(immediate||(transform.position-position).sqrMagnitude>36f)
                transform.SetPositionAndRotation(position,targetRotation);
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

        private void LateUpdate()
        {
            if(!hasTarget||!CoopAuthority.Active||CoopAuthority.LocalIsHost)return;
            float positionBlend=1f-Mathf.Exp(-16f*Time.unscaledDeltaTime);
            float rotationBlend=1f-Mathf.Exp(-18f*Time.unscaledDeltaTime);
            transform.position=Vector3.Lerp(transform.position,targetPosition,positionBlend);
            transform.rotation=Quaternion.Slerp(transform.rotation,targetRotation,rotationBlend);
        }
    }
}
