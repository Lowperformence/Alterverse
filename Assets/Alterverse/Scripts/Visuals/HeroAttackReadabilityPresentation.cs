using UnityEngine;
namespace Alterverse.Visuals
{
    // Serialized component kept for scene compatibility. HeroSkillVfx is the sole attack VFX owner.
    public sealed class HeroAttackReadabilityPresentation : MonoBehaviour
    {
        private void Awake() { enabled=false; }
    }
}
