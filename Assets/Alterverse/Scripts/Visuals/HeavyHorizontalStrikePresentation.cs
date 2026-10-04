using UnityEngine;
namespace Alterverse.Visuals
{
    // Serialized compatibility shell. MeleeBasicAttackAnimation is the single final IK owner.
    public sealed class HeavyHorizontalStrikePresentation : MonoBehaviour
    {
        public static HeavyHorizontalStrikePresentation For(GameObject actor)=>null;
        public void PulseBasic(float windup){}
        private void OnEnable()=>enabled=false;
    }
}
