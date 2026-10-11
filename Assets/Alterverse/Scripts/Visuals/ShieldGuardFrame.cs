using UnityEngine;

namespace Alterverse.Visuals
{
    // A fixed grip frame keeps chest/skin updates from becoming the next frame's wrist correction.
    public sealed class ShieldGuardFrame
    {
        private Transform calibratedHand;
        private Quaternion frameInHand;

        public bool Orient(Transform actor, Transform hand, Transform center, Transform top, Transform front)
        {
            if (actor == null || hand == null || center == null || top == null || front == null) return false;
            if (calibratedHand != hand)
            {
                Vector3 up = top.position - center.position;
                Vector3 forward = front.position - center.position;
                if (up.sqrMagnitude < .000001f || forward.sqrMagnitude < .000001f ||
                    Vector3.Cross(up, forward).sqrMagnitude < .000001f) return false;
                frameInHand = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(forward, up);
                calibratedHand = hand;
            }
            hand.rotation = Quaternion.LookRotation(actor.forward, actor.up) * Quaternion.Inverse(frameInHand);
            return true;
        }
    }
}
