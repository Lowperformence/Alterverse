using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Compatibility shim for an obsolete bomber-audio override.
    /// The Space Bomber now creates its engine clip internally in SpaceBomberEvent,
    /// so no Resources/Audio/BombJetFlyby asset is required.
    /// </summary>
    public sealed class SpaceBomberReferenceAudioOverride : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Intentionally left empty. Older versions installed this component at runtime
            // and attempted to load a non-existent Resources/Audio/BombJetFlyby clip.
        }

        private void Awake()
        {
            // Keep legacy scene references harmless without touching the bomber's real audio.
            enabled = false;
        }
    }
}
