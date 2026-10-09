using System;

namespace Alterverse.Audio
{
    public static class AudioDuckEnvelope
    {
        public const float DuckedGain = .55f;

        // Exponential attack/release is stable at 30, 60 and 120 Hz. It smooths the
        // mix gain only; the selected recording, pitch and playback position survive.
        public static float Advance(float current, bool warningActive, float seconds)
        {
            if (float.IsNaN(current) || float.IsInfinity(current)) current = 1f;
            current = Math.Max(DuckedGain, Math.Min(1f, current));
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return current;
            float target = warningActive ? DuckedGain : 1f;
            float response = warningActive ? .024f : .18f;
            return target + (current - target) * (float)Math.Exp(-seconds / response);
        }
    }
}
