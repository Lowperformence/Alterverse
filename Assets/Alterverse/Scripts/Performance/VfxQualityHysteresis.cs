using System;

namespace Alterverse.Performance
{
    // Presentation quality only. Consecutive sustained samples avoid visible on/off
    // flicker near a frame-budget boundary. Independent of Unity for regression tests.
    public sealed class VfxQualityHysteresis
    {
        public bool UnderBudget { get; private set; }
        private float slowSeconds, healthySeconds;
        private int lastTarget;

        public void Reset()
        {
            UnderBudget = false;
            slowSeconds = healthySeconds = 0f;
            lastTarget = 0;
        }

        public void Observe(float fps, float seconds, int target)
        {
            if (float.IsNaN(fps) || float.IsInfinity(fps) || fps < 0f ||
                float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f || target <= 0)
                return;
            if (target != lastTarget)
            {
                Reset();
                lastTarget = target;
            }
            // A background stall or a loading frame cannot impersonate several samples.
            seconds = Math.Min(seconds, 1.5f);
            if (!UnderBudget)
            {
                healthySeconds = 0f;
                slowSeconds = fps < target * .82f ? slowSeconds + seconds : 0f;
                if (slowSeconds >= 2f) { UnderBudget = true; slowSeconds = 0f; }
            }
            else
            {
                slowSeconds = 0f;
                healthySeconds = fps >= target * .94f ? healthySeconds + seconds : 0f;
                if (healthySeconds >= 4f) { UnderBudget = false; healthySeconds = 0f; }
            }
        }
    }
}
