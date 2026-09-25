using Alterverse.Combat;
using UnityEngine;

namespace Alterverse.Visuals
{
    /// <summary>
    /// Legacy compatibility component retained so older serialized scenes/prefabs do not
    /// become Missing Script. Runtime hit feedback is handled exclusively by
    /// Alterverse.Combat.CombatFeedback.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [System.Obsolete("Use Alterverse.Combat.CombatFeedback instead.")]
    public sealed class CombatFeedback : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<Alterverse.Combat.CombatFeedback>() == null)
                gameObject.AddComponent<Alterverse.Combat.CombatFeedback>();

            // Keep this serialized legacy component inert. Disabling rather than deleting it
            // preserves compatibility with old scenes/prefabs while preventing duplicate VFX.
            enabled = false;
        }
    }
}
