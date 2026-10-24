// Legacy geometry/aiming fixtures have no licensed prefab library or particle engine.
// Keep their existing fallback path; the real native backend is compiled/exercised by
// Tools/Multiplayer/VfxRegression (including exclusive replacement and lifecycle checks).
using UnityEngine;
namespace Alterverse.Visuals
{
    public sealed partial class HeroVfxPool
    {
        internal readonly struct OriginalScope:System.IDisposable {public void Dispose(){}}
        internal static OriginalScope PreserveNerokOriginal()=>new();
        private sealed class EpicVisual {}
        private object epicLibrary;
        private Mesh epicBeam,epicTrace;
        private void LoadEpicLibrary() {}
        private bool TryRecoverEpicSurface()=>false;
        private bool AttachEpic(Slot slot,HeroFxShape shape)=>false;
        private bool DrawEpic(Slot slot,float age,float fade)=>false;
        private void ReleaseEpic(Slot slot) {}
    }
}
