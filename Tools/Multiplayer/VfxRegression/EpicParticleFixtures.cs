// Deterministic particle clock/renderer adapters. No GPU or native Unity simulation.
namespace UnityEngine
{
    public enum ParticleSystemStopBehavior { StopEmittingAndClear }
    public sealed class ParticleSystem : Component
    {
        public sealed class MainModule { public bool loop,playOnAwake;public float simulationSpeed=1; }
        public static bool ForceNoParticles;
        public MainModule main=new();public float Simulated;public int Restarts,PlayCalls;public bool Cleared,EmissionStarted;
        public int particleCount=>!ForceNoParticles&&EmissionStarted&&!Cleared&&Simulated>0?1:0;
        public void Play(bool children){EmissionStarted=true;PlayCalls++;}
        public void Stop(bool children,ParticleSystemStopBehavior behavior){Simulated=0;Cleared=true;EmissionStarted=false;}
        public void Simulate(float time,bool children,bool restart,bool fixedStep){if(restart){Simulated=0;Restarts++;}Simulated+=time;Cleared=false;}
        public void Pause(bool children){}
    }
    public sealed class ParticleSystemRenderer : Renderer {}
}
namespace Alterverse.Visuals {public sealed class AuthoredHeroPresentation : UnityEngine.MonoBehaviour {}}
