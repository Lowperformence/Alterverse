using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Compatibility shell for the former runtime crater overlay.
    /// The arena now uses the unified HD dune surface, so extra crater bowl/rim geometry is intentionally disabled.
    /// Keeping this component type avoids breaking existing runtime/bootstrap references.
    /// </summary>
    [DefaultExecutionOrder(260)]
    public sealed class PlanetSurfaceDetailRuntime : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if(FindAnyObjectByType<PlanetSurfaceDetailRuntime>()!=null)return;
            GameObject host=new("DF_PlanetSurfaceDetailRuntime");
            DontDestroyOnLoad(host);
            host.hideFlags=HideFlags.DontSave;
            host.AddComponent<PlanetSurfaceDetailRuntime>();
        }
    }
}
