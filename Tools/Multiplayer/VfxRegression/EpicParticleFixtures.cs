// Deterministic particle clock/renderer adapters. No GPU or native Unity simulation.
namespace UnityEngine
{
    public enum ParticleSystemStopBehavior { StopEmittingAndClear }
    public sealed class ParticleSystem : Component
    {
        public sealed class MainModule { public bool loop,playOnAwake;public float simulationSpeed=1; }
        public MainModule main=new();public float Simulated;public int Restarts;public bool Cleared;
        public void Stop(bool children,ParticleSystemStopBehavior behavior){Simulated=0;Cleared=true;}
        public void Simulate(float time,bool children,bool restart,bool fixedStep){if(restart){Simulated=0;Restarts++;}Simulated+=time;Cleared=false;}
        public void Pause(bool children){}
    }
    public sealed class ParticleSystemRenderer : Renderer {}
}
