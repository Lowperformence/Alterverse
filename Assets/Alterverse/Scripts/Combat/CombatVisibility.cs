using Alterverse.Characters;
using UnityEngine;
namespace Alterverse.Combat
{
    public static class CombatVisibility
    {
        public static bool HiddenFrom(GameObject observer,Health target)
            =>VoidKnightState.HiddenFrom(observer,target)||CosmicNebulaField.HiddenFrom(observer,target);
    }
}
