using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Compatibility component for existing scenes and prefabs. HeroVisualMotion owns
    /// the guard and attack poses. Keep this script GUID, but never modify the rig here.
    /// </summary>
    public sealed class VeyrAttackPresentation : MonoBehaviour
    {
        private void OnEnable() => enabled = false;
    }
}
