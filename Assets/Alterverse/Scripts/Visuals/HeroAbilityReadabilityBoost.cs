using UnityEngine;
namespace Alterverse.Visuals
{
    // Serialized component kept for scene compatibility. HeroSkillVfx is the sole attack VFX owner.
    public sealed class HeroAbilityReadabilityBoost : MonoBehaviour
    {
        private void Awake() { enabled=false; }
    }
}
