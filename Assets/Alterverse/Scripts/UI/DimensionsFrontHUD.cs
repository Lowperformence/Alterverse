using UnityEngine;
namespace Alterverse.UI
{
    // Compatibility for older scenes: a single match HUD owns timer, kills and control.
    public sealed class DimensionsFrontHUD : MonoBehaviour
    {
        private void OnEnable()
        {
            if(FindAnyObjectByType<Alterverse.Match.DimensionsFrontHUD>()==null)
                gameObject.AddComponent<Alterverse.Match.DimensionsFrontHUD>();
            enabled=false;
        }
    }
}
