using UnityEngine;
namespace Alterverse.Combat
{
    public sealed class CosmicNebulaState:MonoBehaviour
    {
        private float revealedUntil;
        private double lastSample=double.NegativeInfinity;
        public float RevealRemaining=>Mathf.Max(0,revealedUntil-Time.time);
        public static void OffensiveAction(GameObject actor)
        {
            if(!Alterverse.Multiplayer.CoopAuthority.CanSimulateHostGameplay||actor==null)return;
            var state=actor.GetComponent<CosmicNebulaState>()??actor.AddComponent<CosmicNebulaState>();
            state.revealedUntil=Time.time+2f;
            CosmicNebulaField.BroadcastReveal(actor);
        }
        public void Apply(double sample,double serverNow,float remaining)
        {
            if(double.IsNaN(sample)||double.IsInfinity(sample)||double.IsNaN(serverNow)||double.IsInfinity(serverNow)||
                float.IsNaN(remaining)||float.IsInfinity(remaining)||remaining<0||remaining>2.1f||sample<=lastSample)return;
            lastSample=sample;revealedUntil=Time.time+Mathf.Max(0,remaining-(float)Mathf.Max(0,(float)(serverNow-sample)));
        }
    }
}
