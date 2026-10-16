using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>
    /// Client-side presentation for host-authoritative death/respawn snapshots.
    /// It never changes gameplay state; Health remains the source of truth.
    /// </summary>
    public sealed class CoopHeroReplicaPresentation : MonoBehaviour
    {
        private Health health;
        private Renderer[] renderers=System.Array.Empty<Renderer>();
        private bool[] authoredStates=System.Array.Empty<bool>();
        private bool hidden;

        private void Awake()
        {
            health=GetComponent<Health>();
            Refresh();
        }

        public void Refresh()
        {
            renderers=GetComponentsInChildren<Renderer>(true);
            authoredStates=new bool[renderers.Length];
            for(int i=0;i<renderers.Length;i++)
                authoredStates[i]=renderers[i]!=null&&renderers[i].enabled;
        }

        public void Apply(bool dead)
        {
            if(dead==hidden)return;
            hidden=dead;
            if(dead)Alterverse.Audio.DimensionAudio.PlayAt(Alterverse.Audio.DimensionSound.HeroDeath,transform.position,.78f);
            if(renderers==null||renderers.Length==0)Refresh();
            for(int i=0;i<renderers.Length;i++)
            {
                Renderer renderer=renderers[i];
                if(renderer==null)continue;
                renderer.enabled=!dead&&(i<authoredStates.Length?authoredStates[i]:true);
            }
        }

        private void LateUpdate()
        {
            if(CoopAuthority.Active&&!CoopAuthority.LocalHasServerAuthority&&health!=null)
                Apply(health.IsDead);
        }
    }
}


