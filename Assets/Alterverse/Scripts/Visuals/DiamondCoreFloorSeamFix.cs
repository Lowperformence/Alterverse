using Alterverse.Match;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>Retires the extra floor overlays; the original clear crystal floor owns presentation.</summary>
    [DefaultExecutionOrder(1100)]
    public sealed class DiamondCoreFloorSeamFix : MonoBehaviour
    {
        float nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(GameObject.Find("DF_PSRJ1719_FloorSeamFix")!=null)return;
            GameObject go=new("DF_PSRJ1719_FloorSeamFix");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<DiamondCoreFloorSeamFix>();
        }

        void Update()
        {
            if(!ArenaVariantRuntime.IsDiamondCore||Time.unscaledTime<nextScan)return;
            nextScan=Time.unscaledTime+.20f;
            // Also suppress retained overlays when entering play after an editor script reload.
            DisableOverlay("FacetedCrystalGroundOverlay");
            DisableOverlay("DF_PSRJ1719_SeamlessPrismFloor");
        }

        internal static void DisableOverlay(string name)
        {
            GameObject old=GameObject.Find(name);
            if(old==null)return;
            MeshRenderer renderer=old.GetComponent<MeshRenderer>();
            if(renderer!=null)renderer.enabled=false;
        }
    }
}
