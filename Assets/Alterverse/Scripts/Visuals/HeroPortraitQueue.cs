using UnityEngine;
namespace Alterverse.Visuals
{
    // At most one small portrait per frame, outside IMGUI's layout/repaint passes.
    public sealed class HeroPortraitQueue:MonoBehaviour
    {
        private int frameCounter;
        private void LateUpdate()
        {
            if ((++frameCounter % 3) == 0)
                StylizedHeroCard.ProcessOnePortrait();
        }
    }
}
