using UnityEngine;

namespace Alterverse.Multiplayer
{
    /// <summary>Client-visible copy of the host-owned human respawn countdown.</summary>
    public sealed class CoopRespawnReplica : MonoBehaviour
    {
        public bool IsRespawning { get; private set; }
        public float RemainingSeconds { get; private set; }

        public void Apply(bool respawning,float remaining)
        {
            IsRespawning=respawning;
            RemainingSeconds=respawning?Mathf.Max(0f,remaining):0f;
        }

        private void Update()
        {
            if(IsRespawning&&RemainingSeconds>0f&&Time.timeScale>0f)
                RemainingSeconds=Mathf.Max(0f,RemainingSeconds-Time.deltaTime);
        }
    }
}
