using UnityEngine;

namespace Alterverse.Combat
{
    /// <summary>
    /// Retains the serialized legacy component identity. SpaceBomberDirector loads
    /// the approved Audio/BombJetFlyby clip directly, and SpaceBomberFlyover owns
    /// its playback. The former per-frame scene scan/private-field reflection
    /// bridge is no longer needed and is unsuitable for stripped IL2CPP builds.
    /// </summary>
    public sealed class SpaceBomberReferenceAudioOverride : MonoBehaviour
    {
        private void Awake() => enabled = false;
    }
}
