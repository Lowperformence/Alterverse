// Legacy geometry/aiming fixtures have no licensed prefab library or particle engine.
// Keep their existing fallback path; the real native backend is compiled/exercised by
// Tools/Multiplayer/VfxRegression (including exclusive replacement and lifecycle checks).
using UnityEngine;
namespace Alterverse.Visuals
{
    public sealed partial class HeroVfxPool
    {
        private sealed class EpicVisual {}
        private object epicLibrary;
        private Mesh epicBeam,epicTrace;
        private void LoadEpicLibrary() {}
        private bool AttachEpic(Slot slot,HeroFxShape shape)=>false;
        private void DrawEpic(Slot slot,float age,float fade) {}
        private void ReleaseEpic(Slot slot) {}
    }
}
